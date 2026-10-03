# 同Tick动态占用与回营路线

## 目标实现

三只狼追击和回营时采用当前 Tick 的单位占用，不只依赖 RVO 解决堵路；无关路径不每 Tick 无条件重搜。

## 技术方案

Unit 拥有 DynamicNavigationFrame，移动管线在 locomotion 评估前按稳定 UID 单次从权威移动前姿态建立不可变占用和 radius-class 层。A* 查询成本合同同时应用于扩展、对角检查、不可走目标回退和 LOS 简化：立即碰撞足迹为硬障碍，其它动态占用是有限高成本；自身和已预留目的地按合同豁免。

路线的前方走廊因新占用变得不可用时失效重搜，追踪目标移动和原 corridor 失效继续用已有策略。通行优先/营地目的地预留按 movement purpose、CampId、CampSlotIndex、UnitUid 稳定键，RVO 仍负责最终速度平滑。

## 边界情况

不能用异步完成/注册顺序决定通行。所有单位在同 Tick 查询同一 frame。动态 frame 派生，恢复后重建，不进入 Snapshot/checksum；影响后续结果的 route generation/cursor 等状态按需进入 Unit Snapshot/checksum。

不引入速度预测、时空协作 A*、D* Lite、NavMesh 或 Unity 物理权威。兵线流场不改成逐单位 A*。

## 验收条件

动态障碍改变所选路径；自体/目标豁免和半径层正确；平滑不切过硬占用；插入顺序改变仍读同 frame；无关 forward 路径不抖动；三名回营成员抵达不同预留点；重复执行和恢复/重演等价。

## 工程实际核查与附录

工作树存在 DynamicNavigationFrame、AStarPathService 和相关路线状态改动。旧计划仍 Deferred；本期仅登记已有修改与待验收，不能认定这些行为已通过。
