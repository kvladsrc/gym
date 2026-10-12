using UnityEngine;

namespace Soulslike
{
    /// <summary>An enemy's spoken lines (the studio's XTTS, Art/Audio/Voices):
    /// on noticing the hero, sometimes on attacking or being hurt, on dying.
    /// One line at a time, a pause between them.</summary>
    public class EnemyVoice : MonoBehaviour
    {
        public AudioClip[] notice = { }, attack = { }, hurt = { }, death = { };
        public float volume = 1f;
        /// <summary>Seconds of quiet after a line.</summary>
        public float pause = 2.5f;

        float quietUntil;

        public void Notice() => Say(notice, 1f);
        public void Attack() => Say(attack, 0.5f);
        public void Hurt() => Say(hurt, 0.6f);
        public void Death() => Say(death, 1f, always: true);

        void Say(AudioClip[] lines, float chance, bool always = false)
        {
            if (lines.Length == 0 || (!always && (Time.time < quietUntil || Random.value > chance))) return;
            var line = lines[Random.Range(0, lines.Length)];
            Sounds.Voice(line, transform.position + Vector3.up * 1.6f, volume);
            quietUntil = Time.time + line.length + pause;
        }
    }
}
