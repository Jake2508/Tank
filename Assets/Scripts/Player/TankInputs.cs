using System;
using UnityEngine;


namespace Tank
{
    public class TankInputs : MonoBehaviour
    {
        public static TankInputs Instance { get; private set; }

        #region Events
        public event EventHandler OnFireAction;
        public event EventHandler OnPauseAction;

        #endregion

        #region Variables
        [Header("Input Properties")]
        public Camera camera;

        [Tooltip("Ground only. The reticle must not latch onto trees, enemies or pickups.")]
        public LayerMask mask;

        /// <summary>
        /// Far enough to cross the whole visible map from a top-down camera; the ray is
        /// filtered by layer, so this only needs to be generous rather than exact.
        /// </summary>
        const float AimDistance = 500f;
        #endregion

        #region Properties
        private Vector3 reticlePosition;
        public Vector3 RecticlePositon
        { 
            get { return reticlePosition; }
        }

        private Vector3 recticleNormal;
        public Vector3 RecticleNormal
        {
            get { return recticleNormal; }
        }

        private bool aimValid;

        /// <summary>
        /// True when the cursor is actually over ground. False means the position is a
        /// flat-plane fallback, so the reticle should stop pretending to hug a surface.
        /// </summary>
        public bool AimValid
        {
            get { return aimValid; }
        }

        private float forwardInput;
        public float ForwardInput
        {
            get { return forwardInput; }
        }

        private float rotationInput;
        public float RotationInput
        {
            get { return rotationInput; }
        }

        #endregion

        #region Builtin Methods

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // Aiming stops the moment the cursor belongs to the UI. Without this the
            // turret keeps tracking the mouse through the death and pause screens, and
            // the reticle slides around the map while the player is clicking buttons.
            if (camera && !UiOwnsCursor())
            {
                HandleInputs();
            }
            else
            {
                forwardInput = 0f;
                rotationInput = 0f;
                PollPause();
            }
        }

        /// <summary>
        /// True while a menu, the death screen or the win screen has the cursor. Aim and
        /// drive input are suspended; the pause button is not, or Esc could never reopen
        /// anything.
        /// </summary>
        private bool UiOwnsCursor()
        {
            var game = GameManager.Instance;
            return game != null && (game.IsGamePaused() || game.IsDead());
        }

        private void PollPause()
        {
            if (Input.GetButtonDown("Pause"))
            {
                OnPauseAction?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(RecticlePositon, 0.5f);
        }
        #endregion

        #region Custom Methods
        protected virtual void HandleInputs()
        {
            Ray screenRay = camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // The layer mask has to be the FOURTH argument. Passing it third selects the
            // (Ray, out RaycastHit, float maxDistance) overload instead: LayerMask casts
            // silently to int and then to float, so the mask's value was being used as a
            // draw distance and the filter was thrown away entirely. That is why the
            // reticle would snap onto trees, enemies and coins rather than the ground.
            if(Physics.Raycast(screenRay, out hit, AimDistance, mask, QueryTriggerInteraction.Ignore))
            {
                reticlePosition = hit.point;
                recticleNormal = hit.normal;
                aimValid = true;
            }
            else
            {
                // Off the edge of the world, or over a gap. Fall back to the horizontal
                // plane through the tank so the turret keeps tracking the cursor instead
                // of freezing on the last thing the ray happened to touch.
                aimValid = false;
                var ground = new Plane(Vector3.up, transform.position);
                if (ground.Raycast(screenRay, out float enter))
                {
                    reticlePosition = screenRay.GetPoint(enter);
                    recticleNormal = Vector3.up;
                }
            }

            forwardInput = Input.GetAxis("Vertical");
            rotationInput = Input.GetAxis("Horizontal");

            PollPause();
        }

        #endregion
    }

}