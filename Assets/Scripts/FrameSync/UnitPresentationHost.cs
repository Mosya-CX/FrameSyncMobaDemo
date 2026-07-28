using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public sealed class UnitPresentationHost : MonoBehaviour
    {
        [SerializeField] private Unit.Unit _ownerUnit;

        public Unit.Unit OwnerUnit => _ownerUnit;

        /// <summary>
        /// The UnitAnimationProfile for this unit. Provides Animator parameter hashes.
        /// Stored as a ScriptableObject referenced by the Unit prefab.
        /// </summary>
        [SerializeField] private Presentation.UnitAnimationProfile _profile;
        public Presentation.UnitAnimationProfile Profile => _profile;

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
