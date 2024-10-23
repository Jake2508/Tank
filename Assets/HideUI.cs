using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideUI : MonoBehaviour
{
    public GameObject[] objectsToHide;

    // Start is called before the first frame update
    void Start()
    {
        foreach(GameObject obj in objectsToHide)
        {
            obj.GetComponent<CanvasGroup>().alpha = 0;
        }
    }

}
