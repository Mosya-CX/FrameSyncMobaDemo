using System;
using Unity.Mathematics;
using Unity.Mathematics.FixedPoint;

public class DeterministicRandom : Singleton<DeterministicRandom>, IStateful
{
    private uint state;
    public static void Init(uint seed) => Instance.SetSeed(seed);
    public void SetSeed(uint seed)
    {
        state = seed;
    }

    #region 随机功能函数
    // 生成 0-1 之间的定点数
    public fp NextFP()
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return new fp((int)(state % 10000)) / (fp)10000;
    }

    // 生成范围内整数
    public int NextInt(int min, int max)
    {
        return min + (int)(NextFP() * (max - min));
    }
    #endregion

    #region 快照和恢复
    public object CaptureState() => state;
    public void RestoreState(object stateObj) => state = (uint)stateObj;

    #endregion
}

