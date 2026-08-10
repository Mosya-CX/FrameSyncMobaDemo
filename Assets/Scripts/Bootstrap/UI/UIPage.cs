using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Stable page identity (UI design v9.1 2.2). The enum name stays
    /// UIPageId because the MonoBehaviour UIPage already owns the name.
    /// </summary>
    public enum UIPageId : byte
    {
        None = 0,
        Main = 1,
        Match = 2,
        Select = 3,
        Load = 4,
        HUD = 5,
        Shop = 6,
        Result = 7,
    }

    /// <summary>
    /// Page layers (UI design v9.1 2.3): Main layer holds one main page;
    /// BattleOverlay holds Shop above HUD.
    /// </summary>
    public enum UIPageLayer : byte
    {
        Main = 0,
        BattleOverlay = 1,
    }

    [DisallowMultipleComponent]
    public sealed class UIPage : MonoBehaviour
    {
        [SerializeField] private UIPageId pageId;
        [SerializeField] private UIPageLayer layer;

        public UIPageId PageId => pageId;
        public UIPageLayer Layer => layer;
    }
}
