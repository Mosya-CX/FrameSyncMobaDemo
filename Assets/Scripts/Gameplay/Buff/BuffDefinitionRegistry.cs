using System;
using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    public sealed class BuffDefinitionRegistry
    {
        private readonly Dictionary<BuffConfigId, BuffDefinition> definitions =
            new Dictionary<BuffConfigId, BuffDefinition>();

        public void Register(BuffDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!definition.IsValid) throw new ArgumentException("Buff definition is invalid.", nameof(definition));
            if (definitions.ContainsKey(definition.ConfigId))
                throw new InvalidOperationException($"Duplicate BuffConfigId {definition.ConfigId.Value}.");
            definitions.Add(definition.ConfigId, definition);
        }

        public bool TryGet(BuffConfigId configId, out BuffDefinition definition) =>
            definitions.TryGetValue(configId, out definition);
    }
}
