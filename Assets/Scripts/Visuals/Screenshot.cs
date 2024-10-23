using System;
using UnityEngine;


public class Screenshot : MonoBehaviour
{
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            ScreenCapture.CaptureScreenshot("screenshot-" + DateTime.Now.ToString("yyy-MM-dd-HH-mm-ss") + ".png", 4);
        }
    }
}
