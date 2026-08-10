using UnityEngine;


public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance { get; private set; }

    private float currentTime = 0f;
    private float seconds;
    private int minutes;


    private void Awake()
    {
        Instance = this;
    }
    // Update is called once per frame
    void Update()
    {
        if (currentTime < 60)
        {
            currentTime += Time.deltaTime;
            if (seconds == (int)Mathf.Round(currentTime))
            {
                return;
            }
            else
            {
                AddTime();
            }
            UpdateUI();
        }
    }

    private void AddTime()
    {
        seconds = (int)Mathf.Round(currentTime);
        if (seconds == 60)
        {
            currentTime = 0;
            seconds = 0;
            minutes++;

            // Check Win Condition
            GameManager.Instance.CheckWinCondition();
        }
    }

    private void UpdateUI()
    {
        // The HUD only rebuilds its string when the whole second changes, so calling
        // this on every tick is fine.
        if (HudController.Instance != null)
        {
            HudController.Instance.SetTime(minutes, (int)seconds);
        }
    }


    public int GetMin()
    {
        return minutes;
    }
    public int GetSec()
    {
        return (int)Mathf.Round(currentTime);
    }
}
