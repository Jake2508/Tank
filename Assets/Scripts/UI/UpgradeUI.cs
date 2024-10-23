using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeUI : MonoBehaviour
{
    public static UpgradeUI Instance { get; private set; }

    [Header("Buttons")]
    [SerializeField] Button UpgradeButton1;
    [SerializeField] Button UpgradeButton2;
    [SerializeField] Button UpgradeButton3;
    [SerializeField] Button UpgradeButton4;

    public event EventHandler armorUpgrade;
    public event EventHandler turretUpgrade;
    public event EventHandler speedUpgrade;
    public event EventHandler demolitionUpgrade;

    private int lvlA;
    private int lvlT;
    private int lvlS;
    private int lvlD;

    [Header("Upgrade Visuals")]
    [SerializeField] TextMeshProUGUI armorText;
    [SerializeField] Image armorIcon;
    [SerializeField] TextMeshProUGUI turretText;
    [SerializeField] Image turretIcon;
    [SerializeField] TextMeshProUGUI speedText;
    [SerializeField] Image speedIcon;
    [SerializeField] TextMeshProUGUI demolitionText;
    [SerializeField] Image demolitionIcon;

    [SerializeField] Color[] upgradeColor;


    private Animator animator;

    private void Awake()
    {
        Instance = this;
        animator = GetComponent<Animator>();

        UpgradeButton1.onClick.AddListener(() =>
        {
            armorUpgrade?.Invoke(this, EventArgs.Empty);
            UpdateUpgradeUIVisuals(0);
            GameManager.Instance.ToggleUpgradePause();

        });
        UpgradeButton2.onClick.AddListener(() =>
        {
            turretUpgrade?.Invoke(this, EventArgs.Empty);
            UpdateUpgradeUIVisuals(1);
            GameManager.Instance.ToggleUpgradePause();
        });
        UpgradeButton3.onClick.AddListener(() =>
        {
            speedUpgrade?.Invoke(this, EventArgs.Empty);
            UpdateUpgradeUIVisuals(2);
            GameManager.Instance.ToggleUpgradePause();
        });
        UpgradeButton4.onClick.AddListener(() =>
        {
            demolitionUpgrade?.Invoke(this, EventArgs.Empty);
            UpdateUpgradeUIVisuals(3);
            GameManager.Instance.ToggleUpgradePause();
        });
    }

    private void Start()
    {
        GameManager.Instance.OnLevelUp += Instance_OnLevelUp;
        GameManager.Instance.OnLevelUpSelected += Instance_OnLevelUpSelected;

        gameObject.SetActive(false);
    }

    private void Instance_OnLevelUp(object sender, System.EventArgs e)
    {
        Show();
    }
    private void Instance_OnLevelUpSelected(object sender, System.EventArgs e)
    {
        Hide();
    }

    private void Hide()
    {
        animator.SetTrigger("fadeOut");
        
    }
    private void Show()
    {
        gameObject.SetActive(true);
        animator.SetTrigger("fadeIn");
    }

    private void UpdateUpgradeUIVisuals(int index)
    {
        switch (index)
        {
            case 0:
                lvlA++;
                if(lvlA == 1)
                {
                    // Unlock Bronze Upgrade
                    // Increase passive item armor ++ add in value easy add
                    GameManager.Instance.ApplyArmorUpgrade(1);

                    // Update Text & Icon Color Silver
                    armorIcon.color = upgradeColor[0];
                    armorText.text = "Increase ranged damage resistance";
                    ResetSelectedButton(UpgradeButton1);
                }
                if(lvlA == 2)
                {
                    // Unlock Silver Upgrade
                    GameManager.Instance.ApplyArmorUpgrade(2);

                    // Update Text & Icon Color Gold
                    armorIcon.color = upgradeColor[1];
                    armorText.text = "Auto Regenerate Armour Overtime";
                    ResetSelectedButton(UpgradeButton1);
                }
                if (lvlA == 3)
                {
                    // Unlock Gold Upgrade
                    //GameManager.Instance.ApplyArmorUpgrade(3);
                    armorIcon.color = upgradeColor[2];
                    armorText.text = "Max Level Achieved";
                    UpgradeButton1.interactable = false;

                    // Disable Display
                    //UpgradeButton1.gameObject.SetActive(false);
                }

                break;

            case 1:
                lvlT++;
                if (lvlT == 1)
                {
                    GameManager.Instance.ApplyTurretUpgrade(1);

                    // Update Text & Icon Color Silver
                    turretIcon.color = upgradeColor[0];
                    turretText.text = "Increase turret rotation speed";
                    ResetSelectedButton(UpgradeButton2);
                }
                if (lvlT == 2)
                {
                    GameManager.Instance.ApplyTurretUpgrade(2);

                    // Update Text & Icon Color Gold
                    turretIcon.color = upgradeColor[1];
                    turretText.text = "Improve tank shell explosive force";
                    ResetSelectedButton(UpgradeButton2);
                }
                if (lvlT == 3)
                {
                    GameManager.Instance.ApplyTurretUpgrade(3);

                    // Disable Display
                    turretIcon.color = upgradeColor[2];
                    turretText.text = "Max Level Achieved";
                    UpgradeButton2.interactable = false;
                    //UpgradeButton2.gameObject.SetActive(false);
                }

                break;

            case 2:
                lvlS++;
                if (lvlS == 1)
                {
                    GameManager.Instance.ApplySpeedUpgrade(1);

                    // Update Text & Icon Color Silver
                    speedIcon.color = upgradeColor[0];
                    speedText.text = "Increase tank movement speed";
                    // Increase Tank Rotation Speed
                    ResetSelectedButton(UpgradeButton3);

                }
                if (lvlS == 2)
                {
                    GameManager.Instance.ApplySpeedUpgrade(2);

                    // Update Text & Icon Color Gold
                    speedIcon.color = upgradeColor[1];
                    speedText.text = "Drastically increase tank rotation and movement speed";
                    ResetSelectedButton(UpgradeButton3);
                }
                if (lvlS == 3)
                {
                    GameManager.Instance.ApplySpeedUpgrade(3);

                    // Disable Display
                    speedIcon.color = upgradeColor[2];
                    speedText.text = "Max Level Achieved";
                    UpgradeButton3.interactable = false;
                    //UpgradeButton3.gameObject.SetActive(false);
                }
                
                break;

            case 3:
                lvlD++;
                if (lvlD == 1)
                {
                    GameManager.Instance.ApplyDemolitionUpgrade(1);

                    // Update Text & Icon Color Silver
                    demolitionIcon.color = upgradeColor[0];
                    demolitionText.text = "Increase item pickup strength";
                    ResetSelectedButton(UpgradeButton4);

                }
                if (lvlD == 2)
                {
                    GameManager.Instance.ApplyDemolitionUpgrade(2);

                    // Update Text & Icon Color Gold
                    demolitionIcon.color = upgradeColor[1];
                    demolitionText.text = "Drastically increase item pickup range and force";
                    ResetSelectedButton(UpgradeButton4);
                }
                if (lvlD == 3)
                {
                    GameManager.Instance.ApplyDemolitionUpgrade(3);

                    // Disable Display
                    demolitionIcon.color = upgradeColor[2];
                    demolitionText.text = "Max Level Achieved";
                    UpgradeButton4.interactable = false;
                    //UpgradeButton4.gameObject.SetActive(false);
                }
                
                break;

            default:
                Debug.LogWarning("Invalid Upgrade Selected");
                break;
        }

    }


    private void ResetSelectedButton(Button button)
    {
        button.interactable = false;
        button.interactable = true;
    }


}


