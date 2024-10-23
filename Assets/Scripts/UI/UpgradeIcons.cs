using UnityEngine;
using UnityEngine.UI;


public class UpgradeIcons : MonoBehaviour
{
    [SerializeField] Image aImage;
    [SerializeField] Image tImage;
    [SerializeField] Image sImage;
    [SerializeField] Image dImage;

    [SerializeField] Sprite[] sprites;
    [SerializeField] Color[] upgradeColors;

    private int aLevel;
    private int tLevel;
    private int sLevel;
    private int dLevel;

    private const float newWidth = 70;
    private const float newHeight = 70;


    void Start()
    {
        UpgradeUI.Instance.armorUpgrade += Instance_armorUpgrade;
        UpgradeUI.Instance.turretUpgrade += Instance_turretUpgrade;
        UpgradeUI.Instance.speedUpgrade += Instance_speedUpgrade;
        UpgradeUI.Instance.demolitionUpgrade += Instance_demolitionUpgrade;
    }

    private void Instance_demolitionUpgrade(object sender, System.EventArgs e)
    {
        dLevel++;
        if (dLevel == 1)
        {
            dImage.sprite = sprites[3];
            dImage.rectTransform.sizeDelta = new Vector2(60, 60);
        }
        if (dLevel == 2)
        {
            dImage.color = upgradeColors[0];
        }
        if (dLevel == 3)
        {
            dImage.color = upgradeColors[1];
        }
    }

    private void Instance_speedUpgrade(object sender, System.EventArgs e)
    {
        sLevel++;
        if (sLevel == 1)
        {
            sImage.sprite = sprites[2];
            sImage.rectTransform.sizeDelta = new Vector2(newWidth, newHeight);
        }
        if (sLevel == 2)
        {
            sImage.color = upgradeColors[0];
        }
        if (sLevel == 3)
        {
            sImage.color = upgradeColors[1];
        }
    }

    private void Instance_turretUpgrade(object sender, System.EventArgs e)
    {
        tLevel++;
        if (tLevel == 1)
        {
            tImage.sprite = sprites[1];
            tImage.rectTransform.sizeDelta = new Vector2(newWidth, newHeight);
        }
        if (tLevel == 2)
        {
            tImage.color = upgradeColors[0];
        }
        if (tLevel == 3)
        {
            tImage.color = upgradeColors[1];
        }
    }

    private void Instance_armorUpgrade(object sender, System.EventArgs e)
    {
        aLevel++;
        if(aLevel == 1)
        {
            aImage.sprite = sprites[0];
            aImage.rectTransform.sizeDelta = new Vector2(newWidth, newHeight);
        }
        if(aLevel == 2)
        {
            aImage.color = upgradeColors[0];
        }
        if (aLevel == 3)
        {
            aImage.color = upgradeColors[1];
        }
    }
}
