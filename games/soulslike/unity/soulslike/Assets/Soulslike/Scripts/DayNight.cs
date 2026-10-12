using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soulslike
{
    /// <summary>
    /// The level by night or by day (the pause menu switches; the choice is
    /// kept between runs): the sky and its haze, the ambient light, the
    /// directional light (a dim moon with faint shadows, or the sun), the day's
    /// own grade (a volume over the night's), and what only shines at night:
    /// the lanterns, the level's lamps, the fireflies. The bonfires burn in
    /// both.
    /// </summary>
    public class DayNight : MonoBehaviour
    {
        [System.Serializable]
        public class Look
        {
            public Material sky;
            public Color fog = Color.grey;
            public float fogStart = 40f, fogEnd = 135f;
            public Color ambientSky, ambientEquator, ambientGround;
            public Color lightColor = Color.white;
            public float lightIntensity = 1f, shadowStrength = 1f;
            public Vector3 lightAngles = new Vector3(40f, -35f, 0f);
        }

        public static DayNight Instance { get; private set; }
        public static bool Day { get; private set; }

        public Look night = new(), day = new();
        public Light sun;
        /// <summary>The day's grade: weight 1 by day, 0 by night.</summary>
        public Volume dayGrade;
        /// <summary>Shown only at night (the lanterns' lights and glow, the fireflies).</summary>
        public GameObject[] nightOnly = { };

        readonly List<Light> lamps = new();

        void Awake()
        {
            Instance = this;
            // The level's own lamps: every light but the sun and the bonfires'.
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light != sun && light.GetComponentInParent<Bonfire>() == null) lamps.Add(light);
            Apply(PlayerPrefs.GetInt("daytime", 0) == 1);
        }

        public void Toggle() => Apply(!Day);

        void Apply(bool day)
        {
            Day = day;
            PlayerPrefs.SetInt("daytime", day ? 1 : 0);
            var look = day ? this.day : night;
            if (look.sky != null) RenderSettings.skybox = look.sky;
            RenderSettings.fogColor = look.fog;
            RenderSettings.fogStartDistance = look.fogStart;
            RenderSettings.fogEndDistance = look.fogEnd;
            RenderSettings.ambientSkyColor = look.ambientSky;
            RenderSettings.ambientEquatorColor = look.ambientEquator;
            RenderSettings.ambientGroundColor = look.ambientGround;
            if (sun != null)
            {
                sun.color = look.lightColor;
                sun.intensity = look.lightIntensity;
                sun.shadowStrength = look.shadowStrength;
                sun.transform.rotation = Quaternion.Euler(look.lightAngles);
            }
            if (dayGrade != null) dayGrade.weight = day ? 1f : 0f;
            foreach (var go in nightOnly) if (go != null) go.SetActive(!day);
            // Lights under nightOnly follow their objects; the level's own lamps here.
            foreach (var lamp in lamps) if (lamp != null) lamp.enabled = !day;
            DynamicGI.UpdateEnvironment();
        }
    }
}
