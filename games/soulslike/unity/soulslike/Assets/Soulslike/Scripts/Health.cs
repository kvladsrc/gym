using System;
using UnityEngine;

namespace Soulslike
{
    /// <summary>Hit points of the player, an enemy or the boss.</summary>
    public class Health : MonoBehaviour
    {
        public float max = 100f;
        public float Current { get; private set; }
        public bool Dead => Current <= 0f;
        /// <summary>Set during a roll's invulnerability frames.</summary>
        public bool Invulnerable { get; set; }

        public event Action<float> Damaged;
        public event Action Died;

        void Awake() => Current = max;

        public void Restore() => Current = max;

        public void Heal(float amount)
        {
            if (!Dead) Current = Mathf.Min(max, Current + amount);
        }

        public bool Damage(float amount)
        {
            if (Dead || Invulnerable || amount <= 0f) return false;
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount);
            if (Dead) Died?.Invoke();
            return true;
        }
    }
}
