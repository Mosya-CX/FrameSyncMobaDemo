using FrameSyncMoba.LuaBridge;
using FrameSyncMoba.RuntimeConfig;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Hero select grid UI. Displays available heroes from GlobalPrefabTable,
    /// allows selection and lock-in. Bridges Lobby flow to match start.
    /// Presentation-only.
    /// (ExecPlan 0093, UI/Lua Design v9.1)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroSelectPageController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private Canvas selectCanvas;
        [SerializeField] private RectTransform gridContainer;

        [Header("Prefab")]
        [SerializeField] private GameObject heroSlotPrefab;

        [Header("Lock-In")]
        [SerializeField] private Button lockInButton;
        [SerializeField] private Text lockInText;
        [SerializeField] private Text statusText;
        [SerializeField] private TMP_Text lockInTextMeshPro;
        [SerializeField] private TMP_Text statusTextMeshPro;

        private int _selectedHeroId = -1;
        private bool _locked;
        private Font _font;
        private LobbyPanelController _lobbyPanel;
        private ClientUiActionRouter _actionRouter;
        private int[] _heroConfigIds = System.Array.Empty<int>();

        public int SelectedHeroId => _selectedHeroId;
        public bool IsLocked => _locked;

        public void Inject(
            LobbyPanelController lobbyPanel,
            ClientUiActionRouter actionRouter)
        {
            _lobbyPanel = lobbyPanel ??
                throw new System.ArgumentNullException(
                    nameof(lobbyPanel));
            _actionRouter = actionRouter ??
                throw new System.ArgumentNullException(
                    nameof(actionRouter));
        }

        public void ConfigureHeroOptions(
            int[] heroConfigIds)
        {
            if (heroConfigIds == null ||
                heroConfigIds.Length == 0)
                throw new System.ArgumentException(
                    "At least one hero config ID is required.",
                    nameof(heroConfigIds));
            _heroConfigIds =
                (int[])heroConfigIds.Clone();
            System.Array.Sort(_heroConfigIds);
            for (int i = 0;
                 i < _heroConfigIds.Length;
                 i++)
            {
                if (_heroConfigIds[i] <= 0 ||
                    (i > 0 &&
                     _heroConfigIds[i - 1] ==
                     _heroConfigIds[i]))
                    throw new System.ArgumentException(
                        "Hero config IDs must be positive and unique.",
                        nameof(heroConfigIds));
            }
        }

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureCanvas();

            if (lockInButton != null)
                lockInButton.onClick.AddListener(OnLockInClicked);
        }

        public void Show()
        {
            UIPanel panel = GetComponent<UIPanel>();
            if (panel != null &&
                !panel.IsOpen)
                panel.Open();
            else if (selectCanvas.gameObject !=
                     gameObject)
                selectCanvas.gameObject.SetActive(
                    true);
            PopulateGrid();
        }

        public void Hide()
        {
            UIPanel panel = GetComponent<UIPanel>();
            if (panel != null &&
                panel.IsOpen)
                panel.Close();
            else if (selectCanvas.gameObject !=
                     gameObject)
                selectCanvas.gameObject.SetActive(
                    false);
        }

        private void EnsureCanvas()
        {
            if (selectCanvas == null)
            {
                var go = new GameObject("HeroSelectCanvas",
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false);
                selectCanvas = go.GetComponent<Canvas>();
                selectCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                selectCanvas.sortingOrder = 10;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.1f, 1f);
            }
            if (gridContainer == null)
            {
                var go = new GameObject("GridContainer", typeof(RectTransform));
                go.transform.SetParent(selectCanvas.transform, false);
                gridContainer = go.GetComponent<RectTransform>();
                gridContainer.anchorMin = new Vector2(0.15f, 0.15f);
                gridContainer.anchorMax = new Vector2(0.85f, 0.7f);
                gridContainer.offsetMin = Vector2.zero;
                gridContainer.offsetMax = Vector2.zero;
                var glg = go.AddComponent<GridLayoutGroup>();
                glg.cellSize = new Vector2(160, 180);
                glg.spacing = new Vector2(16, 16);
                glg.padding = new RectOffset(24, 24, 24, 24);
                glg.childAlignment = TextAnchor.UpperCenter;
            }
            if (lockInButton == null)
            {
                var go = new GameObject("LockInButton", typeof(RectTransform),
                    typeof(Image), typeof(Button));
                go.transform.SetParent(selectCanvas.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.4f, 0.75f);
                rt.anchorMax = new Vector2(0.6f, 0.82f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                go.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f);
                lockInButton = go.GetComponent<Button>();

                var label = new GameObject("Label", typeof(Text));
                label.transform.SetParent(go.transform, false);
                lockInText = label.GetComponent<Text>();
                lockInText.font = _font;
                lockInText.fontSize = 24;
                lockInText.alignment = TextAnchor.MiddleCenter;
                lockInText.color = Color.white;
                lockInText.text = "Lock In";
            }
            if (statusText == null)
            {
                var go = new GameObject("StatusText", typeof(Text));
                go.transform.SetParent(selectCanvas.transform, false);
                statusText = go.GetComponent<Text>();
                statusText.font = _font;
                statusText.fontSize = 18;
                statusText.alignment = TextAnchor.MiddleCenter;
                statusText.color = new Color(0.8f, 0.8f, 0.8f);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.3f, 0.84f);
                rt.anchorMax = new Vector2(0.7f, 0.9f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                statusText.text = "Select a hero";
            }
        }

        private void PopulateGrid()
        {
            if (gridContainer == null) return;

            for (int i = gridContainer.childCount - 1; i >= 0; i--)
                Destroy(gridContainer.GetChild(i).gameObject);

            for (int i = 0;
                 i < _heroConfigIds.Length;
                 i++)
            {
                int heroId = _heroConfigIds[i];
                GameObject go;
                if (heroSlotPrefab != null)
                    go = Instantiate(heroSlotPrefab, gridContainer);
                else
                {
                    go = new GameObject($"HeroSlot_{heroId}", typeof(RectTransform),
                        typeof(Image), typeof(Button));
                    go.transform.SetParent(gridContainer, false);
                    go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f);
                }

                var btn = go.GetComponent<Button>();
                if (btn == null) btn = go.AddComponent<Button>();
                int capturedId = heroId;
                btn.onClick.AddListener(() => OnHeroClicked(capturedId));

                var label = go.GetComponentInChildren<Text>();
                TMP_Text meshProLabel =
                    go.GetComponentInChildren<TMP_Text>();
                if (label == null &&
                    meshProLabel == null)
                {
                    var labelGo = new GameObject("Label", typeof(Text));
                    labelGo.transform.SetParent(go.transform, false);
                    label = labelGo.GetComponent<Text>();
                }
                if (label != null)
                {
                    label.font = _font;
                    label.fontSize = 16;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = Color.white;
                    label.text = $"Hero {heroId}";
                }
                if (meshProLabel != null)
                    meshProLabel.text = $"Hero {heroId}";
            }
        }

        private void OnHeroClicked(int heroId)
        {
            if (_locked) return;
            _actionRouter?.SelectHero(heroId);
            _selectedHeroId = heroId;
            SetText(
                statusText,
                statusTextMeshPro,
                $"Selected: Hero {heroId}");
        }

        private void OnLockInClicked()
        {
            if (_locked || _selectedHeroId <= 0) return;
            _actionRouter?.LockHero(
                _selectedHeroId);
            _locked = true;
            SetText(
                lockInText,
                lockInTextMeshPro,
                "Locked!");
            SetText(
                statusText,
                statusTextMeshPro,
                $"Locked: Hero {_selectedHeroId}");
            _lobbyPanel?.OnHeroLocked(_selectedHeroId);
        }

        private static void SetText(
            Text legacyText,
            TMP_Text meshProText,
            string value)
        {
            if (legacyText != null)
                legacyText.text = value;
            if (meshProText != null)
                meshProText.text = value;
        }
    }
}
