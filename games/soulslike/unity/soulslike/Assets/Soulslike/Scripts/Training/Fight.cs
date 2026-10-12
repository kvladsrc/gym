using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>What both agents see the same way.</summary>
    static class Fight
    {
        /// <summary>Phases an action is cut into for the agents' eyes: equal
        /// shares of its clip (each action × phase is an input of its own).</summary>
        public const int Phases = 5;

        /// <summary>The other's turning between my snapshots.</summary>
        public struct Track
        {
            public float yaw, time;
        }

        /// <summary>3 numbers about the other's turning, seen however far:
        /// which way it faces, relative to the line to me (sin, cos: cos 1
        /// facing me, -1 its back to me), and its turning, over 360°/s
        /// (clockwise positive), measured since my last snapshot (so it reads
        /// a person's play as well as an agent's). Where it is: <see cref="Points"/>.</summary>
        public static void Relative(VectorSensor sensor, Fighter self, Fighter them, ref Track track)
        {
            // From the bodies' hips, not their controllers: a clip carries a
            // body off its line.
            var other = them.transform;
            var toMe = self.Ground - them.Ground;
            toMe.y = 0f;
            float angle = toMe.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(toMe, other.forward, Vector3.up) * Mathf.Deg2Rad : 0f;
            sensor.AddObservation(Mathf.Sin(angle));
            sensor.AddObservation(Mathf.Cos(angle));
            float dt = Time.time - track.time;
            bool fresh = dt > 1e-4f && track.time > 0f;
            float turning = fresh ? Mathf.DeltaAngle(track.yaw, other.eulerAngles.y) / dt : 0f;
            track = new Track { yaw = other.eulerAngles.y, time = Time.time };
            sensor.AddObservation(Mathf.Clamp(turning / 360f, -1f, 1f));
        }

        /// <summary>A body's points watched: its hips, head, hands and feet (Fighter.Watched).</summary>
        public const int PointCount = 6;
        /// <summary>The numbers <see cref="Points"/> gives for one body.</summary>
        public const int PointObservations = 6 * PointCount;
        /// <summary>A point's speed seen in full, m/s (a fist or foot at its
        /// fastest, ~10).</summary>
        const float PointSpeed = 12f;

        /// <summary>6 numbers for each of a body's points (its hips, head,
        /// left and right hand, left and right foot: the other's, or my own),
        /// all alike, in my frame
        /// (to my right, up, ahead as I face; from the ground under my hips):
        /// where it is, as p / (|p| + my height) (near, nearly metres over my
        /// height; far, the way to it alone, |p| towards 1: the hips at 20 m
        /// and a fist at 0.5 m in one scale); and its speed (right, up,
        /// ahead) since my last snapshot, over <see cref="PointSpeed"/>.
        /// <paramref name="last"/> keeps the points between snapshots; the
        /// numbers given go to <paramref name="seen"/> from <paramref name="at"/>
        /// too, a point's place then its speed (GridView draws them back).</summary>
        public static void Points(VectorSensor sensor, Fighter self, Fighter body, Vector3[] last, ref float time, Vector3[] seen, int at)
        {
            var me = self.transform;
            var from = self.Ground;
            float tall = Fighter.Tall * self.Size;
            float dt = Time.time - time;
            bool fresh = dt > 1e-4f && time > 0f;
            for (int i = 0; i < PointCount; i++)
            {
                var point = body.Watched(i);
                var local = me.InverseTransformDirection(point - from);
                var place = local / (local.magnitude + tall);
                var velocity = fresh ? me.InverseTransformDirection(point - last[i]) / dt / PointSpeed : Vector3.zero;
                velocity = new Vector3(Mathf.Clamp(velocity.x, -1f, 1f), Mathf.Clamp(velocity.y, -1f, 1f), Mathf.Clamp(velocity.z, -1f, 1f));
                sensor.AddObservation(place);
                sensor.AddObservation(velocity);
                seen[at + 2 * i] = place;
                seen[at + 2 * i + 1] = velocity;
                last[i] = point;
            }
            time = Time.time;
        }

        /// <summary>A point's place back from its numbers (<see cref="Points"/>),
        /// in the frame it was seen in, m.</summary>
        public static Vector3 PlaceOf(Vector3 seen, float tall)
        {
            float q = Mathf.Min(seen.magnitude, 0.999f);
            return q < 1e-6f ? Vector3.zero : seen / seen.magnitude * (tall * q / (1f - q));
        }

        /// <summary>A point's speed back from its numbers, m/s.</summary>
        public static Vector3 SpeedOf(Vector3 seen) => seen * PointSpeed;

        /// <summary>4 numbers: my own speeds as I drive them, and whether I
        /// am focused on the other (Inertia).</summary>
        public static void Motion(VectorSensor sensor, Inertia motion)
        {
            sensor.AddObservation(motion.forward);
            sensor.AddObservation(motion.side);
            sensor.AddObservation(motion.turn);
            sensor.AddObservation(motion.focus ? 1f : 0f);
        }

        /// <summary>count + 1 + <see cref="Phases"/> numbers: what a fighter is
        /// doing, one-hot (nothing, or an action: an attack or the roll), and
        /// which phase of it, one-hot (all 0 when doing nothing).</summary>
        public static void Doing(VectorSensor sensor, Fighter fighter)
        {
            sensor.AddOneHotObservation(fighter.Doing + 1, fighter.Actions + 1);
            int phase = Mathf.Min(Phases - 1, Mathf.FloorToInt(fighter.Progress * Phases));
            for (int i = 0; i < Phases; i++) sensor.AddObservation(fighter.Doing >= 0 && i == phase ? 1f : 0f);
        }

        /// <summary>2 numbers: where I am in the arena, in my frame, over its half size.</summary>
        public static void Place(VectorSensor sensor, Transform me, Vector3 centre, float halfSize)
        {
            var local = me.InverseTransformDirection(me.position - centre);
            sensor.AddObservation(Mathf.Clamp(local.x / halfSize, -1.5f, 1.5f));
            sensor.AddObservation(Mathf.Clamp(local.z / halfSize, -1.5f, 1.5f));
        }
    }
}
