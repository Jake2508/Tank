using UnityEngine;

/// <summary>
/// Slides a rect in from an offset whenever it is enabled - the zone heading's
/// entrance on the level-select screen. Ease-out, unscaled time so it still
/// plays while the game is paused.
/// All offsets are in 960x540 canvas units.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIIntroSlide : MonoBehaviour
{
    [SerializeField] Vector2 from = new Vector2(-22.5f, 0f);
    [SerializeField] float duration = 0.35f;

    RectTransform rt;
    Vector2 home;
    float startedAt;
    bool homeCaptured;

    void Awake() { CaptureHome(); }

    void CaptureHome()
    {
        if (rt == null) rt = (RectTransform)transform;
        if (homeCaptured) return;
        home = rt.anchoredPosition;
        homeCaptured = true;
    }

    void OnEnable()
    {
        CaptureHome();
        startedAt = Time.unscaledTime;
        rt.anchoredPosition = home + from;
    }

    void Update()
    {
        if (duration <= 0f) { rt.anchoredPosition = home; return; }

        float t = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);          // cubic ease-out
        rt.anchoredPosition = Vector2.Lerp(home + from, home, eased);
    }
}
