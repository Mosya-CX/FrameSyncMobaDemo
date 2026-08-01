using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UIPageOpenButton : MonoBehaviour
    {
        [SerializeField] private UIPageId targetPage;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OpenTarget);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OpenTarget);
        }

        private void OpenTarget()
        {
            UIManager manager =
                GetComponentInParent<UIManager>();
            if (manager == null)
                return;
            manager.OpenPage(targetPage);
        }
    }
}
