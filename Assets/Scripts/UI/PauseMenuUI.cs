using UnityEngine;
using UnityEngine.UI;


public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] Button resumeButton;
    [SerializeField] Button menuButton;
    [SerializeField] Button quitButton;
    [SerializeField] LevelLoaderNew levelLoader;

    private Animator animator;


    private void Awake()
    {
        // Bind click events to buttons
        resumeButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            GameManager.Instance.TogglePauseGame();
        });
        menuButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            levelLoader.LoadNextLevel(0);
        });
        quitButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            Application.Quit();
        });

        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        GameManager.Instance.OnGamePaused += Instance_OnGamePaused;
        GameManager.Instance.OnGameResumed += Instance_OnGameResumed;

        gameObject.SetActive(false);
    }

    private void Instance_OnGameResumed(object sender, System.EventArgs e)
    {
        Hide();
    }

    private void Instance_OnGamePaused(object sender, System.EventArgs e)
    {
        Show();
    }

    private void Show()
    {
        gameObject.SetActive(true);
        animator.SetTrigger("fadeIn");
    }

    private void Hide()
    {
        animator.SetTrigger("fadeOut");
    }
}
