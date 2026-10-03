#if UNITY_EDITOR
using System.Collections.Generic;
using FrameSyncMoba.RuntimeConfig;
using UnityEditor;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Editor auto-sync between hero UnitPrototype rows and the
    /// HeroDisplayTable. Design v10.2 17.x: the prefab table only holds
    /// prefabs, so after a hero prefab is referenced by a hero prototype the
    /// avatar/name table automatically gains a mapping row (prefab id +
    /// prototype id + name). Content authors only fill avatars.
    /// </summary>
    public static class HeroDisplayTableSync
    {
        public static bool Sync(
            HeroDisplayTable displayTable,
            UnitRuntimeCatalogAsset catalog)
        {
            return Sync(
                displayTable,
                catalog != null
                    ? catalog.UnitPrototypes
                    : null);
        }

        public static bool Sync(
            HeroDisplayTable displayTable,
            IReadOnlyList<UnitPrototypeAuthoring>
                prototypes)
        {
            if (displayTable == null)
                return false;

            var heroes = new List<UnitPrototypeAuthoring>();
            if (prototypes != null)
            {
                for (int i = 0;
                     i < prototypes.Count;
                     i++)
                {
                    UnitPrototypeAuthoring prototype =
                        prototypes[i];
                    if (prototype != null &&
                        prototype.UnitKind ==
                        UnitKind.Hero &&
                        prototype.UnitPrototypeId > 0 &&
                        prototype
                            .RuntimeEntityPrefabId > 0)
                        heroes.Add(prototype);
                }
            }
            heroes.Sort((left, right) =>
                left.UnitPrototypeId.CompareTo(
                    right.UnitPrototypeId));

            bool changed = false;
            for (int i = 0;
                 i < heroes.Count;
                 i++)
            {
                UnitPrototypeAuthoring hero =
                    heroes[i];
                if (displayTable.TryGetByPrototypeId(
                        hero.UnitPrototypeId,
                        out HeroDisplayEntry entry))
                {
                    if (entry.HeroPrefabId !=
                        hero.RuntimeEntityPrefabId)
                    {
                        entry.HeroPrefabId =
                            hero.RuntimeEntityPrefabId;
                        changed = true;
                    }
                    continue;
                }
                displayTable.AddOrReplace(
                    new HeroDisplayEntry
                    {
                        UnitPrototypeId =
                            hero.UnitPrototypeId,
                        HeroPrefabId =
                            hero
                                .RuntimeEntityPrefabId,
                        DisplayName = hero.Name,
                    });
                changed = true;
            }

            for (int i = displayTable.Count - 1;
                 i >= 0;
                 i--)
            {
                HeroDisplayEntry entry =
                    displayTable.GetEntry(i);
                if (entry == null)
                    continue;
                bool exists = false;
                for (int h = 0;
                     h < heroes.Count;
                     h++)
                {
                    if (heroes[h].UnitPrototypeId ==
                        entry.UnitPrototypeId)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    displayTable.RemovePrototype(
                        entry.UnitPrototypeId);
                    changed = true;
                }
            }

            if (changed)
                EditorUtility.SetDirty(displayTable);
            return changed;
        }
    }
}
#endif
