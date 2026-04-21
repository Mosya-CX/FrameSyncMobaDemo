using Unity.Mathematics.FixedPoint;
using UnityEngine;
using Sirenix.OdinInspector;

[RequireComponent(typeof(HeroUnit))]
[RequireComponent(typeof(SkillGroupController))]
public sealed class PlayerSkillInputController : MonoBehaviour
{
    [TitleGroup("输入"), SerializeField, LabelText("地面检测层")]
    private LayerMask groundMask = ~0;

    [TitleGroup("输入"), SerializeField, LabelText("智能施法")]
    private bool smartCast = false;

    [TitleGroup("输入"), SerializeField, LabelText("输入配置")]
    private PlayerSkillInputProfile inputProfile;

    private Camera targetCamera;
    private HeroUnit owner;
    private SkillGroupController skillGroupController;
    private SkillIndicatorDriver indicatorDriver;

    private readonly LocalSkillCastSession session = new();

    public HeroUnit Owner => owner;
    public LocalSkillCastSession Session => session;
    public bool SmartCast => smartCast;
    public PlayerSkillInputProfile InputProfile => inputProfile;

    private void Awake()
    {
        owner = GetComponent<HeroUnit>();
        skillGroupController = GetComponent<SkillGroupController>();
        indicatorDriver = GetComponent<SkillIndicatorDriver>();
        targetCamera = Camera.main;

        if (skillGroupController != null)
            skillGroupController.CurrentGroupChanged += OnCurrentGroupChanged;
    }

    private void OnDestroy()
    {
        if (skillGroupController != null)
            skillGroupController.CurrentGroupChanged -= OnCurrentGroupChanged;
    }

    private void Update()
    {
        if (owner == null || owner.IsDead)
        {
            CancelCurrentSession();
            return;
        }

        if (TryHandleGroupSwitchInput())
            return;

        if (TryHandleBeginCastInput())
            return;

        if (session.IsPreviewing)
        {
            UpdateSessionHover();
            indicatorDriver?.UpdateFromSession(session);

            if (Input.GetMouseButtonDown(0))
            {
                ConfirmCurrentSession();
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                CancelCurrentSession();
                return;
            }
        }
    }

    private bool TryHandleGroupSwitchInput()
    {
        if (skillGroupController == null || inputProfile == null)
            return false;

        if (inputProfile.GroupBindings != null)
        {
            for (int i = 0; i < inputProfile.GroupBindings.Length; i++)
            {
                var binding = inputProfile.GroupBindings[i];
                if (binding.Key == KeyCode.None)
                    continue;

                if (Input.GetKeyDown(binding.Key))
                {
                    skillGroupController.SwitchToGroup(binding.GroupIndex);
                    return true;
                }
            }
        }

        if (inputProfile.NextGroupKey != KeyCode.None && Input.GetKeyDown(inputProfile.NextGroupKey))
        {
            skillGroupController.SwitchNextGroup();
            return true;
        }

        if (inputProfile.PreviousGroupKey != KeyCode.None && Input.GetKeyDown(inputProfile.PreviousGroupKey))
        {
            skillGroupController.SwitchPreviousGroup();
            return true;
        }

        return false;
    }

    private bool TryHandleBeginCastInput()
    {
        if (inputProfile == null || inputProfile.SlotBindings == null)
            return false;

        for (int i = 0; i < inputProfile.SlotBindings.Length; i++)
        {
            var binding = inputProfile.SlotBindings[i];
            if (binding.Key == KeyCode.None)
                continue;

            if (Input.GetKeyDown(binding.Key))
                return TryUseSlot(binding.Slot);
        }

        return false;
    }

    public bool TryUseSlot(SkillSlot slot)
    {
        if (skillGroupController == null)
            return false;

        if (!skillGroupController.TryGetSkillAtSlot(slot, out var skill) || skill == null)
            return false;

        if (skill.IsPassive)
            return false;

        switch (skill.TargetMode)
        {
            case SkillTargetMode.None:
                return SubmitImmediateNoTarget(skill);

            case SkillTargetMode.Unit:
            case SkillTargetMode.Point:
            case SkillTargetMode.Direction:
            case SkillTargetMode.PointOrUnit:
                if (smartCast)
                    return TrySmartCast(skill, slot);

                BeginPreview(skill, slot);
                return true;
        }

        return false;
    }

    private bool SubmitImmediateNoTarget(SkillDef skill)
    {
        var request = new SkillCastRequest
        {
            CasterUid = owner.UnitID,
            SkillId = skill.Id,
            Source = SkillRequestSource.Player,
            IsPreview = false,
            SmartCast = smartCast,
            RequestTick = UnitManager.Instance.CurrentTick,
        };

        return SkillCommandResolver.TrySubmit(owner, request);
    }

