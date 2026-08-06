using UnityEngine;


public class KillCounter : MonoBehaviour
{
    public static KillCounter Instance { get; private set; }

    private int totalKillCount = 0;


    private void Awake()
    {
        Instance = this;
    }

    private void UpdateCounterVisual()
    {
        if (HudController.Instance != null)
        {
            HudController.Instance.SetKills(totalKillCount);
        }
    }


    public void AddKill()
    {
        totalKillCount++;
        UpdateCounterVisual();
    }

    public int GetKillCount()
    {
        return totalKillCount;
    }
}
