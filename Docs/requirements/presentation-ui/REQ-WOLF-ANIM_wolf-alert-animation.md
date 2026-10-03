# 三狼警戒进入退出过渡动画

## 目标实现

靠近/离开营地播放完整 normal→alert 和 alert→normal 动画，再进入循环 idle；靠近只影响表现，敌对合法伤害才触发营地交战。

## 技术方案

MurkWolfCampContentSetup 从 Greater/Mini 提供模型提取四个非循环 clip，创建有限 N2A/A2N 中间 Animator 状态；外部采样仅用于真实 Idle/Move loop。客户端 alert latch/滞环不写 Gameplay。

## 边界情况

过渡中移动、普攻、死亡/回池与再次靠近/远离可正确切换，不能把有限 clip 当外部无限 loop；不改变权威营地目标。资源重生成通过 Unity API，保持营地/anchor/spawn Transform 与 GUID。

## 验收条件

四个 clip 非循环，两个 Animator 有 N2A/A2N 链；PlayMode 进入有限过渡后到 Alert Idle，离开经 A2N 到 Normal Idle；移动、攻击与死亡仍可路由。

## 附录：已知 clip

Greater idle_n2a 1.8 秒、idle_a2n 2.7 秒；Mini idle1_n2a 0.933 秒、idle1_a2n 1.167 秒。此前生成器直接使用 0.08 秒 blend，尚不能当此需求完成。Mini 攻击距离曾作 125 作者单位/1.25 逻辑单位实现假设，应保留该假设来源。
