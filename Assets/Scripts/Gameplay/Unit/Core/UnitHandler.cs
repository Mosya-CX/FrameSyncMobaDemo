using System;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public abstract class UnitHandler : MonoBehaviour
    {
        public Unit Owner { get; private set; }

        internal void BindOwner(Unit owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (Owner != null && Owner != owner)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} is already bound to Unit {Owner.UnitUid}.");
            }

            bool ownerChanged = Owner != owner;
            Owner = owner;
            if (ownerChanged)
            {
                OnOwnerChanged();
            }
            OnOwnerBound();
        }

        protected virtual void OnOwnerChanged()
        {
        }

        protected virtual void OnOwnerBound()
        {
        }

        public virtual void InitializeForNewRuntime()
        {
        }

        public virtual void ClearForDeath()
        {
        }

        public virtual void ClearForRespawn()
        {
        }

        public virtual void ResetForPool()
        {
        }
    }
}
