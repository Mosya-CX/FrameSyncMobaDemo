using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Client-local hover outline driver for the full match flow. Resolves
    /// the unit under the pointer every frame and drives that unit's
    /// ClientUnitOutline (green for allies, red for enemies). Presentation
    /// only; never touches deterministic state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitOutlineHoverDriver : MonoBehaviour
    {
        private MouseWorldResolver resolver;
        private UnitWorld unitWorld;
        private TeamId localTeam = TeamId.Neutral;
        private UnitUid currentHovered;
        private ClientUnitOutline currentOutline;

        public void Initialize(
            MouseWorldResolver mouseResolver,
            UnitWorld world,
            TeamId team)
        {
            resolver = mouseResolver;
            unitWorld = world;
            localTeam = team;
            currentHovered = default;
            currentOutline = null;
        }

        private void LateUpdate()
        {
            if (resolver == null ||
                unitWorld == null)
            {
                return;
            }

            UnitUid? hovered = resolver
                .ResolveUnitTarget(
                    resolver.LastScreenPosition);
            if (hovered.HasValue &&
                hovered.Value == currentHovered &&
                currentOutline != null)
            {
                return;
            }

            if (currentOutline != null)
            {
                currentOutline.SetHighlighted(
                    false,
                    Color.white);
                currentOutline = null;
            }
            currentHovered = default;

            if (!hovered.HasValue ||
                !unitWorld.TryGetUnit(
                    hovered.Value,
                    out FrameSyncMoba.Unit.Unit
                        unit) ||
                unit.LifeState !=
                    LifeState.Alive)
            {
                return;
            }

            ClientUnitOutline outline =
                unit.GetComponent<
                    ClientUnitOutline>();
            if (outline == null)
            {
                return;
            }

            bool friendly =
                unit.TeamId == localTeam;
            outline.SetHighlighted(
                true,
                friendly
                    ? Color.green
                    : Color.red);
            currentOutline = outline;
            currentHovered = hovered.Value;
        }
    }
}
