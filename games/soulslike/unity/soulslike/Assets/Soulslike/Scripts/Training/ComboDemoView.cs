using System.Collections.Generic;
using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>
    /// What the editor's combo demo (Soulslike/Combo Demo) shows besides the
    /// fight: the target's hurtboxes as see-through capsules, the striking
    /// part's swept path while its window is open, sparks and a sound where a
    /// blow lands (a swing's sound as one begins), the two's health and the
    /// striker's adrenaline and combo on the screen, and, for a blow that missed, how far it stayed from the
    /// hurtboxes (in the console). Its camera stands behind the striker, over
    /// its shoulder, turned to the target. Between blows it drives the striker
    /// as a simple bot would: walking and turning (its inertia, as an agent
    /// does) till it faces the target <see cref="gap"/> m from it
    /// (<see cref="Ready"/>), since the blows' clips carry the body away.
    /// </summary>
    public class ComboDemoView : MonoBehaviour
    {
        public Fighter striker, target;
        /// <summary>The ground kept between the bodies, m.</summary>
        public float gap = 0.5f;
        /// <summary>How near the gap and the facing count as there, m and degrees.</summary>
        public float gapSlack = 0.15f, aimSlack = 12f;
        public float back = 2.2f, up = 1.8f, aside = 1.9f, follow = 5f;
        /// <summary>A label kept over the two, facing the camera.</summary>
        public Transform label;
        public AudioClip hitSound, swingSound;
        public AudioClip[] hurtVoices = { };
        /// <summary>The target's health shown (it cannot die: the bar fills
        /// anew when spent).</summary>
        public float shownHealth = 200f;
        int lastDoing = -1;
        float lastProgress, lastDamage, strikerRose = 99f, targetRose = 99f;

        Material glass;
        Collider[] hurtboxes;
        readonly List<(LineRenderer line, float born)> trail = new List<(LineRenderer, float)>();
        float nearest = float.MaxValue, lastSwept;
        Vector3 lastEnd;
        const float TrailLife = 0.4f;
        /// <summary>The hammer's standing capsules (Fighter.Guard), shown all
        /// the time, a blow or not.</summary>
        readonly LineRenderer[] guard = new LineRenderer[2];

        void Start()
        {
            glass = new Material(Shader.Find("Sprites/Default"));
            var boxes = new List<Collider>();
            foreach (var hurt in target.GetComponentsInChildren<CapsuleCollider>())
            {
                if (hurt.gameObject.layer != Fighter.HurtboxLayer) continue;
                boxes.Add(hurt);
                var shape = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(shape.GetComponent<Collider>());
                shape.name = "Hurtbox shape";
                shape.transform.SetParent(hurt.transform, false);
                shape.transform.localPosition = hurt.center;
                shape.transform.localScale = new Vector3(hurt.radius * 2f, hurt.height / 2f, hurt.radius * 2f);
                var paint = shape.GetComponent<MeshRenderer>();
                paint.sharedMaterial = glass;
                paint.material.color = new Color(0.3f, 0.7f, 1f, 0.25f);
                paint.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            hurtboxes = boxes.ToArray();
            for (int i = 0; i < guard.Length; i++)
            {
                var line = guard[i] = new GameObject("Guard").AddComponent<LineRenderer>();
                line.sharedMaterial = glass;
                line.positionCount = 2;
                line.numCapVertices = 6;
                line.startColor = line.endColor = new Color(0.3f, 1f, 0.4f, 0.4f);
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.enabled = false;
            }
            striker.Swept += OnSwept;
            striker.Hit += OnHit;
            striker.Blocked += OnBlocked;
            striker.Struck += OnStruck;
            striker.LevelRose += OnStrikerRose;
            target.LevelRose += OnTargetRose;
        }

        void OnDestroy()
        {
            if (striker == null) return;
            striker.Swept -= OnSwept;
            striker.Hit -= OnHit;
            striker.Blocked -= OnBlocked;
            striker.Struck -= OnStruck;
            striker.LevelRose -= OnStrikerRose;
            if (target != null) target.LevelRose -= OnTargetRose;
        }

        void OnSwept(Vector3 a, Vector3 b, float radius)
        {
            // A limb's point: its way since the last frame instead.
            var from = (a - b).sqrMagnitude < 1e-4f && Time.time - lastSwept < 0.05f ? lastEnd : a;
            lastEnd = b;
            lastSwept = Time.time;
            var line = new GameObject("Swept").AddComponent<LineRenderer>();
            line.sharedMaterial = glass;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.SetPositions(new[] { from, b });
            line.widthMultiplier = radius * 2f;
            line.startColor = line.endColor = new Color(1f, 0.9f, 0.2f, 0.6f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.Add((line, Time.time));
            for (int k = 0; k <= 4; k++)
            {
                var p = Vector3.Lerp(a, b, k / 4f);
                foreach (var hurt in hurtboxes)
                    nearest = Mathf.Min(nearest, Vector3.Distance(hurt.ClosestPoint(p), p) - radius);
            }
        }

        void OnBlocked(Vector3 at) => DuelShow.Block(at, glass);

        void OnHit(Vector3 at)
        {
            DuelShow.Hit(at, DuelShow.StrikingWith(striker), glass, hitSound, hurtVoices);
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(flash.GetComponent<Collider>());
            flash.name = "Hit flash";
            flash.transform.position = at;
            var paint = flash.GetComponent<MeshRenderer>();
            paint.sharedMaterial = glass;
            paint.material.color = new Color(1f, 0.35f, 0.1f, 0.9f);
            paint.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            StartCoroutine(Fade(flash.transform, paint));
        }

        System.Collections.IEnumerator Fade(Transform flash, MeshRenderer paint)
        {
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                float f = t / 0.2f;
                flash.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.3f, f);
                var c = paint.material.color;
                c.a = 0.9f * (1f - f);
                paint.material.color = c;
                yield return null;
            }
            Destroy(flash.gameObject);
        }

        void OnStrikerRose(int level)
        {
            strikerRose = 0f;
            DuelShow.LevelUp(striker, level, glass);
        }

        void OnTargetRose(int level)
        {
            targetRose = 0f;
            DuelShow.LevelUp(target, level, glass);
        }

        void OnGUI()
        {
            // The target cannot die: its bar fills anew when spent.
            float taken = target.Health.max - target.Health.Current;
            DuelShow.Panel(new Rect(28f, 24f, 380f, 110f), "Striker", striker, lastDamage, strikerRose);
            DuelShow.Panel(new Rect(28f, 160f, 380f, 110f), "Target", target,
                shownHealth - taken % shownHealth, shownHealth, 0f, targetRose);
        }

        void OnStruck(bool landed, float damage)
        {
            if (landed) lastDamage = damage;
            if (!landed)
                Debug.Log(nearest < float.MaxValue
                    ? $"Combo demo: missed by {Mathf.Max(0f, nearest) * 100f:F0} cm"
                    : "Combo demo: missed, its striking part never open");
            nearest = float.MaxValue;
        }

        void Update()
        {
            strikerRose += Time.deltaTime;
            targetRose += Time.deltaTime;
            // A blow begun (or the same begun anew): its swing heard.
            if (striker.Doing >= 0 && striker.Doing < striker.Attacks
                && (striker.Doing != lastDoing || striker.Progress < lastProgress))
                DuelShow.Play(swingSound, striker.transform.position + Vector3.up, 0.5f, 1f);
            lastDoing = striker.Doing;
            lastProgress = striker.Progress;
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                var (line, born) = trail[i];
                float age = Time.time - born;
                if (age > TrailLife)
                {
                    Destroy(line.gameObject);
                    trail.RemoveAt(i);
                    continue;
                }
                var c = new Color(1f, 0.9f, 0.2f, 0.6f * (1f - age / TrailLife));
                line.startColor = line.endColor = c;
            }
            Steer();
        }

        /// <summary>Whether the striker faces the target at the gap.</summary>
        public bool Ready { get; private set; }

        void Steer()
        {
            var line = target.transform.position - striker.transform.position;
            line.y = 0f;
            float angle = Vector3.SignedAngle(striker.transform.forward, line, Vector3.up);
            float off = line.magnitude - 2f * striker.GetComponent<CharacterController>().radius * striker.Size - gap;
            Ready = Mathf.Abs(off) < gapSlack && Mathf.Abs(angle) < aimSlack;
            if (striker.Doing >= 0 || striker.Staggered || Ready)
            {
                striker.motion.Stop();
                return;
            }
            // Turning first, walking once roughly facing it.
            striker.motion.turn = Mathf.Clamp(angle / 45f, -1f, 1f);
            striker.motion.forward = Mathf.Abs(angle) < 45f ? Mathf.Clamp(off / 0.6f, -1f, 1f) : 0f;
            striker.motion.side = 0f;
        }

        void LateUpdate()
        {
            var holder = target.Guards > 0 ? target : striker;
            for (int i = 0; i < guard.Length; i++)
            {
                guard[i].enabled = i < holder.Guards;
                if (!guard[i].enabled) continue;
                var part = holder.Guard[i];
                guard[i].SetPositions(new[] { part.a2, part.b2 });
                guard[i].widthMultiplier = part.radius * 2f;
            }
            var view = Camera.main;
            if (view == null) return;
            var a = striker.transform.position;
            var line = target.transform.position - a;
            line.y = 0f;
            var ahead = line.sqrMagnitude > 0.01f ? line.normalized : striker.transform.forward;
            var right = Vector3.Cross(Vector3.up, ahead);
            var place = a - ahead * back + Vector3.up * up + right * aside;
            var look = Vector3.Lerp(a, target.transform.position, 0.65f) + Vector3.up * 1.2f;
            float share = 1f - Mathf.Exp(-follow * Time.deltaTime);
            view.transform.SetPositionAndRotation(Vector3.Lerp(view.transform.position, place, share),
                Quaternion.Slerp(view.transform.rotation, Quaternion.LookRotation(look - place), share));
            if (label != null)
            {
                label.position = Vector3.Lerp(a, target.transform.position, 0.5f) + Vector3.up * 2.5f;
                label.rotation = view.transform.rotation;
            }
        }
    }
}
