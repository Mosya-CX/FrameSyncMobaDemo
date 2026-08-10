using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Presentation-only red line from a tower to its projectile-locked
    /// target (NonHero v5 搂9). Shown while a tower shot is unresolved;
    /// never touches deterministic state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerTargetLinePresenter :
        MonoBehaviour
    {
        [SerializeField] private Unit towerUnit;
        private LineRenderer line;

        private void Awake()
        {
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
                    TowerAttackHandler tower) ||
                towerUnit.World == null)
            {
                return;
            }
            // Continuous target-lock line (pure presentation): shown while
            // the tower has a valid, living attack target, independent of
            // the attack cadence / in-flight projectile.
            UnitUid targetUid =
                tower.LockedTarget.IsValid()
                    ? tower.LockedTarget
                    : tower.CurrentTargetUid;
            if (!targetUid.IsValid() ||
                !towerUnit.World.TryGetUnit(
                    targetUid,
                    out Unit target) ||
                target.LifeState != LifeState.Alive)
            {
                return;
            }
            Vector3 from = towerUnit.transform.position;
            from.y += 2f;
            line.SetPosition(0, from);
            line.SetPosition(1, target.transform.position);
            line.enabled = true;
        }
    }
}
