using Unity.Mathematics.FixedPoint;
using UnityEngine;

public sealed class LocalController : MonoBehaviour
{
    public static LocalController Local { get; private set; }

    [SerializeField]
    private HeroUnit localHero;

    public HeroUnit LocalHero => localHero;

    private void Awake()
    {
        Local = this;
    }

    public void SetLocalHero(HeroUnit hero)
    {
        if (localHero == hero)
            return;

        ClearBindings();
        localHero = hero;
        ApplyBindings(localHero, true);
    }

    private void Update()
    {
        if (localHero == null || localHero.IsDead)
            return;

        if (Input.GetMouseButtonDown(1))
            HandleRightClick();
    }

    private void HandleRightClick()
    {
        var camera = Camera.main;
        if (camera == null)
            return;

        var ray = camera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out var hit, 1000f))
        {
            var unit = hit.collider != null ? hit.collider.GetComponentInParent<UnitCore>() : null;
            if (unit != null && unit.TeamID != localHero.TeamID && !unit.IsDead)
            {
                localHero.IssueAttackOrder(unit);
                return;
            }

            var point = hit.point;
            localHero.IssueMoveOrder(new fp3((fp)point.x, (fp)point.y, (fp)point.z));
        }
    }

    public void BindHero(HeroUnit hero)
    {
        if (hero == null)
            return;

        ClearBindings();
        localHero = hero;
        ApplyBindings(localHero, true);
    }

    public void UnbindHero(HeroUnit hero)
    {
        if (hero == null || localHero != hero)
            return;

        ApplyBindings(localHero, false);
        localHero = null;
    }

    public void ClearBindings()
    {
        if (localHero != null)
            ApplyBindings(localHero, false);

        localHero = null;
    }

    private static void ApplyBindings(HeroUnit hero, bool enabled)
    {
        if (hero == null)
            return;

        var input = hero.GetComponent<PlayerSkillInputController>();
        if (input != null)
            input.enabled = enabled;

        var indicator = hero.GetComponent<SkillIndicatorDriver>();
        if (indicator != null)
        {
            if (!enabled)
                indicator.HideCurrent();

            indicator.enabled = enabled;
        }
    }

    public static void TryCreate()
    {
        if (Local != null)
            return;

        var existing = FindFirstObjectByType<LocalController>();
        if (existing != null)
        {
            Local = existing;
            return;
        }

        var go = new GameObject(nameof(LocalController));
        Local = go.AddComponent<LocalController>();
    }
}
