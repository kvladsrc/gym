using UnityEngine;

namespace Realms
{
    /// <summary>A creature standing on the map: breathes and now and then looks around.</summary>
    public sealed class Idle : MonoBehaviour
    {
        public float breath = 0.035f;
        public float look = 14f;

        float phase, speed, yaw;
        Vector3 scale;

        void Start()
        {
            phase = Random.value * 100f;
            speed = Random.Range(0.7f, 1.3f);
            yaw = transform.localEulerAngles.y;
            scale = transform.localScale;
        }

        void Update()
        {
            float t = Time.time * speed + phase;
            float s = 1f + breath * Mathf.Sin(t * 2.4f);
            transform.localScale = new Vector3(scale.x * (2f - s), scale.y * s, scale.z * (2f - s));
            float turn = Mathf.Sin(t * 0.37f) + 0.5f * Mathf.Sin(t * 0.91f + 1.3f);
            transform.localRotation = Quaternion.Euler(0f, yaw + look * turn, 0f);
        }
    }
}
