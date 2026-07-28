using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.UI;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Renders a minimap overlay with unit dots, lane path lines,
    /// and tower position markers.
    /// Reads unit positions from UnitWorld each frame.
    /// Presentation-only.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 4-5
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapController : MonoBehaviour
    {
        [Header("Minimap Settings")]
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private int textureWidth = 256;
        [SerializeField] private int textureHeight = 256;
        [SerializeField] private fp worldWidth = (fp)200;
        [SerializeField] private fp worldHeight = (fp)200;

        [Header("Dot Colors")]
        [SerializeField] private Color32 allyColor = new Color32(0, 100, 255, 255);
        [SerializeField] private Color32 enemyColor = new Color32(255, 50, 50, 255);
        [SerializeField] private Color32 neutralColor = new Color32(200, 200, 0, 255);

        [Header("Structure Markers")]
        [SerializeField] private Color32 towerAllyColor = new Color32(0, 80, 200, 255);
        [SerializeField] private Color32 towerEnemyColor = new Color32(200, 50, 50, 255);
        [SerializeField] private Color32 laneLineColor = new Color32(40, 40, 60, 255);

        [Header("Visibility")]
        [SerializeField] private bool alwaysVisible = true;

        private Texture2D _texture;
        private Color32[] _pixels;
        private int _localPlayerSlot = -1;
        private UnitType _controlledUnit;
        private UnitWorld _unitWorld;

        // Cached lane endpoints for overlay drawing
        private static readonly fp2[] _laneTop = new[] { new fp2(-fp.one * 80, fp.one * 80), new fp2(fp.one * 80, fp.one * 80) };
        private static readonly fp2[] _laneMid = new[] { new fp2(-fp.one * 80, fp.zero), new fp2(fp.one * 80, fp.zero) };
        private static readonly fp2[] _laneBot = new[] { new fp2(-fp.one * 80, -fp.one * 80), new fp2(fp.one * 80, -fp.one * 80) };

        private void Awake()
        {
            _texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            _texture.filterMode = FilterMode.Point;
            _pixels = new Color32[textureWidth * textureHeight];

            if (minimapImage == null)
                minimapImage = GetComponentInChildren<RawImage>();
            if (minimapImage != null)
                minimapImage.texture = _texture;
        }

        /// <summary>
        /// Bind to the current local player and UnitWorld for team-aware dot coloring.
        /// </summary>
        public void Bind(UnitType controlledUnit, UnitWorld unitWorld)
        {
            _controlledUnit = controlledUnit;
            _unitWorld = unitWorld;
            _localPlayerSlot = controlledUnit?.ControlledByPlayerSlot ?? -1;
        }

        private void Update()
        {
            if (!alwaysVisible || _unitWorld == null || _texture == null) return;

            ClearTexture();
            DrawLaneLines();

            var units = _unitWorld.GetAllUnits();
            if (units == null) return;

            TeamId localTeam = (_controlledUnit != null) ? _controlledUnit.TeamId : TeamId.Neutral;

            for (int i = 0; i < units.Count; i++)
            {
                UnitType unit = units[i];
                if (unit == null || unit.LifeState != LifeState.Alive) continue;

                PhysicsEntity2D entity = unit.PhysicsEntity;
                if (entity == null) continue;

                fp x = entity.Transform2D.Position.x;
                fp y = entity.Transform2D.Position.y;

                if (unit.UnitKind == UnitKind.Structure)
                {
                    DrawStructureDot(x, y, unit, localTeam);
                }
                else
                {
                    DrawDot(x, y, GetDotColor(unit, localTeam));
                }
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply();
        }

        private void DrawLaneLines()
        {
            DrawLineSegment(_laneTop[0], _laneTop[1], laneLineColor);
            DrawLineSegment(_laneMid[0], _laneMid[1], laneLineColor);
            DrawLineSegment(_laneBot[0], _laneBot[1], laneLineColor);
        }

        private void DrawLineSegment(fp2 start, fp2 end, Color32 color)
        {
            int sx = WorldToTexX(start.x);
            int sy = WorldToTexY(start.y);
            int ex = WorldToTexX(end.x);
            int ey = WorldToTexY(end.y);

            int dx = ex - sx;
            int dy = ey - sy;
            int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            if (steps <= 0) return;

            for (int i = 0; i <= steps; i++)
            {
                int px = sx + (dx * i) / steps;
                int py = sy + (dy * i) / steps;
                if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                {
                    int idx = py * textureWidth + px;
                    if (idx >= 0 && idx < _pixels.Length)
                        _pixels[idx] = color;
                }
            }
        }

        private void DrawStructureDot(fp worldX, fp worldY, UnitType unit, TeamId localTeam)
        {
            int cx = WorldToTexX(worldX);
            int cy = WorldToTexY(worldY);
            Color32 color = unit.TeamId == localTeam ? towerAllyColor : towerEnemyColor;
            int radius = 3;

            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                    {
                        int idx = py * textureWidth + px;
                        if (idx >= 0 && idx < _pixels.Length)
                            _pixels[idx] = color;
                    }
                }
        }

        private Color32 GetDotColor(UnitType unit, TeamId localTeam)
        {
            if (unit.TeamId == localTeam) return allyColor;
            if (unit.TeamId.Value != localTeam.Value && localTeam.Value != 0 && unit.TeamId.Value != 0) return enemyColor;
            return neutralColor;
        }

        private void DrawDot(fp worldX, fp worldY, Color32 color)
        {
            int px = WorldToTexX(worldX);
            int py = WorldToTexY(worldY);
            if (px < 0 || px >= textureWidth || py < 0 || py >= textureHeight) return;
            int index = py * textureWidth + px;
            if (index >= 0 && index < _pixels.Length)
                _pixels[index] = color;
        }

        private void ClearTexture()
        {
            var bg = new Color32(10, 10, 20, 255);
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = bg;
        }

        private int WorldToTexX(fp x) => (int)((x / worldWidth + (fp)0.5m) * (fp)textureWidth);
        private int WorldToTexY(fp y) => (int)((y / worldHeight + (fp)0.5m) * (fp)textureHeight);
    }
}
