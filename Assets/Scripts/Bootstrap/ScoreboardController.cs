using FrameSyncMoba.LuaBridge;
using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
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

        [Header("Team Colors")]
        [SerializeField] private Color teamBlueColor = new Color(0.3f, 0.5f, 1f);
        [SerializeField] private Color teamRedColor = new Color(1f, 0.3f, 0.3f);

        private ScoreboardRow[] _rows = System.Array.Empty<ScoreboardRow>();
        private bool _isVisible;
        private int _sortMode;
        private int[] _sortMap;

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
                rowsContainer.anchorMin = new Vector2(0.2f, 0.3f);
                rowsContainer.anchorMax = new Vector2(0.8f, 0.85f);
                rowsContainer.offsetMin = Vector2.zero;
                rowsContainer.offsetMax = Vector2.zero;
                var bg = go.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.85f);
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
            if (_isVisible && Input.GetMouseButtonDown(0))
                _sortMode = (_sortMode + 1) % 3;
            if (!_isVisible) return;
            if (!LuaDataCache.HasValidData) return;

            var dto = LuaDataCache.Latest;
            int count = dto.AllPlayerKills?.Count ?? 0;
            EnsureRows(count);
            BuildSortMap(dto, count);

            for (int displayIdx = 0; displayIdx < count; displayIdx++)
            {
                int srcIdx = (_sortMap != null && displayIdx < _sortMap.Length)
                    ? _sortMap[displayIdx] : displayIdx;
                if (_rows[displayIdx] == null) continue;

                string name = (dto.AllPlayerNames != null && srcIdx < dto.AllPlayerNames.Count)
                    ? dto.AllPlayerNames[srcIdx] : $"Player {srcIdx + 1}";
                int kills = (dto.AllPlayerKills != null && srcIdx < dto.AllPlayerKills.Count)
                    ? dto.AllPlayerKills[srcIdx] : 0;
                int deaths = (dto.AllPlayerDeaths != null && srcIdx < dto.AllPlayerDeaths.Count)
                    ? dto.AllPlayerDeaths[srcIdx] : 0;
                int assists = (dto.AllPlayerAssists != null && srcIdx < dto.AllPlayerAssists.Count)
                    ? dto.AllPlayerAssists[srcIdx] : 0;
                Color rowColor = srcIdx < count / 2 ? teamBlueColor : teamRedColor;
                _rows[displayIdx].Set(name, kills, deaths, assists, rowColor);
            }
        }

        private void BuildSortMap(UiSnapshotDto dto, int count)
        {
            if (_sortMap == null || _sortMap.Length != count)
                _sortMap = new int[count];
            for (int i = 0; i < count; i++)
                _sortMap[i] = i;

            switch (_sortMode)
            {
                case 0:
                    System.Array.Sort(_sortMap, (a, b) =>
                    {
                        int sA = GetKdaScore(dto, a);
                        int sB = GetKdaScore(dto, b);
                        int cmp = sB.CompareTo(sA);
                        if (cmp != 0) return cmp;
                        return a.CompareTo(b);
                    });
                    break;
                case 1:
                    System.Array.Sort(_sortMap, (a, b) =>
                    {
                        int ka = ValAt(dto.AllPlayerKills, a);
                        int kb = ValAt(dto.AllPlayerKills, b);
                        int cmp = kb.CompareTo(ka);
                        if (cmp != 0) return cmp;
                        return a.CompareTo(b);
                    });
                    break;
                case 2:
                    System.Array.Sort(_sortMap, (a, b) =>
                    {
                        int da = ValAt(dto.AllPlayerDeaths, a);
                        int db = ValAt(dto.AllPlayerDeaths, b);
                        int cmp = da.CompareTo(db);
                        if (cmp != 0) return cmp;
                        return a.CompareTo(b);
                    });
                    break;
            }
        }

        private static int ValAt(System.Collections.Generic.List<int> list, int idx)
            => (list != null && idx < list.Count) ? list[idx] : 0;

        private static int GetKdaScore(UiSnapshotDto dto, int idx)
        {
            int k = ValAt(dto.AllPlayerKills, idx);
            int d = ValAt(dto.AllPlayerDeaths, idx);
            int a = ValAt(dto.AllPlayerAssists, idx);
            return (k + a) * 10 - d * 5;
        }

        private void EnsureRows(int count)
        {
            if (_rows.Length == count) return;
            for (int i = 0; i < _rows.Length; i++)
                if (_rows[i] != null)
                    Destroy(_rows[i].gameObject);
            _rows = new ScoreboardRow[count];
            for (int i = 0; i < count; i++)
                _rows[i] = CreateRow(i);
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
            nameGo.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 24);

            var kdaGo = new GameObject("KDA", typeof(RectTransform));
            kdaGo.transform.SetParent(transform, false);
            _kdaText = kdaGo.AddComponent<Text>();
            _kdaText.font = font;
            _kdaText.fontSize = 14;
            _kdaText.alignment = TextAnchor.MiddleRight;
            kdaGo.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 24);
        }

        public void Set(string name, int kills, int deaths, int assists, Color teamColor)
        {
            if (_nameText != null)
            {
                _nameText.text = name;
                _nameText.color = teamColor;
            }
            if (_kdaText != null)
            {
                _kdaText.text = $"{kills} / {deaths} / {assists}";
                _kdaText.color = new Color(0.9f, 0.9f, 0.9f);
            }
        }
    }
}
