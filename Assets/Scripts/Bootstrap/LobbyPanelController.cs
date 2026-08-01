using FrameSyncMoba.LuaBridge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Lobby panel managing hero select and ready states.
    /// Orchestrates HeroSelectPageController visibility.
    /// Presentation-only.
    /// (ExecPlan 0093, UI/Lua Design v9.1)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyPanelController : MonoBehaviour
    {
        [SerializeField] private HeroSelectPageController heroSelectPage;
        [SerializeField] private Text readyStatusText;
        [SerializeField] private TMP_Text readyStatusTextMeshPro;
        [SerializeField] private Button readyButton;
        [SerializeField] private ClientUiActionRouter actionRouter;

        private bool _isReady;
        private Font _font;

        public bool IsReady => _isReady;

        public void Inject(
            ClientUiActionRouter router)
        {
            actionRouter = router ??
                throw new System.ArgumentNullException(
                    nameof(router));
            heroSelectPage?.Inject(this, router);
        }

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureUI();
            if (readyButton != null)
                readyButton.onClick.AddListener(ToggleReady);
        }

        public void Show()
        {
            heroSelectPage?.Show();
            SetStatus("Select a hero and lock in");
        }

        public void Hide()
        {
            heroSelectPage?.Hide();
        }

        public void OnHeroLocked(int heroId)
        {
            SetStatus($"Hero {heroId} locked. Ready up!");
        }

        private void ToggleReady()
        {
            bool nextReady = !_isReady;
            actionRouter?.SetReady(nextReady);
            _isReady = nextReady;
            SetStatus(
                _isReady
                    ? "Ready! Waiting for others..."
                    : "Not ready");
            if (readyButton != null)
            {
                var label = readyButton.GetComponentInChildren<Text>();
                if (label != null) label.text = _isReady ? "Unready" : "Ready";
            }
        }

        private void EnsureUI()
        {
            if (readyStatusText == null &&
                readyStatusTextMeshPro == null)
            {
                var go = new GameObject("ReadyStatus", typeof(Text));
                go.transform.SetParent(transform, false);
                readyStatusText = go.GetComponent<Text>();
                readyStatusText.font = _font;
                readyStatusText.fontSize = 16;
                readyStatusText.alignment = TextAnchor.MiddleCenter;
                readyStatusText.color = Color.white;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.3f, 0.05f);
                rt.anchorMax = new Vector2(0.7f, 0.12f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            if (readyButton == null)
            {
                var go = new GameObject("ReadyButton", typeof(RectTransform),
                    typeof(Image), typeof(Button));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.4f, 0.14f);
                rt.anchorMax = new Vector2(0.6f, 0.2f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                go.GetComponent<Image>().color = new Color(0.2f, 0.5f, 0.7f);
                readyButton = go.GetComponent<Button>();

                var label = new GameObject("Label", typeof(Text));
                label.transform.SetParent(go.transform, false);
                var text = label.GetComponent<Text>();
                text.font = _font;
                text.fontSize = 20;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.text = "Ready";
            }
        }

        private void SetStatus(string value)
        {
            if (readyStatusText != null)
                readyStatusText.text = value;
            if (readyStatusTextMeshPro != null)
                readyStatusTextMeshPro.text = value;
        }
    }
}
