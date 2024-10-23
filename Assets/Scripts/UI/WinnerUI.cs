using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;


public class WinnerUI : MonoBehaviour
{
    [SerializeField] GameObject globalVolumeGO;
    private GameObject gameOverPanel;

    private Volume volume;
    private ColorAdjustments colorAdjustments;
    [SerializeField] LevelLoaderNew levelLoader; 

    [Header("Stats")]
    [SerializeField] TextMeshProUGUI levelText;
    [SerializeField] TextMeshProUGUI timeText;
    [SerializeField] TextMeshProUGUI bronzeText;
    [SerializeField] TextMeshProUGUI silverlText;
    [SerializeField] TextMeshProUGUI goldlText;
    [SerializeField] TextMeshProUGUI elimText;

    [Header("Buttons")]
    [SerializeField] Button endlessButton;
    [SerializeField] Button MainMenuButton;
    [SerializeField] Button QuitButton;
    private Animator animator;


    private void Awake()
    {
        volume = globalVolumeGO.GetComponent<Volume>();
        animator = GetComponent<Animator>();

        MainMenuButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            levelLoader.LoadNextLevel(0);
        });

        QuitButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            Application.Quit();
        });
        endlessButton.onClick.AddListener(() =>
        {
            Cursor.visible = true;
            Hide();
            Time.timeScale = 1f;
            SoundManager.Instance.PlayButtonSound();

        });
    }

    private void Start()
    {
        GameManager.Instance.OnWin += Instance_OnWin;
        Hide();
    }

    private void Instance_OnWin(object sender, System.EventArgs e)
    {
        UpdatePostProcessEffect();
        WinnerStats();
        Show();

        Time.timeScale = 0f;
    }

    private void UpdatePostProcessEffect()
    {
        volume.profile.TryGet(out colorAdjustments);
        colorAdjustments.saturation.value = +100f;
    }

    private void WinnerStats()
    {
        // --- Update Stats ---

        // Level
        int level = Level.Instance.GetLevel() - 1;
        levelText.text = level.ToString();
        // Time
        int seconds = TimerManager.Instance.GetSec();
        int minutes = TimerManager.Instance.GetMin();
        timeText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        // Coins
        int bCoins = Level.Instance.GetBronzeTotal();
        int sCoins = Level.Instance.GetSilverTotal();
        int gCoins = Level.Instance.GetGoldTotal();
        bronzeText.text = bCoins.ToString();
        silverlText.text = sCoins.ToString();
        goldlText.text = gCoins.ToString();
        // Kill Counter
        int  kills = KillCounter.Instance.GetKillCount();
        elimText.text = kills.ToString();
    }


    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
