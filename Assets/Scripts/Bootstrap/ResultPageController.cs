using FrameSyncMoba.FrameSync;
using FrameSyncMoba.LuaBridge;
using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Displays match result when MatchFlow reaches Finished phase.
    /// Reads MatchResultSnapshot via GameBootstrap.MatchFlow.Result.
    /// Presentation-only; never enters GameplaySnapshot.
    /// (ExecPlan 0092, UI/Lua Design v9.1)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultPageController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private Canvas resultCanvas;
        [SerializeField] private RectTransform contentPanel;

        [Header("Text Elements")]
        [SerializeField] private Text winnerText;
        [SerializeField] private Text durationText;
        [SerializeField] private Text endReasonText;

        [Header("KDA Container")]
        [SerializeField] private RectTransform kdaContainer;
        [SerializeField] private GameObject kdaRowPrefab;

        [Header("Action")]
        [SerializeField] private Button returnButton;
        [SerializeField] private Text returnButtonText;

        private Font _font;
        private bool _shown;

        public bool IsShown => _shown;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureCanvas();
            resultCanvas.gameObject.SetActive(false);
            if (returnButton != null)
                returnButton.onClick.AddListener(Hide);
        }

        public void Show(MatchResultSnapshot resultInfo)
        {
            if (_shown) return;
            _shown = true;

            EnsureCanvas();
            resultCanvas.gameObject.SetActive(true);

            // Winner
            string winnerLabel = resultInfo.WinningTeamId.Value == 0
                ? "Draw" : $"Team {resultInfo.WinningTeamId.Value} Wins!";
            if (winnerText != null) winnerText.text = winnerLabel;

            // Duration
            int seconds = resultInfo.DurationTicks / 30; // 30 ticks/sec
            int mins = seconds / 60;
            int secs = seconds % 60;
            if (durationText != null) durationText.text = $"Duration: {mins}:{secs:D2}";

            // End reason
            if (endReasonText != null)
                endReasonText.text = resultInfo.EndReason.ToString();

            // KDA rows
            PopulateKda(resultInfo.Statistics);
        }

        public void Hide()
        {
            if (!_shown) return;
            _shown = false;
            resultCanvas.gameObject.SetActive(false);
        }

        private void EnsureCanvas()
        {
            if (resultCanvas == null)
            {
                var go = new GameObject("ResultCanvas",
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false);
                resultCanvas = go.GetComponent<Canvas>();
                resultCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                resultCanvas.sortingOrder = 100;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);
            }
            if (contentPanel == null)
            {
                var go = new GameObject("ContentPanel", typeof(RectTransform));
                go.transform.SetParent(resultCanvas.transform, false);
                contentPanel = go.GetComponent<RectTransform>();
                contentPanel.anchorMin = new Vector2(0.3f, 0.2f);
                contentPanel.anchorMax = new Vector2(0.7f, 0.8f);
                contentPanel.offsetMin = Vector2.zero;
                contentPanel.offsetMax = Vector2.zero;
                var vlg = go.AddComponent<VerticalLayoutGroup>();
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.spacing = 16f;
                vlg.padding = new RectOffset(24, 24, 24, 24);
                vlg.childAlignment = TextAnchor.UpperCenter;
            }

            if (winnerText == null)
            {
                var go = new GameObject("WinnerText", typeof(Text));
                go.transform.SetParent(contentPanel, false);
                winnerText = go.GetComponent<Text>();
                winnerText.font = _font;
                winnerText.fontSize = 36;
                winnerText.alignment = TextAnchor.MiddleCenter;
                winnerText.color = new Color(1f, 0.85f, 0.3f);
            }

            if (durationText == null)
            {
                var go = new GameObject("DurationText", typeof(Text));
                go.transform.SetParent(contentPanel, false);
                durationText = go.GetComponent<Text>();
                durationText.font = _font;
                durationText.fontSize = 20;
                durationText.alignment = TextAnchor.MiddleCenter;
                durationText.color = Color.white;
            }

            if (endReasonText == null)
            {
                var go = new GameObject("EndReasonText", typeof(Text));
                go.transform.SetParent(contentPanel, false);
                endReasonText = go.GetComponent<Text>();
                endReasonText.font = _font;
                endReasonText.fontSize = 16;
                endReasonText.alignment = TextAnchor.MiddleCenter;
                endReasonText.color = new Color(0.7f, 0.7f, 0.7f);
            }

            if (kdaContainer == null)
            {
                var go = new GameObject("KDAContainer", typeof(RectTransform));
                go.transform.SetParent(contentPanel, false);
                kdaContainer = go.GetComponent<RectTransform>();
                var vlg = go.AddComponent<VerticalLayoutGroup>();
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.spacing = 4f;
                vlg.padding = new RectOffset(0, 0, 12, 0);
            }

            if (returnButton == null)
            {
                var go = new GameObject("ReturnButton", typeof(RectTransform),
                    typeof(Image), typeof(Button));
                go.transform.SetParent(contentPanel, false);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(200, 48);
                var img = go.GetComponent<Image>();
                img.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                returnButton = go.GetComponent<Button>();
                returnButton.onClick.AddListener(Hide);

                var label = new GameObject("Label", typeof(Text));
                label.transform.SetParent(go.transform, false);
                returnButtonText = label.GetComponent<Text>();
                returnButtonText.font = _font;
                returnButtonText.fontSize = 18;
                returnButtonText.alignment = TextAnchor.MiddleCenter;
                returnButtonText.color = Color.white;
                returnButtonText.text = "Return to Main Menu";
            }
        }

        private void PopulateKda(in FrameSync.MatchStatisticsResult stats)
        {
            // Clear existing
            if (kdaContainer != null)
            {
                for (int i = kdaContainer.childCount - 1; i >= 0; i--)
                    Destroy(kdaContainer.GetChild(i).gameObject);
            }

            if (stats.Entries == null || kdaContainer == null) return;

            for (int i = 0; i < stats.Entries.Length; i++)
            {
                var entry = stats.Entries[i];
                GameObject go;
                if (kdaRowPrefab != null)
                    go = Instantiate(kdaRowPrefab, kdaContainer);
                else
                {
                    go = new GameObject($"KDARow_{i}", typeof(RectTransform));
                    go.transform.SetParent(kdaContainer, false);
                }

                var text = go.GetComponent<Text>();
                if (text == null)
                    text = go.AddComponent<Text>();

                text.font = _font;
                text.fontSize = 14;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.text = $"Hero #{entry.HeroUnitUid.SpawnLogicTick}  "
                    + $"K: {entry.Kills}  D: {entry.Deaths}  A: {entry.Assists}";
            }
        }
    }
}
