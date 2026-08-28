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
        public const string ClientHero1001Group = "Client-Hero-1001";
        public const string ClientHero1002Group = "Client-Hero-1002";

        public const string LogicCoreGroup = "Logic-Core";
        public const string LogicMap1Group = "Logic-Map-1";
        public const string LogicHero1001Group = "Logic-Hero-1001";
        public const string LogicHero1002Group = "Logic-Hero-1002";

        public static readonly string[] ClientGroups =
        {
            UnitViewsGroup,
            ProjectileViewsGroup,
            VfxGroup,
            AudioGroup,
            UiGroup,
            SharedGroup,
            ClientHero1001Group,
            ClientHero1002Group,
        };

        public static readonly string[] LogicGroups =
        {
            LogicCoreGroup,
            LogicMap1Group,
            LogicHero1001Group,
            LogicHero1002Group,
        };

        public static readonly string[] LocalGroups =
        {
            UnitViewsGroup,
            ProjectileViewsGroup,
            VfxGroup,
            AudioGroup,
            UiGroup,
            SharedGroup,
            ClientHero1001Group,
            ClientHero1002Group,
            LogicCoreGroup,
            LogicMap1Group,
            LogicHero1001Group,
            LogicHero1002Group,
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
