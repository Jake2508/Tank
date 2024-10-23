using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI secondsText;
    [SerializeField] private TextMeshProUGUI minutesText;
    [SerializeField] private Image timerIcon;

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
        secondsText.text = seconds.ToString("00");
        minutesText.text = minutes.ToString("00");

        timerIcon.fillAmount = seconds / 60;
        //Debug.Log(seconds);
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
