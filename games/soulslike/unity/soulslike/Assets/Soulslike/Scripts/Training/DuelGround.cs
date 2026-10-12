using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>
    /// Where a duellist's agent fights, as it sees it (FighterAgent): the
    /// square's centre and half its side (its place in it), and the fight's
    /// clock. The training arena's episode (DuelEpisode), or in the game a
    /// creature's own post (Creature).
    /// </summary>
    public abstract class DuelGround : MonoBehaviour
    {
        public abstract Vector3 Centre { get; }
        /// <summary>Half the square's side, m.</summary>
        public abstract float HalfSize { get; }
        /// <summary>The share of the fight's time limit gone, 0–1.</summary>
        public abstract float Elapsed { get; }
    }
}
