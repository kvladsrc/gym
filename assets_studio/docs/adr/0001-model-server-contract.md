# ADR-001. Model server contract, version 1

<!-- markdownlint-disable MD013 -->

Status: accepted (M1a). Extended by ADR-003 (text, video, primary parameters).

## Context

Users manually start models as independent local HTTP servers. Studio does
not launch them or monitor laptop resources: it knows server addresses,
discovers capabilities and submits jobs. To connect new models without
changing Studio code, all servers implement one contract and describe their
own capabilities.

## Decision

Two endpoints at `http://127.0.0.1:<port>`, with JSON request and response bodies.
The reference definition is the Pydantic models in
`model_server_sdk/src/model_server_sdk/contract.py`; this document covers rules
not expressed by those models.

### `GET /v1/info`

Self-description and status. Always returns `200`, even while loading or after
load failure, so Studio can display the reason.

```json
{
  "contract": 1,
  "status": "ready",
  "status_message": null,
  "model": {"id": "fake", "name": "Fake model", "revision": "1",
            "license": "MIT", "source": null,
            "details": {"parameters": "1 M", "quantization": "none"}},
  "tasks": [
    {
      "task": "image-to-image",
      "prompt": "optional",
      "inputs": [{"role": "image", "mime": ["image/png"], "required": true,
                  "description": null}],
      "params_schema": {"type": "object", "properties": {"strength": {"type": "number", "default": 0.5}}},
      "max_count": 4,
      "output_mime": ["image/png"]
    }
  ]
}
```

- `status`: `loading` → `ready` ⇄ `busy`; `error` on model load failure, with
  the reason in `status_message`. Loading runs in the background after startup,
  so `/v1/info` is immediately available.
- Also `error` after generation breaks the process's CUDA context (for example,
  a native library writes outside a buffer, making all later generation fail).
  The SDK returns `generation_failed` with `retryable: true` for that request,
  then `not_ready` with `retryable: true` for subsequent requests. After one
  second it exits with code 75 (`EXIT_GPU_BROKEN`); the launcher restarts it
  immediately rather than waiting the usual crash delay.
- `task` is a lowercase Latin letter/digit identifier with `-` or `_` separators.
  Known values: `text-to-image`, `image-to-image`, `image-to-3d`, `text-to-speech`,
  `text-to-audio`, `audio-to-audio`. Studio displays a generic form for unknown
  tasks; new tasks need no contract version change.
- `prompt`: `required`, `optional` or `none`.
- `inputs`: role-based input files. `mime` lists accepted types; `type/*`
  wildcards are allowed. Image, audio and 3D inputs **must accept their kind's
  canonical format**: `image/png`, `audio/wav`, `model/gltf-binary`. Studio
  converts other formats (such as uploaded JPEGs) before submission.
- `params_schema`: parameter-object JSON Schema. Parameters are **flat scalars**
  (`string`, `number`, `integer`, `boolean`) or enumerations (`enum`), without
  nested objects, lists, `$ref` or `anyOf`. **Every parameter has a default**,
  making requests without `params` valid and allowing Studio to build an
  “Advanced” form. Schema and request names match, without aliases.
- `max_count`: maximum outputs per call.
- `output_mime`: output file types for the task.

### `POST /v1/generate`

```json
{
  "task": "image-to-image",
  "prompt": "moss on stone",
  "inputs": [{"role": "image", "mime": "image/png", "data_b64": "…"}],
  "params": {"strength": 0.45},
  "count": 3,
  "seed": 42
}
```

Successful `200` response:

```json
{
  "model": {"id": "fake", "name": "Fake model", "revision": "1", "license": "MIT", "source": null},
  "task": "image-to-image",
  "seed": 42,
  "params": {"strength": 0.45},
  "outputs": [
    {"mime": "image/png", "data_b64": "…", "meta": {"seed": 42}},
    {"mime": "image/png", "data_b64": "…", "meta": {"seed": 43}},
    {"mime": "image/png", "data_b64": "…", "meta": {"seed": 44}}
  ],
  "timing": {"generate_s": 1.73}
}
```

