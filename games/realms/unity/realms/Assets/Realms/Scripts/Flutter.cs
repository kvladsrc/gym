using UnityEngine;

namespace Realms
{
    /// <summary>
    /// A hero's banner flying harder while the hero rides and settling within half a second of the
    /// stop (the visual plan's proposal 3 of 2026-11-11): a wave height for this figure's cloth
    /// alone, given to the flag shader through a property block, so every other flag keeps its own.
    /// HeroView kicks it on every frame of a step.
    /// </summary>
    public sealed class Flutter : MonoBehaviour
    {
        static readonly int WaveId = Shader.PropertyToID("_Wave");

        /// <summary>How much harder the cloth waves on the way, and how long it takes to settle, in seconds.</summary>
        const float Hard = 1.8f, Settle = 0.5f;

        Renderer[] cloths;
        float[] calm;
        MaterialPropertyBlock block;
        float kicked = -10f;
        bool set;

        /// <summary>The banner is carried along this frame.</summary>
        public void Kick()
        {
            kicked = Time.time;
            enabled = true;
        }

        void Awake()
        {
            var found = new System.Collections.Generic.List<Renderer>();
            var waves = new System.Collections.Generic.List<float>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(WaveId))
                    continue;
                found.Add(renderer);
                waves.Add(material.GetFloat(WaveId));
            }
            cloths = found.ToArray();
            calm = waves.ToArray();
        }

        void Update()
        {
            float k = 1f - Mathf.Clamp01((Time.time - kicked) / Settle);
            if (k <= 0f)
            {
                if (set)
                {
                    // Back to the shared material, and to its batching.
                    foreach (Renderer cloth in cloths)
                    {
                        if (cloth != null)
                            cloth.SetPropertyBlock(null);
                    }
                    set = false;
                }
                enabled = false;
                return;
            }
            block ??= new MaterialPropertyBlock();
            float harder = Mathf.Lerp(1f, Hard, k * k * (3f - 2f * k));
            for (int i = 0; i < cloths.Length; i++)
            {
                if (cloths[i] == null)
                    continue;
                block.SetFloat(WaveId, calm[i] * harder);
                cloths[i].SetPropertyBlock(block);
            }
            set = true;
        }
    }
}
