using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The level-up card picker (MENUS_IMPLEMENTATION.md section 3).
///
/// Four columns under a banner over a full scrim - the whole screen belongs to the
/// choice. Always four cards, always in the fixed order Armour, Turret, Speed,
/// Demolition, because players learn positions. A card already at tier 3 keeps its
/// slot but cannot be taken.
///
/// The open and close sequences are driven from Update on unscaled time rather than
/// coroutines: gameplay is frozen at timeScale 0 throughout, and a coroutine on a
/// GameObject that deactivates mid-sequence would be dropped part-finished.
/// </summary>
public class UpgradeScreen : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] CanvasGroup group;
    [SerializeField] Image scrim;
    [SerializeField] RectTransform banner;
    [SerializeField] CanvasGroup bannerGroup;
    [SerializeField] CanvasGroup subtitleGroup;
    [SerializeField] CanvasGroup hintGroup;
    [SerializeField] MenuFocusGroup focus;

    [Tooltip("Four, in the fixed order Armour, Turret, Speed, Demolition.")]
    [SerializeField] UpgradeCard[] cards = new UpgradeCard[4];

    // ---- open, seconds -------------------------------------------------------
    const float ScrimSeconds = 0.12f;
    const float BannerDelay = 0.06f;
    const float BannerSeconds = 0.22f;
    const float BannerRise = 18f;
    const float BannerPeak = 1.12f;
    const float CardDelay = 0.10f;
    const float CardStagger = 0.05f;
    const float InputUnlock = 0.36f;

    // ---- close ---------------------------------------------------------------
    const float CloseScrimSeconds = 0.16f;
    const float CloseSeconds = 0.30f;

    /// <summary>The player took this upgrade. Raised the instant they commit.</summary>
    public event Action<UpgradeType> Chosen;

    /// <summary>The close animation has finished and gameplay may resume.</summary>
    public event Action Closed;

    enum Phase { Hidden, Opening, Idle, Closing }

    Phase phase = Phase.Hidden;
    float phaseStart;

    Vector2 bannerHome;
    bool bannerHomeCaptured;

    void Awake()
    {
        CaptureBannerHome();

        // Cards subscribe once; which one fires is decided by the focus group.
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            var card = cards[i];
            card.Activated += () => Take(card);
        }
    }

    void CaptureBannerHome()
    {
        if (bannerHomeCaptured || banner == null) return;
        bannerHome = banner.anchoredPosition;
        bannerHomeCaptured = true;
    }

    /// <summary>
    /// Deals the four cards from the run's current build and plays the open sequence.
    /// </summary>
    public void Show(UpgradeModel model)
    {
        CaptureBannerHome();
        gameObject.SetActive(true);

        for (int i = 0; i < cards.Length && i < UpgradeCatalog.Order.Length; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            var type = UpgradeCatalog.Order[i];
            int tier = model != null ? model.Tier(type) : 0;

            card.SetHeadLabel(UpgradeCatalog.DisplayName(type));
            card.SetOffer(tier, UpgradeCatalog.NextDescription(type, tier), i + 1);
            card.ResetPose();
            card.PlayIntro(CardDelay + i * CardStagger);
        }

        phase = Phase.Opening;
        phaseStart = Time.unscaledTime;

        if (group != null) group.alpha = 1f;
        if (scrim != null) scrim.color = MenuPalette.Fade(MenuPalette.ScrimUpgrade, 0f);
        if (bannerGroup != null) bannerGroup.alpha = 0f;
        if (subtitleGroup != null) subtitleGroup.alpha = 0f;
        if (hintGroup != null) hintGroup.alpha = 0f;

        // Card 2 on open, never a card at either end - the middle is the shortest
        // travel to anything else, and it is never the "safe" option by accident.
        if (focus != null) focus.Open(1, InputUnlock);
    }

    public void Hide()
    {
        phase = Phase.Hidden;
        if (focus != null) focus.Close();
        gameObject.SetActive(false);
    }

    void Take(UpgradeCard card)
    {
        if (phase != Phase.Opening && phase != Phase.Idle) return;
        if (card == null || card.Maxed) return;

        phase = Phase.Closing;
        phaseStart = Time.unscaledTime;

        if (focus != null) focus.Close();

        foreach (var other in cards)
        {
            if (other == null) continue;
            if (other == card) other.PlayTaken();
            else other.PlayDismissed();
        }

        Chosen?.Invoke(card.Type);
    }

    void Update()
    {
        if (phase == Phase.Hidden) return;

        float t = Time.unscaledTime - phaseStart;

        if (phase == Phase.Closing)
        {
            float fade = 1f - Mathf.Clamp01(t / CloseScrimSeconds);
            if (scrim != null) scrim.color = MenuPalette.Fade(MenuPalette.ScrimUpgrade, fade);
            if (bannerGroup != null) bannerGroup.alpha = fade;
            if (subtitleGroup != null) subtitleGroup.alpha = fade;
            if (hintGroup != null) hintGroup.alpha = fade;

            if (t >= CloseSeconds)
            {
                Hide();
                Closed?.Invoke();
            }
            return;
        }

        // ---- opening ---------------------------------------------------------
        if (scrim != null)
            scrim.color = MenuPalette.Fade(MenuPalette.ScrimUpgrade,
                                           Mathf.Clamp01(t / ScrimSeconds));

        float b = Mathf.Clamp01((t - BannerDelay) / BannerSeconds);
        float eased = 1f - (1f - b) * (1f - b);                  // ease out

        if (banner != null)
        {
            banner.anchoredPosition = bannerHome + new Vector2(0f, Mathf.Lerp(BannerRise, 0f, eased));
            banner.localScale = Vector3.one * Mathf.Lerp(BannerPeak, 1f, eased);
        }
        if (bannerGroup != null) bannerGroup.alpha = b;
        if (subtitleGroup != null) subtitleGroup.alpha = b;

        // The hint arrives with the last card rather than with the banner - it is the
        // least important thing on screen and should not compete on the way in.
        if (hintGroup != null)
            hintGroup.alpha = Mathf.Clamp01((t - CardDelay - 3f * CardStagger) / 0.18f);

        if (t >= InputUnlock) phase = Phase.Idle;
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: draw the screen fully open for a layout capture.</summary>
    public void EditorPose(int[] tiers, int focusedIndex)
    {
        CaptureBannerHome();
        gameObject.SetActive(true);
        phase = Phase.Idle;

        if (group != null) group.alpha = 1f;
        if (scrim != null) scrim.color = MenuPalette.ScrimUpgrade;
        if (bannerGroup != null) bannerGroup.alpha = 1f;
        if (subtitleGroup != null) subtitleGroup.alpha = 1f;
        if (hintGroup != null) hintGroup.alpha = 1f;

        if (banner != null)
        {
            banner.anchoredPosition = bannerHome;
            banner.localScale = Vector3.one;
        }

        // Mirror MenuFocusGroup.Nearest: if the card that would open focused is already
        // maxed, the highlight lands on the next one that can actually be taken.
        int effective = focusedIndex;
        for (int step = 0; step < cards.Length; step++)
        {
            int i = (focusedIndex + step) % cards.Length;
            int tier = tiers != null && i < tiers.Length ? tiers[i] : 0;
            if (tier < UpgradeCatalog.MaxTier) { effective = i; break; }
        }

        for (int i = 0; i < cards.Length && i < UpgradeCatalog.Order.Length; i++)
        {
            if (cards[i] == null) continue;
            var type = UpgradeCatalog.Order[i];
            int tier = tiers != null && i < tiers.Length ? tiers[i] : 0;

            cards[i].SetHeadLabel(UpgradeCatalog.DisplayName(type));
            cards[i].EditorPose(tier, UpgradeCatalog.NextDescription(type, tier), i + 1,
                                i == effective);
        }
    }
#endif
}
