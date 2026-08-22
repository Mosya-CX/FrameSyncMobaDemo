using System.Collections;
using System.Collections.Generic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    /// <summary>
    /// Real Input System simulation (InputTestFixture): input actions -> local
    /// event buffer -> template-driven requester -> canonical Commands, plus
    /// UI pointer blocking. Strict expectations with debug logs.
    /// </summary>
    public sealed class PlayerInputSimulationPlayModeTests :
        InputTestFixture
    {
        private GameObject cameraObject;
        private Camera camera;
        private PlayerInputController controller;
        private LocalInputEventBuffer buffer;
        private PlayerCommandRequester requester;
        private CommandCollector collector;
        private MouseWorldResolver resolver;
        private readonly List<GameObject> created =
            new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            if (Keyboard.current == null)
                InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null)
                InputSystem.AddDevice<Mouse>();
        }

        [TearDown]
        public void TearDownCreatedObjects()
        {
            for (int i = 0; i < created.Count; i++)
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private static InputActionAsset CreateActionsAsset()
        {
            var asset =
                ScriptableObject.CreateInstance<
                    InputActionAsset>();

            var gameplay =
                new InputActionMap("Gameplay");
            gameplay.AddAction(
                "PointerPosition",
                InputActionType.Value,
                "<Pointer>/position");
            gameplay.AddAction(
                "PrimaryClick",
                InputActionType.Button,
                "<Mouse>/leftButton");
            gameplay.AddAction(
                "SecondaryClick",
                InputActionType.Button,
                "<Mouse>/rightButton");
            gameplay.AddAction(
                "Cancel",
                InputActionType.Button,
                "<Keyboard>/escape");
            gameplay.AddAction(
                "AbilityQ",
                InputActionType.Button,
                "<Keyboard>/q");
            gameplay.AddAction(
                "AbilityW",
                InputActionType.Button,
                "<Keyboard>/w");
            gameplay.AddAction(
                "AbilityE",
                InputActionType.Button,
                "<Keyboard>/e");
            gameplay.AddAction(
                "AbilityR",
                InputActionType.Button,
                "<Keyboard>/r");
            gameplay.AddAction(
                "ExpandStats",
                InputActionType.Button,
                "<Keyboard>/c");
            asset.AddActionMap(gameplay);

            var ui = new InputActionMap("UI");
            ui.AddAction(
                "Point",
                InputActionType.Value,
                "<Pointer>/position");
            ui.AddAction(
                "Move",
                InputActionType.Value,
                "<Keyboard>/w");
            ui.AddAction(
                "Submit",
                InputActionType.Button,
                "<Keyboard>/enter");
            ui.AddAction(
                "Cancel",
                InputActionType.Button,
                "<Keyboard>/escape");
            ui.AddAction(
                "LeftClick",
                InputActionType.Button,
                "<Mouse>/leftButton");
            ui.AddAction(
                "MiddleClick",
                InputActionType.Button,
                "<Mouse>/middleButton");
            ui.AddAction(
                "RightClick",
                InputActionType.Button,
                "<Mouse>/rightButton");
            ui.AddAction(
                "ScrollWheel",
                InputActionType.Value,
                "<Mouse>/scroll");
            ui.AddAction(
                "TrackedDevicePosition",
                InputActionType.Value);
            ui.AddAction(
                "TrackedDeviceOrientation",
                InputActionType.Value);
            asset.AddActionMap(ui);
            return asset;
        }

        private static InputSystemUIInputModule
            CreateUiModule(
                InputActionAsset asset)
        {
            var go = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            var module =
                go.GetComponent<InputSystemUIInputModule>();
            InputActionMap ui =
                asset.FindActionMap("UI", true);
            module.actionsAsset = asset;
            module.point = InputActionReference.Create(
                ui.FindAction("Point", true));
            module.move = InputActionReference.Create(
                ui.FindAction("Move", true));
            module.submit = InputActionReference.Create(
                ui.FindAction("Submit", true));
            module.cancel = InputActionReference.Create(
                ui.FindAction("Cancel", true));
            module.leftClick =
                InputActionReference.Create(
                    ui.FindAction("LeftClick", true));
            module.middleClick =
                InputActionReference.Create(
                    ui.FindAction("MiddleClick", true));
            module.rightClick =
                InputActionReference.Create(
                    ui.FindAction("RightClick", true));
            module.scrollWheel =
                InputActionReference.Create(
                    ui.FindAction("ScrollWheel", true));
            return module;
        }

        private static GameObject CreateBlockingUi()
        {
            var canvasGo = new GameObject(
                "BlockingCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas =
                canvasGo.GetComponent<Canvas>();
            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;
            var imageGo = new GameObject(
                "BlockingImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageGo.transform.SetParent(
                canvasGo.transform,
                false);
            var rect =
                imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image =
                imageGo.GetComponent<Image>();
            image.raycastTarget = true;
            image.color = new Color(1f, 0f, 0f, 0.5f);
            return canvasGo;
        }

        private void BuildController(
            IPlayerAbilityInputProfileProvider
                provider)
        {
            cameraObject =
                new GameObject(
                    "TestCamera",
                    typeof(Camera));
            camera =
                cameraObject.GetComponent<Camera>();
            cameraObject.transform.position =
                new Vector3(0f, 10f, 0f);
            cameraObject.transform.rotation =
                Quaternion.Euler(90f, 0f, 0f);

            UnitType unit = UnitTestFactory.CreateUnit(
                new UnitUid(20, 4, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));

            collector = new CommandCollector();
            buffer = new LocalInputEventBuffer();
            resolver =
                new MouseWorldResolver(
                    camera,
                    fp.zero,
                    null);
            requester = new PlayerCommandRequester(
                unit,
                new GameplayInputGate(),
                collector,
                2,
                77,
                new CommandTargetTickResolver(
                    () => 12,
                    () => 13,
                    2,
                    12),
                provider);

            InputActionAsset asset =
                CreateActionsAsset();
            var controllerGo = new GameObject(
                "PlayerInputController",
                typeof(PlayerInputController));
            controller =
                controllerGo
                    .GetComponent<PlayerInputController>();
            typeof(PlayerInputController)
                .GetField(
                    "inputActions",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .SetValue(controller, asset);
            controller.Initialize(
                buffer,
                resolver,
                requester);
        }

        [UnityTest]
        public IEnumerator
            HoldReleaseDefault_SimulatedPressFocus_ReleaseNoCommit_LeftClickCommitsOnce()
        {
            BuildController(
                new HoldReleaseTestProvider());
            Set(
                Mouse.current.position,
                new Vector2(960f, 540f));
            yield return null;
            yield return null;

            // Press Q: process the queued state event synchronously, then
            // drain the buffer deterministically.
            Press(Keyboard.current.qKey);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;
            Debug.Log(
                $"[InputSim] after Q press buffer={buffer.Count} commands={collector.GetCanonicalCommands().Count} state={requester.GetAbilityState(0).Kind}");
            Assert.That(
                collector.GetCanonicalCommands(),
                Has.Count.EqualTo(1),
                "Q press must create one Focus command.");
            Assert.That(
                collector.GetCanonicalCommands()[0]
                    .AbilityVerb,
                Is.EqualTo(AbilitySignalVerb.Focus));

            // Release Q: default template binds release to None.
            Release(Keyboard.current.qKey);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;
            Debug.Log(
                $"[InputSim] after Q release commands={collector.GetCanonicalCommands().Count}");
            Assert.That(
                collector.GetCanonicalCommands(),
                Has.Count.EqualTo(1),
                "Release must not Commit under the default template.");
            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind
                        .FocusRequested));

            // Left click Commits.
            Press(Mouse.current.leftButton);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;
            Debug.Log(
                $"[InputSim] after left click commands={collector.GetCanonicalCommands().Count}");
            Assert.That(
                collector.GetCanonicalCommands(),
                Has.Count.EqualTo(2),
                "Left click must Commit.");
            Assert.That(
                collector.GetCanonicalCommands()[1]
                    .AbilityVerb,
                Is.EqualTo(AbilitySignalVerb.Commit));
            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind
                        .CommitRequested));

            // Duplicate left click after CommitRequested must be suppressed.
            Release(Mouse.current.leftButton);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;
            Press(Mouse.current.leftButton);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;
            Assert.That(
                collector.GetCanonicalCommands(),
                Has.Count.EqualTo(2),
                "Duplicate left click must be suppressed.");

            Release(Mouse.current.leftButton);
            InputSystem.Update();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator
            UiPointerBlocking_SimulatedClicks_ProduceNoWorldCommands()
        {
            InputActionAsset asset =
                CreateActionsAsset();
            CreateUiModule(asset);
            GameObject canvas = CreateBlockingUi();
            BuildController(
                new HoldReleaseTestProvider());
            Set(
                Mouse.current.position,
                new Vector2(960f, 540f));
            yield return null;
            yield return null;

            Press(Mouse.current.leftButton);
            yield return null;
            yield return null;
            Press(Mouse.current.rightButton);
            yield return null;
            yield return null;

            Debug.Log(
                $"[InputSim] UI blocking: buffer={buffer.Count} commands={collector.GetCanonicalCommands().Count}");
            Assert.That(
                buffer.Count,
                Is.Zero,
                "Clicks over blocking UI must be dropped at the source.");
            Assert.That(
                collector.GetCanonicalCommands(),
                Is.Empty,
                "Clicks over blocking UI must not generate world Commands.");
            Object.Destroy(canvas);
        }

        [UnityTest]
        public IEnumerator
            LocalAimDefault_SimulatedPressAimOnly_LeftCommit_RightClosesAim()
        {
            BuildController(
                new LocalAimTestProvider());
            Set(
                Mouse.current.position,
                new Vector2(960f, 540f));
            yield return null;
            yield return null;

            Press(Keyboard.current.qKey);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;
            Debug.Log(
                $"[InputSim] local aim press: state={requester.GetAbilityState(0).Kind} commands={collector.GetCanonicalCommands().Count}");
            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind
                        .LocalAiming));
            Assert.That(
                collector.GetCanonicalCommands(),
                Is.Empty,
                "LocalAim press must not create a Command.");

            // Right click closes local aim only.
            Press(Mouse.current.rightButton);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;
            Assert.That(
                requester.GetAbilityState(0).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind.Idle),
                "Right click must close local aim.");
            Assert.That(
                collector.GetCanonicalCommands(),
                Is.Empty,
                "Right click must not generate Cancel/Move/Attack.");
            Release(Mouse.current.rightButton);
            InputSystem.Update();
            yield return null;
            yield return null;

            // Release Q, then re-press (button transition) and left click.
            Release(Keyboard.current.qKey);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;
            Press(Keyboard.current.qKey);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;
            Press(Mouse.current.leftButton);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;
            Debug.Log(
                $"[InputSim] local aim commit: commands={collector.GetCanonicalCommands().Count}");
            Assert.That(
                collector.GetCanonicalCommands(),
                Has.Count.EqualTo(1));
            Assert.That(
                collector.GetCanonicalCommands()[0]
                    .AbilityVerb,
                Is.EqualTo(AbilitySignalVerb.Commit));

            Release(Keyboard.current.qKey);
            Release(Mouse.current.leftButton);
            InputSystem.Update();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator
            ToggleNoAim_SimulatedWPressCommitsImmediately()
        {
            BuildController(
                new ToggleTestProvider());
            Set(
                Mouse.current.position,
                new Vector2(960f, 540f));
            yield return null;
            yield return null;

            Press(Keyboard.current.wKey);
            InputSystem.Update();
            requester.ProcessFrame(buffer, resolver);
            yield return null;
            yield return null;

            IReadOnlyList<GameplayCommand> commands =
                collector.GetCanonicalCommands();
            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(commands[0].AbilitySlot, Is.EqualTo(1));
            Assert.That(
                commands[0].AbilityVerb,
                Is.EqualTo(AbilitySignalVerb.Commit));
            Assert.That(
                requester.GetAbilityState(1).Kind,
                Is.EqualTo(
                    LocalAbilityInputStateKind.CommitRequested),
                "A no-aim Toggle must not enter LocalAiming or wait for a click.");

            Release(Keyboard.current.wKey);
            InputSystem.Update();
            yield return null;
            yield return null;
        }

        private sealed class HoldReleaseTestProvider :
            IPlayerAbilityInputProfileProvider
        {
            public bool TryGetTemplate(
                byte slot,
                out InputMappingTemplate template)
            {
                template =
                    AbilityInputMapping
                        .BuildHoldReleaseDefault();
                return true;
            }

            public bool TryGetAimKind(
                byte slot,
                out AimKind aimKind)
            {
                aimKind = AimKind.Point;
                return true;
            }
        }

        private sealed class LocalAimTestProvider :
            IPlayerAbilityInputProfileProvider
        {
            public bool TryGetTemplate(
                byte slot,
                out InputMappingTemplate template)
            {
                template =
                    AbilityInputMapping
                        .BuildLocalAimDefault();
                return true;
            }

            public bool TryGetAimKind(
                byte slot,
                out AimKind aimKind)
            {
                aimKind = AimKind.Point;
                return true;
            }
        }

        private sealed class ToggleTestProvider :
            IPlayerAbilityInputProfileProvider
        {
            public bool TryGetTemplate(
                byte slot,
                out InputMappingTemplate template)
            {
                template = AbilityInputMapping.BuildDefault(
                    new ToggleCastModelDef(),
                    AimKind.None);
                return true;
            }

            public bool TryGetAimKind(
                byte slot,
                out AimKind aimKind)
            {
                aimKind = AimKind.None;
                return true;
            }
        }
    }
}