    private bool TrySmartCast(SkillDef skill, SkillSlot slot)
    {
        UpdateSessionForSkill(skill, slot);

        var request = BuildRequestFromSession(skill);
        if (!request.HasValue)
            return false;

        return SkillCommandResolver.TrySubmit(owner, request.Value);
    }

    private void BeginPreview(SkillDef skill, SkillSlot slot)
    {
        UpdateSessionForSkill(skill, slot);
        session.State = LocalSkillCastSessionState.Preview;
        session.WaitingForConfirm = true;
        indicatorDriver?.Show(session);
    }

    private void UpdateSessionForSkill(SkillDef skill, SkillSlot slot)
    {
        session.Clear();
        session.Caster = owner;
        session.Skill = skill;
        session.Slot = slot;
        session.GroupIndex = skillGroupController != null ? skillGroupController.CurrentGroupIndex : 0;
        session.State = LocalSkillCastSessionState.Preview;
        session.WaitingForConfirm = true;

        UpdateSessionHover();
    }

    private void UpdateSessionHover()
    {
        session.HoveredUnit = null;
        session.HoveredPoint = null;
        session.AimDirection = null;
        session.IsValid = false;

        if (targetCamera == null || session.Skill == null || owner == null)
            return;

        var ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 1000f, groundMask))
            return;

        var point = hit.point;
        session.HoveredPoint = new fp3((fp)point.x, (fp)point.y, (fp)point.z);

        var hitUnit = hit.collider != null ? hit.collider.GetComponentInParent<UnitCore>() : null;
        session.HoveredUnit = hitUnit;

        var dir = session.HoveredPoint.Value - owner.LogicPosition;
        if (fpmath.lengthsq(dir) > fp.zero)
            session.AimDirection = fpmath.normalize(dir);

        session.IsValid = ValidateSessionTarget();
    }

    private bool ValidateSessionTarget()
    {
        if (session.Skill == null)
            return false;

        switch (session.Skill.TargetMode)
        {
            case SkillTargetMode.None:
                return true;
            case SkillTargetMode.Unit:
                return session.HoveredUnit != null && !session.HoveredUnit.IsDead;
            case SkillTargetMode.Point:
                return session.HoveredPoint.HasValue;
            case SkillTargetMode.Direction:
                return session.AimDirection.HasValue;
            case SkillTargetMode.PointOrUnit:
                return session.HoveredUnit != null || session.HoveredPoint.HasValue;
        }

        return false;
    }

    private void ConfirmCurrentSession()
    {
        if (!session.IsPreviewing || session.Skill == null || !session.IsValid)
            return;

        var request = BuildRequestFromSession(session.Skill);
        if (!request.HasValue)
            return;

        SkillCommandResolver.TrySubmit(owner, request.Value);
        EndSession();
    }

    private void CancelCurrentSession()
    {
        EndSession();
    }

    private void EndSession()
    {
        indicatorDriver?.Hide(session);
        session.Clear();
    }

    private void OnCurrentGroupChanged(int previousGroup, int currentGroup)
    {
        if (session.IsPreviewing && session.GroupIndex != currentGroup)
            CancelCurrentSession();
    }

    private SkillCastRequest? BuildRequestFromSession(SkillDef skill)
    {
        if (owner == null || skill == null)
            return null;

        var request = new SkillCastRequest
        {
            CasterUid = owner.UnitID,
            SkillId = skill.Id,
            Source = SkillRequestSource.Player,
            IsPreview = false,
            SmartCast = smartCast,
            RequestTick = UnitManager.Instance.CurrentTick,
        };

        switch (skill.TargetMode)
        {
            case SkillTargetMode.None:
                break;

            case SkillTargetMode.Unit:
                if (session.HoveredUnit == null)
                    return null;
                request.TargetUnitUid = session.HoveredUnit.UnitID;
                break;

            case SkillTargetMode.Point:
                if (!session.HoveredPoint.HasValue)
                    return null;
                request.TargetPoint = session.HoveredPoint;
                break;

            case SkillTargetMode.Direction:
                if (!session.AimDirection.HasValue)
                    return null;
                request.AimDirection = session.AimDirection;
                request.TargetPoint = session.HoveredPoint;
                break;

            case SkillTargetMode.PointOrUnit:
                if (session.HoveredUnit != null)
                    request.TargetUnitUid = session.HoveredUnit.UnitID;
                else if (session.HoveredPoint.HasValue)
                    request.TargetPoint = session.HoveredPoint;
                else
                    return null;
                break;
        }

        return request;
    }
}
