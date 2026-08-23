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
        private Color friendlyColor = Color.green;
        private Color enemyColor = Color.red;
        private float outlineWidth = 0.05f;

        public void Initialize(
            MouseWorldResolver mouseResolver,
            UnitWorld world,
            TeamId team)
        {
            Initialize(
                mouseResolver,
                world,
                team,
                Color.green,
                Color.red,
                0.05f);
        }

        public void Initialize(
            MouseWorldResolver mouseResolver,
            UnitWorld world,
            TeamId team,
            Color friendlyOutlineColor,
            Color enemyOutlineColor,
            float width)
        {
            resolver = mouseResolver;
            unitWorld = world;
            localTeam = team;
            friendlyColor = friendlyOutlineColor;
            enemyColor = enemyOutlineColor;
            outlineWidth = Mathf.Max(0.001f, width);
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

            // D-048 moved the render/outline tree into the asynchronous
            // client view, which is parented under the deterministic unit
            // root. Search the view subtree instead of the logic root only.
            ClientUnitOutline outline =
                unit.GetComponentInChildren<
                    ClientUnitOutline>(true);
            if (outline == null)
            {
                return;
            }

            bool friendly =
                unit.TeamId == localTeam;
            outline.SetOutlineWidth(outlineWidth);
            outline.SetHighlighted(
                true,
                friendly
                    ? friendlyColor
                    : enemyColor);
            currentOutline = outline;
            currentHovered = hovered.Value;
        }
    }
}
