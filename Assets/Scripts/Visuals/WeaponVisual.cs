using UnityEngine;
using UnityEngine.UI;


public class WeaponVisual : MonoBehaviour
{
    private Image weaponImage;

    bool UIReloaded = true;


    private void Awake()
    {
        weaponImage = GetComponent<Image>();
    }

    private void FixedUpdate()
    {
        if(GameManager.Instance.CanFire())
        {
            // Fully Reloaded
            weaponImage.color = Color.green;
            UIReloaded = false;
            Debug.Log("UPDATE ICON");
        }
        else
        {
            // Reloading
            weaponImage.color = Color.red;
            UIReloaded=true;

        }
    }
}
