class_name OpenRouterAdapter
extends HTTPRequest

signal completion_succeeded(response_text: String)
signal completion_failed(error: String)

const API_URL := "https://openrouter.ai/api/v1/chat/completions"
const DEFAULT_MODEL := "openai/gpt-5.4-mini"
const REQUEST_TIMEOUT_SECONDS := 90.0

var busy := false
var last_request_body: Dictionary = {}
var last_raw_response_body := ""
var last_response_code := 0


func _ready() -> void:
	add_to_group("openrouter_adapter")
	timeout = REQUEST_TIMEOUT_SECONDS
	request_completed.connect(_on_request_completed)


func create_completion(messages: Array[Dictionary]) -> void:
	if busy:
		completion_failed.emit("OpenRouter request is already running.")
		return

	var api_key := OS.get_environment("OPENROUTER_API_KEY")
	if api_key == "":
		completion_failed.emit("OPENROUTER_API_KEY is not set.")
		return

	var model := OS.get_environment("OPENROUTER_MODEL")
	if model == "":
		model = DEFAULT_MODEL

	var body := {
		"model": model,
		"messages": messages,
		"temperature": 0.2,
		"max_completion_tokens": 4096,
		"response_format": {"type": "json_object"},
	}
	last_request_body = body.duplicate(true)
	last_raw_response_body = ""
	last_response_code = 0
	var headers := [
		"Authorization: Bearer %s" % api_key,
		"Content-Type: application/json",
		"HTTP-Referer: https://github.com/local/cothic",
		"X-Title: Cothic",
	]

	busy = true
	var error := request(API_URL, headers, HTTPClient.METHOD_POST, JSON.stringify(body))
	if error != OK:
		busy = false
		completion_failed.emit("Failed to start OpenRouter request: %s" % error_string(error))


func _on_request_completed(
	result: int, response_code: int, _headers: PackedStringArray, body: PackedByteArray
) -> void:
	busy = false
	var raw_body := body.get_string_from_utf8()
	last_raw_response_body = raw_body
	last_response_code = response_code

	if result != HTTPRequest.RESULT_SUCCESS:
		completion_failed.emit("OpenRouter request failed: %s" % _request_result_name(result))
		return

	if response_code < 200 or response_code >= 300:
		completion_failed.emit("OpenRouter returned HTTP %s: %s" % [response_code, raw_body])
		return

	var parsed: Variant = JSON.parse_string(raw_body)
	if typeof(parsed) != TYPE_DICTIONARY:
		completion_failed.emit("OpenRouter returned invalid JSON.")
		return

	var choices: Array = parsed.get("choices", [])
	if choices.is_empty():
		completion_failed.emit("OpenRouter response did not include choices.")
		return

	var first_choice: Dictionary = choices[0]
	var message: Dictionary = first_choice.get("message", {})
	var content := str(message.get("content", ""))
	if content == "":
		completion_failed.emit("OpenRouter response was empty.")
		return

	completion_succeeded.emit(content)


func _request_result_name(result: int) -> String:
	var names := {
		HTTPRequest.RESULT_CHUNKED_BODY_SIZE_MISMATCH: "chunked body size mismatch",
		HTTPRequest.RESULT_CANT_CONNECT: "cannot connect",
		HTTPRequest.RESULT_CANT_RESOLVE: "cannot resolve host",
		HTTPRequest.RESULT_CONNECTION_ERROR: "connection error",
		HTTPRequest.RESULT_TLS_HANDSHAKE_ERROR: "TLS handshake error",
		HTTPRequest.RESULT_NO_RESPONSE: "no response",
		HTTPRequest.RESULT_BODY_SIZE_LIMIT_EXCEEDED: "body size limit exceeded",
		HTTPRequest.RESULT_BODY_DECOMPRESS_FAILED: "body decompress failed",
		HTTPRequest.RESULT_REQUEST_FAILED: "request failed",
		HTTPRequest.RESULT_DOWNLOAD_FILE_CANT_OPEN: "download file cannot open",
		HTTPRequest.RESULT_DOWNLOAD_FILE_WRITE_ERROR: "download file write error",
		HTTPRequest.RESULT_REDIRECT_LIMIT_REACHED: "redirect limit reached",
		HTTPRequest.RESULT_TIMEOUT: "timeout",
	}
	return names.get(result, "result %s" % result)


func get_last_request_body() -> Dictionary:
	return last_request_body.duplicate(true)


func get_last_raw_response_body() -> String:
	return last_raw_response_body


func get_last_response_code() -> int:
	return last_response_code
