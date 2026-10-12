using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// What blows land on: capsules on a character's bones (hurtboxes), which
    /// a fighter's striking part is swept against (Fighter); and the share of
    /// a blow's top speed that marks its window (LevelBuilder.Arena's MeasureDuel).
    /// </summary>
    public static partial class LevelBuilder
    {
        /// <summary>The share of an attack's peak speed above which it is
        /// dangerous: the frames around the peak faster than this are the blow.</summary>
        const float StrikeThreshold = 0.45f;

        /// <summary>Capsules on a character's bones that blows meet (triggers on
        /// the Hurtbox layer): the torso, the head, the upper and lower arms and
        /// legs, their size from the character's height.</summary>
        static void Hurtboxes(GameObject root, float height)
        {
            float scale = height / 1.8f;
            // Each from one bone to the next, past both by its radius; one
            // ending at the far bone stops there (the torso's: else a hood
            // stood over the shoulders round the neck).
            void Capsule(string from, string to, float radius, bool endsAtBone = false)
            {
                var a = Bone(root, from);
                var b = Bone(root, to);
                if (a == null || b == null) return;
                var box = new GameObject("Hurtbox " + from).transform;
                box.SetParent(a, false);
                box.position = (a.position + b.position) / 2f;
                box.rotation = Quaternion.FromToRotation(Vector3.up, b.position - a.position);
                box.localScale = Vector3.one / a.lossyScale.x; // world metres
                box.gameObject.layer = Training.Fighter.HurtboxLayer;
                var capsule = box.gameObject.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.direction = 1;
                capsule.radius = radius * scale;
                capsule.height = Vector3.Distance(a.position, b.position) + radius * scale * (endsAtBone ? 1f : 2f);
                if (endsAtBone) capsule.center = Vector3.down * radius * scale / 2f;
            }
            if (Bone(root, "Hips") == null)
            {
                // No rig (a placeholder): one capsule, the body's.
                var body = new GameObject("Hurtbox").AddComponent<CapsuleCollider>();
                body.transform.SetParent(root.transform, false);
                body.gameObject.layer = Training.Fighter.HurtboxLayer;
                body.isTrigger = true;
                body.height = height;
                body.radius = 0.35f * scale;
                body.center = Vector3.up * height / 2f;
                return;
            }
            Capsule("Hips", "Neck", 0.14f, endsAtBone: true);
            Capsule("Neck", "Head", 0.13f);
            foreach (var side in new[] { "Left", "Right" })
            {
                Capsule(side + "Arm", side + "ForeArm", 0.07f);
                Capsule(side + "ForeArm", side + "Hand", 0.06f);
                Capsule(side + "UpLeg", side + "Leg", 0.09f);
                Capsule(side + "Leg", side + "Foot", 0.07f);
            }
        }
    }
}
