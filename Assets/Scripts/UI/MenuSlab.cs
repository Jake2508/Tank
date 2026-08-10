using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One skewed slab in the title-screen menu column. Drives its own hover/press
/// visuals; the Button on the same object still owns the click behaviour.
/// All offsets are in 960x540 canvas units.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MenuSlab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] Button button;
    [SerializeField] RectTransform shadow;
    [SerializeField] Image body;
    [SerializeField] RectTransform chevron;

    [Tooltip("For slabs outside the title-screen column (the level-select Back button): " +
             "highlight on hover instead of asking MainMenuNav to move its selection.")]
    [SerializeField] bool selfHover;

    [Header("Body tint")]
    [SerializeField] Color idleTint = new Color32(31, 23, 18, 235);
    [SerializeField] Color hoverTint = new Color32(44, 33, 26, 245);

    [Header("Slab offset")]
    [SerializeField] Vector2 hoverOffset = new Vector2(5f, 2.5f);
    [SerializeField] Vector2 pressOffset = new Vector2(5f, -1.5f);

    [Header("Shadow offset")]
    [SerializeField] Vector2 shadowIdle = new Vector2(7f, -7f);
    [SerializeField] Vector2 shadowHover = new Vector2(10.5f, -11f);
    [SerializeField] Vector2 shadowPress = new Vector2(3.5f, -3.5f);

    [Header("Availability")]
    [Tooltip("Alpha for a slab that is unavailable on this platform. Low enough to read " +
             "as switched off next to a live one, high enough that the label is still " +
             "legible - a button you cannot read is a button you cannot rule out.")]
    [Range(0.2f, 1f)]
    [SerializeField] float unavailableAlpha = 0.42f;

    const float Speed = 1f / 0.13f;
    const float ChevronTravel = 4.5f;
    const float ChevronPeriod = 1.1f;

    RectTransform rt;
    CanvasGroup group;
    Vector2 homePos;
    Vector2 chevronHome;
    bool selected, pressed;
    bool available = true;

    public Button Button => button;

    /// <summary>
    /// Two different kinds of "off", kept apart on purpose.
    ///
    /// The Button's own interactable flag is the temporary one - the whole column is
    /// switched off while the level-select popup owns the screen, and greying the menu
    /// out every time that happened would read as the menu breaking.
    ///
    /// Available is the permanent one: this slab does nothing on this platform and is
    /// drawn dimmed to say so.
    /// </summary>
    public bool Interactable => available && (button == null || button.interactable);

    public bool Available => available;
    public bool IsSelected => selected;

    /// <summary>Greys the slab out and takes it out of the keyboard rotation for good.</summary>
    public void SetAvailable(bool on)
    {
        available = on;
        if (!on) SetSelected(false);

        // Awake has not run in edit mode, so there is no Update to lerp the alpha.
        // Setting it outright means an editor capture shows the real state.
        if (group != null) return;

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = on ? 1f : unavailableAlpha;
    }

    void Awake()
    {
        rt = (RectTransform)transform;
        homePos = rt.anchoredPosition;

        // One group rather than a reference to every graphic on the slab: the body,
        // keyline, label, shadow and key pill all have to dim together, and listing
        // them would break the moment the builder adds another child.
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        if (shadow) shadow.anchoredPosition = shadowIdle;
        if (body) body.color = idleTint;
        if (chevron)
        {
            chevronHome = chevron.anchoredPosition;
            chevron.gameObject.SetActive(false);
        }
    }

    public void SetSelected(bool on)
    {
        selected = on && Interactable;
        if (!selected) pressed = false;
        if (chevron) chevron.gameObject.SetActive(selected);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (!Interactable) return;
        if (selfHover) { SetSelected(true); return; }
        if (MainMenuNav.Instance) MainMenuNav.Instance.Select(this);
    }

    public void OnPointerExit(PointerEventData e)
    {
        // Slabs in a nav column keep their highlight until the selection moves;
        // a standalone slab has nothing to hand it to.
        if (!selfHover) return;
        SetSelected(false);
        pressed = false;
    }

    public void OnPointerDown(PointerEventData e) => pressed = Interactable;
    public void OnPointerUp(PointerEventData e) => pressed = false;

    void Update()
    {
        if (selected && !Interactable) SetSelected(false);

        Vector2 target = homePos;
        Vector2 shadowTarget = shadowIdle;

        if (selected)
        {
            target += pressed ? pressOffset : hoverOffset;
            shadowTarget = pressed ? shadowPress : shadowHover;
        }

        float t = Mathf.Clamp01(Time.unscaledDeltaTime * Speed);
        rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, t);
        if (shadow) shadow.anchoredPosition = Vector2.Lerp(shadow.anchoredPosition, shadowTarget, t);
        if (body) body.color = Color.Lerp(body.color, selected ? hoverTint : idleTint, t);

        if (group) group.alpha = Mathf.Lerp(group.alpha, available ? 1f : unavailableAlpha, t);

        if (chevron && selected)
        {
            float ping = Mathf.PingPong(Time.unscaledTime / ChevronPeriod * 2f, 1f);
            chevron.anchoredPosition = chevronHome + Vector2.right * (ping * ChevronTravel);
        }
    }
}
