using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>
    /// A ring spreading on the ground about a fighter whose adrenaline rose
    /// (DuelShow.LevelUp): wider and longer the higher the level, fading out;
    /// then gone.
    /// </summary>
    public class LevelUpRing : MonoBehaviour
    {
        public Transform follow;
        public float reach = 1.5f, life = 0.6f;
        public Color color = Color.white;
        LineRenderer line;
        float age;

        void Start()
        {
            line = GetComponent<LineRenderer>();
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = age / life;
            if (t >= 1f || follow == null)
            {
                Destroy(gameObject);
                return;
            }
            float radius = Mathf.Lerp(0.3f, reach, 1f - (1f - t) * (1f - t));
            var at = follow.position + Vector3.up * 0.05f;
            for (int i = 0; i < line.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
            var c = color;
            c.a = 1f - t;
            line.startColor = line.endColor = c;
            line.widthMultiplier = Mathf.Lerp(0.12f, 0.03f, t);
        }
    }
}
