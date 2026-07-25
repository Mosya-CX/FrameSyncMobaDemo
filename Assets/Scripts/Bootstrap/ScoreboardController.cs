using FrameSyncMoba.LuaBridge;
using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Reads KDA statistics from LuaDataCache each Unity frame
    /// and renders a simple scoreboard with hero names, kills,
    /// deaths, and assists. Presentation-only.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 4-5, 10
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreboardController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private Canvas scoreboardCanvas;
        [SerializeField] private RectTransform rowsContainer;

        [Header("Row Prefabs")]
        [SerializeField] private GameObject rowPrefab;

        [Header("Visibility")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        [Header("Templates")]
        [SerializeField] private Font rowFont;

        private ScoreboardRow[] _rows = System.Array.Empty<ScoreboardRow>();
        private bool _isVisible;

        private void Awake()
        {
            if (scoreboardCanvas == null)
            {
                var go = new GameObject("ScoreboardCanvas", typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false);
                scoreboardCanvas = go.GetComponent<Canvas>();
                scoreboardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (rowsContainer == null)
            {
                var go = new GameObject("RowsContainer", typeof(RectTransform));
                go.transform.SetParent(scoreboardCanvas.transform, false);
                rowsContainer = go.GetComponent<RectTransform>();
                rowsContainer.anchorMin = new Vector2(0.25f, 0.35f);
                rowsContainer.anchorMax = new Vector2(0.75f, 0.85f);
                rowsContainer.offsetMin = Vector2.zero;
                rowsContainer.offsetMax = Vector2.zero;
                go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
                var vlg = go.AddComponent<VerticalLayoutGroup>();
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.spacing = 2f;
                vlg.padding = new RectOffset(12, 12, 12, 12);
            }
            scoreboardCanvas.gameObject.SetActive(false);

            if (rowFont == null)
                rowFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _isVisible = !_isVisible;
                scoreboardCanvas.gameObject.SetActive(_isVisible);
            }

            if (!_isVisible) return;

            if (!LuaDataCache.HasValidData) return;

            var dto = LuaDataCache.Latest;
            int count = dto.AllPlayerKills?.Count ?? 0;

            EnsureRows(count);

            for (int i = 0; i < count; i++)
            {
                if (_rows[i] == null) continue;
                string name = (dto.AllPlayerNames != null && i < dto.AllPlayerNames.Count)
                    ? dto.AllPlayerNames[i] : $"Player {i + 1}";
                int kills = (dto.AllPlayerKills != null && i < dto.AllPlayerKills.Count)
                    ? dto.AllPlayerKills[i] : 0;
                int deaths = (dto.AllPlayerDeaths != null && i < dto.AllPlayerDeaths.Count)
                    ? dto.AllPlayerDeaths[i] : 0;
                int assists = (dto.AllPlayerAssists != null && i < dto.AllPlayerAssists.Count)
                    ? dto.AllPlayerAssists[i] : 0;
                _rows[i].Set(name, kills, deaths, assists);
            }
        }

        private void EnsureRows(int count)
        {
            if (_rows.Length == count) return;

            for (int i = 0; i < _rows.Length; i++)
                if (_rows[i] != null)
                    Destroy(_rows[i].gameObject);

            _rows = new ScoreboardRow[count];
            for (int i = 0; i < count; i++)
            {
                _rows[i] = CreateRow(i);
            }
        }

        private ScoreboardRow CreateRow(int index)
        {
            GameObject go;
            if (rowPrefab != null)
            {
                go = Instantiate(rowPrefab, rowsContainer);
            }
            else
            {
                go = new GameObject($"Row_{index}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                go.transform.SetParent(rowsContainer, false);
                var hlg = go.GetComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.spacing = 8f;
                hlg.padding = new RectOffset(4, 4, 2, 2);
            }

            var row = go.AddComponent<ScoreboardRow>();
            row.Initialize(rowFont);
            return row;
        }
    }

    /// <summary>
    /// Single scoreboard row: name, kills, deaths, assists.
    /// </summary>
    public sealed class ScoreboardRow : MonoBehaviour
    {
        private Text _nameText;
        private Text _kdaText;

        public void Initialize(Font font)
        {
            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(transform, false);
            _nameText = nameGo.AddComponent<Text>();
            _nameText.font = font;
            _nameText.fontSize = 14;
            _nameText.alignment = TextAnchor.MiddleLeft;
            _nameText.color = Color.white;
            nameGo.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 24);

            var kdaGo = new GameObject("KDA", typeof(RectTransform));
            kdaGo.transform.SetParent(transform, false);
            _kdaText = kdaGo.AddComponent<Text>();
            _kdaText.font = font;
            _kdaText.fontSize = 14;
            _kdaText.alignment = TextAnchor.MiddleRight;
            _kdaText.color = new Color(0.8f, 0.8f, 0.8f);
            kdaGo.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 24);
        }

        public void Set(string name, int kills, int deaths, int assists)
        {
            if (_nameText != null) _nameText.text = name;
            if (_kdaText != null) _kdaText.text = $"{kills} / {deaths} / {assists}";
        }
    }
}
