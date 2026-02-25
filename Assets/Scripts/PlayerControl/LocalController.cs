using System.Collections.Generic;
using UnityEngine;

public class LocalController : MonoBehaviour
{
    public static LocalController Local;

    private GamePlayer playerHandle;
    public GamePlayer PlayerHandle => playerHandle;

    private HeroUnit controlledHero;

    private Dictionary<KeyCode, int> keyAbilityMap = new()
    {
        { KeyCode.Q, 1 },
        { KeyCode.W, 2 },
        { KeyCode.E, 3 },
        { KeyCode.R, 4 }
    };

    // 记录每帧输入状态
    private struct InputState
    {
        public bool qPressed, wPressed, ePressed, rPressed;
        public bool qReleased, wReleased, eReleased, rReleased;
        public Vector3 mousePosition; // 用于技能释放位置
    }
    private InputState currentInput;

    private uint frameOffset = 3; // 可动态调整
    private Queue<ICommand> pendingOutgoingCommands = new();

    private void Awake()
    {
        if (Local != null && Local != this)
        {
            DestroyImmediate(gameObject);
            return;
        }
        Local = this;
    }

    public void Init(GamePlayer playerHandle, HeroUnit controlledHero)
    {
        this.playerHandle = playerHandle;
        this.controlledHero = controlledHero;
    }

    private void Update()
    {
        // 清除上一帧的释放标记
        currentInput.qReleased = false;
        currentInput.wReleased = false;
        currentInput.eReleased = false;
        currentInput.rReleased = false;

        // 检测按下
        if (Input.GetKeyDown(KeyCode.Q)) currentInput.qPressed = true;
        if (Input.GetKeyDown(KeyCode.W)) currentInput.wPressed = true;
        if (Input.GetKeyDown(KeyCode.E)) currentInput.ePressed = true;
        if (Input.GetKeyDown(KeyCode.R)) currentInput.rPressed = true;

        // 检测抬起
        if (Input.GetKeyUp(KeyCode.Q)) currentInput.qReleased = true;
        if (Input.GetKeyUp(KeyCode.W)) currentInput.wReleased = true;
        if (Input.GetKeyUp(KeyCode.E)) currentInput.eReleased = true;
        if (Input.GetKeyUp(KeyCode.R)) currentInput.rReleased = true;

        // 记录鼠标位置
        if (currentInput.qReleased || currentInput.wReleased || currentInput.eReleased || currentInput.rReleased)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f))
                currentInput.mousePosition = hit.point;
        }
    }

    // 在每次逻辑帧Tick时由GameFlowManager调用
    public void GenerateCommandsForTick(uint localTick)
    {
        if (controlledHero == null) return;

        uint targetTick = localTick + frameOffset;

        // 生成按下指令
        if (currentInput.qPressed)
        {
            var cmd = new AbilityCommand
            {
                CommandType = CommandType.AbilityPress,
                ControlledUnitId = controlledHero.UnitID,
                AbilityId = 1,
                TargetTick = targetTick
            };
            AddCommand(cmd);
            currentInput.qPressed = false;
        }
        if (currentInput.wPressed)
        {
            var cmd = new AbilityCommand
            {
                CommandType = CommandType.AbilityPress,
                ControlledUnitId = controlledHero.UnitID,
                AbilityId = 2,
                TargetTick = targetTick
            };
            AddCommand(cmd);
            currentInput.wPressed = false;
        }
        if (currentInput.ePressed)
        {
            var cmd = new AbilityCommand
            {
                CommandType = CommandType.AbilityPress,
                ControlledUnitId = controlledHero.UnitID,
                AbilityId = 1,
                TargetTick = targetTick
            };
            AddCommand(cmd);
            currentInput.ePressed = false;
        }
        if (currentInput.rPressed)
        {
            var cmd = new AbilityCommand
            {
                CommandType = CommandType.AbilityPress,
                ControlledUnitId = controlledHero.UnitID,
                AbilityId = 1,
                TargetTick = targetTick
            };
            AddCommand(cmd);
            currentInput.rPressed = false;
        }

        // 生成释放指令
        if (currentInput.qReleased)
        {
            var cmd = new AbilityCommand
            {
                CommandType = CommandType.AbilityRelease,
                ControlledUnitId = controlledHero.UnitID,
                AbilityId = 1,
                HasTargetPosition = true,
                TargetPosition = currentInput.mousePosition,
                TargetTick = targetTick
            };
            AddCommand(cmd);
            currentInput.qReleased = false;
        }
        if (currentInput.wReleased)
        {
            var cmd = new AbilityCommand
            {
                CommandType = CommandType.AbilityRelease,
                ControlledUnitId = controlledHero.UnitID,
                AbilityId = 1,
                HasTargetPosition = true,
                TargetPosition = currentInput.mousePosition,
                TargetTick = targetTick
            };
            AddCommand(cmd);
            currentInput.wReleased = false;
        }
        if (currentInput.eReleased)
        {
            var cmd = new AbilityCommand
            {
                CommandType = CommandType.AbilityRelease,
                ControlledUnitId = controlledHero.UnitID,
                AbilityId = 1,
                HasTargetPosition = true,
                TargetPosition = currentInput.mousePosition,
                TargetTick = targetTick
            };
            AddCommand(cmd);
            currentInput.eReleased = false;
        }
        if (currentInput.rReleased)
        {
            var cmd = new AbilityCommand
            {
                CommandType = CommandType.AbilityRelease,
                ControlledUnitId = controlledHero.UnitID,
                AbilityId = 1,
                HasTargetPosition = true,
                TargetPosition = currentInput.mousePosition,
                TargetTick = targetTick
            };
            AddCommand(cmd);
            currentInput.rReleased = false;
        }

        // 移动指令
    }

    private void AddCommand(ICommand cmd)
    {
        // 本地预测执行
        PredictionSystem.Instance.AddLocalCommand(cmd);

        // 加入待发送队列
        pendingOutgoingCommands.Enqueue(cmd);
    }

    // 由FrameSyncCoreSystem调用，获取待发送指令并清空
    public List<ICommand> FlushOutgoingCommands()
    {
        var list = new List<ICommand>(pendingOutgoingCommands);
        pendingOutgoingCommands.Clear();
        return list;
    }
}
