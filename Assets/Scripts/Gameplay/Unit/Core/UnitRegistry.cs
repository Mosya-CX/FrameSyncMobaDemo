using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FrameSyncMoba.Unit
{
    internal sealed class UnitRegistry
    {
        private readonly Dictionary<UnitUid, Unit> unitsByUid = new Dictionary<UnitUid, Unit>();
        private readonly List<Unit> orderedUnits = new List<Unit>();
        private readonly ReadOnlyCollection<Unit> readOnlyOrderedUnits;

        public UnitRegistry()
        {
            readOnlyOrderedUnits = orderedUnits.AsReadOnly();
        }

        public bool TryGet(UnitUid unitUid, out Unit unit)
        {
            return unitsByUid.TryGetValue(unitUid, out unit);
        }

        public IReadOnlyList<Unit> GetAll()
        {
            return readOnlyOrderedUnits;
        }

        public IReadOnlyList<Unit> GetByKind(UnitKind kind)
        {
            var result = new List<Unit>();
            for (int index = 0; index < orderedUnits.Count; index++)
            {
                Unit unit = orderedUnits[index];
                if (unit.UnitKind == kind)
                {
                    result.Add(unit);
                }
            }
            return result;
        }

        public IReadOnlyList<Unit> GetBySubKind(UnitKind kind, ushort subKindId)
        {
            var result = new List<Unit>();
            for (int index = 0; index < orderedUnits.Count; index++)
            {
                Unit unit = orderedUnits[index];
                if (unit.UnitKind == kind && unit.UnitSubKindId == subKindId)
                {
                    result.Add(unit);
                }
            }
            return result;
        }

        public IReadOnlyList<Unit> GetByTeam(TeamId teamId)
        {
            var result = new List<Unit>();
            for (int index = 0; index < orderedUnits.Count; index++)
            {
                Unit unit = orderedUnits[index];
                if (unit.TeamId == teamId)
                {
                    result.Add(unit);
                }
            }
            return result;
        }
        public void Register(Unit unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (unitsByUid.ContainsKey(unit.UnitUid))
            {
                throw new InvalidOperationException("A Unit with the same UnitUid is already registered.");
            }

            int insertionIndex = FindInsertionIndex(unit.UnitUid);
            unitsByUid.Add(unit.UnitUid, unit);
            orderedUnits.Insert(insertionIndex, unit);
        }

        public void Unregister(Unit unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (!unitsByUid.TryGetValue(unit.UnitUid, out Unit registeredUnit)
                || !ReferenceEquals(registeredUnit, unit))
            {
                throw new InvalidOperationException("The specified Unit instance is not registered for its UnitUid.");
            }

            int orderedIndex = FindExactIndex(unit.UnitUid);
            if (orderedIndex < 0 || !ReferenceEquals(orderedUnits[orderedIndex], unit))
            {
                throw new InvalidOperationException("The Unit registry lookup and stable order are inconsistent.");
            }

            unitsByUid.Remove(unit.UnitUid);
            orderedUnits.RemoveAt(orderedIndex);
        }

        private int FindInsertionIndex(UnitUid unitUid)
        {
            int minimum = 0;
            int maximum = orderedUnits.Count;

            while (minimum < maximum)
            {
                int middle = minimum + ((maximum - minimum) / 2);
                if (orderedUnits[middle].UnitUid.CompareTo(unitUid) < 0)
                {
                    minimum = middle + 1;
                }
                else
                {
                    maximum = middle;
                }
            }

            return minimum;
        }

        private int FindExactIndex(UnitUid unitUid)
        {
            int index = FindInsertionIndex(unitUid);
            if (index >= orderedUnits.Count || orderedUnits[index].UnitUid != unitUid)
            {
                return -1;
            }

            return index;
        }
    }
}
