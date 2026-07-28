using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Global table of StatDefinitions keyed by StatId (Unit v27.3 section 5.2.2).
    /// </summary>
    public sealed class StatDefinitionTable
    {
        private readonly Dictionary<StatId, StatDefinition> definitions = new Dictionary<StatId, StatDefinition>();

        public void Add(StatDefinition definition)
        {
            if (definition == null)
                throw new System.ArgumentNullException(nameof(definition));
            if (definitions.ContainsKey(definition.Id))
                throw new System.ArgumentException(
                    $"Duplicate StatId {definition.Id}.", nameof(definition));
            definitions.Add(definition.Id, definition);
        }

        public bool TryGet(StatId statId, out StatDefinition definition)
        {
            return definitions.TryGetValue(statId, out definition);
        }

        public bool Contains(StatId statId)
        {
            return definitions.ContainsKey(statId);
        }

        public int Count => definitions.Count;
    }
}
