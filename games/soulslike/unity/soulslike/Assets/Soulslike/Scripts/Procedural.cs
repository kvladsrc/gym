using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// Animation without rigs: the model (a child of the moving body) bobs,
    /// leans, lunges, spins on a roll and falls when dead.
    /// The models are single TripoSR meshes; Mixamo rigs are a later step.
    /// </summary>
    public class Procedural : MonoBehaviour
    {
        public Transform visual;
        /// <summary>The model has an Animator: its clips do the walking, the
        /// attack and the roll.</summary>
        public bool animated;

        Vector3 restPosition;
        Quaternion restRotation;
        float stride;

        void Awake()
        {
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
        }

        /// <summary>Called every frame by the owner with its current state.</summary>
        /// <param name="speed">Ground speed, m/s.</param>
        /// <param name="lunge">0..1 forward lunge of an attack (negative: wind-up lean back).</param>
        /// <param name="roll">0..1 progress of a roll, or -1.</param>
        public void Pose(float speed, float lunge, float roll)
        {
            if (animated)
            {
                speed = 0f;
                lunge = 0f;
            }
            stride += speed * Time.deltaTime * 2.2f;
            float bob = speed > 0.1f ? Mathf.Abs(Mathf.Sin(stride)) * 0.08f : 0f;
            float sway = speed > 0.1f ? Mathf.Sin(stride) * 4f : 0f;
            Vector3 position = restPosition + Vector3.up * bob + Vector3.forward * (0.45f * Mathf.Max(lunge, 0f));
            Quaternion rotation = restRotation
                * Quaternion.Euler(Mathf.Clamp(speed, 0f, 6f) * 1.5f + lunge * 18f, lunge * 25f, sway);
            if (roll >= 0f && !animated)
            {
                // A forward somersault, low to the ground.
                rotation = Quaternion.Euler(roll * 360f, 0f, 0f) * restRotation;
                position = restPosition + Vector3.up * (0.4f * Mathf.Sin(roll * Mathf.PI)) - Vector3.up * 0.3f;
            }
            visual.localPosition = Vector3.Lerp(visual.localPosition, position, 1f - Mathf.Exp(-20f * Time.deltaTime));
            visual.localRotation = Quaternion.Slerp(visual.localRotation, rotation, 1f - Mathf.Exp(-20f * Time.deltaTime));
        }

        /// <summary>The fall of a dead body: over on its back and into the ground.</summary>
        public void Fallen(float progress)
        {
            visual.localRotation = restRotation * Quaternion.Euler(-85f * Mathf.Clamp01(progress * 1.5f), 0f, 0f);
            visual.localPosition = restPosition - Vector3.up * Mathf.Clamp01(progress - 0.6f) * 1.5f;
        }

        /// <summary>Back to the rest pose (MonoBehaviour.Reset is an editor message).</summary>
        public void Rest()
        {
            if (visual == null) return;
            visual.localPosition = restPosition;
            visual.localRotation = restRotation;
        }

    }
}
