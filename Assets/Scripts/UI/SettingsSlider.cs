using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One volume row on the settings screen: label, value, trough, fill and knob, wrapped
/// around a Unity <see cref="Slider"/> so mouse dragging comes for free.
///
/// A MenuFocusItem so it lives in the same focus group as the BACK button and obeys the
/// same rule the rest of the UI does - hover moves the one highlight rather than adding
/// a second. Focus here is the knob going full-bright and the label to full alpha;
/// nothing moves, matching the pause screen's buttons.
///
/// Left/right adjust by 5 with hold-to-repeat, because reaching a specific number with
/// a keyboard one step per press is miserable.
/// </summary>
public class SettingsSlider : MenuFocusItem
{
    [Header("Refs")]
    [SerializeField] Slider slider;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] TextMeshProUGUI value;
    [SerializeField] RectTransform fill;
    [SerializeField] RectTransform knob;
    [SerializeField] Image knobImage;
    [SerializeField] Image troughKeyline;

    [Header("Keyboard")]
    [Tooltip("Change per key press, and per repeat tick.")]
    [SerializeField] int step = 5;

    [Tooltip("Seconds held before the value starts repeating.")]
    [SerializeField] float repeatDelay = 0.4f;

    [Tooltip("Repeats per second once it starts.")]
    [SerializeField] float repeatRate = 12f;

    /// <summary>Fired on every change, including drags - drives the live audio.</summary>
    public event Action<int> Changed;

    /// <summary>Fired when a drag or a key repeat finishes, for the preview blip.</summary>
    public event Action Settled;

    float heldSince = float.NegativeInfinity;
    float nextRepeat;
    int lastValue = -1;
    bool adjusting;

    public int Value => slider != null ? Mathf.RoundToInt(slider.value) : 0;

    protected override void Awake()
    {
        base.Awake();

        if (slider != null)
        {
            slider.wholeNumbers = true;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        Render(0f);
    }

    public void SetLabel(string text)
    {
        if (label != null) label.text = text;
    }

    /// <summary>Sets the value without raising Changed - used when loading saved prefs.</summary>
    public void SetValueSilent(int v)
    {
        if (slider == null) return;

        slider.SetValueWithoutNotify(Mathf.Clamp(v, 0, 100));
        lastValue = Value;
        Redraw();
    }

    void OnSliderChanged(float v)
    {
        int rounded = Mathf.RoundToInt(v);
        if (rounded == lastValue) return;

        lastValue = rounded;
        Redraw();
        Changed?.Invoke(rounded);
    }

    void Adjust(int delta)
    {
        if (slider == null) return;
        slider.value = Mathf.Clamp(Value + delta, 0, 100);
    }

    protected override void Tick(float dt)
    {
        if (!IsFocused || !Interactable) { adjusting = false; return; }

        int direction = 0;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) direction = 1;
        else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) direction = -1;

        if (direction == 0)
        {
            // Let go: this is the moment to play the preview, not every frame of a drag.
            if (adjusting) { adjusting = false; Settled?.Invoke(); }
            heldSince = float.NegativeInfinity;
            return;
        }

        // First press moves immediately; the repeat only starts after the delay, so a
        // tap is always exactly one step.
        if (float.IsNegativeInfinity(heldSince))
        {
            heldSince = Time.unscaledTime;
            nextRepeat = heldSince + repeatDelay;
            adjusting = true;
            Adjust(direction * step);
            return;
        }

        if (Time.unscaledTime < nextRepeat) return;

        nextRepeat = Time.unscaledTime + 1f / Mathf.Max(1f, repeatRate);
        Adjust(direction * step);
    }

    /// <summary>
    /// Only the numeral. The fill and the knob belong to Unity's Slider, which drives
    /// their anchors from the value - positioning them here as well would fight it every
    /// frame, and the two would disagree the moment the slider was dragged.
    /// </summary>
    void Redraw()
    {
        MarkDirty();
        if (value != null) value.text = Value.ToString();
    }

    /// <summary>
    /// Clicking a slider adjusts it; it does not confirm anything. The shared activate
    /// click belongs to buttons, and firing it here would put a confirm sound on every
    /// drag of the volume you are trying to hear.
    /// </summary>
    protected override bool PlaysActivateSound => false;

    protected override void Render(float t)
    {
        if (label != null)
            label.color = Color.Lerp(MenuPalette.StatLabel, MenuPalette.Cream, t);

        if (knobImage != null)
            knobImage.color = Color.Lerp(MenuPalette.At(MenuPalette.Cream, 200), MenuPalette.Cream, t);

        if (troughKeyline != null)
            troughKeyline.color = Color.Lerp(MenuPalette.IconPlateBorder, MenuPalette.Cream, t);
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: pose a value and focus state for a layout capture.</summary>
    public void EditorPose(string text, int v, bool focused)
    {
        if (label != null) label.text = text;

        if (slider != null)
        {
            slider.wholeNumbers = true;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.SetValueWithoutNotify(Mathf.Clamp(v, 0, 100));
        }

        lastValue = v;
        Redraw();
        EditorSnap(focused);
    }
#endif
}
