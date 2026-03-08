using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using Sirenix.OdinInspector;

public sealed class RVOGenerator : MonoSingleton<RVOGenerator>
{
    [SerializeField, LabelText("更新间隔")]

    private fp simulationUpdateInterval = 0.1m;

    [SerializeField, LabelText("避障半径")]
    private fp avoidanceRadius = 3.0m;

    [SerializeField, LabelText("最大避障力")]
    private fp maxForce = 2.0m;

    [SerializeField, LabelText("时间视野")]
    private fp timeHorizon = 1.0m;

    [SerializeField, LabelText("切向因子")]
    private fp tangentialFactor = 0.5m;

    [SerializeField, LabelText("径向因子")]
    private fp radialFactor = 0.8m;

    [SerializeField, LabelText("启用异步计算")]
    private bool enableAsync = true;

    //[SerializeField, LabelText("异步阈值（障碍物数量）")]
    //private int asyncThreshold = 50;

    private fp simulationTimer;
    private List<IDynamicObstacle> obstacles = new List<IDynamicObstacle>();
    private Dictionary<IDynamicObstacle, fp3> lastModifiedDirection = new Dictionary<IDynamicObstacle, fp3>();

    private static readonly fp FP_EPSILON = (fp)1e-6m;

    public fp DeltaTime => GameFlowManager.Instance.TickIntervalFP;

    public void Register(IDynamicObstacle obstacle)
    {
        if (!obstacles.Contains(obstacle))
        {
            obstacles.Add(obstacle);
            lastModifiedDirection[obstacle] = obstacle.ObstacleDirection;
        }
    }

    public void Unregister(IDynamicObstacle obstacle)
    {
        obstacles.Remove(obstacle);
        lastModifiedDirection.Remove(obstacle);
    }

    public fp3 GetModifiedDirection(IDynamicObstacle obstacle)
    {
        if (lastModifiedDirection.TryGetValue(obstacle, out fp3 dir))
            return dir;
        return obstacle.ObstacleDirection;
    }

    public IEnumerator Init() { yield break; }
    public void Begin() { }
    public void Clean()
    {
        obstacles.Clear();
        lastModifiedDirection.Clear();
    }

    public void Tick(uint currentTick)
    {
        simulationTimer += DeltaTime;
        if (simulationTimer > simulationUpdateInterval)
        {
            simulationTimer -= simulationUpdateInterval;
            Simulate();
        }
    }

    #region 核心模拟

    /// <summary>
    /// 障碍物快照
    /// </summary>
    private struct ObstacleSnapshot
    {
        public fp3 Position;
        public fp3 Direction;
        public fp Speed;
        public int IgnoreIndex; // 忽略对象的索引，-1 表示无忽略
        public int OriginalIndex; // 在原列表中的索引，用于写回结果
    }

    private void Simulate()
    {
        int count = obstacles.Count;
        if (count == 0) return;
        
        if (enableAsync)
            SimulateAsync(count);
        else
            SimulateSync(count);

        //if (enableAsync && count >= asyncThreshold)
        //{
        //    SimulateAsync(count);
        //}
        //else
        //{
        //    SimulateSync(count);
        //}
    }

    /// <summary>
    /// 同步版本
    /// </summary>
    private void SimulateSync(int count)
    {
        // 预取所有数据
        var positions = new fp3[count];
        var directions = new fp3[count];
        var speeds = new fp[count];
        var velocities = new fp3[count];
        var ignores = new IDynamicObstacle[count];

        for (int i = 0; i < count; i++)
        {
            var obs = obstacles[i];
            positions[i] = obs.ObstaclePosition;
            directions[i] = obs.ObstacleDirection;
            speeds[i] = obs.ObstacleSpeed;
            velocities[i] = obs.ObstacleDirection * obs.ObstacleSpeed;
            ignores[i] = obs.Ingore;
        }

        fp3[] newDirections = new fp3[count];

        for (int i = 0; i < count; i++)
        {
            newDirections[i] = ComputeForceForAgent(i, positions, directions, velocities, speeds, ignores);
        }

        // 写回字典
        for (int i = 0; i < count; i++)
        {
            lastModifiedDirection[obstacles[i]] = newDirections[i];
        }
    }

