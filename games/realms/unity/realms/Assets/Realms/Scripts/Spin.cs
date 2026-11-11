using UnityEngine;

namespace Realms
{
    /// <summary>Turns steadily around the local z axis: windmill sails.</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float speed = 40f;

        void Update() => transform.Rotate(0f, 0f, speed * Time.deltaTime, Space.Self);
    }
}
