using UnityEngine;


public class Level : MonoBehaviour
{
    public static Level Instance { get; private set; }

    int level = 1;
    int experience = 0;

    private int bCoinTotal;
    private int sCoinTotal;
    private int gCoinTotal;

    int TO_LEVEL_UP
    {
        get
        {
            return level * 750;
        }
    }


    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ReportProgress();
        SetLevelText();
    }


    public void AddExperience(int amount)
    {
        if (GameManager.Instance.IsDead())
        {
            return;
        }
        if(level < 13)
        {
            experience += amount;
            CheckLevelUp();
        }
    }

    public void RecordCoinType(string coin)
    {
        switch(coin.ToLower())
        {
            case "bronze":
                bCoinTotal++;
                //Debug.Log("Bronze" + bCoinTotal);
                break;
            case "silver":
                sCoinTotal++;
                //Debug.Log("Silver" + sCoinTotal);
                break;
            case "gold":
                gCoinTotal++;
                //Debug.Log("Gold" + gCoinTotal);
                break;

            default:
                Debug.LogWarning($"Unknown Coin Type: {coin}");
                break;
        }
    }

    public void CheckLevelUp()
    {
        if(experience >= TO_LEVEL_UP)
        {
            if(SoundManager.Instance != null)
            {
                SoundManager.Instance.LevelUpSound();
            }

            experience -= TO_LEVEL_UP;
            level += 1;
            SetLevelText();
            GameManager.Instance.ToggleUpgradePause();
        }

        if(level == 13)
        {
            ReportProgress(1f);
        }
        else
        {
            ReportProgress();
        }
    }


    /// <summary>Pushes XP to the HUD, which may not be in the scene.</summary>
    private void ReportProgress()
    {
        ReportProgress(TO_LEVEL_UP <= 0 ? 0f : (float)experience / TO_LEVEL_UP);
    }

    private void ReportProgress(float normalised)
    {
        if (HudController.Instance != null)
        {
            HudController.Instance.SetXp(normalised);
        }
    }

    private void SetLevelText()
    {
        // The HUD chip shows the run level itself; the game-over screen keeps its own
        // "levels gained" reading, which is where the -1 belonged.
        if (HudController.Instance != null)
        {
            HudController.Instance.SetLevel(level);
        }
    }


    public int GetLevel()
    {
        return level;
    }

    public int GetBronzeTotal()
    {
        return bCoinTotal;
    }
    public int GetSilverTotal()
    {
        return sCoinTotal;
    }
    public int GetGoldTotal()
    {
        return gCoinTotal;
    }

}

