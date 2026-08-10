using UnityEngine;
using UnityEngine.UI;


public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI instance { get; private set; }

    [SerializeField] Button playButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button quitButton;

    private Animator animator;

    /// <summary>
    /// Application.Quit does nothing in a browser - the page owns the tab, not the
    /// player - so on WebGL the button is greyed out rather than left looking live and
    /// doing nothing when clicked.
    ///
    /// Compiled out rather than checked at runtime, so the desktop build carries no
    /// trace of it and the editor keeps testing the desktop path.
    /// </summary>
    const bool CanQuit =
#if UNITY_WEBGL && !UNITY_EDITOR
        false;
#else
        true;
#endif

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

        // The slab and its animation have always been here; nothing was ever listening.
        if (settingsButton)
        {
            settingsButton.onClick.AddListener(() =>
            {
                SoundManager.Instance.PlayButtonSound();
                animator.SetTrigger("fadeOut");
                if (SettingsMenu.Instance) SettingsMenu.Instance.Show();
                Hide();
            });
        }

        quitButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.PlayButtonSound();
            Application.Quit();
        });

        if (!CanQuit) GreyOutQuit();

        Time.timeScale = 1f;
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Marks the quit slab permanently unavailable. MainMenuNav already walks past any
    /// slab that is not interactable, so this also takes it out of the W/S rotation
    /// without touching the column's layout, its indices or its fade clip.
    /// </summary>
    private void GreyOutQuit()
    {
        if (quitButton == null) return;

        quitButton.interactable = false;

        var slab = quitButton.GetComponent<MenuSlab>();
        if (slab != null) slab.SetAvailable(false);
    }


    public void Show()
    {
        animator.SetTrigger("fadeIn");

        SetInteractable(true);
    }
    public void Hide()
    {
        SetInteractable(false);
    }

    private void SetInteractable(bool on)
    {
        playButton.interactable = on;
        // Show() runs every time the level-select popup closes, so without the guard
        // it would hand Quit back on a platform that cannot use it.
        quitButton.interactable = on && CanQuit;
        if (settingsButton) settingsButton.interactable = on;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only: pose the WebGL state so a capture can show the greyed button
    /// without building for the browser.
    /// </summary>
    public void EditorPoseNoQuit()
    {
        GreyOutQuit();
    }
#endif
}
