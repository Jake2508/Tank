using UnityEngine;

namespace Tank
{
    /// <summary>
    /// What firing looks like: a flash and sparks at the muzzle, a puff of smoke behind
    /// them, and the barrel riding back in its mount.
    ///
    /// The shot previously had no visible origin at all - the shell simply appeared in
    /// mid-air a little ahead of the tank, which is why it read as spawning rather than
    /// being fired. The flash is the piece that ties the round to the gun.
    ///
    /// Everything here is cosmetic and listens to TankController.Fired; nothing in this
    /// file affects where the shell goes.
    /// </summary>
    public class TankGunFx : MonoBehaviour
    {
        [SerializeField] TankController tank;

        [Tooltip("Barrel model. Slides back along its own local Z on firing.")]
        [SerializeField] Transform barrel;

        [Header("Effects")]
        [SerializeField] GameObject muzzleFlash;
        [SerializeField] GameObject muzzleSmoke;

        [Header("Recoil")]
        [Tooltip("How far the barrel rides back, in local units.")]
        [SerializeField] float recoilDistance = 0.32f;

        [Tooltip("Seconds to snap back and settle. Short: the gun reloads in half a " +
                 "second and a slow return would still be moving on the next shot.")]
        [SerializeField] float recoilReturn = 0.13f;

        Vector3 barrelHome;
        float recoil;

        void Awake()
        {
            if (tank == null) tank = GetComponentInParent<TankController>();
            if (barrel != null) barrelHome = barrel.localPosition;
        }

        void OnEnable()
        {
            if (tank != null) tank.Fired += OnFired;
        }

        void OnDisable()
        {
            if (tank != null) tank.Fired -= OnFired;
        }

        void OnFired()
        {
            recoil = 1f;

            var muzzle = tank != null ? tank.FirePoint : null;
            if (muzzle == null) return;

            // Unparented, in world space: a flash parented to a turret that is still
            // slewing gets dragged around by it for the frames it is alive.
            if (muzzleFlash != null)
                Instantiate(muzzleFlash, muzzle.position, muzzle.rotation);

            if (muzzleSmoke != null)
                Instantiate(muzzleSmoke, muzzle.position, muzzle.rotation);
        }

        void Update()
        {
            if (barrel == null) return;

            if (recoil <= 0.0001f)
            {
                // Settled: leave the transform alone entirely rather than writing the
                // same value every frame.
                if (recoil != 0f)
                {
                    recoil = 0f;
                    barrel.localPosition = barrelHome;
                }
                return;
            }

            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime / Mathf.Max(0.0001f, recoilReturn));

            // Squared, so the kick is sharp on the way out and eases back in.
            float offset = recoil * recoil * recoilDistance;
            barrel.localPosition = barrelHome - Vector3.forward * offset;
        }
    }
}
