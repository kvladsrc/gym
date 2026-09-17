using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mosslight
{
    public sealed class MosslightGame : MonoBehaviour
    {
        public Transform player;
        public Transform visual;
        public Camera gameCamera;
        public Transform[] crystals;
        public Light portalLight;

        private CharacterController controller;
        private readonly List<Transform> limbs = new List<Transform>();
        private readonly List<Vector3> restPositions = new List<Vector3>();
        private readonly HashSet<int> collected = new HashSet<int>();
        private readonly Vector3 spawn = new Vector3(0, 0.3f, -6);
        private float verticalSpeed;
        private float yaw;
        private float pitch = 30;
        private float distance = 7.5f;
        private float walkPhase;
        private float startTime;
        private float maxTravel;
        private bool won;
        private bool smoke;
        private bool finishing;
        private bool captured;
        private string capturePath;
        private GUIStyle title, text, small, count, badge;
        private Texture2D panel;

        private void Start()
        {
            controller = player.GetComponent<CharacterController>();
            foreach (Transform child in visual.GetComponentsInChildren<Transform>())
            {
                if (child.name.StartsWith("Arm") || child.name.StartsWith("Leg") || child.name.StartsWith("Boot"))
                {
                    limbs.Add(child);
                    restPositions.Add(child.localPosition);
                }
            }
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg == "--smoke") smoke = true;
                if (arg.StartsWith("--capture=")) capturePath = arg.Substring(10);
            }
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            startTime = Time.time;
            UpdateCamera(true);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Input.GetKeyDown(KeyCode.R)) ResetRun();
            bool orbit = Input.GetMouseButton(1);
            Cursor.lockState = orbit ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !orbit;
            if (orbit)
            {
                yaw += Input.GetAxis("Mouse X") * 3;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2, 15, 65);
            }
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 0.4f, 4, 11);

            Vector3 direction;
            if (smoke)
            {
                Vector3 target = crystals[3].position;
                direction = target - player.position;
                direction.y = 0;
                direction = direction.magnitude > 0.55f ? direction.normalized : Vector3.zero;
            }
            else
            {
                float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                    - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
                float z = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0)
                    - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
                direction = Quaternion.Euler(0, yaw, 0) * Vector3.ClampMagnitude(new Vector3(x, 0, z), 1);
            }
            float speed = Input.GetKey(KeyCode.LeftShift) ? 7 : 4.5f;
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space)) verticalSpeed = 7;
            verticalSpeed += Physics.gravity.y * 2 * Time.deltaTime;
            controller.Move((direction * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (direction.sqrMagnitude > 0.01f)
            {
                visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(direction), 12 * Time.deltaTime);
                walkPhase += Time.deltaTime * speed * 2.8f;
            }
            float stride = direction.magnitude * (controller.isGrounded ? 1 : 0.25f);
            for (int i = 0; i < limbs.Count; i++)
            {
                float sign = limbs[i].name.Contains("Left") ? 1 : -1;
                if (limbs[i].name.StartsWith("Arm")) sign *= -1;
                limbs[i].localPosition = restPositions[i] + new Vector3(0, Mathf.Max(0, Mathf.Sin(walkPhase) * sign) * 0.10f * stride, Mathf.Sin(walkPhase) * sign * 0.12f * stride);
            }
            if (player.position.y < -8) Respawn();
            maxTravel = Mathf.Max(maxTravel, Vector3.Distance(player.position, spawn));
            for (int i = 0; i < crystals.Length; i++)
            {
                if (collected.Contains(i)) continue;
                crystals[i].Rotate(0, 70 * Time.deltaTime, 0, Space.World);
                Vector3 p = crystals[i].position;
                p.y = 1.25f + Mathf.Sin(Time.time * 2 + i) * 0.15f;
                crystals[i].position = p;
                if (Vector3.Distance(player.position + Vector3.up, p) < 1.15f)
                {
                    collected.Add(i);
                    crystals[i].gameObject.SetActive(false);
                    Debug.Log("Mosslight collected " + collected.Count + "/" + crystals.Length);
                }
            }
            portalLight.intensity = collected.Count == crystals.Length ? 5 : 0.7f;
            if (collected.Count == crystals.Length && Vector3.Distance(player.position, new Vector3(0, 0.4f, -1)) < 2)
                won = true;
            if (!captured && !string.IsNullOrEmpty(capturePath) && Time.time - startTime > 0.7f)
            {
                captured = true;
                StartCoroutine(Capture());
            }
            if (smoke && !finishing && Time.time - startTime > 10)
            {
                finishing = true;
                StartCoroutine(FinishSmoke());
            }
        }

        private IEnumerator Capture()
        {
            yield return new WaitForEndOfFrame();
            string dir = Path.GetDirectoryName(capturePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(capturePath);
        }

        private IEnumerator FinishSmoke()
        {
            bool passed = maxTravel > 5 && collected.Count > 0 && player.position.y > -1 && player.position.y < 2;
            Debug.Log("MOSSLIGHT_SMOKE " + JsonUtility.ToJson(new SmokeResult {
                passed = passed, distance = maxTravel, crystals = collected.Count,
                playerY = player.position.y
            }));
            yield return new WaitForSeconds(0.5f);
            Application.Quit(passed ? 0 : 1);
        }

        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public float distance;
            public int crystals;
            public float playerY;
        }

        private void LateUpdate() { UpdateCamera(false); }

        private void UpdateCamera(bool instant)
        {
            Vector3 target = player.position + Vector3.up * 1.1f;
            Vector3 offset = Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -distance);
            float actualDistance = distance;
            if (Physics.SphereCast(target, 0.2f, offset.normalized, out RaycastHit hit, distance, 1 << 0))
                actualDistance = Mathf.Max(1.5f, hit.distance - 0.15f);
            Vector3 desired = target + offset.normalized * actualDistance;
            gameCamera.transform.position = instant ? desired : Vector3.Lerp(gameCamera.transform.position, desired, 1 - Mathf.Exp(-10 * Time.deltaTime));
            gameCamera.transform.LookAt(target);
        }

        private void Respawn()
        {
            controller.enabled = false;
            player.position = spawn;
            controller.enabled = true;
            verticalSpeed = 0;
            UpdateCamera(true);
        }

        private void ResetRun()
        {
            collected.Clear();
            foreach (Transform crystal in crystals) crystal.gameObject.SetActive(true);
            won = false;
            Respawn();
        }

        private void OnGUI()
        {
            if (title == null)
            {
                panel = new Texture2D(1, 1);
                panel.SetPixel(0, 0, new Color(0.04f, 0.12f, 0.14f, 0.90f));
                panel.Apply();
                title = Style(28, new Color(0.96f, 0.96f, 0.84f), FontStyle.Bold);
                text = Style(16, new Color(0.83f, 0.90f, 0.85f));
                small = Style(12, new Color(0.59f, 0.77f, 0.72f));
                count = Style(25, new Color(0.48f, 0.96f, 0.80f), FontStyle.Bold);
                badge = Style(13, new Color(0.96f, 0.64f, 0.32f), FontStyle.Bold);
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.DrawTexture(new Rect(24, 24, 345, 129), panel);
            GUI.Label(new Rect(44, 36, 320, 24), "A SMALL JOURNEY ABOVE THE CLOUDS", small);
            GUI.Label(new Rect(43, 60, 320, 42), "MOSSLIGHT", title);
            GUI.Label(new Rect(44, 109, 320, 25), "Find the lights. Wake the ancient gate.", text);
            GUI.DrawTexture(new Rect(width - 200, 24, 176, 84), panel);
            GUI.Label(new Rect(width - 180, 36, 156, 20), "LIGHTS RETURNED", small);
            GUI.Label(new Rect(width - 180, 59, 156, 35), collected.Count + " / " + crystals.Length, count);
            GUI.DrawTexture(new Rect(24, height - 83, 620, 59), panel);
            GUI.Label(new Rect(44, height - 72, 580, 24), "WASD  move     SHIFT  run     SPACE  jump     RMB  orbit", text);
            GUI.Label(new Rect(44, height - 48, 580, 20), "SCROLL  zoom     R  restart     Walk back to the stone gate with all five lights.", small);
            GUI.Label(new Rect(width - 184, height - 48, 170, 22), "BLENDER  /  UNITY", badge);
            if (collected.Count == crystals.Length)
            {
                GUI.DrawTexture(new Rect(width / 2 - 225, height / 2 - 58, 450, 116), panel);
                GUI.Label(new Rect(width / 2 - 200, height / 2 - 40, 430, 40), won ? "THE GARDEN IS AWAKE" : "THE GATE IS CALLING", title);
                GUI.Label(new Rect(width / 2 - 200, height / 2 + 6, 420, 32), won ? "A little light goes a long way.   R to explore again." : "Return to the stone arch at the centre.", text);
            }
        }

        private static GUIStyle Style(int size, Color color, FontStyle font = FontStyle.Normal)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = font, normal = { textColor = color } };
        }
    }
}
