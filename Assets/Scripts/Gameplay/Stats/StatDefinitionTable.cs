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
            definitions[definition.Id] = definition;
        }

        public bool TryGet(StatId statId, out StatDefinition definition)
        {
            return definitions.TryGetValue(statId, out definition);
        }

        public bool Contains(StatId statId)
        {
            return definitions.ContainsKey(statId);
        }
    }
}