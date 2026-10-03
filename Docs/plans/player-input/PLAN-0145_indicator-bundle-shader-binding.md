# 指示器 Bundle Shader 绑定

## 本次执行范围

本计划对应原编码 0145 的一次执行：指示器 Bundle Shader 绑定。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [技能指示器与本地生命周期](../../requirements/player-input/REQ-FEAT-081_skill-indicator.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs`：`SkillIndicatorDriver`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs`：

```csharp
using System.Collections.Generic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Manages ability aim indicator GameObjects.
    ///
    /// Supports the MOBA skill-indicator rules:
    /// - ordinary Direction aims show a rounded bar toward the cursor;
    /// - directional multi-zone stages draw their exact primary and sweet-
    ///   spot outlines from the formal runtime definition;
    /// - Point/Unit aims show a cursor-following ground circle;
    /// - aiming abilities may also show a cast-range circle.
    ///
    /// Presentation only; never affects Gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillIndicatorDriver : MonoBehaviour
    {
        [Header("Indicator prefabs (assigned in Inspector or code)")]
        [SerializeField] private GameObject directionIndicatorPrefab;
        [SerializeField] private GameObject rangeCirclePrefab;
        [SerializeField] private GameObject groundTargetPrefab;

        [Header("Defaults")]
        [Tooltip("Ground circle radius used when the ability config does not "
            + "provide one.")]
        [SerializeField] private float groundTargetDefaultRadius = 0.5f;

        private GameObject _directionInstance;
        private GameObject _rangeCircleInstance;
        private GameObject _groundTargetInstance;
        private GameObject _worldSpaceRoot;

        private Transform _directionBody;
        private Transform _directionHead;
        private Transform _groundDisc;
        private Transform _rangeDisc;
        private LineRenderer _directionalZoneOutline;
        private LineRenderer _directionalSweetSpotOutline;
        private Material _runtimeLineMaterial;
        private readonly List<Material> _runtimeGenericMaterials =
            new List<Material>();

        private AimKind _activeKind;
        private fp _activeRange;
        private bool _visible;
        private bool _showRangeCircle;
        private fp _groundRadius;
        private fp _directionLength;
        private DirectionalMultiZoneDamageStageDef
            _activeDirectionalZone;

        public bool IsVisible => _visible;
        public AimKind ActiveKind => _activeKind;
        public DirectionalMultiZoneDamageStageDef
            ActiveDirectionalZone => _activeDirectionalZone;

        /// <summary>
        /// Assign prefabs before the driver is enabled. Safe to call before
        /// Awake; existing instances are recreated when the prefabs change.
        /// </summary>
        public void Configure(
            GameObject directionPrefab,
            GameObject rangeCirclePrefab,
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**技能指示器与本地生命周期**

SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。

受控单位变化、禁用、死亡、UI 门禁和 Session 结束关闭；游戏回滚后跟随 Runtime，不反写命令或技能距离。

**单位视图绑定与语义挂点**

UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。

视图不能反写 Gameplay；异步加载不能复用旧生命；逻辑空间所有者不随模型层级变化。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

没有找到原范围精确命名的当前测试文件；关闭记录依据用户人工功能确认，不伪造自动化用例。

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
