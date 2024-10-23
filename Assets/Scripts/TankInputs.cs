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
        public LayerMask mask;
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
            if(camera)
            {
                HandleInputs();
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
            if(Physics.Raycast(screenRay, out hit, mask))
            {
                reticlePosition = hit.point;
                recticleNormal = hit.normal;
            }

            forwardInput = Input.GetAxis("Vertical");
            rotationInput = Input.GetAxis("Horizontal");

            if(Input.GetButtonDown("Pause"))
            {
                //Debug.Log("Pause trigged");
                OnPauseAction?.Invoke(this, EventArgs.Empty);
                
            }
        }

        #endregion
    }

}