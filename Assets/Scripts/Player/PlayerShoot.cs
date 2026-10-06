using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private Transform shootPoint;
    [SerializeField] private GunData currentGun;

    private PlayerStatus playerStatus;
    private float nextShootTime;

    void Start()
    {
        playerStatus = GetComponent<PlayerStatus>();
    }

    void Update()
    {
        if (playerStatus.IsDead)
            return;

        bool wantToShoot = false;   

        if (currentGun.FireMode == FireMode.SemiAuto)
        {
            wantToShoot = Mouse.current.leftButton.wasPressedThisFrame;
        }

        else if (currentGun.FireMode == FireMode.FullAuto)
        {
            wantToShoot = Mouse.current.leftButton.isPressed;
        }

        if (wantToShoot)
        {
            Shoot();
        }

        Debug.DrawRay(shootPoint.position,  shootPoint.forward * currentGun.Distance, Color.green);
    }

    void Shoot()
    {
        if (Time.time < nextShootTime) return;

        nextShootTime = Time.time + 1f / currentGun.DelaySecond;

        if(Physics.Raycast(shootPoint.position, shootPoint.forward, out RaycastHit hit, currentGun.Distance))
        {
            Debug.Log(hit.collider.gameObject.name);
        }
    }
}
