using UnityEngine;
using UnityEngine.UI;


public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI instance { get; private set; }

    [SerializeField] Button playButton;
    [SerializeField] Button quitButton;

    private Animator animator;

    private void Awake()
    {
        instance = this;

        playButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            animator.SetTrigger("fadeOut");
            LevelSelectUI.instance.Show();
            Hide();
        });

        quitButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            Application.Quit();
        });

        Time.timeScale = 1f;
        animator = GetComponent<Animator>();
    }


    public void Show()
    {
        animator.SetTrigger("fadeIn");

        playButton.interactable = true;
        quitButton.interactable = true;
    }
    public void Hide()
    {
        playButton.interactable = false;
        quitButton.interactable = false;
    }
}
