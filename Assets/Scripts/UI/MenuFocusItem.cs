using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One focusable thing in a <see cref="MenuFocusGroup"/> - a pause button, a game-over
/// button or an upgrade card (MENUS_IMPLEMENTATION.md section 2).
///
/// The base owns a single 0-1 blend between the resting and focused looks, and
/// subclasses only have to draw a given blend. That is what keeps mouse and keyboard
/// from ever disagreeing: there is one focus value per item and one way to render it,
/// so hovering moves the highlight rather than adding a second one.
///
/// Everything ticks on unscaled time - gameplay is frozen whenever a menu is up.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public abstract class MenuFocusItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    /// <summary>Raised on Enter/Space/click/number key. Wiring lives in MenuController.</summary>
    public event Action Activated;

    [SerializeField] bool interactable = true;

    [Tooltip("Seconds to cross-fade between resting and focused. Pause uses 0.08, the " +
             "upgrade cards 0.12.")]
    [SerializeField] float focusSeconds = 0.1f;

    bool focused;
    float blend;
    bool dirty = true;

    RectTransform rt;

    internal MenuFocusGroup Group;

    public RectTransform Rect => rt != null ? rt : rt = (RectTransform)transform;
    public bool IsFocused => focused;

    public bool Interactable
    {
        get => interactable;
        set
        {
            if (interactable == value) return;
            interactable = value;
            if (!interactable && focused) focused = false;
            dirty = true;
        }
    }

    protected virtual void Awake()
    {
        rt = (RectTransform)transform;
    }

    /// <summary>Tween to the given focus state.</summary>
    public void SetFocused(bool on)
    {
        on = on && interactable;
        if (focused == on) return;
        focused = on;
        dirty = true;
    }

    /// <summary>
    /// Jump straight to a focus state with no tween - used when a menu opens, so the
    /// first frame already shows the correct item highlighted rather than easing into
    /// it from nothing.
    /// </summary>
    public void SnapFocus(bool on)
    {
        focused = on && interactable;
        blend = focused ? 1f : 0f;
        dirty = true;
    }

    public void Activate()
    {
        if (!interactable) return;
        Activated?.Invoke();
    }

    void Update()
    {
        // Subclasses hook in here rather than declaring their own Update: a second
        // Update() further down the hierarchy hides this one, and the focus tween
        // would silently stop running.
        Tick(Time.unscaledDeltaTime);

        float target = focused ? 1f : 0f;
        if (!Mathf.Approximately(blend, target))
        {
            blend = Mathf.MoveTowards(blend, target,
                                      Time.unscaledDeltaTime / Mathf.Max(0.0001f, focusSeconds));
            dirty = true;
        }

        if (!dirty) return;
        dirty = false;

        // Eased rather than linear: over 0.08-0.12s the curve is barely perceptible on
        // a colour, but it stops the card's 10px rise from looking mechanical.
        Render(Mathf.SmoothStep(0f, 1f, blend));
    }

    /// <summary>Draw the item at <paramref name="t"/>, 0 resting and 1 focused.</summary>
    protected abstract void Render(float t);

    /// <summary>
    /// Per-frame work that is not the focus tween - the upgrade card's tier-chip pulse
    /// uses this to keep asking for redraws while a chip is pulsing.
    /// </summary>
    protected virtual void Tick(float unscaledDeltaTime) { }

    /// <summary>Force a redraw next frame after changing something Render reads.</summary>
    protected void MarkDirty() => dirty = true;

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only: snap to a focus state *and* draw it. Update never runs in edit
    /// mode, so SnapFocus alone leaves a layout capture showing the resting look.
    /// </summary>
    public void EditorSnap(bool on)
    {
        SnapFocus(on);
        dirty = false;
        Render(on ? 1f : 0f);
    }
#endif

    // Hover moves the group's focus; it never lights an item on its own. Clicking
    // focuses first and then activates, so a click and a keyboard press take exactly
    // the same path.
    public void OnPointerEnter(PointerEventData e)
    {
        if (Group != null) Group.FocusOn(this);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (Group != null) Group.ClickOn(this);
    }
}