    /// <summary>
    /// 异步版本
    /// </summary>
    private void SimulateAsync(int count)
    {
        // 构建快照数组
        var snapshot = new ObstacleSnapshot[count];
        var positions = new fp3[count];
        var directions = new fp3[count];
        var speeds = new fp[count];
        var velocities = new fp3[count];
        var ignoreIndices = new int[count]; // 存储每个障碍物的忽略对象索引，-1 表示无

        for (int i = 0; i < count; i++)
        {
            var obs = obstacles[i];
            positions[i] = obs.ObstaclePosition;
            directions[i] = obs.ObstacleDirection;
            speeds[i] = obs.ObstacleSpeed;
            velocities[i] = obs.ObstacleDirection * obs.ObstacleSpeed;
            snapshot[i].OriginalIndex = i;
            snapshot[i].Position = positions[i];
            snapshot[i].Direction = directions[i];
            snapshot[i].Speed = speeds[i];
            snapshot[i].IgnoreIndex = -1;
        }

        // 先找出忽略对象的索引
        for (int i = 0; i < count; i++)
        {
            var ignore = obstacles[i].Ingore;
            if (ignore != null)
            {
                int ignoreIdx = obstacles.IndexOf(ignore);
                snapshot[i].IgnoreIndex = ignoreIdx; // 若找不到，则为 -1
            }
        }

        // 结果数组
        fp3[] newDirections = new fp3[count];

        // 并行计算
        Parallel.For(0, count, i =>
        {
            // 为每个障碍物计算受力，使用快照数据
            newDirections[i] = ComputeForceFromSnapshot(i, snapshot);
        });

        // 写回主线程字典
        for (int i = 0; i < count; i++)
        {
            int originalIdx = snapshot[i].OriginalIndex;
            lastModifiedDirection[obstacles[originalIdx]] = newDirections[i];
        }
    }

    /// <summary>
    /// 基于快照数组为指定索引的障碍物计算新方向
    /// </summary>
    private fp3 ComputeForceFromSnapshot(int index, ObstacleSnapshot[] snapshot)
    {
        var self = snapshot[index];
        fp3 posA = self.Position;
        fp3 dirA = self.Direction;
        fp speedA = self.Speed;
        fp3 velA = dirA * speedA;
        int ignoreIdx = self.IgnoreIndex;

        fp3 totalForce = fp3.zero;
        int count = snapshot.Length;

        for (int j = 0; j < count; j++)
        {
            if (j == index) continue;
            if (j == ignoreIdx) continue; // 忽略指定对象

            var other = snapshot[j];
            fp3 posB = other.Position;
            fp3 velB = other.Direction * other.Speed;

            fp3 relPos = posA - posB;
            fp3 relVel = velA - velB;

            fp distSq = fpmath.lengthsq(relPos);
            if (distSq > avoidanceRadius * avoidanceRadius) continue;

            fp dist = fpmath.sqrt(distSq);
            fp3 dirToOther = relPos / (dist + FP_EPSILON); // 从 B 指向 A

            // 径向相对速度
            fp radialSpeed = fpmath.dot(relVel, dirToOther);

            fp timeToCollision = fp.max_value;
            if (radialSpeed < -FP_EPSILON) // 正在靠近
            {
                timeToCollision = dist / (-radialSpeed);
            }

            fp3 force = fp3.zero;

            if (timeToCollision < timeHorizon)
            {
                // 径向强度：距离碰撞越近，力越大
                fp radialStrength = fp.one - fpmath.clamp(timeToCollision / timeHorizon, fp.zero, fp.one);

                // 径向力（推开）
                fp3 radialForce = dirToOther * radialStrength * maxForce * radialFactor;

                // 切向力（横向避让）
                fp3 perp = new fp3(-dirToOther.z, 0, dirToOther.x); // 垂直于 dirToOther
                // 根据相对速度的切向分量决定左右
                fp3 relVelTangent = relVel - dirToOther * radialSpeed;
                fp tangentSign = fpmath.sign(fpmath.dot(relVelTangent, perp));
                fp3 tangentialForce = perp * tangentSign * radialStrength * maxForce * tangentialFactor;

                force = radialForce + tangentialForce;
            }
            else
            {
                // 即使不会立即碰撞，距离过近也施加基础排斥
                fp closeness = fp.one - fpmath.clamp(dist / avoidanceRadius, fp.zero, fp.one);
                force = dirToOther * closeness * maxForce * 0.5m;
            }

            totalForce += force;
        }

        // 合并原始方向与避障力
        fp3 desiredDir = dirA;
        fp3 newDir = desiredDir + totalForce;

        fp lenSq = fpmath.lengthsq(newDir);
        if (lenSq > FP_EPSILON)
            return fpmath.normalize(newDir);
        else
            return desiredDir;
    }

