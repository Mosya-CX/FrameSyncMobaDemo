using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public sealed class UnitPresentationHost : MonoBehaviour
    {
        [SerializeField] private Unit.Unit _ownerUnit;

        public Unit.Unit OwnerUnit => _ownerUnit;

        public void Bind(Unit.Unit unit)
        {
            _ownerUnit = unit;
            UnitPresentationRegistry.Register(unit.UnitUid, this);
        }

        private void OnEnable()
        {
            if (_ownerUnit != null)
                UnitPresentationRegistry.Register(_ownerUnit.UnitUid, this);
        }

        private void OnDisable()
        {
            if (_ownerUnit != null)
                UnitPresentationRegistry.Unregister(_ownerUnit.UnitUid);
        }
    }
}
