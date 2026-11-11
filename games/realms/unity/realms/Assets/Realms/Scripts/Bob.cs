using UnityEngine;

namespace Realms
{
    /// <summary>Rocks gently on the water: boats.</summary>
    public sealed class Bob : MonoBehaviour
    {
        public float height = 0.008f, roll = 4f;

        float phase;
        Vector3 position;
        Quaternion rotation;

        void Start()
        {
            phase = Random.value * 10f;
            position = transform.localPosition;
            rotation = transform.localRotation;
        }

        void Update()
        {
            float t = Time.time * 1.3f + phase;
            transform.localPosition = position + new Vector3(0f, height * Mathf.Sin(t), 0f);
            transform.localRotation = rotation * Quaternion.Euler(roll * 0.4f * Mathf.Sin(t * 0.7f), 0f, roll * Mathf.Sin(t));
        }
    }
}
