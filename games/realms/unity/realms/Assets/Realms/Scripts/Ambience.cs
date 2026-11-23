using System;
using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The quiet ground of sound under everything (the sound plan's П5, the simple form of its
    /// fal.ai amendments of 2026-11-12): looping layers of wind, sea, forest, meadow, hills and
    /// town, each as loud as the share of the view it fills. The view is sampled on a 16×9 grid
    /// once a second; land under the fog adds only wind. Near the ground the near layers come
    /// forward, from afar the wind; every level glides over 1.5 s so the sound does not breathe
    /// with the camera. The town and hero screens, battles and the main menu set their own mix.
    /// Library entries: AmbWind, AmbSea, AmbForest, AmbMeadow, AmbHills, AmbTown.
    /// </summary>
    public sealed class Ambience : MonoBehaviour
    {
        public enum Place
        {
            Off,
            Menu,
            Map,
            Town,
            Hero,
            Battle,
        }

        enum Layer
        {
            Wind,
            Sea,
            Forest,
            Meadow,
            Hills,
            Town,
        }

        static readonly string[] Names = { "AmbWind", "AmbSea", "AmbForest", "AmbMeadow", "AmbHills", "AmbTown" };
        const int Columns = 16, Rows = 9, Count = 6;
        const float Glide = 1.5f;

        static Ambience instance;

        /// <summary>Where the players are; the screens and battles set it.</summary>
        public static Place Now = Place.Off;

        /// <summary>The ground of the hero's cell or of the battle's field.</summary>
        public static Biome Ground = Biome.Meadow;

        readonly AudioSource[] sources = new AudioSource[Count];
        readonly SoundLibrary.Entry[] entries = new SoundLibrary.Entry[Count];
        readonly float[] level = new float[Count], target = new float[Count], share = new float[Count];
        MapView view;
        MapCamera mapCamera;
        Func<int, bool> seen;
        float measureAt;

        public static Ambience Create(GameObject host) => host.AddComponent<Ambience>();

        /// <summary>The map and what the players have seen of it, for the view's shares.</summary>
        public static void Bind(MapView view, MapCamera camera, Func<int, bool> seen)
        {
            if (instance == null)
                return;
            instance.view = view;
            instance.mapCamera = camera;
            instance.seen = seen;
        }

        void Awake()
        {
            instance = this;
        }

        void Start()
        {
            SoundLibrary library = Sounds.Library;
            for (int i = 0; i < Count; i++)
            {
                entries[i] = library?.Find(Names[i]);
                if (entries[i] == null || entries[i].clips.Count == 0)
                    continue;
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.loop = true;
                source.clip = entries[i].clips[UnityEngine.Random.Range(0, entries[i].clips.Count)];
                source.volume = 0f;
                // Each layer starts somewhere in its loop, so that they never line up the same way.
                source.time = UnityEngine.Random.Range(0f, source.clip.length * 0.9f);
                source.Play();
                sources[i] = source;
            }
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now >= measureAt)
            {
                measureAt = now + 1f;
                Measure();
            }
            float step = Time.unscaledDeltaTime / Glide;
            float gain = SoundSettings.Gain(SoundGroup.Ambience) * Sounds.AmbienceDuck;
            for (int i = 0; i < Count; i++)
            {
                level[i] = Mathf.MoveTowards(level[i], target[i], step);
                if (sources[i] != null)
                    sources[i].volume = level[i] * entries[i].volume * gain;
            }
        }

        void Measure()
        {
            Array.Clear(target, 0, Count);
            switch (Now)
            {
                case Place.Menu:
                    target[(int)Layer.Sea] = 0.6f;
                    target[(int)Layer.Wind] = 0.5f;
                    break;
                case Place.Map:
                    MapShares();
                    break;
                case Place.Town:
                    // The town in front, the rest of the map far behind it.
                    MapShares();
                    for (int i = 0; i < Count; i++)
                        target[i] *= 0.4f;
                    target[(int)Layer.Town] = 1f;
                    break;
                case Place.Hero:
                    target[(int)LayerOf(Ground)] = 1f;
                    target[(int)Layer.Wind] = Mathf.Max(target[(int)Layer.Wind], 0.4f);
                    break;
                case Place.Battle:
                    target[(int)LayerOf(Ground)] = 0.6f;
                    break;
            }
        }

        static Layer LayerOf(Biome biome) => biome switch
        {
            Biome.Ocean or Biome.Shallows or Biome.Beach => Layer.Sea,
            Biome.Forest => Layer.Forest,
            Biome.Hills or Biome.Mountains => Layer.Hills,
            _ => Layer.Meadow,
        };

        /// <summary>The layers' levels from the shares of the view: the square root of each share, nearer louder.</summary>
        void MapShares()
        {
            Camera camera = Camera.main;
            World world = view != null ? view.World : null;
            if (camera == null || world == null)
            {
                target[(int)Layer.Wind] = 0.6f;
                return;
            }
            Array.Clear(share, 0, Count);
            float weight = 0f, fog = 0f;
            for (int j = 0; j < Rows; j++)
            {
                for (int i = 0; i < Columns; i++)
                {
                    Ray ray = camera.ViewportPointToRay(new Vector3((i + 0.5f) / Columns, (j + 0.5f) / Rows, 0f));
                    if (ray.direction.y > -0.001f)
                    {
                        fog += 1f;
                        weight += 1f;
                        continue;
                    }
                    Vector3 point = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
                    int cell = view.CellAt(point);
                    if (cell < 0)
                    {
                        // Off the map is open sea.
                        share[(int)Layer.Sea] += 1f;
                        weight += 1f;
                        continue;
                    }
                    if (seen != null && !seen(cell))
                    {
                        fog += 1f;
                        weight += 1f;
                        continue;
                    }
                    Cell c = world.Cells[cell];
                    // Towns are few cells: each counts four times.
                    if (c.City >= 0 || c.District >= 0)
                    {
                        share[(int)Layer.Town] += 4f;
                        weight += 4f;
                        continue;
                    }
                    Layer layer = LayerOf(c.Biome);
                    share[(int)layer] += c.Biome == Biome.Beach ? 0.5f : 1f;
                    if (c.Biome == Biome.Beach)
                        share[(int)Layer.Meadow] += 0.5f;
                    weight += 1f;
                }
            }
            float far = Mathf.Clamp01(Sounds.Far);
            float near = Mathf.Lerp(1f, 0.35f, far);
            for (int i = 1; i < Count; i++)
                target[i] = weight > 0f ? Mathf.Sqrt(share[i] / weight) * near : 0f;
            target[(int)Layer.Wind] = Mathf.Clamp01(Mathf.Lerp(0.4f, 1f, far) + 0.3f * (weight > 0f ? fog / weight : 0f));
        }
    }
}
