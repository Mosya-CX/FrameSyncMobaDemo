using System;
using System.Collections.Generic;
using FrameSyncMoba.FrameSync;

namespace FrameSyncMoba.Bootstrap
{
    public sealed class MatchContentSelection
    {
        private readonly int[] heroConfigIds;

        public int MapConfigId { get; }
        public IReadOnlyList<int> HeroConfigIds => heroConfigIds;

        public MatchContentSelection(
            int mapConfigId,
            IEnumerable<int> selectedHeroConfigIds)
        {
            if (mapConfigId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(mapConfigId));
            if (selectedHeroConfigIds == null)
                throw new ArgumentNullException(
                    nameof(selectedHeroConfigIds));
            MapConfigId = mapConfigId;
            var values = new List<int>();
            foreach (int heroId in selectedHeroConfigIds)
            {
                if (heroId <= 0)
                    throw new ArgumentOutOfRangeException(
                        nameof(selectedHeroConfigIds),
                        "Selected HeroConfigId must be positive.");
                values.Add(heroId);
            }
            values.Sort();
            for (int i = values.Count - 1; i > 0; i--)
                if (values[i] == values[i - 1])
                    values.RemoveAt(i);
            if (values.Count == 0)
                throw new InvalidOperationException(
                    "Match content selection requires at least one selected hero.");
            heroConfigIds = values.ToArray();
        }

        public static MatchContentSelection FromGameStartConfig(
            in GameStartConfig config)
        {
            var heroIds =
                new int[config.PlayerSlots.Length];
            for (int i = 0;
                 i < config.PlayerSlots.Length;
                 i++)
                heroIds[i] =
                    config.PlayerSlots[i].HeroConfigId;
            return new MatchContentSelection(
                config.MapConfigId,
                heroIds);
        }

        public bool HasSameContent(
            MatchContentSelection other)
        {
            if (other == null ||
                MapConfigId != other.MapConfigId ||
                heroConfigIds.Length !=
                    other.heroConfigIds.Length)
                return false;
            for (int i = 0;
                 i < heroConfigIds.Length;
                 i++)
                if (heroConfigIds[i] !=
                    other.heroConfigIds[i])
                    return false;
            return true;
        }

        public bool ContainsHeroConfigId(int heroConfigId)
        {
            return Array.BinarySearch(
                    heroConfigIds,
                    heroConfigId) >= 0;
        }

        public override string ToString()
        {
            return $"Map={MapConfigId};Heroes=[{string.Join(",", heroConfigIds)}]";
        }
    }
}
