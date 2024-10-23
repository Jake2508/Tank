using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/*
namespace TankCameras
{
    //[CustomEditor(typeof(TopDownCamera))]
    public class TopDownCamera_Editor : Editor
    {
        #region Variables
        private TopDownCamera targetCamera;

        #endregion




        #region Main Methods

        private void OnEnable()
        {
            targetCamera = (TopDownCamera)target;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }

        private void OnSceneGUI()
        {
            // Make sure target is set
            if(!targetCamera.m_Target)
            {
                return;
            }

            // Store Target Reference
            Transform camTarget = targetCamera.m_Target;

            // Draw Distance Disc
            Handles.color = new Color(1f, 0f, 0f, 0.25f);
            Handles.DrawSolidDisc(targetCamera.m_Target.position, Vector3.up, targetCamera.m_Distance);

            Handles.color = new Color(1f, 1f, 0f, 0.75f);
            Handles.DrawWireDisc(targetCamera.m_Target.position, Vector3.up, targetCamera.m_Distance);

            // Slider Handles to adjust camera properties
            Handles.color = new Color(1f, 1f, 0f, 0.5f);
            targetCamera.m_Distance = Handles.ScaleSlider(targetCamera.m_Distance, camTarget.position, -camTarget.forward, Quaternion.identity, targetCamera.m_Distance, 1f);
            targetCamera.m_Distance = Mathf.Clamp(targetCamera.m_Distance, 5f, float.MaxValue);

            Handles.color = new Color(0f, 0f, 1f, 0.5f);
            targetCamera.m_Height = Handles.ScaleSlider(targetCamera.m_Height, camTarget.position, Vector3.up, Quaternion.identity, targetCamera.m_Height, 1f);
            targetCamera.m_Height = Mathf.Clamp(targetCamera.m_Height, 10f, float.MaxValue);

            // Setup Custom Label Style
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.fontSize = 15;
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            // Create Labels
            Handles.Label(camTarget.position + (-camTarget.forward * targetCamera.m_Distance), "Distance", labelStyle);
            Handles.Label(camTarget.position + (Vector3.up * targetCamera.m_Height), "Height", labelStyle);
        }


        #endregion

    }
}*/
