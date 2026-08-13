using System;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    [Serializable]
    public struct CameraSideSettings
    {
        public Vector3 EulerAngles;
        public Vector3 FollowOffset;
        [Min(0.01f)] public float FollowSpeed;
        [Min(0.01f)] public float PanSpeed;
        [Min(0f)] public float EdgeSize;
        [Range(1f, 179f)] public float FieldOfView;

        public static CameraSideSettings BlueDefault =>
            new CameraSideSettings
            {
                EulerAngles = new Vector3(47f, 0f, 0f),
                FollowOffset = new Vector3(0f, 10f, -10f),
                FollowSpeed = 8f,
                PanSpeed = 40f,
                EdgeSize = 24f,
                FieldOfView = 50f,
            };

        public static CameraSideSettings RedDefault =>
            new CameraSideSettings
            {
                EulerAngles = new Vector3(47f, 180f, 0f),
                FollowOffset = new Vector3(0f, 10f, 10f),
                FollowSpeed = 8f,
                PanSpeed = 40f,
                EdgeSize = 24f,
                FieldOfView = 50f,
            };
    }

    /// <summary>
    /// Shared client-presentation configuration used by the formal match and
    /// CameraDebugScene. It contains no deterministic Gameplay state.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MobaCameraPresentationConfig",
        menuName = "FrameSyncMoba/Presentation/Camera Configuration")]
    public sealed class MobaCameraPresentationConfig : ScriptableObject
    {
        [Header("Team mapping")]
        [SerializeField] private byte blueTeamId = 1;
        [SerializeField] private byte redTeamId = 2;

        [Header("Side cameras")]
        [SerializeField] private CameraSideSettings blueSide =
            CameraSideSettings.BlueDefault;
        [SerializeField] private CameraSideSettings redSide =
            CameraSideSettings.RedDefault;

        [Header("Pointer selection")]
        [SerializeField] private float pointerGroundY;
        [SerializeField, Min(0.01f)] private float pointerPickRadius = 4f;
        [SerializeField] private Color friendlyOutlineColor = Color.green;
        [SerializeField] private Color enemyOutlineColor = Color.red;
        [SerializeField, Min(0.001f)] private float outlineWidth = 0.05f;

        [Header("Render pacing")]
        [SerializeField] private bool enforceVSync = true;
        [SerializeField, Range(0, 4)] private int vSyncCount = 1;
        [SerializeField] private int targetFrameRate = -1;

        [Header("Logic-pose presentation smoothing")]
        [SerializeField] private bool smoothLogicPose = true;
        [SerializeField, Min(0.001f)] private float smoothingDuration = 0.033333f;
        [SerializeField, Min(0.01f)] private float smoothingSnapDistance = 6f;

        public byte BlueTeamId => blueTeamId;
        public byte RedTeamId => redTeamId;
        public CameraSideSettings BlueSide => blueSide;
        public CameraSideSettings RedSide => redSide;
        public float PointerGroundY => pointerGroundY;
        public float PointerPickRadius => pointerPickRadius;
        public Color FriendlyOutlineColor => friendlyOutlineColor;
        public Color EnemyOutlineColor => enemyOutlineColor;
        public float OutlineWidth => outlineWidth;
        public bool SmoothLogicPose => smoothLogicPose;
        public float SmoothingDuration => smoothingDuration;
        public float SmoothingSnapDistance => smoothingSnapDistance;

        public CameraSideSettings ResolveSide(TeamId team)
        {
            return team.Value == redTeamId
                ? redSide
                : blueSide;
        }

        public CameraSideSettings ResolveSide(byte teamId)
        {
            return teamId == redTeamId
                ? redSide
                : blueSide;
        }

        public Color ResolveOutlineColor(byte localTeamId, byte targetTeamId)
        {
            return localTeamId == targetTeamId
                ? friendlyOutlineColor
                : enemyOutlineColor;
        }

        public void ApplyRenderPacing()
        {
            if (!enforceVSync)
                return;
            QualitySettings.vSyncCount = vSyncCount;
            Application.targetFrameRate = targetFrameRate;
        }

#if UNITY_EDITOR
        public void EditorCopyFrom(
            CameraSideSettings blue,
            CameraSideSettings red,
            float groundY,
            float pickRadius,
            Color friendlyColor,
            Color enemyColor,
            float width)
        {
            blueSide = blue;
            redSide = red;
            pointerGroundY = groundY;
            pointerPickRadius = Mathf.Max(0.01f, pickRadius);
            friendlyOutlineColor = friendlyColor;
            enemyOutlineColor = enemyColor;
            outlineWidth = Mathf.Max(0.001f, width);
        }
#endif
    }
}
