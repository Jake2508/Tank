using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The level-select screen. Owns showing/hiding and what Confirm/Back do;
/// LevelSelectNav owns which card is highlighted and LevelCard owns the card
/// visuals. Scene loading still goes through LevelLoaderNew so the wipe
/// transition is unchanged.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    public static LevelSelectUI instance { get; private set; }

    [SerializeField] LevelLoaderNew levelLoader;
    [SerializeField] List<LevelCard> cards = new List<LevelCard>();
    [SerializeField] Button backButton;

    /// <summary>Matches the alpha ramp in LevelSelectFade.anim.</summary>
    const float FadeOutTime = 0.3333333f;

    /// <summary>Entrance delay between cards, left to right.</summary>
    const float CardStagger = 0.06f;

    Animator animator;
    LevelSelectNav nav;
    CanvasGroup group;
    bool deploying;

    void Awake()
    {
        instance = this;
        animator = GetComponent<Animator>();
        nav = GetComponent<LevelSelectNav>();
        group = GetComponent<CanvasGroup>();

        if (backButton) backButton.onClick.AddListener(Back);

        Hide();
    }

    void OnDestroy() { if (instance == this) instance = null; }

    public void Show()
    {
        // The group still holds whatever alpha it was left on, and the animator does
        // not evaluate until the frame after activation - so without this the screen
        // pops at full opacity for one frame before the fade-in starts.
        if (group) group.alpha = 0f;

        gameObject.SetActive(true);
        deploying = false;

        if (animator)
        {
            animator.ResetTrigger("fadeOut");
            animator.SetTrigger("fadeIn");
        }

        // The cards sit inside a rect that ScaleToFitWidth may have just resized,
        // so let layout settle before each card records its resting position.
        Canvas.ForceUpdateCanvases();
        for (int i = 0; i < cards.Count; i++)
        {
            if (!cards[i]) continue;
            cards[i].CaptureHome();
            cards[i].PlayIntro(i * CardStagger);
        }

        if (nav) nav.Open();
    }

    /// <summary>Immediate hide, no fade. Used on load and by anything that needs the screen gone now.</summary>
    public void Hide()
    {
        if (nav) nav.Close();
        gameObject.SetActive(false);
    }

    /// <summary>Back to the title screen, cross-fading the two.</summary>
    public void Back()
    {
        if (deploying) return;

        PlayClick();
        if (nav) nav.Close();
        if (MainMenuUI.instance) MainMenuUI.instance.Show();

        if (!isActiveAndEnabled) { Hide(); return; }
        StartCoroutine(FadeOutThenHide());
    }

    /// <summary>Loads the card's level. Input is locked out for the rest of the screen's life.</summary>
    public void Deploy(LevelCard card)
    {
        if (deploying || card == null) return;
        deploying = true;

        PlayClick();
        if (nav) nav.Close();

        StartCoroutine(DeployRoutine(card));
    }

    IEnumerator DeployRoutine(LevelCard card)
    {
        yield return new WaitForSecondsRealtime(0.08f);   // let the punch read

        if (levelLoader) levelLoader.LoadNextLevel(card.BuildIndex);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(card.BuildIndex);
    }

    IEnumerator FadeOutThenHide()
    {
        if (animator)
        {
            animator.ResetTrigger("fadeIn");
            animator.SetTrigger("fadeOut");
        }

        yield return new WaitForSecondsRealtime(FadeOutTime);
        gameObject.SetActive(false);
    }

    static void PlayClick()
    {
        if (SoundManager.Instance) SoundManager.Instance.PlayButtonSound();
    }
}
