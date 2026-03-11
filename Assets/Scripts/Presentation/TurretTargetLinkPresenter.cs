using UnityEngine;

[RequireComponent(typeof(Turret))]
public class TurretTargetLinkPresenter : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform startAnchor;
    [SerializeField] private float targetHeightOffset = 1.0f;
    [SerializeField] private bool hideWhenNoTarget = true;

    private Turret turret;

    private void Awake()
    {
        turret = GetComponent<Turret>();

        if (lineRenderer == null)
            lineRenderer = GetComponentInChildren<LineRenderer>(true);

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (turret == null || lineRenderer == null)
            return;

        var target = turret.CurrentTarget;
        if (target == null || target.IsDead)
        {
            if (hideWhenNoTarget)
                lineRenderer.enabled = false;
            return;
        }

        Vector3 startPos = startAnchor != null
            ? startAnchor.position
            : transform.position;

        Vector3 endPos = new Vector3(
            (float)target.LogicPosition.x,
            (float)target.LogicPosition.y + targetHeightOffset,
            (float)target.LogicPosition.z);

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);
    }
}