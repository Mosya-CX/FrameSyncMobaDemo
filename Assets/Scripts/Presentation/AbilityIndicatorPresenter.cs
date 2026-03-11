using UnityEngine;

public sealed class AbilityIndicatorPresenter
{
    private readonly HeroUnit owner;

    private AbilityRuntime runtime;
    private LocalCastSession session;
    private AbilityPreviewContext previewContext;
    private AbilityIndicatorBase indicator;
    private bool isShowing;

    public AbilityIndicatorPresenter(HeroUnit owner)
    {
        this.owner = owner;
    }

    public void Show(AbilityRuntime runtime, LocalCastSession session)
    {
        Hide();

        if (runtime == null || session == null || runtime.Data.Indicator == null)
            return;

        this.runtime = runtime;
        this.session = session;
        indicator = runtime.Data.Indicator;

        previewContext = new AbilityPreviewContext
        {
            Caster = owner,
            Runtime = runtime,
            Session = session,
        };

        indicator.OnCreate();
        indicator.OnShow(previewContext);
        isShowing = true;
    }

    public void Update(float deltaTime)
    {
        if (!isShowing || indicator == null || runtime == null || session == null)
            return;

        var result = ResolvePreview(runtime, session);
        indicator.OnUpdate(previewContext, result, deltaTime);
    }

    public void Hide()
    {
        if (isShowing && indicator != null)
            indicator.OnHide();

        runtime = null;
        session = null;
        previewContext = null;
        indicator = null;
        isShowing = false;
    }

    private AbilityPreviewResult ResolvePreview(AbilityRuntime runtime, LocalCastSession session)
    {
        var result = new AbilityPreviewResult();

        switch (runtime.Data.TargetMode)
        {
            case AbilityTargetMode.None:
                result.Validity = AbilityPreviewValidity.Valid;
                result.PreviewPosition = owner.LogicPosition;
                break;

            case AbilityTargetMode.Unit:
                if (session.Aim.SelectedUnit == null)
                {
                    result.Validity = AbilityPreviewValidity.Invalid;
                    break;
                }

                result.PreviewTarget = session.Aim.SelectedUnit;
                result.PreviewPosition = session.Aim.SelectedUnit.LogicPosition;
                result.Validity = IsInCastRange(session.Aim.SelectedUnit.LogicPosition, runtime)
                    ? AbilityPreviewValidity.Valid
                    : (runtime.Data.AllowAutoApproach ? AbilityPreviewValidity.NeedApproach : AbilityPreviewValidity.Invalid);
                break;

            case AbilityTargetMode.Point:
            case AbilityTargetMode.PointOrUnit:
            case AbilityTargetMode.Direction:
                if (!session.Aim.TargetPosition.HasValue)
                {
                    result.Validity = AbilityPreviewValidity.Invalid;
                    break;
                }

                result.PreviewPosition = session.Aim.TargetPosition;
                result.Validity = IsInCastRange(session.Aim.TargetPosition.Value, runtime)
                    ? AbilityPreviewValidity.Valid
                    : (runtime.Data.AllowAutoApproach ? AbilityPreviewValidity.NeedApproach : AbilityPreviewValidity.Invalid);

                if (runtime.Data.TargetMode == AbilityTargetMode.PointOrUnit && session.Aim.SelectedUnit != null)
                    result.PreviewTarget = session.Aim.SelectedUnit;
                break;
        }

        return result;
    }

    private bool IsInCastRange(Unity.Mathematics.FixedPoint.fp3 targetPos, AbilityRuntime runtime)
    {
        var range = (Unity.Mathematics.FixedPoint.fp)runtime.Data.CastRange;
        var delta = targetPos - owner.LogicPosition;
        return Unity.Mathematics.FixedPoint.fpmath.lengthsq(delta) <= range * range;
    }
}