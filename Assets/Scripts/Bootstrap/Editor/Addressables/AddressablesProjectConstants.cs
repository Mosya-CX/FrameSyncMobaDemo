namespace FrameSyncMoba.EditorTools.Addressables
{
    public static class AddressablesProjectConstants
    {
        public const string UnitViewsGroup = "Client-UnitViews";
        public const string ProjectileViewsGroup = "Client-ProjectileViews";
        public const string VfxGroup = "Client-VFX";
        public const string AudioGroup = "Client-Audio";
        public const string UiGroup = "Client-UI";
        public const string SharedGroup = "Client-Shared";

        public static readonly string[] ClientGroups =
        {
            UnitViewsGroup,
            ProjectileViewsGroup,
            VfxGroup,
            AudioGroup,
            UiGroup,
            SharedGroup,
        };

        public const string BaselineCsv =
            "Docs/Implementation/Addressables/BASELINE_DEPENDENCIES.csv";
        public const string BaselineMarkdown =
            "Docs/Implementation/Addressables/BASELINE_DEPENDENCIES.md";
        public const string CurrentCsv =
            "Docs/Implementation/Addressables/CURRENT_DEPENDENCIES.csv";
        public const string CurrentMarkdown =
            "Docs/Implementation/Addressables/CURRENT_DEPENDENCIES.md";
        public const string AddressableRootsCsv =
            "Docs/Implementation/Addressables/ADDRESSABLE_ROOTS.csv";
        public const string AddressableRootsMarkdown =
            "Docs/Implementation/Addressables/ADDRESSABLE_ROOTS.md";
    }
}
