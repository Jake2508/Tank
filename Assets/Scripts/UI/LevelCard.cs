using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One level card on the level-select screen. Drives its own hover/selected
/// visuals; LevelSelectNav decides which card is selected and LevelSelectUI
/// owns the scene load.
/// All offsets are in 960x540 canvas units.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class LevelCard : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Data")]
    [SerializeField] int buildIndex = 2;        // scene build index, handed to LevelLoaderNew

    [Header("Refs")]
    [SerializeField] Image thumbKeyline;
    [SerializeField] Image plateBody;
    [SerializeField] Image badgeKeyline;
    [SerializeField] TMPro.TextMeshProUGUI tierWord;

    static readonly Color Cream = new Color32(244, 238, 220, 255);
    static readonly Color CreamDim = new Color32(244, 238, 220, 140);
    static readonly Color CreamMid = new Color32(244, 238, 220, 180);
    static readonly Color Terracotta = new Color32(196, 85, 47, 255);
    static readonly Color InkPlate = new Color32(31, 23, 18, 240);
    static readonly Color InkText = new Color32(28, 20, 16, 205);

    const float Speed = 1f / 0.14f;
    const float HoverLift = 7.5f;
    const float PressLift = 3f;
    const float PunchLift = 12f;
    const float PunchTime = 0.08f;
    const float PressDim = 0.9f;
    const float IntroRise = 14f;

    RectTransform rt;
    CanvasGroup group;
    Vector2 home;
    bool selected, pressed;
    float punchUntil;
    float introAt;          // unscaled time this card starts its entrance

    public int BuildIndex => buildIndex;

    void Awake()
    {
        rt = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        home = rt.anchoredPosition;
        Apply(true);
    }

    /// <summary>
    /// Drops the card below its resting spot and fades it out, so the shared
    /// lerp in Apply carries it home. Staggered left-to-right by the caller.
    /// </summary>
    public void PlayIntro(float delay)
    {
        introAt = Time.unscaledTime + delay;
        rt.anchoredPosition = home - Vector2.up * IntroRise;
        if (group) group.alpha = 0f;
    }

    /// <summary>Re-reads the resting position. Call after the row is repositioned.</summary>
    public void CaptureHome()
    {
        if (rt == null) rt = (RectTransform)transform;
        home = rt.anchoredPosition;
    }

    public void SetSelected(bool on)
    {
        selected = on;
        if (!selected) pressed = false;
    }

    /// <summary>Stamps the card down hard on confirm - reads as a punch and covers the load hitch.</summary>
    public void Punch()
    {
        punchUntil = Time.unscaledTime + PunchTime;
        rt.anchoredPosition = home + Vector2.up * PunchLift;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (LevelSelectNav.Instance) LevelSelectNav.Instance.Select(this);
    }

    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;

    public void OnPointerClick(PointerEventData e)
    {
        if (LevelSelectNav.Instance) LevelSelectNav.Instance.Confirm();
    }

    void OnEnable() { Apply(true); }

    void Update() { Apply(false); }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only: snap straight to a resting or selected pose so a layout capture
    /// doesn't have to reproduce the palette by hand. The caller restores whatever
    /// it changed.
    /// </summary>
    public void EditorPose(bool on)
    {
        rt = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        home = rt.anchoredPosition;
        selected = on;
        pressed = false;
        punchUntil = 0f;
        introAt = 0f;
        Apply(true);
    }
#endif

    void Apply(bool instant)
    {
        // Held below its resting spot until this card's turn in the stagger.
        if (Time.unscaledTime < introAt) return;

        float t = instant ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime * Speed);

        if (group && group.alpha < 1f)
            group.alpha = instant ? 1f : Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime / 0.2f);

        if (Time.unscaledTime < punchUntil)
        {
            rt.anchoredPosition = home + Vector2.up * PunchLift;
        }
        else
        {
            float lift = selected ? (pressed ? PressLift : HoverLift) : 0f;
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, home + Vector2.up * lift, t);
        }

        Color plate = selected ? Terracotta : InkPlate;
        if (selected && pressed) plate = new Color(plate.r * PressDim, plate.g * PressDim, plate.b * PressDim, plate.a);

        if (thumbKeyline) thumbKeyline.color = Color.Lerp(thumbKeyline.color, selected ? Cream : CreamDim, t);
        if (badgeKeyline) badgeKeyline.color = Color.Lerp(badgeKeyline.color, selected ? Cream : CreamMid, t);
        if (plateBody) plateBody.color = Color.Lerp(plateBody.color, plate, t);
        if (tierWord) tierWord.color = Color.Lerp(tierWord.color, selected ? InkText : CreamDim, t);
    }
}
