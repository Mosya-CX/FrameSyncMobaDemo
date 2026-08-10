using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public sealed class UnitPresentationHost : MonoBehaviour
    {
        [SerializeField] private Unit.Unit _ownerUnit;
        private Unit.UnitUid _registeredUid;

        public Unit.Unit OwnerUnit => _ownerUnit;

        /// <summary>
        /// The UnitAnimationProfile for this unit. Provides Animator parameter hashes.
        /// Stored as a ScriptableObject referenced by the Unit prefab.
        /// </summary>
        [SerializeField] private Presentation.UnitAnimationProfile _profile;
        public Presentation.UnitAnimationProfile Profile => _profile;

        private PresentationSocketSet _sockets;

        /// <summary>
        /// Named socket transforms for VFX / SFX attachment (Presentation
        /// Design v13.2 section 6). Null when the unit prefab has no socket
        /// set; consumers fall back to the host root.
        /// </summary>
        public PresentationSocketSet Sockets =>
            _sockets != null
                ? _sockets
                : _sockets =
                    GetComponent<PresentationSocketSet>();

        public void Bind(Unit.Unit unit)
        {
            if (_registeredUid.IsValid())
                UnitPresentationRegistry.Unregister(_registeredUid);
            _ownerUnit = unit;
            RefreshRegistration();
        }

        private void OnEnable()
        {
            if (_ownerUnit == null)
                _ownerUnit = GetComponent<Unit.Unit>();
            RefreshRegistration();
        }

        private void LateUpdate()
        {
            RefreshRegistration();
        }

        private void OnDisable()
        {
            if (_registeredUid.IsValid())
                UnitPresentationRegistry.Unregister(_registeredUid);
            _registeredUid = default;
        }

        private void RefreshRegistration()
        {
            if (_ownerUnit == null)
                return;

            Unit.UnitUid currentUid = _ownerUnit.UnitUid;
            if (!currentUid.IsValid() || currentUid == _registeredUid)
                return;

            if (_registeredUid.IsValid())
                UnitPresentationRegistry.Unregister(_registeredUid);
            UnitPresentationRegistry.Register(currentUid, this);
            _registeredUid = currentUid;
        }
    }
}
