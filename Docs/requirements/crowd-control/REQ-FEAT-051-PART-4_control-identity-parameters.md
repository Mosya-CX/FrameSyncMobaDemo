# 控制实例身份与参数块

## 本功能范围

本案细化“控制实例与模块参数”中的控制实例身份与参数块，仅覆盖下列明确接口与边界。

## 目标实现

配置型控制通过静态模块表和有界参数块执行。

## 技术方案

CrowdControlHandler 唯一运行入口；Definition 经 Bake 形成 module op 与 ParamLayout；实例按 Key 暴露、按 Offset 存储，不创建每实例模块对象。

## 边界情况

不新增 Kind、软硬控枚举或 Combat 控制管线；模块重入使用明确延迟规则；缺键和容量溢出可见失败。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

实现证据与已有测试位置关联总案；字段存在不能认定行为已验收。

### Instance 的最小字段

~~~csharp
public readonly struct CrowdControlInstance
{
    public readonly int InstanceId;
    public readonly CrowdControlId ControlId;
    public readonly int StartTick;
    public readonly int ExpireTick;
    public readonly CrowdControlParamBlock Params;
}
~~~

只有五项核心数据。Instance 是值类型；创建控制只把紧凑值写入 Handler 已复用的容器，不为实例创建模块对象或黑板 Dictionary。

明确不保存：

- SourceType；
- SourceConfigId；
- MergeKey；
- Kind；
- 模块对象列表；
- Definition 对象引用；
- 剩余秒数；
- 伤害、治疗或护盾数据。

### 为什么不保存来源

控制实例只需要回答：

- 我是什么配置；
- 我何时开始和结束；
- 我的模块需要哪些动态参数。

技能 ID、装备 ID、Buff ID 对控制计算没有通用意义，因此不进入基础实例。

如果 Charm 或 Taunt 需要施加者 UnitUid，调用方用 TargetUnit 等参数 Key 写入。它只对绑定这个 Key 的 ForcedBehavior 模块有意义。

### 不合并，也不修改既有实例

每次 Add 都创建独立实例。例如连续两次添加 30 Tick 的同一控制，会分别得到 Instance 101 与 Instance 102。

创建后：

- 不 ResetDuration；
- 不 ExtendDuration；
- 不 PatchParams；
- Handle 只用于 Remove 和查询。

同一效果再次生效时重新 Add。若一个外部 Buff 必须结束自己添加的控制，它保存 Handle，并在 Buff 移除时调用一次 Remove。

### 对外按 Key，内部按 Offset

参数系统分为三部分：

~~~mermaid
flowchart TD
    A["Inspector 字符串 Key"] --> B["Bake 为 ParamKeyId"]
    B --> C["调用方 Writer.Set Key Value"]
    C --> D["Add 按 ParamLayout 写入 ParamBlock"]
    D --> E["模块按已编译 Offset 读取"]
~~~

设计师和调用方都不分配槽位。模块热路径也不做字符串查找或哈希。

### CrowdControlParamKey

Inspector 使用可读字符串，例如：

- TargetUnit；
- MoveSlowRatio；
- Direction；
- Distance；
- BehaviorPriority。

Bake 时使用项目统一的 StableStringId32 生成 CrowdControlParamKey：

~~~csharp
public readonly struct CrowdControlParamKey
{
    public readonly uint Value;
}
~~~

要求：

- 不使用 string.GetHashCode；
- 构建时检查哈希碰撞；
- 同名 Key 在全项目只能注册一种值类型；
- 代码侧生成常量，不在运行时从字符串计算。

### 支持的数据类型

ParamType 只允许有限值类型：

| ParamType | 字节 | 示例 |
|---|---:|---|
| Byte | 1 | 小枚举、开关 |
| Short | 2 | 小范围优先级 |
| Int | 4 | Tick、索引 |
| Long | 8 | 大整数 |
| Bool | 1 | 运行标志 |
| Fp | 按项目 fp | 比例、距离 |
| UnitUid | 按 UnitUid | 行为目标 |
| Fp2 | 2 × fp | 方向、目标点 |
| Mask32 / Mask64 | 4 / 8 | 位标志 |

enum 使用明确的 byte、short 或 int 底层类型。

不支持：

- float / double 运行值；
- string；
- Unity Object；
- object；
- List；
- Dictionary；
- 未注册的任意 struct。

### ParameterSchema 与 ParamLayout

ParameterSchema 是 Inspector 配置：

