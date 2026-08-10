using System;
using UnityEngine;

namespace FrameSyncMoba.Presentation
{
    /// <summary>
    /// Presentation v13.2 §3.4 - maps an ability stage to an animation state name
    /// and normalized time range.
    /// </summary>
    [Serializable]
    public struct StageAnimationBinding
    {
        /// <summary>The ability definition ID this binding applies to.</summary>
        public int AbilityId;

        /// <summary>Zero-based stage index within the ability.</summary>
        public int StageIndex;

        /// <summary>Full name hash of the Animator state to play.</summary>
        public int StateNameHash;

        /// <summary>Normalized time (0..1) to start playback from.</summary>
        [Range(0f, 1f)]
        public float StartNormalizedTime;

        /// <summary>Normalized time (0..1) to end playback at.</summary>
        [Range(0f, 1f)]
        public float EndNormalizedTime;

        /// <summary>
        /// When true the stage plays on the "CastOverlay" animator layer
        /// (upper-body mask) while the base layer keeps locomotion, for
        /// movable-cast stages such as a charge Hold.
        /// </summary>
        public bool OverlayLayer;

        public static readonly StageAnimationBinding Empty = default;
    }
}
