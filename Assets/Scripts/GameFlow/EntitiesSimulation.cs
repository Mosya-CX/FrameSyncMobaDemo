using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class EntitiesSimulation : MonoSingleton<EntitiesSimulation>, IStateful
{
    public IReadOnlyDictionary<UnitUID, UnitCore> simulableUnits => UnitManager.Instance.Spawns;
    public IReadOnlyDictionary<MissleUID, BaseMissle> simulationMissles => MissleManager.Instance.Spawns;

    private Queue<MissleTriggerEvent> missleTriggerEventQueue = new();

    private uint localTick;

    public IEnumerator Init()
    {
        yield break;
    }

    public void Begin()
    {
        missleTriggerEventQueue.Clear();
    }

    public void Clean()
    {
        missleTriggerEventQueue.Clear();
    }

    public void Tick(uint currentTick)
    {
        localTick = currentTick;

        Simulation(currentTick);

        while (missleTriggerEventQueue.Count > 0)
        {
            var triggerEvent = missleTriggerEventQueue.Dequeue();
            if (MissleManager.Instance.Spawns.TryGetValue(triggerEvent.triggeredMissleId, out var missle) &&
                UnitManager.Instance.Spawns.TryGetValue(triggerEvent.triggeredUnitUid, out var unit))
                missle.OnMissleTrigger(unit);
        }
    }

    #region 模拟
    private void Simulation(ulong currentTick)
    {
        // 遍历所有投掷物和单位，检测相交
        foreach (var misslePair in simulationMissles)
        {
            BaseMissle missle = misslePair.Value;
            fp3 misslePos = missle.LogicPosition;
            fp2 rot = missle.LogicRotation; // (y, w) 对应四元数的 y 和 w 分量
            // 计算 forward 方向（绕Y轴旋转）
            fp sinHalf = rot.x; // sin(θ/2)
            fp cosHalf = rot.y; // cos(θ/2)
            fp2 forward = new fp2(2 * sinHalf * cosHalf, cosHalf * cosHalf - sinHalf * sinHalf); // (x, z)
            fp2 right = new fp2(-forward.y, forward.x);
            // 假设 LogicSize 为全长，因此半长 = x/2，半宽 = z/2
            fp halfLength = missle.LogicSize.x / 2;
            fp halfWidth = missle.LogicSize.z / 2;

            foreach (var unitPair in simulableUnits)
            {
                UnitCore unit = unitPair.Value;
                fp3 unitPos = unit.LogicPosition;
                fp unitRadius = unit.unitSizeRadius;

                // 将单位中心转换到投掷物局部坐标系
                fp2 delta = new fp2(unitPos.x - misslePos.x, unitPos.z - misslePos.z);
                fp f = fpmath.dot(delta, forward); // 沿 forward 方向的投影
                fp r = fpmath.dot(delta, right);   // 沿 right 方向的投影

                // 计算圆心到矩形的最短距离
                fp df = fpmath.max(fpmath.abs(f) - halfLength, 0);
                fp dr = fpmath.max(fpmath.abs(r) - halfWidth, 0);
                fp dist = fpmath.sqrt(df * df + dr * dr);
                if (dist <= unitRadius)
                {
                    missleTriggerEventQueue.Enqueue(new MissleTriggerEvent
                    {
                        triggeredUnitUid = unitPair.Key,
                        triggeredMissleId = misslePair.Key
                    });
                }
            }
        }
    }

    public struct MissleTriggerEvent
    {
        public UnitUID triggeredUnitUid;
        public MissleUID triggeredMissleId;
    }

    #endregion

    #region 工具函数
    // 查找以origin为矩形底边中点，toward为矩形朝向，l为矩形长度，w为矩形宽度的矩形范围内符合条件的单位
    public IReadOnlyList<UnitCore> SearchRectRangeUnits(fp3 origin, fp3 toward, fp l, fp w, SimulationDetectUnitTeam detectTeam = SimulationDetectUnitTeam.All, SimulationDetectUnitType detectType = SimulationDetectUnitType.All)
    {
        List<UnitCore> result = new List<UnitCore>();

        // 转换为XZ平面二维向量
        fp2 originXZ = new fp2(origin.x, origin.z);
        fp2 forward = new fp2(toward.x, toward.z);

        // 方向向量无效时返回空
        if (fpmath.lengthsq(forward) < fp.precision)
            return result;

        fpmath.normalize(forward);
        fp2 right = new fp2(-forward.y, forward.x);
        fp halfW = w / 2;

        foreach (var unit in simulableUnits.Values)
        {
            if (!CheckUnitConformToDetection(unit, detectTeam, detectType))
                continue;

            fp2 posXZ = new fp2(unit.LogicPosition.x, unit.LogicPosition.z);
            fp radius = unit.unitSizeRadius; // 单位的判定半径

            fp halfR = radius / 2;
            int countInside = 0;

            // 遍历9个采样点 (dx, dy) 取值 -halfR, 0, halfR
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    fp dx = i * halfR;
                    fp dy = j * halfR;
                    fp2 samplePos = posXZ + dx * forward + dy * right;
                    fp2 v = samplePos - originXZ;
                    fp f = fpmath.dot(v, forward);
                    fp r = fpmath.dot(v, right);

                    if (f >= 0 && f <= l && fpmath.abs(r) <= halfW)
                        countInside++;
                }
            }

            // 9个点中至少5个在矩形内则认为单位在矩形判定内
            if (countInside >= 5)
                result.Add(unit);
        }

        return result;
    }

    // 查找以origin为梯形底边中点，toward为梯形朝向，buttonLength为梯形底边长度，topLength为梯形顶边长度，height为梯形高度的梯形范围内符合条件的单位
    public IReadOnlyList<UnitCore> SearchLadderRabgeUnits(fp3 origin, fp3 toward, fp buttonLength, fp topLength, fp height, SimulationDetectUnitTeam detectTeam = SimulationDetectUnitTeam.All, SimulationDetectUnitType detectType = SimulationDetectUnitType.All)
    {
        List<UnitCore> result = new List<UnitCore>();

        // 计算局部坐标系
        fp2 originXZ = new fp2(origin.x, origin.z);
        fp2 forward = new fp2(toward.x, toward.z);
        if (fpmath.lengthsq(forward) < fp.precision)
            return result;
        fpmath.normalize(forward);
        fp2 right = new fp2(-forward.y, forward.x);
        fp halfBottom = buttonLength / 2;
        fp halfTop = topLength / 2;

        foreach (var unit in simulableUnits.Values)
        {
            if (!CheckUnitConformToDetection(unit, detectTeam, detectType))
                continue;

            fp2 unitPosXZ = new fp2(unit.LogicPosition.x, unit.LogicPosition.z);
            fp unitR = unit.unitSizeRadius;
            fp halfR = unitR / 2;
            int countInside = 0;

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    fp dx = i * halfR; // 沿 right 方向偏移
                    fp dy = j * halfR; // 沿 forward 方向偏移
                    fp2 samplePos = unitPosXZ + dx * right + dy * forward;
                    fp2 v = samplePos - originXZ;
                    fp f = fpmath.dot(v, forward);
                    fp r = fpmath.dot(v, right);

                    // 梯形条件：0 <= f <= height 且 |r| <= 线性插值
                    if (f >= 0 && f <= height)
                    {
                        fp maxR = halfBottom + (halfTop - halfBottom) * (f / height);
                        if (fpmath.abs(r) <= maxR)
                            countInside++;
                    }
                }
            }

            if (countInside >= 5)
                result.Add(unit);
        }

        return result;
    }

    // 查找以origin为圆形中点，radius为圆形半径的圆形范围内符合条件的单位
    public IReadOnlyList<UnitCore> SearchRoundRabgeUnits(fp3 origin, fp radius, SimulationDetectUnitTeam detectTeam = SimulationDetectUnitTeam.All, SimulationDetectUnitType detectType = SimulationDetectUnitType.All)
    {
        List<UnitCore> result = new List<UnitCore>();

        fp2 originXZ = new fp2(origin.x, origin.z);

        foreach (var unit in simulableUnits.Values)
        {
            if (!CheckUnitConformToDetection(unit, detectTeam, detectType))
                continue;

            fp2 unitPosXZ = new fp2(unit.LogicPosition.x, unit.LogicPosition.z);
            fp unitR = unit.unitSizeRadius;
            fp halfR = unitR / 2;
            int countInside = 0;

            // 使用世界坐标轴作为采样方向
            fp2 dirX = new fp2(1, 0);
            fp2 dirZ = new fp2(0, 1);

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    fp dx = i * halfR;
                    fp dz = j * halfR;
                    fp2 samplePos = unitPosXZ + dx * dirX + dz * dirZ;
                    fp2 v = samplePos - originXZ;
                    if (fpmath.length(v) <= radius)
                        countInside++;
                }
            }

            if (countInside >= 5)
                result.Add(unit);
        }

        return result;
    }

    // 查找以origin为扇形起点，toward为扇形朝向，radius为扇形半径，angle为扇形角度的扇形范围内符合条件的单位
    public IReadOnlyList<UnitCore> SearchFanShapedRabgeUnits(fp3 origin, fp3 toward, fp radius, fp angle, SimulationDetectUnitTeam detectTeam = SimulationDetectUnitTeam.All, SimulationDetectUnitType detectType = SimulationDetectUnitType.All)
    {
        List<UnitCore> result = new List<UnitCore>();

        fp2 originXZ = new fp2(origin.x, origin.z);
        fp2 forward = new fp2(toward.x, toward.z);
        if (fpmath.lengthsq(forward) < fp.precision)
            return result;
        fpmath.normalize(forward);
        fp2 right = new fp2(-forward.y, forward.x);

        fp cosHalfAngle = fpmath.cos(fpmath.clamp(angle, 0, 360) / 2);

        foreach (var unit in simulableUnits.Values)
        {
            if (!CheckUnitConformToDetection(unit, detectTeam, detectType))
                continue;

            fp2 unitPosXZ = new fp2(unit.LogicPosition.x, unit.LogicPosition.z);
            fp unitR = unit.unitSizeRadius;
            fp halfR = unitR / 2;
            int countInside = 0;

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    fp dx = i * halfR;
                    fp dy = j * halfR;
                    fp2 samplePos = unitPosXZ + dx * right + dy * forward;
                    fp2 v = samplePos - originXZ;
                    fp dist = fpmath.length(v);
                    if (dist <= radius)
                    {
                        if (dist > 0)
                        {
                            fp dot = fpmath.dot(v, forward);
                            if (dot >= dist * cosHalfAngle)
                                countInside++;
                        }
                        else
                        {
                            // 采样点与原点重合，视为在扇形内
                            countInside++;
                        }
                    }
                }
            }

            if (countInside >= 5)
                result.Add(unit);
        }

        return result;
    }

    private bool CheckUnitConformToDetection(in UnitCore target, in SimulationDetectUnitTeam detectTeam, in SimulationDetectUnitType detectType)
    {
        if (target.TeamID == 1)
        {
            if (!detectTeam.HasFlag(SimulationDetectUnitTeam.Neutral))
                return false;
        }
        else if (target.TeamID == 2)
        {
            if (!detectTeam.HasFlag(SimulationDetectUnitTeam.BlueTeam))
                return false;
        }
        else if (target.TeamID == 3)
        {
            if (!detectTeam.HasFlag(SimulationDetectUnitTeam.RedTeam))
                return false;
        }
        else
            return false;

        if (target.CompareTag("Hero"))
        {
            if (!detectType.HasFlag(SimulationDetectUnitType.Hero))
                return false;
        }
        else if (target.CompareTag("Mob"))
        {
            if (!detectType.HasFlag(SimulationDetectUnitType.Mob))
                return false;
        }
        else if (target.CompareTag("Monster"))
        {
            if (!detectType.HasFlag(SimulationDetectUnitType.Monster))
                return false;
        }
        else
            return false;

        return true;
    }

    #endregion

    #region 快照和回滚
    [System.Serializable]
    public class SimulationSnapshot
    {
        public uint tick;
        public List<MissleTriggerEvent> missleTriggerEventCache;
    }


    public object CaptureState()
    {
        return new SimulationSnapshot
        {
            tick = localTick,
            missleTriggerEventCache = new List<MissleTriggerEvent>(missleTriggerEventQueue),
        };
    }

    public void RestoreState(object state)
    {
        if (state is SimulationSnapshot snapshot)
        {
            localTick = snapshot.tick;
            missleTriggerEventQueue.Clear();
            missleTriggerEventQueue = new(snapshot.missleTriggerEventCache);
        }
    }

    #endregion
}

[System.Flags]
public enum SimulationDetectUnitTeam
{
    Neutral = 1,
    RedTeam = 2,
    BlueTeam = 4,
    All = Neutral | RedTeam | BlueTeam,
}
[System.Flags]
public enum SimulationDetectUnitType
{
    Hero = 1,
    Mob = 2,
    Monster = 4,
    All = Hero | Mob | Monster,
}