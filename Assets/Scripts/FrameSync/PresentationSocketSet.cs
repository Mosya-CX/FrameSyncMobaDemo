using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Presentation v13.2 §2.1 — MonoBehaviour that exposes named socket
    /// Transforms on a unit prefab for VFX and SFX attachment.
    /// 
    /// Managed by UnitPresentationHost. Queried by VfxManager and AudioManager.
    /// Does NOT manage any playing instances.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PresentationSocketSet : MonoBehaviour
    {
        [Header("Core Sockets")]
        [Tooltip("Root/center of the unit.")]
        public Transform Root;

        [Tooltip("Head socket for overhead effects.")]
        public Transform Head;

        [Tooltip("Chest/body socket.")]
        public Transform Chest;

        [Header("Hand Sockets")]
        [Tooltip("Right hand socket (weapon hand).")]
        public Transform Hand_R;

        [Tooltip("Left hand socket (off-hand).")]
        public Transform Hand_L;

        [Header("Foot Sockets")]
        [Tooltip("Right foot socket.")]
        public Transform Foot_R;

        [Tooltip("Left foot socket.")]
        public Transform Foot_L;

        [Header("Projectile")]
        [Tooltip("Projectile spawn socket.")]
        public Transform ProjectileOrigin;

        /// <summary>
        /// Tries to get a socket by name. Case-insensitive match against
        /// the common socket names.
        /// </summary>
        public bool TryGetSocket(string socketName, out Transform socket)
        {
            socket = null;
            if (string.IsNullOrEmpty(socketName)) return false;

            switch (socketName.ToLowerInvariant())
            {
                case "root": socket = Root; break;
                case "head": socket = Head; break;
                case "chest": case "body": socket = Chest; break;
                case "hand_r": case "righthand": socket = Hand_R; break;
                case "hand_l": case "lefthand": socket = Hand_L; break;
                case "foot_r": case "rightfoot": socket = Foot_R; break;
                case "foot_l": case "leftfoot": socket = Foot_L; break;
                case "projectile": case "projectileorigin": socket = ProjectileOrigin; break;
                default: return false;
            }

            return socket != null;
        }

        private void Reset()
        {
            // Auto-assign common child transforms
            Root = transform;
            Head = transform.Find("Head") ?? transform.Find("head");
            Chest = transform.Find("Chest") ?? transform.Find("chest") ?? transform.Find("Spine") ?? transform.Find("spine");
            Hand_R = FindDeepChild(transform, "Hand_R") ?? FindDeepChild(transform, "hand_r");
            Hand_L = FindDeepChild(transform, "Hand_L") ?? FindDeepChild(transform, "hand_l");
            Foot_R = FindDeepChild(transform, "Foot_R") ?? FindDeepChild(transform, "foot_r");
            Foot_L = FindDeepChild(transform, "Foot_L") ?? FindDeepChild(transform, "foot_l");
            ProjectileOrigin = Hand_R ?? Chest ?? Root;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