Rules:

- **One request returns exactly `count` outputs**, one file per candidate.
  Output `i` uses seed `seed + i` modulo 2³² and records it in `meta.seed`.
- **Each result depends only on its own seed.** Requesting `seed + i` with
  `count = 1` reproduces candidate `i`, enabling Variants and evolution.
  Diffusion models need one generator per seed, not one per batch. Identical
  requests produce identical results. Different seeds differ unless the model
  is inherently deterministic (for example Piper). GPU batch and single outputs
  need not be byte-identical; this is allowed.
- Response `params` are **effective** parameters: requested values plus defaults.
  Together with `model`, they capture how the result was made, since server
  defaults may change.
- If omitted, the server chooses `seed` and returns it.
- Response `model` identifies the model that **actually** ran. Studio stores
  this snapshot, not `/v1/info`: the server at a port could have changed between
  requests.
- Input roles must be unique. Unknown roles, parameters and extra fields are rejected.
- Only one generation runs at a time. Concurrent requests receive `409 busy`;
  `/v1/info` reports `busy` meanwhile.
- Files are base64 in JSON. For loopback assets up to tens of MB, this is
  simpler than multipart. Reconsider for version 2 if it becomes a problem.
- Version 1 has no generation progress or mid-generation cancellation.

### Errors

Every error, on any route including unknown routes, has this body:

```json
{"error": {"code": "busy", "message": "…", "retryable": true}}
```

| HTTP | `code` | `retryable` | When |
| --- | --- | --- | --- |
| 400 | `unsupported_task` | No | Task not advertised by server |
| 404, 405 | `invalid_request` | No | Unknown route or method |
| 409 | `busy` | Yes | Another generation is running |
| 422 | `invalid_request` | No | Invalid schema, missing required input, invalid MIME/base64, `count > max_count`, unknown parameter |
| 500 | `generation_failed` | Depends | Model failed on a valid request |
| 500 | `internal` | No | Server implementation error |
| 503 | `not_ready` | Yes for `loading` or broken GPU; no for load failure | Model not loaded or process restarting |

`retryable` means the same request may succeed later. Studio retries only
these errors.

Optional `model.details` (2026-10-22) provides technical information for the
model panel as string key/value pairs (size, quantization, RAM offload, peak
VRAM). Studio displays it as supplied and makes no decisions from it.

## Implementation

`model_server_sdk` implements the whole contract: background loading, validation,
error codes, seeds and concurrent-request locking. Authors declare tasks
(`TaskSpec` with Pydantic parameter models) and implement `load()` and
`generate(job)`. The SDK checks declarations at startup using the same rules
as `/v1/info`. Loading and generation run sequentially on one dedicated thread,
preserving thread-local settings such as `torch.set_grad_enabled(False)`.

`python -m model_server_sdk.check URL` checks a running server against the
contract, the M0 acceptance criterion. It checks declarations, each task's
generation, error paths and seed rules:

- Identical requests must repeat, proving seed control. Failure is an error
  (`--allow-nondeterministic` downgrades it to a warning).
- Different seeds must differ. Failure is an error (`--allow-seed-independent`
  is available for deterministic models).
- Batch results must match single requests. Differences are warnings
  (`--strict-determinism` makes them errors).

Explain acceptance warnings in the report.

The SDK supports Python 3.11+: some models (TripoSR) pin PyTorch builds for 3.11.

## Alternatives

- **Existing APIs** (OpenAI Images, ComfyUI) do not cover 3D and audio uniformly
  or describe parameters for generated forms.
- **Asynchronous model-server jobs** (`POST` → ID → polling). Studio already
  owns a queue; synchronous loopback calls are simpler and avoid duplicated state.
- **Multipart instead of base64** saves about 33% traffic but complicates the
  contract and clients, with little loopback benefit.
