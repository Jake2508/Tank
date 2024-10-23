using UnityEngine;
using UnityEngine.UI;


public class LevelSelectUI : MonoBehaviour
{
    public static LevelSelectUI instance { get; private set; }

    [SerializeField] LevelLoaderNew levelLoader;
    [SerializeField] Button woodlandButton;
    [SerializeField] Button desertButton; 
    [SerializeField] Button snowylandsButton;
    [SerializeField] Button backButton;

    private Animator animator;


    private void Awake()
    {
        instance = this;

        woodlandButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            levelLoader.LoadNextLevel(2);
            //Loader.Load(Loader.Scene.L_Woodlands);
        });

        desertButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            levelLoader.LoadNextLevel(3);
            //Loader.Load(Loader.Scene.L_Desert);
        });
        snowylandsButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            levelLoader.LoadNextLevel(4);
            //Loader.Load(Loader.Scene.L_Snowy);
        });

        backButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            MainMenuUI.instance.Show();
            animator.SetTrigger("fadeOut");
            Hide();
        });

        animator = GetComponent<Animator>();
        Hide();
    }

    public void Show()
    {
        animator.SetTrigger("fadeIn");
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
