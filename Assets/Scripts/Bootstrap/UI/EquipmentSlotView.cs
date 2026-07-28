using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Per-slot reusable UI controller for equipment display.
    /// Used in both the shop catalog list and the owned equipment grid.
    /// Presentation-only — never writes deterministic Gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentSlotView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text priceText;
        [SerializeField] private Text stackText;
        [SerializeField] private GameObject highlightFrame;

        public int EquipmentId { get; private set; }
        public int SlotIndex { get; private set; }

        private System.Action<EquipmentSlotView> _onClick;

        public void Initialize(
            int equipmentId,
            string displayName,
            int price,
            int stackCount,
            int slotIndex,
            Sprite icon,
            System.Action<EquipmentSlotView> onClick)
        {
            EquipmentId = equipmentId;
            SlotIndex = slotIndex;
            _onClick = onClick;

            if (nameText != null) nameText.text = displayName ?? "";
            if (priceText != null) priceText.text = price > 0 ? price.ToString() : "";
            if (stackText != null) stackText.text = stackCount > 1 ? stackCount.ToString() : "";
            if (iconImage != null) iconImage.sprite = icon;
            if (highlightFrame != null) highlightFrame.SetActive(false);

            // Code-driven creation when no prefab is assigned
            if (iconImage == null && nameText == null && priceText == null && stackText == null)
                BuildDefaultHierarchy();
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlightFrame != null)
                highlightFrame.SetActive(highlighted);
        }

        public void OnSlotClicked()
        {
            _onClick?.Invoke(this);
        }

        private void BuildDefaultHierarchy()
        {
            // Simple code-driven layout: horizontal row
            var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            layout.padding = new RectOffset(4, 4, 2, 2);

            // Icon — populated via Initialize(..., Sprite icon, ...)
            var iconGo = CreateChild("Icon");
            iconImage = iconGo.AddComponent<Image>();
            iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(32, 32);

            // Name
            var nameGo = CreateChild("Name");
            nameText = nameGo.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 12;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = Color.white;
            nameGo.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 32);

            // Price
            var priceGo = CreateChild("Price");
            priceText = priceGo.AddComponent<Text>();
            priceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            priceText.fontSize = 12;
            priceText.alignment = TextAnchor.MiddleRight;
            priceText.color = new Color(1f, 0.85f, 0.3f);
            priceGo.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 32);

            // Stack count
            var stackGo = CreateChild("Stack");
            stackText = stackGo.AddComponent<Text>();
            stackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stackText.fontSize = 10;
            stackText.alignment = TextAnchor.MiddleCenter;
            stackText.color = Color.white;
            stackGo.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 32);

            // Click button (invisible, covers the whole row)
            var btn = gameObject.AddComponent<Button>();
            btn.onClick.AddListener(OnSlotClicked);
            var btnImage = gameObject.AddComponent<Image>();
            btnImage.color = new Color(1f, 1f, 1f, 0.01f);
            btn.targetGraphic = btnImage;
        }

        private GameObject CreateChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }
    }
}
