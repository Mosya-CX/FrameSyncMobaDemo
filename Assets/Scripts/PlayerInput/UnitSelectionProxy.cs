using FrameSyncMoba.Unit;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.PlayerInput
{
    [DisallowMultipleComponent]
    public sealed class UnitSelectionProxy : MonoBehaviour
    {
        [SerializeField] private UnitType unit;
        [SerializeField] private int selectionPriority;

        public UnitUid UnitUid => unit != null ? unit.UnitUid : default;
        public int SelectionPriority => selectionPriority;

        private void Reset()
        {
            unit = GetComponentInParent<UnitType>();
        }

        private void OnValidate()
        {
            unit ??= GetComponentInParent<UnitType>();
        }
    }
}
