
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;


public class GameOverUI : MonoBehaviour
{
    [SerializeField] GameObject globalVolumeGO;
    private GameObject gameOverPanel;

    private Volume lightingVolume;
    private ColorAdjustments colorAdjustments;

    [SerializeField] GameObject characterExplosion;
    [SerializeField] LevelLoaderNew levelLoader;

    [Header("Stats")]
    [SerializeField] TextMeshProUGUI levelText;
    [SerializeField] TextMeshProUGUI timeText;
    [SerializeField] TextMeshProUGUI bronzeText;
    [SerializeField] TextMeshProUGUI silverlText;
    [SerializeField] TextMeshProUGUI goldlText;
    [SerializeField] TextMeshProUGUI elimText;

    [Header("Buttons")]
    [SerializeField] Button MainMenuButton;
    [SerializeField] Button QuitButton;
    private Animator animator;


    private void Awake()
    {
        lightingVolume = globalVolumeGO.GetComponent<Volume>();
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

    }

    private void Start()
    {
        GameManager.Instance.OnDeath += Instance_OnDeath;
        Hide();
    }

    private void Instance_OnDeath(object sender, System.EventArgs e)
    {
        UpdatePostProcessEffect();
        GameOverVisuals();
        GameOverStats();
        Show();
        
        Time.timeScale = 0f;
    }

    private void UpdatePostProcessEffect()
    {
        lightingVolume.profile.TryGet(out colorAdjustments);
        colorAdjustments.saturation.value = -100f;

        //volume.GetComponent<ColorAdjustments>().active = false;
    }

    private void GameOverVisuals()
    {
        // Character Destruction
        Transform c = GameManager.Instance.playerTransform;
        Instantiate(characterExplosion, c.position, Quaternion.identity);
    }

    private void GameOverStats()
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
        animator.SetTrigger("fadeIn");
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }   
}
