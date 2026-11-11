using UnityEngine;

/// <summary>Page2 컷씬 전용 총알. 물리 충돌이나 전투 피해를 처리하지 않는다.</summary>
public class Page2BulletProjectile : MonoBehaviour
{
    [SerializeField] private GameObject bulletVisual;
    [SerializeField] private LineRenderer bulletLine;
    [SerializeField, Min(1f)] private float speed = 26f;
    [SerializeField, Min(.01f)] private float lineLength = .8f;
    [SerializeField, Min(.001f)] private float lineWidth = .035f;
    private Vector3 targetPosition;
    private Vector3 direction;
    private Vector3 impactNormal;
    private ParticleSystem impactPrefab;
    private Transform effectParent;
    private Page2GunfireSequence owner;
    private bool isFlying;

    public Vector3 FlightDirection { get { return direction; } }

    public void Launch(Vector3 start, Vector3 target, Vector3 normal,
        ParticleSystem impact, Transform parent, Page2GunfireSequence sequence)
    {
        targetPosition = target;
        direction = (target - start).normalized;
        impactNormal = normal;
        impactPrefab = impact;
        effectParent = parent;
        owner = sequence;
        transform.SetPositionAndRotation(start, Quaternion.LookRotation(direction));
        bulletVisual.SetActive(true);
        bulletLine.positionCount = 2;
        bulletLine.useWorldSpace = true;
        bulletLine.alignment = LineAlignment.View;
        bulletLine.widthMultiplier = lineWidth;
        bulletLine.enabled = true;
        isFlying = true;
        UpdateLine();
    }

    private void Update()
    {
        if (!isFlying) return;
        // S06은 unscaled Timeline을 사용한다. 같은 시간 기준으로 총알도 이동한다.
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.unscaledDeltaTime);
        UpdateLine();
        if ((transform.position - targetPosition).sqrMagnitude < .000001f) Arrive();
    }

    private void UpdateLine()
    {
        bulletLine.SetPosition(0, transform.position - direction * lineLength);
        bulletLine.SetPosition(1, transform.position);
    }

    private void Arrive()
    {
        isFlying = false;
        bulletVisual.SetActive(false);
        bulletLine.enabled = false;
        if (impactPrefab != null)
        {
            ParticleSystem impact = Instantiate(impactPrefab, targetPosition,
                Quaternion.LookRotation(impactNormal), effectParent);
            impact.Play(true);
            Destroy(impact.gameObject, 2f);
        }
        if (owner != null) owner.NotifyImpact();
        Destroy(gameObject);
    }
}
