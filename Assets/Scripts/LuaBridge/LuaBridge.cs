using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.LuaBridge
{
    /// <summary>
    /// Tick-end consumer that reads deterministic Gameplay state and
    /// pushes a read-only UiSnapshotDto into the LuaRuntime global
    /// state for presentation consumption.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 1.4, 1.9, 10
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LuaBridge : MonoBehaviour
    {
        public LuaRuntime Runtime { get; } = new LuaRuntime();

        public void PushTickData(int tick, in UiSnapshotDto dto, UnitType controlledUnit)
        {
            Runtime.Clear();

            Runtime.SetTableField("HUD", "CurrentHealth", dto.CurrentHealth);
            Runtime.SetTableField("HUD", "MaxHealth", dto.MaxHealth);
            Runtime.SetTableField("HUD", "UnitLevel", dto.UnitLevel);
            Runtime.SetTableField("HUD", "CurrentExperience", dto.CurrentExperience);
            Runtime.SetTableField("HUD", "ExperienceForNextLevel", dto.ExperienceForNextLevel);
            Runtime.SetTableField("HUD", "CurrentGold", dto.CurrentGold);
            Runtime.SetTableField("HUD", "ConfirmedGold", dto.ConfirmedGold);
            Runtime.SetTableField("HUD", "CooldownRemaining0", dto.CooldownRemaining0);
            Runtime.SetTableField("HUD", "CooldownRemaining1", dto.CooldownRemaining1);
            Runtime.SetTableField("HUD", "CooldownRemaining2", dto.CooldownRemaining2);
            Runtime.SetTableField("HUD", "CooldownRemaining3", dto.CooldownRemaining3);
            Runtime.SetTableField("HUD", "CooldownTotal0", dto.CooldownTotal0);
            Runtime.SetTableField("HUD", "CooldownTotal1", dto.CooldownTotal1);
            Runtime.SetTableField("HUD", "CooldownTotal2", dto.CooldownTotal2);
            Runtime.SetTableField("HUD", "CooldownTotal3", dto.CooldownTotal3);
            Runtime.SetGlobal("CurrentTick", tick);

            // Scoreboard
            Runtime.SetTableField("HUD", "PlayerCount", dto.PlayerCount);
            Runtime.SetTableField("HUD", "Kills", dto.Kills);
            Runtime.SetTableField("HUD", "Deaths", dto.Deaths);
            Runtime.SetTableField("HUD", "Assists", dto.Assists);

            // All-player scoreboard arrays
            SetIntArray("Scoreboard", "Kills", dto.AllPlayerKills?.ToArray());
            SetIntArray("Scoreboard", "Deaths", dto.AllPlayerDeaths?.ToArray());
            SetIntArray("Scoreboard", "Assists", dto.AllPlayerAssists?.ToArray());
            SetStringArray("Scoreboard", "Names", dto.AllPlayerNames?.ToArray());

            if (controlledUnit != null)
                Runtime.SetGlobal("ControlledUnitName", controlledUnit.name ?? "");
        }

        public void PushTickDataWithBindings(
            int tick,
            in UiSnapshotDto dto,
            UnitType controlledUnit,
            IReadOnlyDictionary<string, string> bindingDict)
        {
            PushTickData(tick, dto, controlledUnit);

            if (bindingDict == null || bindingDict.Count == 0) return;

            foreach (var kvp in bindingDict)
            {
                object value = ResolveGameplayField(kvp.Key, dto);
                if (value != null)
                    SetByPath(kvp.Value, value);
            }
        }

        private static object ResolveGameplayField(string field, in UiSnapshotDto dto)
        {
            switch (field)
            {
                case "CurrentHealth": return (float)dto.CurrentHealth;
                case "MaxHealth": return (float)dto.MaxHealth;
                case "CurrentGold": return dto.CurrentGold;
                case "ConfirmedGold": return dto.ConfirmedGold;
                case "UnitLevel": return dto.UnitLevel;
                case "CurrentExperience": return dto.CurrentExperience;
                case "ExperienceForNextLevel": return dto.ExperienceForNextLevel;
                case "CooldownRemaining0": return dto.CooldownRemaining0;
                case "CooldownRemaining1": return dto.CooldownRemaining1;
                case "CooldownRemaining2": return dto.CooldownRemaining2;
                case "CooldownRemaining3": return dto.CooldownRemaining3;
                case "CooldownTotal0": return dto.CooldownTotal0;
                case "CooldownTotal1": return dto.CooldownTotal1;
                case "CooldownTotal2": return dto.CooldownTotal2;
                case "CooldownTotal3": return dto.CooldownTotal3;
                default: return null;
            }
        }

        private void SetByPath(string luaPath, object value)
        {
            if (string.IsNullOrEmpty(luaPath)) return;

            int dotIndex = luaPath.IndexOf('.');
            if (dotIndex < 0)
            {
                if (value is int intVal) Runtime.SetGlobal(luaPath, intVal);
                else if (value is fp fpVal) Runtime.SetGlobal(luaPath, fpVal);
                else if (value is float floatVal) Runtime.SetGlobal(luaPath, floatVal);
                else if (value is string strVal) Runtime.SetGlobal(luaPath, strVal);
            }
            else
            {
                string tableName = luaPath.Substring(0, dotIndex);
                string field = luaPath.Substring(dotIndex + 1);
                if (value is int intVal) Runtime.SetTableField(tableName, field, intVal);
                else if (value is fp fpVal) Runtime.SetTableField(tableName, field, fpVal);
                else if (value is float floatVal) Runtime.SetTableField(tableName, field, floatVal);
                else if (value is string strVal) Runtime.SetTableField(tableName, field, strVal);
            }
        }

        private void SetIntArray(string tableName, string field, int[] values)
            => Runtime.SetTableArray(tableName, field, values);

        private void SetStringArray(string tableName, string field, string[] values)
            => Runtime.SetTableArray(tableName, field, values);
    }
}
