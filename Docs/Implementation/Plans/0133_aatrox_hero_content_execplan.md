# ExecPlan 0133 - Aatrox formal hero vertical slice

> Status: Complete (2026-08-11).

## Goal

Add Aatrox as a second formal hero by following the existing
`TestHeroRuntime.prefab` authoring and registration pipeline while implementing
his mechanics through reusable deterministic Ability, Buff, Combat, Movement
and Projectile extension points.

The completed slice must provide:

- a formal hero prototype, loadout, ability catalog entries, runtime prefab,
  display entry and global-prefab registrations;
- a three-cast Q whose direction is locked per cast while its impact geometry
  follows the caster's deterministic displacement;
- distinct Q1/Q2/Q3 shapes and sweet spots, scaling, knock-up and target rules;
- W projectile impact, slow, a stationary deterministic area projectile,
  escape/completion settlement and pull;
- E dash usable alongside other ability sessions, attack-timer reset and
  passive omnivamp;
- R's decaying movement speed, attack-damage/healing modifiers, ghosting and
  kill-participation duration refresh;
- the passive empowered attack, cooldown reductions and actual-damage healing;
- snapshot, restore/replay and checksum coverage for all cross-Tick state.

## Authoritative inputs

- `Docs/Architecture/DECISION_LOG.md`
- `Docs/Architecture/DESIGN_INDEX.md`
- `Docs/Design/AbilitySystem_Design_v15_2.md`
- `Docs/Design/UnitFramework_Design_v27_3.md`
- `Docs/Design/BuffSystem_Design_v14_2_PermanentBuffRespawnPatch.md`
- `Docs/Design/ProjectileSystem_Design_v19.md`
- `Docs/Design/AttackSystem_Design_v6_2.md`
- `Docs/Design/CrowdControlSystem_Design_v6_2.md`
- `Docs/Design/moba_combat_system_design_v13_2.md`
- `Docs/Design/PhysicsSystem_Design_v13_1.md`
- `Docs/Design/PresentationSystem_Design_v13_2.md`
- `E:/EgdeDownLoad/暗裔剑魔·亚托克斯 英雄设计案.md`

The current user request explicitly authorizes this production hero content.
No older archived design is an implementation source.

## Design audit and unresolved authoring values

- Ability v15.2 section 7.8 explicitly anticipates a three-stage recast model.
  The implementation will use a neutrally named reusable cast model rather
  than a hero-named core type.
- Physics v13.1 has no public trapezoid shape contract. W and Q2 therefore use
  deterministic exact narrow-phase geometry after a supported broad-phase
  query; the Physics public shape schema is not expanded.
- The supplied hero document conflicts between its W target rules: section
  4.2 says only enemy heroes form a tether, while section 4.3 says enemy heroes
  or large monsters. The user resolved this on 2026-08-11: only enemy heroes
  form a tether; large monsters are intentionally deferred.
- The supplied appendix omits Q3 wind-up, R fear radius/duration, and W pull
  duration. The user resolved these as Q3 `0.3s`, R fear `200` authored-distance
  radius for `1.5s`, and W pull-to-centre over `0.3s`. Pull speed is derived
  deterministically from the displacement and duration, not separately authored.

## Contract decisions

- Proposed stable IDs: hero prototype `1002`, runtime prefab `1102`, passive
  `10020`, Q/W/E/R `10021`-`10024`, W projectile definition `109`.
- Gameplay calculations use project `fp`; authored distances use the existing
  stat-distance conversion path.
- Q broad-phase candidates are sorted by stable UnitUid before exact overlap
  and Combat request emission.
- Structure is excluded from all Aatrox ability, equipment-skill and passive
  targeting. Ordinary attack targeting remains governed by Attack rules.
- E remains a separate ability session. It never mutates Q's locked direction;
  Q samples the caster position only when its impact stage executes.
- Runtime framework types remain content-neutral. Aatrox names are limited to
  formal content assets, display data, tests/fixtures and presentation profiles.
- A ready fixed passive exposes the generic
  `PassiveAbilityEffectDef.EmpowersBasicAttack` semantic. `AttackHandler`
  resolves it only at `BeginAttack` and locks `IsEmpoweredAttack` in the
  existing deterministic attack snapshot. Presentation reads that locked value;
  it does not infer an empowered attack from Buff/passive state. The generic
  passive target-eligibility hook keeps Aatrox's Structure exclusion aligned
  between damage, consumption and animation.
