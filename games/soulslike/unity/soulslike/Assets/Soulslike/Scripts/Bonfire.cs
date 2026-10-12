using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulslike
{
    /// <summary>Checkpoint: resting heals the player and brings the hollows back.</summary>
    public class Bonfire : MonoBehaviour
    {
        public float radius = 2.6f;
        public Light flame;

        public bool PlayerNear { get; private set; }
        public Vector3 SpawnPoint => transform.position + transform.forward * 2f;
        public float SpawnYaw => transform.eulerAngles.y;

        Hero player;
        float baseIntensity;
        Sounds.Loop crackle;

        void Start()
        {
            player = FindFirstObjectByType<Hero>();
            if (flame != null) baseIntensity = flame.intensity;
            crackle = new Sounds.Loop(gameObject, spatial: true, volume: 1f, minDistance: 3f, maxDistance: 20f);
            if (Sounds.Instance != null && Sounds.Instance.bonfire != null) crackle.Play(Sounds.Instance.bonfire);
        }

        void Update()
        {
            crackle?.Update();
            if (flame != null)
                flame.intensity = baseIntensity * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, 0f));
            PlayerNear = player != null && !player.Health.Dead
                && Vector3.Distance(player.transform.position, transform.position) < radius;
            if (PlayerNear && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                GameManager.Instance?.Rest(this);
        }
    }
}
