using TMPro;
using UnityEngine;


public class KillCounter : MonoBehaviour
{
    public static KillCounter Instance { get; private set; }

    [SerializeField] TextMeshProUGUI killCounterText;
    private int totalKillCount = 0;


    private void Awake()
    {
        Instance = this;
    }

    private void UpdateCounterVisual()
    {
        killCounterText.text = totalKillCount.ToString();
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
