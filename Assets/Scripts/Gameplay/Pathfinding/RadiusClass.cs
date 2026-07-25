using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit radius classification for grid clearance layers
    /// (Pathfinding Design v13.1 section 14.2).
    /// Maps to three walkability layers in PathGridMap2D.
    /// </summary>
    public enum RadiusClass : byte
    {
        /// <summary>Small units: minions, projectiles (radius ~0.25)</summary>
        Small = 0,

        /// <summary>Medium units: heroes (radius ~0.5)</summary>
        Medium = 1,

        /// <summary>Large units: turrets, bosses (radius ~0.75)</summary>
        Large = 2,
    }

    public static class RadiusClassHelper
    {
        public const int Count = 3;

        // Default radius values in fp world units
        public static readonly fp SmallRadius = (fp)0.25m;
        public static readonly fp MediumRadius = (fp)0.5m;
        public static readonly fp LargeRadius = (fp)0.75m;

        public static fp GetRadius(RadiusClass rc) => rc switch
        {
            RadiusClass.Small => SmallRadius,
            RadiusClass.Medium => MediumRadius,
            RadiusClass.Large => LargeRadius,
            _ => MediumRadius,
        };

        /// <summary>
        /// Derive RadiusClass from a physics radius.
        /// Small: radius <= 0.35; Large: radius > 0.6; else Medium.
        /// </summary>
        public static RadiusClass FromRadius(fp radius)
        {
            if (radius <= (fp)0.35m) return RadiusClass.Small;
            if (radius > (fp)0.6m) return RadiusClass.Large;
            return RadiusClass.Medium;
        }
    }
}
