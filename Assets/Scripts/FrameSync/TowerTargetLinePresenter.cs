using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Client presentation-only red line from a tower to its current attack
    /// intent. Target replacement is followed immediately; the component
    /// never modifies deterministic Gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerTargetLinePresenter :
        MonoBehaviour
    {
        [SerializeField] private Unit towerUnit;
        private LineRenderer line;

        private void Awake()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#else
            line = gameObject.AddComponent<LineRenderer>();
            Shader shader =
                Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.material =
                    new Material(shader);
                line.material.color =
                    new Color(1f, 0.1f, 0.1f, 0.9f);
            }
            line.startWidth = 0.15f;
            line.endWidth = 0.15f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.enabled = false;
#endif
        }

        private void LateUpdate()
        {
            if (line == null)
            {
                return;
            }
            line.enabled = false;
            if (towerUnit == null)
            {
                towerUnit =
                    GetComponentInParent<Unit>();
            }
            if (towerUnit == null ||
                !(towerUnit.AttackHandler is
                    TowerAttackHandler) ||
                towerUnit.World == null)
            {
                return;
            }

            // Never fall back to the last projectile lock: that UID may be
            // stale after the target dies or later respawns.
            if (!TryResolveDisplayTarget(
                    towerUnit,
                    out Unit target))
            {
                return;
            }

            Vector3 from = towerUnit.transform.position;
            from.y += 2f;
            line.SetPosition(0, from);
            line.SetPosition(1, target.transform.position);
            line.enabled = true;
        }

        private static bool TryResolveDisplayTarget(
            Unit tower,
            out Unit target)
        {
            target = null;
            if (tower?.World == null ||
                tower.Intent.Kind != IntentKind.AttackTarget)
            {
                return false;
            }

            UnitUid targetUid = tower.Intent.TargetUnit;
            if (!targetUid.IsValid() ||
                !tower.World.TryGetUnit(targetUid, out target) ||
                target == null ||
                target.LifeState != LifeState.Alive ||
                !target.CapabilityState.IsTargetable ||
                target.TeamId == TeamId.Neutral ||
                target.TeamId == tower.TeamId)
            {
                target = null;
                return false;
            }

            return true;
        }
    }
}
