using UnityEngine;

namespace Soulslike
{
    /// <summary>Death and respawn, resting, the bosses' fights and themes, the banner text.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public Hero player;
        public Bonfire bonfire;
        /// <summary>The last boss: beating her ends the level.</summary>
        public Creature boss;
        /// <summary>Every boss on the way, the last one too; each has her own
        /// theme and, once beaten, stays dead.</summary>
        public Creature[] bosses = { };
        /// <summary>Where the death sting's hit is (seconds); the banner peaks at 0.8 s.</summary>
        public float deathStingFrom;

        public string Banner { get; private set; } = "";
        public Color BannerColor { get; private set; }
        public float BannerAge { get; private set; } = 99f;
        public bool Victory { get; private set; }
        /// <summary>A boss while she fights: from the moment she notices the hero.</summary>
        public Creature BossInFight
        {
            get
            {
                foreach (var b in Bosses)
                    if (b != null && !b.Health.Dead && b.Engaged) return b;
                return null;
            }
        }

        Creature[] Bosses => bosses.Length > 0 ? bosses : boss != null ? new[] { boss } : bosses;

        /// <summary>The boss theme starts this near the boss
        /// (its intro plays on the way up; the song reaches its chorus about
        /// 50 s in) and ends beyond <see cref="bossMusicLeave"/>.</summary>
        public float bossMusicRange = 40f, bossMusicLeave = 60f;

        bool bossMusic;
        Creature themeOf, engagedBoss;

        float respawnAt = -1f;

        void Awake() => Instance = this;

        void Start()
        {
            player.Health.Died += OnPlayerDied;
            if (bonfire != null) player.Respawn(bonfire.SpawnPoint, bonfire.SpawnYaw);
        }

        void Update()
        {
            BannerAge += Time.deltaTime;
            if (!Victory) BossTheme();
            if (respawnAt > 0f && Time.time >= respawnAt)
            {
                respawnAt = -1f;
                ResetWorld();
                player.Respawn(bonfire.SpawnPoint, bonfire.SpawnYaw);
            }
        }

        /// <summary>The theme of the boss in the fight or the nearest one
        /// follows the hero's approach; the roar the moment a boss engages.</summary>
        void BossTheme()
        {
            if (player.Health.Dead) return;
            Creature near = BossInFight;
            if (near == null)
            {
                float best = float.MaxValue;
                foreach (var b in Bosses)
                {
                    if (b == null || b.Health.Dead) continue;
                    float distance = Vector3.Distance(player.transform.position, b.transform.position);
                    float range = bossMusic && b == themeOf ? bossMusicLeave : bossMusicRange;
                    if (distance < range && distance < best) { best = distance; near = b; }
                }
            }
            if (near != themeOf || (near != null) != bossMusic)
            {
                bossMusic = near != null;
                themeOf = near;
                if (near != null) Sounds.Instance?.PlayMusic(near.theme != null ? near.theme : Sounds.Instance.bossMusic);
                else AmbientAgain();
            }
            var engaged = BossInFight;
            if (engaged != null && engaged != engagedBoss) Sounds.At(Sounds.Instance?.bossRoar, engaged.transform.position);
            engagedBoss = engaged;
        }

        void Show(string text, Color color)
        {
            Banner = text;
            BannerColor = color;
            BannerAge = 0f;
        }

        void OnPlayerDied()
        {
            Show("YOU DIED", new Color(0.62f, 0.07f, 0.05f));
            Sounds.Instance?.StopMusic();
            bossMusic = false;
            themeOf = null;
            Sounds.Flat(Sounds.Instance?.deathSting, 1f, deathStingFrom);
            respawnAt = Time.time + 4.5f;
        }

        public void OnEnemyDied(Creature enemy)
        {
            if (enemy != boss && System.Array.IndexOf(bosses, enemy) < 0) return;
            // The boss theme ends; after a quiet moment the courtyard's music returns.
            Sounds.Instance?.StopMusic();
            bossMusic = false;
            themeOf = null;
            Invoke(nameof(AmbientAgain), 6f);
            if (enemy == boss)
            {
                Victory = true;
                Show("VICTORY ACHIEVED", new Color(0.85f, 0.7f, 0.3f));
            }
            else Show("GREAT FOE FELLED", new Color(0.85f, 0.7f, 0.3f));
        }

        public void Rest(Bonfire at)
        {
            bonfire = at;
            ResetWorld();
            player.Respawn(at.SpawnPoint, at.SpawnYaw);
            Show("BONFIRE LIT", new Color(0.95f, 0.6f, 0.25f));
            Sounds.At(Sounds.Instance?.rest, at.transform.position, 0.7f);
        }

        void ResetWorld()
        {
            foreach (var creature in FindObjectsByType<Creature>(FindObjectsInactive.Include))
                if (!(creature.isBoss && creature.Health.Dead)) creature.Revive();
            bossMusic = false;
            themeOf = null;
            AmbientAgain();
        }

        void AmbientAgain() => Sounds.Instance?.PlayMusic(Sounds.Instance.ambientMusic);
    }
}
