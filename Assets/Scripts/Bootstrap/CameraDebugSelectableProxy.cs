using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Lightweight pointer-selection proxy for CameraDebugScene. It has no
    /// Gameplay Unit, physics authority, networking, or Tick lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraDebugSelectableProxy : MonoBehaviour
    {
        private static readonly List<CameraDebugSelectableProxy> Active =
            new List<CameraDebugSelectableProxy>(128);

        [SerializeField] private int stableId;
        [SerializeField] private byte teamId = 1;
        [SerializeField] private ClientUnitOutline outline;

        public static IReadOnlyList<CameraDebugSelectableProxy> ActiveProxies =>
            Active;
        public int StableId => stableId;
        public byte TeamId => teamId;
        public ClientUnitOutline Outline => outline;
        public Vector3 SelectionPoint => transform.position;

        public void Configure(
            int id,
            byte team,
            ClientUnitOutline targetOutline)
        {
            stableId = id;
            teamId = team;
            outline = targetOutline;
            if (isActiveAndEnabled)
            {
                Active.Remove(this);
                InsertStable(this);
            }
        }

        private void OnEnable()
        {
            InsertStable(this);
            ApplyTeamTint();
        }

        private void OnValidate()
        {
            ApplyTeamTint();
        }

        private void ApplyTeamTint()
        {
            MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
            if (renderer == null)
                return;
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Color tint = teamId == 2
                ? new Color(0.75f, 0.12f, 0.12f, 1f)
                : new Color(0.1f, 0.3f, 0.85f, 1f);
            properties.SetColor("_BaseColor", tint);
            properties.SetColor("_Color", tint);
            renderer.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private static void InsertStable(CameraDebugSelectableProxy proxy)
        {
            if (proxy == null || Active.Contains(proxy))
                return;
            int index = 0;
            while (index < Active.Count &&
                   Active[index].stableId <= proxy.stableId)
            {
                index++;
            }
            Active.Insert(index, proxy);
        }
    }
}
