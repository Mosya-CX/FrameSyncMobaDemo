using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    public enum CameraDebugSide : byte
    {
        Blue = 1,
        Red = 2,
    }

    /// <summary>
    /// Editable draft owned by CameraDebugScene. Its Inspector provides the
    /// explicit one-click copy into the formal shared configuration asset.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CameraDebugWorkbench : MonoBehaviour
    {
        [SerializeField] private MobaCameraPresentationConfig formalConfig;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private CameraDebugPointerProbe pointerProbe;
        [SerializeField] private CameraDebugSide previewSide =
            CameraDebugSide.Blue;

        [Header("Camera draft")]
        [SerializeField] private CameraSideSettings blueSide =
            CameraSideSettings.BlueDefault;
        [SerializeField] private CameraSideSettings redSide =
            CameraSideSettings.RedDefault;

        [Header("Pointer draft")]
        [SerializeField] private float pointerGroundY;
        [SerializeField, Min(0.01f)] private float pointerPickRadius = 4f;
        [SerializeField] private Color friendlyOutlineColor = Color.green;
        [SerializeField] private Color enemyOutlineColor = Color.red;
        [SerializeField, Min(0.001f)] private float outlineWidth = 0.05f;

        public MobaCameraPresentationConfig FormalConfig => formalConfig;
        public CameraSideSettings BlueSide => blueSide;
        public CameraSideSettings RedSide => redSide;
        public float PointerGroundY => pointerGroundY;
        public float PointerPickRadius => pointerPickRadius;
        public Color FriendlyOutlineColor => friendlyOutlineColor;
        public Color EnemyOutlineColor => enemyOutlineColor;
        public float OutlineWidth => outlineWidth;
        public byte PreviewTeamId =>
            previewSide == CameraDebugSide.Red
                ? formalConfig != null
                    ? formalConfig.RedTeamId
                    : (byte)2
                : formalConfig != null
                    ? formalConfig.BlueTeamId
                    : (byte)1;

        private void OnEnable()
        {
            ApplyPreview();
        }

        private void Update()
        {
            if (Application.isPlaying && Input.GetKeyDown(KeyCode.Tab))
                TogglePreviewSide();
        }

        private void OnValidate()
        {
            pointerPickRadius = Mathf.Max(0.01f, pointerPickRadius);
            outlineWidth = Mathf.Max(0.001f, outlineWidth);
            ApplyPreview();
        }

        public void TogglePreviewSide()
        {
            previewSide = previewSide == CameraDebugSide.Blue
                ? CameraDebugSide.Red
                : CameraDebugSide.Blue;
            ApplyPreview();
        }

        public void SetPreviewSide(CameraDebugSide side)
        {
            previewSide = side;
            ApplyPreview();
        }

        public void ApplyPreview()
        {
            if (cameraController != null)
            {
                CameraSideSettings side = previewSide == CameraDebugSide.Red
                    ? redSide
                    : blueSide;
                cameraController.ApplyDebugSide(side, PreviewTeamId);
            }
            if (pointerProbe != null)
                pointerProbe.InvalidateResolver();
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MobaCameraPresentationConfig config,
            CameraController controller,
            CameraDebugPointerProbe probe)
        {
            formalConfig = config;
            cameraController = controller;
            pointerProbe = probe;
            if (formalConfig != null)
            {
                blueSide = formalConfig.BlueSide;
                redSide = formalConfig.RedSide;
                pointerGroundY = formalConfig.PointerGroundY;
                pointerPickRadius = formalConfig.PointerPickRadius;
                friendlyOutlineColor = formalConfig.FriendlyOutlineColor;
                enemyOutlineColor = formalConfig.EnemyOutlineColor;
                outlineWidth = formalConfig.OutlineWidth;
            }
            ApplyPreview();
        }
#endif
    }
}