- `UnitAnimationProfile` maps an optional fixed-passive ready state and an
  optional form Buff to Animator parameters and alternate stage-state hashes.
  For Aatrox these are passive `10020` and World Ender Buff `12024`; the
  generic driver contains no Aatrox-specific ID or branch.
- Q1/Q2/Q3 and W tether authoring geometry is visualized by the
  `AatroxAbilityZoneAuthoringGizmo` editor component on the formal hero prefab.
  Q reads the actual Ability authoring; W reads the same
  `ProjectileContainmentZoneAuthoring` component baked from the area projectile
  prefab. The Gizmo uses selection-independent `OnDrawGizmos` rendering and
  supports user-controlled `Off`, `All` and `Single` modes; `Single` selects Q1/Q2/Q3/W,
  while `All` may draw the four zones separated or overlaid. It has no runtime
  output, snapshot or checksum participation.
- W's first missile is definition/prefab `109/2105`; the second-stage stationary
  area is definition/prefab `110/2106`. Both runtime prefabs are the supplied
  assets under `Assets/Resources/Prefab/Missle/`, with the Physics/authoring
  components added directly. No wrapper projectile or tether-only VFX prefab is
  retained under the unit-prefab directory.

## Implementation steps

- [x] Audit current designs, existing Varus formal content and imported Aatrox resources.
- [x] Add reusable sequential-recast and directional multi-zone stage support.
- [x] Add reusable passive/tether/refreshable-buff mechanisms and movement hooks.
- [x] Add deterministic unit tests and snapshot round-trip coverage. No new
  snapshot member was required; existing Ability session state owns stage and
  elapsed-Tick replay state.
- [x] Author Aatrox formal ability, buff, projectile, animation and hero assets.
- [x] Create the runtime prefab through Unity MCP and register all global tables.
- [x] Compile through Unity MCP and inspect all relevant Console output.
- [x] Run focused EditMode and PlayMode tests.
- [x] Review the diff against the designs and update module status/handoff.

## Verification evidence

- Unity MCP AssetDatabase refresh/script compilation: passed with no C#
  compilation errors. The MCP log-clear endpoint remains unable to delete its
  own locked cache file; the retained diagnostic error is from an earlier
  failed inspection query, not project compilation/runtime.
- `AttackHandlerTests`: 14/14 passed, including ready empowered-passive
  resolution, attack-snapshot locking, animation-snapshot projection and
  restore persistence.
- `AatroxFormalContentTests`: 8/8 focused EditMode tests passed. Coverage includes combined
  hero catalog Bake, stable registrations, exact Q geometries, Structure
  exclusion, W hero-only tether filter, rank-1 Q/W/E/Buff values after a Unity
  domain reload, stationary-area prefab Bake, exact trapezoid containment,
  pending-projectile cancellation and sequential-session snapshot round-trip.
- `ProjectileCombatPipelineTests.InfernalChainsHit_SpawnsStationaryContainmentProjectile`
  passed: a W missile hit applies the tether, queues area projectile `110`,
  captures it in the projectile snapshot, and cancels it when the target escapes.
- `AbilityInputMappingTests`: 16/16 passed, including Direction local-aim plus
  primary-click Commit and Q1/Q2/Q3 indicator-stage projection.
  `CooldownPipelineTests`: 6/6 passed, including the required KnockBack
  half-height presentation arc.
- `AatroxPrefabPlayModeTests`: 3/3 PlayMode passed. The formal hero prefab and
  stationary tether-area projectile instantiate and clean up with their
  required components; the real Animator routes passive-ready locomotion,
  World Ender locomotion and World Ender empowered attack states.
- `HeroTestSceneEquipmentPlayModeTests`: 2/2 passed after switching the
  standalone Tick-only fixture to prototype `1002`. The spawned Q/W/E/R
  definitions are `10021`-`10024`, and the existing shop/gold flow remains
  intact. A direct scene Play smoke check confirmed the same runtime IDs with
  zero Console errors.
