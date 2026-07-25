using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Manages the Shop page lifecycle as a BattleOverlay over the HUD.
    /// Reads EquipmentDatabase for catalog, EquipmentShopRuntime for validation
    /// and transaction execution. Presentation-only.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 11-13
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopPageController : MonoBehaviour
    {
        [Header("Page root")]
        [SerializeField] private Canvas shopCanvas;

        [Header("Catalog (scrollable list)")]
        [SerializeField] private RectTransform catalogContent;
        [SerializeField] private ScrollRect catalogScroll;

        [Header("Detail panel")]
        [SerializeField] private Text detailNameText;
        [SerializeField] private Text detailDescText;
        [SerializeField] private Text detailPriceText;
        [SerializeField] private Text detailStatsText;

        [Header("Owned equipment grid")]
        [SerializeField] private RectTransform ownedGridContent;

        [Header("Action buttons")]
        [SerializeField] private Button buyButton;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button closeButton;

        [Header("Gold display")]
        [SerializeField] private Text goldText;

        // Runtime references (injected by GameBootstrap)
        private FrameSyncGameRuntime _runtime;
        private EquipmentShopRuntime _shopRuntime;
        private EquipmentDatabase _database;
        private Unit.Unit _controlledUnit;

        // Selection state
        private int _selectedCatalogId = -1;
        private int _selectedOwnedSlot = -1;
        private EquipmentSlotView _selectedCatalogView;
        private EquipmentSlotView _selectedOwnedView;

        // Slot view lists
        private System.Collections.Generic.List<EquipmentSlotView> _catalogViews =
            new System.Collections.Generic.List<EquipmentSlotView>();
        private EquipmentSlotView[] _ownedSlotViews = new EquipmentSlotView[6];

        // Queued transaction (processed during tick callback)
        private enum QueuedAction { None, Purchase, Sell, Undo }
        private QueuedAction _queuedAction;
        private int _queuedEquipmentId;
        private int _queuedEquipmentSlot;

        private bool _isVisible;
        private bool _needsRefresh;

        public void Inject(
            FrameSyncGameRuntime runtime,
            EquipmentShopRuntime shopRuntime,
            EquipmentDatabase database,
            Unit.Unit controlledUnit)
        {
            _runtime = runtime;
            _shopRuntime = shopRuntime;
            _database = database;
            _controlledUnit = controlledUnit;
        }

        private void Awake()
        {
            if (shopCanvas == null)
                shopCanvas = GetComponentInChildren<Canvas>(true)
                    ?? gameObject.AddComponent<Canvas>();

            BuildUiIfNeeded();
            BindButtons();
            shopCanvas.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (_isVisible) return;
            shopCanvas.gameObject.SetActive(true);
            _isVisible = true;
            _needsRefresh = true;
        }

        public void Hide()
        {
            if (!_isVisible) return;
            shopCanvas.gameObject.SetActive(false);
            _isVisible = false;
        }

        public void Toggle()
        {
            if (_isVisible) Hide();
            else Show();
        }

        /// <summary>
        /// Called from tick-completed callback to process queued transactions.
        /// Must be called when SimulationTickContext is active.
        /// </summary>
        public void ProcessQueuedTransaction()
        {
            if (_queuedAction == QueuedAction.None) return;
            if (_shopRuntime == null || _controlledUnit == null) { ClearQueue(); return; }

            int playerSlot = _controlledUnit.ControlledByPlayerSlot;
            if (playerSlot < 0) { ClearQueue(); return; }

            var handler = _controlledUnit.EquipmentHandler;
            if (handler == null) { ClearQueue(); return; }

            switch (_queuedAction)
            {
                case QueuedAction.Purchase:
                    if (_shopRuntime.TryBuildPurchasePlan(playerSlot, _queuedEquipmentId,
                        GetCurrentAvailableGold(), handler,
                        out EquipmentPurchasePlan plan, out _))
                    {
                        _shopRuntime.ProcessPurchase(playerSlot, plan, handler, out _);
                    }
                    break;

                case QueuedAction.Sell:
                    if (_shopRuntime.TrySell(playerSlot, _queuedEquipmentSlot, handler,
                        out int sellValue, out _))
                    {
                        _shopRuntime.ProcessSell(playerSlot, _queuedEquipmentSlot, handler,
                            sellValue, out _);
                    }
                    break;

                case QueuedAction.Undo:
                    if (_shopRuntime.CanUndo(playerSlot, out _))
                    {
                        _shopRuntime.ProcessUndo(playerSlot, handler, out _);
                    }
                    break;
            }

            ClearQueue();
            _needsRefresh = true;
        }

        /// <summary>
        /// Called each Unity frame for visual updates only.
        /// </summary>
        public void TickVisual()
        {
            if (!_isVisible) return;

            if (_needsRefresh)
            {
                _needsRefresh = false;
                RebuildCatalog();
                RebuildOwnedGrid();
                ClearDetail();
            }

            RefreshGold();
        }

        // ---- Catalog ----

        private void RebuildCatalog()
        {
            ClearCatalogViews();
            var allDefs = _database?.AllDefinitions;
            if (allDefs == null) return;

            for (int i = 0; i < allDefs.Count; i++)
            {
                var def = allDefs[i];
                if (def == null) continue;
                EquipmentSlotView view = CreateSlotView(catalogContent, _catalogViews.Count);
                view.Initialize(def.Id, def.Name ?? "", def.Value, 1, i, OnCatalogSlotClicked);
                _catalogViews.Add(view);
            }
        }

        private void ClearCatalogViews()
        {
            for (int i = 0; i < _catalogViews.Count; i++)
            {
                if (_catalogViews[i] != null)
                    Destroy(_catalogViews[i].gameObject);
            }
            _catalogViews.Clear();
        }

        private void OnCatalogSlotClicked(EquipmentSlotView view)
        {
            if (_selectedCatalogView != null)
                _selectedCatalogView.SetHighlighted(false);
            _selectedCatalogView = view;
            _selectedCatalogId = view.EquipmentId;
            _selectedOwnedSlot = -1;
            if (_selectedOwnedView != null)
            {
                _selectedOwnedView.SetHighlighted(false);
                _selectedOwnedView = null;
            }
            view.SetHighlighted(true);
            ShowDetail(view.EquipmentId);
            UpdateButtonStates();
        }

        // ---- Owned grid ----

        private void RebuildOwnedGrid()
        {
            for (int i = 0; i < 6; i++)
            {
                if (_ownedSlotViews[i] != null)
                    Destroy(_ownedSlotViews[i].gameObject);
                _ownedSlotViews[i] = null;
            }

            var handler = _controlledUnit?.EquipmentHandler;
            if (handler == null) return;

            for (int slot = 0; slot < 6; slot++)
            {
                var def = handler.GetSlotDef(slot);
                EquipmentSlotView view = CreateSlotView(ownedGridContent, slot);
                if (def != null)
                {
                    var inst = handler.GetSlot(slot);
                    int stack = inst?.StackCount ?? 1;
                    int sellPrice = CalculateSellPrice(def);
                    view.Initialize(def.Id, def.Name ?? "", sellPrice, stack, slot,
                        OnOwnedSlotClicked);
                }
                else
                {
                    view.Initialize(0, "(empty)", 0, 0, slot, OnOwnedSlotClicked);
                }
                _ownedSlotViews[slot] = view;
            }
        }

        private void OnOwnedSlotClicked(EquipmentSlotView view)
        {
            if (_selectedOwnedView != null)
                _selectedOwnedView.SetHighlighted(false);
            _selectedOwnedView = view;
            _selectedOwnedSlot = view.SlotIndex;
            _selectedCatalogId = -1;
            if (_selectedCatalogView != null)
            {
                _selectedCatalogView.SetHighlighted(false);
                _selectedCatalogView = null;
            }
            view.SetHighlighted(true);

            var def = _controlledUnit?.EquipmentHandler?.GetSlotDef(view.SlotIndex);
            if (def != null)
                ShowDetail(def.Id);
            else
                ClearDetail();
            UpdateButtonStates();
        }

        // ---- Detail ----

        private void ShowDetail(int equipmentId)
        {
            var def = _database?.GetDefinition(equipmentId);
            if (def == null) { ClearDetail(); return; }

            if (detailNameText != null) detailNameText.text = def.Name ?? "";
            if (detailDescText != null) detailDescText.text = def.Description ?? "";
            if (detailStatsText != null && def.BakedFixedStats != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < def.BakedFixedStats.Length; i++)
                {
                    var fs = def.BakedFixedStats[i];
                    sb.Append(fs.Stat).Append(": +").Append((float)fs.Value).AppendLine();
                }
                detailStatsText.text = sb.ToString();
            }

            UpdateDetailPrice(def);
        }

        private void UpdateDetailPrice(EquipmentDefinition def)
        {
            if (detailPriceText == null || def == null) return;
            int price;
            if (_selectedCatalogId > 0)
                price = def.Value;
            else if (_selectedOwnedSlot >= 0)
                price = CalculateSellPrice(def);
            else
                price = 0;
            detailPriceText.text = price > 0 ? $"Price: {price}" : "";
        }

        private void ClearDetail()
        {
            if (detailNameText != null) detailNameText.text = "";
            if (detailDescText != null) detailDescText.text = "";
            if (detailPriceText != null) detailPriceText.text = "";
            if (detailStatsText != null) detailStatsText.text = "";
        }

        // ---- Buttons ----

        private void BindButtons()
        {
            if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
            if (sellButton != null) sellButton.onClick.AddListener(OnSellClicked);
            if (undoButton != null) undoButton.onClick.AddListener(OnUndoClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        private void OnBuyClicked()
        {
            if (_selectedCatalogId <= 0 || _shopRuntime == null || _controlledUnit == null) return;
            int playerSlot = _controlledUnit.ControlledByPlayerSlot;
            if (playerSlot < 0) return;

            var handler = _controlledUnit.EquipmentHandler;
            if (handler == null) return;

            if (_shopRuntime.TryBuildPurchasePlan(playerSlot, _selectedCatalogId,
                GetCurrentAvailableGold(), handler, out _, out _))
            {
                _queuedAction = QueuedAction.Purchase;
                _queuedEquipmentId = _selectedCatalogId;
                _queuedEquipmentSlot = 0;
            }
        }

        private void OnSellClicked()
        {
            if (_selectedOwnedSlot < 0 || _shopRuntime == null || _controlledUnit == null) return;
            int playerSlot = _controlledUnit.ControlledByPlayerSlot;
            if (playerSlot < 0) return;

            var handler = _controlledUnit.EquipmentHandler;
            if (handler == null) return;

            if (_shopRuntime.TrySell(playerSlot, _selectedOwnedSlot, handler, out _, out _))
            {
                _queuedAction = QueuedAction.Sell;
                _queuedEquipmentSlot = _selectedOwnedSlot;
                _queuedEquipmentId = 0;
            }
        }

        private void OnUndoClicked()
        {
            if (_shopRuntime == null || _controlledUnit == null) return;
            int playerSlot = _controlledUnit.ControlledByPlayerSlot;
            if (playerSlot < 0) return;

            if (_shopRuntime.CanUndo(playerSlot, out _))
            {
                _queuedAction = QueuedAction.Undo;
                _queuedEquipmentId = 0;
                _queuedEquipmentSlot = 0;
            }
        }

        private void UpdateButtonStates()
        {
            if (buyButton != null)
                buyButton.interactable = _selectedCatalogId > 0;
            if (sellButton != null)
                sellButton.interactable = _selectedOwnedSlot >= 0
                    && _controlledUnit?.EquipmentHandler?.GetSlotDef(_selectedOwnedSlot) != null;
            if (undoButton != null && _shopRuntime != null && _controlledUnit != null)
                undoButton.interactable = _shopRuntime.CanUndo(
                    _controlledUnit.ControlledByPlayerSlot, out _);
        }

        // ---- Gold ----

        private void RefreshGold()
        {
            if (goldText == null) return;
            int gold = GetCurrentAvailableGold();
            goldText.text = $"Gold: {gold}";
        }

        private int GetCurrentAvailableGold()
        {
            if (_controlledUnit == null || _runtime == null) return 0;
            int slot = _controlledUnit.ControlledByPlayerSlot;
            if (slot < 0) return 0;
            int confirmedGold = _runtime.GoldIncome?.GetConfirmedAvailableGold(slot) ?? 0;
            int shopDelta = _shopRuntime?.ComputeEffectiveShopGoldDelta(slot) ?? 0;
            return confirmedGold + shopDelta;
        }

        private int CalculateSellPrice(EquipmentDefinition def)
        {
            if (def == null || _shopRuntime == null) return 0;
            return (int)((Unity.Mathematics.FixedPoint.fp)def.Value * _shopRuntime.SellRate);
        }

        // ---- Helpers ----

        private void ClearQueue()
        {
            _queuedAction = QueuedAction.None;
            _queuedEquipmentId = 0;
            _queuedEquipmentSlot = 0;
        }

        private EquipmentSlotView CreateSlotView(Transform parent, int index)
        {
            var go = new GameObject($"Slot_{index}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<EquipmentSlotView>();
        }

        private void BuildUiIfNeeded()
        {
            if (catalogContent != null && ownedGridContent != null && buyButton != null) return;

            // Canvas
            if (shopCanvas == null)
            {
                var canvasGo = new GameObject("ShopCanvas", typeof(Canvas), typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                canvasGo.transform.SetParent(transform, false);
                shopCanvas = canvasGo.GetComponent<Canvas>();
                shopCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            // Background panel
            var panelGo = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(shopCanvas.transform, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.08f, 0.08f);
            panelRt.anchorMax = new Vector2(0.92f, 0.92f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.13f, 0.96f);

            // --- Left: Catalog ---
            var catalogRegion = CreateRegion(panelGo.transform, "CatalogRegion",
                new Vector2(0.02f, 0.08f), new Vector2(0.48f, 0.96f));
            catalogRegion.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 1f);

            // Catalog scroll
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform),
                typeof(ScrollRect), typeof(Image), typeof(Mask));
            scrollGo.transform.SetParent(catalogRegion.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(4, 4);
            scrollRt.offsetMax = new Vector2(-4, -4);
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);
            catalogScroll = scrollGo.GetComponent<ScrollRect>();

            var viewportGo = new GameObject("Viewport", typeof(RectTransform),
                typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);

            var contentGo = new GameObject("Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            catalogContent = contentGo.GetComponent<RectTransform>();
            catalogContent.anchorMin = new Vector2(0, 1);
            catalogContent.anchorMax = new Vector2(1, 1);
            catalogContent.pivot = new Vector2(0.5f, 1);
            catalogContent.sizeDelta = new Vector2(0, 0);
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(2, 2, 2, 2);
            contentGo.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            catalogScroll.viewport = viewportRt;
            catalogScroll.content = catalogContent;
            catalogScroll.horizontal = false;

            // --- Right Top: Detail ---
            var detailRegion = CreateRegion(panelGo.transform, "DetailRegion",
                new Vector2(0.52f, 0.52f), new Vector2(0.98f, 0.96f));
            detailRegion.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 1f);

            var detailLayout = new GameObject("Layout", typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            detailLayout.transform.SetParent(detailRegion.transform, false);
            var dlRt = detailLayout.GetComponent<RectTransform>();
            dlRt.anchorMin = Vector2.zero;
            dlRt.anchorMax = Vector2.one;
            dlRt.offsetMin = new Vector2(8, 8);
            dlRt.offsetMax = new Vector2(-8, -8);
            var dvlg = detailLayout.GetComponent<VerticalLayoutGroup>();
            dvlg.childControlWidth = true;
            dvlg.childControlHeight = false;
            dvlg.childForceExpandWidth = true;
            dvlg.childForceExpandHeight = false;
            dvlg.spacing = 4f;

            detailNameText = MakeText(detailLayout.transform, "Name", 16, Color.white,
                TextAnchor.UpperLeft);
            detailDescText = MakeText(detailLayout.transform, "Desc", 12,
                new Color(0.7f, 0.7f, 0.7f), TextAnchor.UpperLeft);
            detailPriceText = MakeText(detailLayout.transform, "Price", 14,
                new Color(1f, 0.85f, 0.3f), TextAnchor.UpperLeft);
            detailStatsText = MakeText(detailLayout.transform, "Stats", 12,
                new Color(0.5f, 0.9f, 0.5f), TextAnchor.UpperLeft);

            // --- Right Bottom: Owned Grid ---
            var ownedRegion = CreateRegion(panelGo.transform, "OwnedRegion",
                new Vector2(0.52f, 0.08f), new Vector2(0.98f, 0.48f));
            ownedRegion.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 1f);

            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(ownedRegion.transform, false);
            ownedGridContent = gridGo.GetComponent<RectTransform>();
            ownedGridContent.anchorMin = Vector2.zero;
            ownedGridContent.anchorMax = Vector2.one;
            ownedGridContent.offsetMin = new Vector2(8, 8);
            ownedGridContent.offsetMax = new Vector2(-8, -8);
            var glg = gridGo.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(180, 52);
            glg.spacing = new Vector2(8, 4);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            // --- Buttons ---
            var btnArea = new GameObject("Buttons", typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            btnArea.transform.SetParent(panelGo.transform, false);
            var baRt = btnArea.GetComponent<RectTransform>();
            baRt.anchorMin = new Vector2(0.02f, 0.01f);
            baRt.anchorMax = new Vector2(0.48f, 0.07f);
            baRt.offsetMin = Vector2.zero;
            baRt.offsetMax = Vector2.zero;
            var bhlg = btnArea.GetComponent<HorizontalLayoutGroup>();
            bhlg.childControlWidth = true;
            bhlg.childControlHeight = true;
            bhlg.childForceExpandWidth = true;
            bhlg.spacing = 4f;

            buyButton = MakeButton(btnArea.transform, "BuyBtn", "Buy");
            sellButton = MakeButton(btnArea.transform, "SellBtn", "Sell");
            undoButton = MakeButton(btnArea.transform, "UndoBtn", "Undo");
            closeButton = MakeButton(btnArea.transform, "CloseBtn", "Close");

            // --- Gold Label ---
            var goldGo = new GameObject("GoldLabel", typeof(RectTransform));
            goldGo.transform.SetParent(panelGo.transform, false);
            var goldRt = goldGo.GetComponent<RectTransform>();
            goldRt.anchorMin = new Vector2(0.52f, 0.96f);
            goldRt.anchorMax = new Vector2(0.75f, 1.0f);
            goldRt.offsetMin = Vector2.zero;
            goldRt.offsetMax = Vector2.zero;
            goldText = goldGo.AddComponent<Text>();
            goldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goldText.fontSize = 20;
            goldText.alignment = TextAnchor.MiddleLeft;
            goldText.color = new Color(1f, 0.85f, 0.3f);
        }

        private static RectTransform CreateRegion(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Text MakeText(Transform parent, string name, int size,
            Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, size + 8);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            return t;
        }

        private static Button MakeButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.35f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();

            var lbl = new GameObject("Label", typeof(RectTransform));
            lbl.transform.SetParent(go.transform, false);
            var lrt = lbl.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var lt = lbl.AddComponent<Text>();
            lt.text = label;
            lt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lt.fontSize = 14;
            lt.alignment = TextAnchor.MiddleCenter;
            lt.color = Color.white;

            return btn;
        }
    }
}
