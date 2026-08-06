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

    const float Speed = 1f / 0.13f;
    const float ChevronTravel = 4.5f;
    const float ChevronPeriod = 1.1f;

    RectTransform rt;
    Vector2 homePos;
    Vector2 chevronHome;
    bool selected, pressed;

    public Button Button => button;
    public bool Interactable => button == null || button.interactable;
    public bool IsSelected => selected;

    void Awake()
    {
        rt = (RectTransform)transform;
        homePos = rt.anchoredPosition;

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

        if (chevron && selected)
        {
            float ping = Mathf.PingPong(Time.unscaledTime / ChevronPeriod * 2f, 1f);
            chevron.anchoredPosition = chevronHome + Vector2.right * (ping * ChevronTravel);
        }
    }
}