| 字段 | 作用 |
|---|---|
| Key | 可读字符串 |
| Type | 初始类型，之后不可更改 |
| Required | Add 时是否必须提供 |

Bake 后，同一 Definition 内生成 ParamLayout：

| 字段 | 作用 |
|---|---|
| ParamKeyId | 稳定 Key |
| Type | 运行时类型校验 |
| Offset | 在固定字节块中的位置 |
| Size | 实际占用字节 |

Offset 按 Size 与 Alignment 自动分配。设计师不会看到或填写 Offset。

示例：KnockBack

| Key | Type | Required | 自动布局示例 |
|---|---|---:|---:|
| Direction | Fp2 | 是 | Offset 0 |
| Distance | Fp | 是 | Offset 16 |
| MoveTicks | Int | 是 | Offset 24 |

具体 Offset 由 Bake 结果决定，示例数值不构成配置约定。

### CrowdControlParamWriter

调用方使用短生命周期值类型 Writer：

~~~csharp
CrowdControlParamWriter writer = default;
writer.Set(ControlParamKeys.TargetUnit, targetUid);
writer.Set(ControlParamKeys.MoveSlowRatio, slowFp);
~~~

Set 的签名使用 Set<T>(CrowdControlParamKey key, in T value) where T : unmanaged，避免装箱。

Set<T>：

1. 根据 T 得到已注册 ParamType；
2. 记录 ParamKeyId、ParamType、Size 和原始值；
3. 同一个 Key 重复 Set 时覆盖 Writer 中的旧值；
4. 超过最大 Entry 数立即失败。

Writer 建议最多保存 8 个 Entry，每个 Entry 最多 16 字节，可覆盖 fp2。它只存在于调用栈或技能阶段临时状态，不进入 CrowdControlInstance。

值的初始类型同时记录在全局 ParamKey 注册表、Definition.ParameterSchema 和 Writer Entry 中；实例 ParamBlock 不重复保存 TypeCode，因为可由 ControlId 对应的 ParamLayout 唯一恢复。

### Add 时物化参数块

CrowdControlParamBlock 建议使用固定 64 字节数据区：

~~~csharp
public struct CrowdControlParamBlock
{
    private FixedBytes64 data;
}
~~~

FixedBytes64 可以是项目自有定长字节结构；若已经依赖 Unity.Collections，也可使用对应 fixed-bytes 类型，不为此单独增加依赖。

物化算法：

~~~text
Materialize(definition.ParamLayout, writer):
    block.Clear()
    writtenRequiredMask = 0

    遍历 writer entries:
        layout = 按 ParamKeyId 查找
        找不到 -> InvalidParams
        entry.Type != layout.Type -> InvalidParams
        entry.Size != layout.Size -> InvalidParams

        把 entry.Value 复制到
            block[layout.Offset, layout.Size]

        标记对应 Required Key 已写入

    若存在未写入的 Required Key:
        返回 InvalidParams

    返回 block
~~~

Definition 的 ParamLayout 通常只有数项，Add 阶段可以使用排序数组二分或小数组线性查找。模块运行热路径不走这次 Key 查找。

未写入的可选 Key 保持全零语义。真正的静态常量应写在 Module Authoring 中并 Bake 到 ControlModuleOp，不为动态黑板再维护 DefaultParams。

### 模块读取

模块配置在 Inspector 绑定 ParamKey。Bake 时已经把 Key 编译为 ControlModuleOp.ParamOffset。

外部低频读取仍然可以按 Key：

~~~csharp
instance.TryGetParam(
    ControlParamKeys.TargetUnit,
    out UnitUid target);
~~~

TryGetParam 通过 instance.ControlId 取得 Definition.ParamLayout，校验类型后读取对应 Offset。

模块热路径则直接使用 Bake 好的 Offset：

~~~text
slow = instance.Params.ReadFp(op.ParamOffset0)
target = instance.Params.ReadUnitUid(op.ParamOffset1)
~~~

因此对外获得了 Key 黑板的易用性，对内仍然是固定偏移读取。

### 容量规则

建议初始上限：

- 每个 Definition 最多 8 个动态 Key；
- ParamBlock 最多 64 字节；
- 单个值最多 16 字节。

超限是构建错误，不在运行时退化成 Dictionary。

若实际内容证明 64 字节不足，统一提升 ParamBlock 容量；不要为单个控制引入堆分配的可变容器。

---



## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

