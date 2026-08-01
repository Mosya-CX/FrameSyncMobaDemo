using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    public enum UIPageId : byte
    {
        None = 0,
        Lobby = 1,
        HeroSelect = 2,
        GameplayHud = 3,
        Shop = 4,
        Result = 5,
    }

    public enum UIPageLayer : byte
    {
        Page = 0,
        Popup = 1,
        Overlay = 2,
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