    /// <summary>
    /// 同步版本的计算函数（与上面逻辑相同，但使用数组参数）
    /// </summary>
    private fp3 ComputeForceForAgent(int index, fp3[] positions, fp3[] directions,
                                     fp3[] velocities, fp[] speeds, IDynamicObstacle[] ignores)
    {
        fp3 posA = positions[index];
        fp3 dirA = directions[index];
        fp speedA = speeds[index];
        fp3 velA = velocities[index];
        var ignoreA = ignores[index];

        fp3 totalForce = fp3.zero;
        int count = positions.Length;

        for (int j = 0; j < count; j++)
        {
            if (j == index) continue;
            if (ignores[j] == ignoreA) continue; // 如果对方忽略自己，或自己忽略对方？这里按原逻辑：如果对方是被忽略对象则跳过

            fp3 posB = positions[j];
            fp3 velB = velocities[j];

            fp3 relPos = posA - posB;
            fp3 relVel = velA - velB;

            fp distSq = fpmath.lengthsq(relPos);
            if (distSq > avoidanceRadius * avoidanceRadius) continue;

            fp dist = fpmath.sqrt(distSq);
            fp3 dirToOther = relPos / (dist + FP_EPSILON);

            fp radialSpeed = fpmath.dot(relVel, dirToOther);

            fp timeToCollision = fp.max_value;
            if (radialSpeed < -FP_EPSILON)
            {
                timeToCollision = dist / (-radialSpeed);
            }

            fp3 force = fp3.zero;

            if (timeToCollision < timeHorizon)
            {
                fp radialStrength = fp.one - fpmath.clamp(timeToCollision / timeHorizon, fp.zero, fp.one);
                fp3 radialForce = dirToOther * radialStrength * maxForce * radialFactor;
                fp3 perp = new fp3(-dirToOther.z, 0, dirToOther.x);
                fp3 relVelTangent = relVel - dirToOther * radialSpeed;
                fp tangentSign = fpmath.sign(fpmath.dot(relVelTangent, perp));
                fp3 tangentialForce = perp * tangentSign * radialStrength * maxForce * tangentialFactor;
                force = radialForce + tangentialForce;
            }
            else
            {
                fp closeness = fp.one - fpmath.clamp(dist / avoidanceRadius, fp.zero, fp.one);
                force = dirToOther * closeness * maxForce * 0.5m;
            }

            totalForce += force;
        }

        fp3 desiredDir = dirA;
        fp3 newDir = desiredDir + totalForce;

        fp lenSq = fpmath.lengthsq(newDir);
        if (lenSq > FP_EPSILON)
            return fpmath.normalize(newDir);
        else
            return desiredDir;
    }

    #endregion
}

public interface IDynamicObstacle
{
    void RegisterRVOGenerator();
    void UnregisterRVOGenerator();
    fp3 ObstaclePosition { get; }
    fp3 ObstacleDirection { get; }
    fp ObstacleSpeed { get; }
    IDynamicObstacle Ingore { get; } // 修正时忽略其影响的对象
}
