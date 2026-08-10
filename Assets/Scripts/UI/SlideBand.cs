using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The angled band that slides in from the left, its dim, and the staggered reveal of
/// whatever sits in its column.
///
/// Shared by the pause screen and settings so there is exactly one copy of the curve.
/// The settings spec is explicit that the band must not be rebuilt or its tween
/// re-authored - two implementations of "0.16s ease-out from X -420" drift apart the
/// first time either is touched.
///
/// Draw() takes a single 0-to-1 progress, so opening and closing are the same code run
/// in opposite directions rather than two animations that have to agree.
///
/// Unscaled time throughout: pause runs at timeScale 0, and settings works unchanged if
/// it is ever opened from there.
/// </summary>
public class SlideBand : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RectTransform band;
    [SerializeField] Image dim;

    [Tooltip("In top-to-bottom order; they fade in on a stagger.")]
    [SerializeField] CanvasGroup[] contentItems;

    [Header("Look")]
    [Tooltip("Full-strength dim colour. Pause sits lighter than settings, because " +
             "settings covers title art rather than live gameplay.")]
    [SerializeField] Color dimColour = new Color32(18, 12, 8, 89);

    [Header("Timing, seconds")]
    [SerializeField] float bandSeconds = 0.16f;
    [SerializeField] float bandOffscreenX = -420f;
    [SerializeField] float dimSeconds = 0.12f;
    [SerializeField] float itemSeconds = 0.12f;
    [SerializeField] float itemStagger = 0.03f;
    [SerializeField] float itemSlideX = -8f;

    float bandHomeX;
    Vector2[] itemHomes;
    bool captured;

    /// <summary>How long a full open takes, band plus the last item's fade.</summary>
    public float OpenSeconds => bandSeconds + ContentTail;

    float ContentTail => contentItems != null && contentItems.Length > 0
        ? (contentItems.Length - 1) * itemStagger + itemSeconds
        : 0f;

    public void SetDimColour(Color colour) => dimColour = colour;

    /// <summary>
    /// Records the resting positions. Called before the first Draw, and safe to call
    /// again - it only reads once, so a Draw mid-animation cannot be captured as home.
    /// </summary>
    public void CaptureHomes()
    {
        if (captured) return;

        if (band != null) bandHomeX = band.anchoredPosition.x;

        if (contentItems != null)
        {
            itemHomes = new Vector2[contentItems.Length];
            for (int i = 0; i < contentItems.Length; i++)
                if (contentItems[i] != null)
                    itemHomes[i] = ((RectTransform)contentItems[i].transform).anchoredPosition;
        }

        captured = true;
    }

    /// <summary>
    /// Draws the band at <paramref name="k"/>: 0 fully out, 1 fully in.
    ///
    /// <paramref name="k"/> is progress across the *whole* open, not across the band's
    /// own slide. Each element then reads its own real elapsed time from it, so the band
    /// still lands at 0.16s and the column still staggers at 0.03s per item regardless
    /// of how many items there are. Scaling every element by the band's duration instead
    /// compresses the entire reveal into those 0.16s and throws the stagger away.
    /// </summary>
    public void Draw(float k)
    {
        CaptureHomes();

        float elapsed = Mathf.Clamp01(k) * OpenSeconds;

        if (band != null)
        {
            float b = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, bandSeconds));
            float eased = 1f - (1f - b) * (1f - b);          // ease out
            var p = band.anchoredPosition;
            p.x = Mathf.Lerp(bandOffscreenX, bandHomeX, eased);
            band.anchoredPosition = p;
        }

        if (dim != null)
        {
            // Lands before the band does, so the backdrop is already set by the time the
            // column arrives over it.
            float d = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, dimSeconds));
            dim.color = new Color(dimColour.r, dimColour.g, dimColour.b, dimColour.a * d);
        }

        if (contentItems == null) return;

        for (int i = 0; i < contentItems.Length; i++)
        {
            if (contentItems[i] == null) continue;

            float a = Mathf.Clamp01((elapsed - bandSeconds - i * itemStagger) / itemSeconds);
            contentItems[i].alpha = a;

            if (itemHomes == null || i >= itemHomes.Length) continue;
            var rt = (RectTransform)contentItems[i].transform;
            rt.anchoredPosition = itemHomes[i] + new Vector2(Mathf.Lerp(itemSlideX, 0f, a), 0f);
        }
    }
}
