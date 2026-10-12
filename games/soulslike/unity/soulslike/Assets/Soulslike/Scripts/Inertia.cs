using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// How an agent drives a fighter (the training arena): three speeds, each
    /// from -1 to 1 of the fighter's own most: ahead (back when below 0), to
    /// the right (left), turning clockwise (anticlockwise). Each decision
    /// keeps a speed, or orders it one way: the same way as it goes adds
    /// <see cref="Step"/>; from standing, or against the way it goes, it is at
    /// once <see cref="Step"/> the new way (so a turn about, and a roll any
    /// way, takes one decision). Turning, against the way it turns stops it
    /// first (so it can stop on its aim; the next order turns it back).
    /// Nothing else slows it but an action: any attack, quick move or roll
    /// stops all three (Fighter.Act).
    ///
    /// Or the turning is left to <see cref="focus"/>: the body turns to the
    /// other by itself, at its most turning speed, till a turning order
    /// (either way) takes it back by hand.
    /// </summary>
    [System.Serializable]
    public struct Inertia
    {
        /// <summary>A third: from still to the most in three decisions (0.6 s).
        /// A quarter was too slow to learn to start at all.</summary>
        public const float Step = 1f / 3f;

        public float forward, side, turn;
        /// <summary>Turning to the other by itself (Fighter), not by <see cref="turn"/>.</summary>
        public bool focus;

        /// <summary>One decision: per speed 0 keeps it, 1 orders it up (ahead,
        /// right, clockwise), 2 down; turning 3 to focus on the other (an order
        /// up or down ends the focus, from standing still).</summary>
        public void Push(int forwardOrder, int sideOrder, int turnOrder)
        {
            forward = Order(forward, forwardOrder);
            side = Order(side, sideOrder);
            if (turnOrder == 3)
            {
                focus = true;
                turn = 0f;
            }
            else
            {
                if (turnOrder == 1 || turnOrder == 2) focus = false;
                turn = Order(turn, turnOrder, stopFirst: true);
            }
        }

        public void Stop() => forward = side = turn = 0f;

        static float Order(float now, int order, bool stopFirst = false)
        {
            if (order != 1 && order != 2) return now;
            float way = order == 1 ? 1f : -1f;
            if (stopFirst && now * way < 0f) return 0f;
            // From standing or against the way it goes: the least speed the new way.
            if (now * way <= 0f) return way * Step;
            return Mathf.Clamp(now + way * Step, -1f, 1f);
        }
    }
}
