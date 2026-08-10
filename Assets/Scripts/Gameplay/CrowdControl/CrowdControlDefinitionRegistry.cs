using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Global runtime registry of CrowdControlDefinitions (CC v6.2 2.1).
    /// Registered once at match start from the catalog asset.
    /// </summary>
    public sealed class CrowdControlDefinitionRegistry
    {
        private readonly Dictionary<CrowdControlId, CrowdControlDefinition>
            definitions =
                new Dictionary<CrowdControlId, CrowdControlDefinition>();

        public void Register(
            CrowdControlDefinition definition)
        {
            if (definition == null)
            {
                throw new System.ArgumentNullException(
                    nameof(definition));
            }
            if (!definition.IsValid)
            {
                throw new System.ArgumentException(
                    "CrowdControlDefinition is not baked/valid.",
                    nameof(definition));
            }
            if (definitions.ContainsKey(
                    definition.ControlId))
            {
                throw new System.InvalidOperationException(
                    $"Duplicate CrowdControlId {definition.ControlId.Value}.");
            }
            definitions.Add(
                definition.ControlId,
                definition);
        }

        public bool TryGet(
            CrowdControlId controlId,
            out CrowdControlDefinition definition) =>
            definitions.TryGetValue(
                controlId,
                out definition);
    }
}