- The pre-existing broad `StageDefBakeTests` class still reports its two known
  legacy Dash fixture failures (default authoring has no StageKey; a worldless
  Dash test expects Running). Neither failure is introduced by this slice.

## Delivered formal content

- Prototype/prefab/display: `1002` / `1102` / Aatrox display row.
- Passive/Q/W/E/R: `10020`-`10024`; combined global catalog preserves Varus.
- Buffs: W slow `12021`, W tether `12022`, World Ender `12024`.
- Projectiles: W missile `109/2105`; W stationary tether area `110/2106`.
- VFX: Q1/Q2/Q3 `3101`-`3103`; W's second stage is not a VFX entry.
- Resource ownership: `AatroxHeroRuntime.prefab` is the only Aatrox unit
  prefab under `Assets/Config/Formal/Prefabs/`. The supplied
  `AatroxSpellWMissle.prefab` and `InfernalChainsArea.prefab` are configured
  directly under `Assets/Resources/Prefab/Missle/`. The obsolete wrapper,
  tether VFX and `Unit_Hero_Aatrox.prefab` were deleted after reference checks.
- `AatroxAnimator.controller` contains the supplied locomotion/attack/ability
  clips plus `AatroxDeath.anim` in 25 states and 14
  state-derived parameters. Six mutually exclusive attack routes select
  Attack1/Attack2/Passive across normal and World Ender forms; six locomotion
  routes select Idle/Walk across normal, passive-ready and World Ender forms;
  all attack/cast states retain explicit completion exits. Death is routed by
  `LifeState`, and every living AnyState route is guarded against overriding
  death. The animation
  profile maps Q1/Q2/Q3/W/E/R stages to normal, passive-ready Dash and `_ULT`
  variants. The controller asset was recreated through Unity AssetDatabase so
  its embedded State/Transition objects remain valid after domain reload, and
  the formal hero prefab was rebound to the new controller GUID.
- All six Q clips (normal and `_ULT`) are retimed to exactly `1.0s`; their
  deterministic impact stages are `30` Ticks (`1.0s`) so authored damage and
  the presentation windup finish together.
- Crowd control: KnockUp `113`; R uses existing Fear `112`. A generic,
  presentation-only vertical-motion presenter applies duration-derived
  parabolic model Y motion; KnockBack uses half the KnockUp peak while keeping
  authoritative planar displacement unchanged.
- World Ender Buff `12024` controls four authored wing/banner bone roots through
  a generic presentation component. No Gameplay state depends on their transforms.
- HeroTestScene now spawns Aatrox (`1002`) while its target dummies remain
  prototype `1001`. The driver reads the combined formal hero Ability catalog;
  Q/W enter local aim on key press and Commit toward the cursor only on primary
  click, E commits its dash toward the cursor immediately, and R self-casts.
- Runtime Q aim outlines resolve the actual current sequential-recast stage and
  switch Q1 -> Q2 -> Q3 geometry without duplicating balance values. W uses the
  standard direction-bar indicator, matching Varus R.
- `AbilityLevelValue` is now Unity-serializable while remaining mutation-free
  through its public API. This is required because Buff definitions store
  fixed-point rank arrays directly inside managed-reference effects; the final
  assets were saved and verified again after a full domain reload.

## Out of scope

- Rebalancing Varus or existing formal equipment.
- Replacing the project's generic targeting/input protocols.
- Network protocol changes not required by this hero's deterministic state.
- C/S or UOS packaging unless separately requested.
- W tethering of large monsters, per the user's explicit deferral.

## 2026-08-12 UI, dash-facing and VFX-direction follow-up

- HeroTest now projects `FixedPassive.NextReadyLogicTick` and the level-scaled
  passive cooldown to the existing passive HUD mask/text bridge.
- Sequential-recast waiting windows use the next impact's authored icon, so
  Q1 completion exposes Q2 and Q2 completion exposes Q3 immediately.
- Dash translation preserves an already active movement-locking cast's facing;
  Aatrox E can reposition during Q without rotating the unit or changing Q's
  locked direction.
- Attached directional VFX follow unit position while maintaining
  `VfxEvent.WorldDirection`, preventing host rotation from rotating Q VFX twice.
