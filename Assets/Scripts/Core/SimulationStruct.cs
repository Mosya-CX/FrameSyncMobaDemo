[System.Flags]
public enum SimulationEntityType
{
    None = 0,
    Hero = 1 << 0,
    Minion = 1 << 1,
    Monster = 1 << 2,
    Turret = 1 << 3,
    Summon = 1 << 4,
    All = ~0,
}

[System.Flags]
public enum SimulationTeamMask
{
    None = 0,
    Neutral = 1 << 0,
    Blue = 1 << 1,
    Red = 1 << 2,
    All = Neutral | Blue | Red,
}

public struct SimulationFilter
{
    public SimulationTeamMask TeamMask;
    public SimulationEntityType EntityMask;

    public static SimulationFilter Default => new SimulationFilter
    {
        TeamMask = SimulationTeamMask.All,
        EntityMask = SimulationEntityType.All,
    };
}

public interface IUnitContactListener
{
    void OnUnitContact(UnitContactEventType eventType, UnitCore other);
}