using Unity.Mathematics.FixedPoint;

public sealed class DashMotor
{
    private readonly UnitCore owner;

    private bool isDashing;
    private fp timer;
    private fp3 startPos;
    private fp3 endPos;

    public bool IsDashing => isDashing;

    public DashMotor(UnitCore owner)
    {
        this.owner = owner;
    }

    public void StartDash(fp3 from, fp3 to, fp duration)
    {
        isDashing = true;
        timer = duration;
        startPos = from;
        endPos = to;
    }

    public void Tick(fp dt)
    {
        if (!isDashing)
            return;

        timer -= dt;
        if (timer <= 0)
        {
            owner.LogicPosition = endPos;
            isDashing = false;
            return;
        }

        var t = 1 - timer / (fp)1; // 这里只是示意，实际用 duration 缓存
        owner.LogicPosition = fpmath.lerp(startPos, endPos, t);
    }

    public void Cancel()
    {
        isDashing = false;
    }
}