- Focused verification: `AatroxFormalContentTests` 8/8,
  `MovementHandlerTests` 19/19, `HeroTestSceneEquipmentPlayModeTests` 2/2 and
  `AatroxPrefabPlayModeTests` 4/4. Unity MCP compilation and the final project
  Console check passed with zero errors. No package was built.

## 2026-08-12 W timing and full-model outline follow-up

- Aatrox W's deterministic Commit stage is now 14 Ticks (0.4667 seconds).
- `AatroxSpellW.anim` and `AatroxSpellW_ULT.anim` are both retimed to exactly
  14/30 seconds, and the projectile spawn delay uses the same 14 Ticks.
- `ClientUnitOutline` now assigns its inverted-hull material once per source
  submesh. This fixes the Aatrox model's five-material mesh previously drawing
  only the first Wings submesh while retaining one shared material instance.
- Focused verification: `AatroxFormalContentTests` 8/8 and
  `AatroxPrefabPlayModeTests` 5/5. The new PlayMode case instantiates the formal
  Aatrox prefab and proves the outline renderer has one outline material slot
  for every source submesh.

## 2026-08-13 locomotion graph follow-up

- The current unit-prefab root is `Assets/Resources/Prefab/Unit/`; all eight
  hero, minion and tower prefabs there were audited. Varus and minions already
  use direct Idle/Move transitions, and towers have no movement states.
- Aatrox's previous anti-reentry edit left only normal Idle able to enter a walk
  state. Passive-ready/World Ender Idle and all Walk variants were dead ends, so
  the common passive-ready pose could not start its movement animation.
- `AatroxAnimator.controller` now has a complete direct graph among normal,
  passive-ready and World Ender Idle/Walk variants. Movement is never entered
  from AnyState, every locomotion state has five non-self exits, and the
  persistent `IsMoving` condition therefore cannot restart a looping walk clip.
- Regression coverage now scans every current unit prefab for AnyState entry
  into a looping movement clip and verifies direct start/stop movement routes.
  A PlayMode test also proves Aatrox walk normalized time advances and that
  passive/World Ender variants switch while moving.
- Verification: `AatroxFormalContentTests` 10/10,
  `UnitPrefabAnimatorTopologyTests` 1/1 and `AatroxPrefabPlayModeTests` 6/6.
  Unity compilation and the final Console error check passed. No package was built.

## 2026-08-13 exact timing and camera follow-up

- Q1/Q2/Q3 impact-stage durations and `impactDelayTicks` now equal their 1.0s
  clips at 30 Ticks.
- W Commit, projectile spawn and both normal/World-Ender clips now equal 14
  Ticks (14/30s); 0.45s was rounded up because it is not representable at the
  deterministic 30 Hz rate.
- Right-click and diagnostic movement were verified to share the same Move
  Command path. The remaining screen-space shake was caused by the locked
  camera and the unit root sampling different render timelines.
- `CameraController` now executes after `PhysicsEntity2D.LateUpdate` and exactly
  follows Gameplay targets. Non-Gameplay camera-debug rigs keep damping.
- Verification: Unity MCP compilation/Console passed with zero errors;
  `AatroxFormalContentTests` 10/10, `UnitPrefabAnimatorTopologyTests` 1/1,
  `CameraControllerPlayModeTests` 1/1, `AatroxPrefabPlayModeTests` 6/6 and
  `PlayerInputSimulationPlayModeTests` 3/3 passed. No package was built.

## 2026-08-13 passive/World Ender presentation follow-up

- Passive-ready locomotion transitions now blend for 0.18 seconds instead of
  snapping between normal and empowered poses; empowered attack entry blends
  for 0.10 seconds.
- The generic animation profile/driver exposes an optional
  `AnimationVariantExit` trigger. Aatrox consumes it to play the supplied
  `AatroxULTOut.anim` when World Ender Buff `12024` ends, then returns to the
  correct moving/idle and passive-ready variant.
- `ClientUnitOutline` now bakes after `BuffDrivenBoneVisibility`; hidden wing
  bones therefore stay absent from both the model and generated outline.
- Verification: `AatroxFormalContentTests` 10/10 and
  `AatroxPrefabPlayModeTests` 8/8. No package was built.
