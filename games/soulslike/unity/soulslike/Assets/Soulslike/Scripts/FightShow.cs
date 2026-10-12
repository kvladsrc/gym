using Soulslike.Training;
using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// What the level's fights show and sound like, as the arena viewer's
    /// (ArenaRecorder): sparks and a sound where a blow lands, a flash and a
    /// ring of steel where a hammer stops a sword (DuelShow), a swing's sound
    /// as a blow begins. For every fighter in the scene, the hero and the
    /// creatures.
    /// </summary>
    public class FightShow : MonoBehaviour
    {
        public AudioClip hitSound, swingSound;
        /// <summary>The sparks' shader (set by the level's builder).</summary>
        public Shader effectShader;

        Fighter[] fighters = { };
        int[] lastDoing = { };
        float[] lastProgress = { };
        Material glass;

        void Start()
        {
            if (effectShader != null) glass = new Material(effectShader);
            fighters = FindObjectsByType<Fighter>(FindObjectsSortMode.None);
            lastDoing = new int[fighters.Length];
            lastProgress = new float[fighters.Length];
            foreach (var fighter in fighters)
            {
                var me = fighter;
                me.Hit += at => DuelShow.Hit(at, DuelShow.StrikingWith(me), glass, hitSound, null);
                me.Blocked += at => DuelShow.Block(at, glass);
            }
        }

        void Update()
        {
            for (int i = 0; i < fighters.Length; i++)
            {
                var me = fighters[i];
                // A blow begun (or the same begun anew): its swing heard.
                if (me.Doing >= 0 && me.Doing < me.Attacks && (me.Doing != lastDoing[i] || me.Progress < lastProgress[i]))
                    DuelShow.Play(swingSound, me.transform.position + Vector3.up, 0.5f, 1f);
                lastDoing[i] = me.Doing;
                lastProgress[i] = me.Progress;
            }
        }
    }
}
