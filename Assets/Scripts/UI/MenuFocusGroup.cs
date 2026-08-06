using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Keyboard and mouse selection for one menu (MENUS_IMPLEMENTATION.md section 2).
/// All three menus use this - only the axis, the starting item and whether number
/// keys are live differ.
///
/// The rule the whole thing exists to enforce: there is exactly one focused item at
/// any moment, and hovering with the mouse moves that focus rather than adding a
/// second highlight. Clicking focuses and then activates, so a mouse click and an
/// Enter press run identical code.
///
/// Runs on unscaled time; gameplay is at timeScale 0 whenever this is up.
/// </summary>
public class MenuFocusGroup : MonoBehaviour
{
    [Tooltip("In visual order - left to right, or top to bottom.")]
    [SerializeField] List<MenuFocusItem> items = new List<MenuFocusItem>();

    [Tooltip("Cards sit in a row; pause and game-over buttons in a column.")]
    [SerializeField] bool horizontal = true;

    [Tooltip("Upgrade screen only: 1-4 pick the matching card outright.")]
    [SerializeField] bool numberKeys;

    [Tooltip("Off means the ends stop rather than wrapping. Consistent either way, " +
             "which is what the QA list actually asks for.")]
    [SerializeField] bool wrap = true;

    /// <summary>Esc. Resume on pause; nothing is subscribed on the other two.</summary>
    public event Action Back;

    int index;
    bool open;
    float inputUnlocksAt;

    public int Index => index;
    public bool IsOpen => open;
    public IReadOnlyList<MenuFocusItem> Items => items;

    void Awake()
    {
        Bind();
    }

    /// <summary>
    /// Called by the builder as well as Awake, so an item list assembled in the editor
    /// is wired without needing play mode.
    /// </summary>
    public void Bind()
    {
        foreach (var item in items)
            if (item != null) item.Group = this;
    }

    public void SetItems(IEnumerable<MenuFocusItem> next)
    {
        items.Clear();
        items.AddRange(next);
        Bind();
    }

    /// <summary>
    /// Focus <paramref name="startIndex"/> and start accepting input after
    /// <paramref name="inputLockSeconds"/>. Game over locks for a second so a player
    /// mashing fire cannot skip their own summary.
    /// </summary>
    public void Open(int startIndex, float inputLockSeconds = 0f)
    {
        open = true;
        inputUnlocksAt = Time.unscaledTime + inputLockSeconds;

        index = Nearest(startIndex);
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null) items[i].SnapFocus(i == index);

        PushToEventSystem();
    }

    public void Close()
    {
        open = false;
        foreach (var item in items)
            if (item != null) item.SnapFocus(false);
    }

    void Update()
    {
        if (!open || Time.unscaledTime < inputUnlocksAt) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Back?.Invoke();
            return;
        }

        // A card can max out while the screen is open in the editor, and a button can
        // be switched off; never leave the highlight parked on something dead.
        var current = Current();
        if (current == null || !current.Interactable) Step(+1);

        if (horizontal)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(+1);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Step(+1);
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Step(-1);
        }

        if (numberKeys)
        {
            for (int i = 0; i < items.Count && i < 9; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i)) continue;
                if (items[i] == null || !items[i].Interactable) continue;
                SetIndex(i);
                items[i].Activate();
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            var item = Current();
            if (item != null && item.Interactable) item.Activate();
        }
    }

    public void SetIndex(int next)
    {
        next = Mathf.Clamp(next, 0, Mathf.Max(0, items.Count - 1));
        if (next == index && Current() != null && Current().IsFocused) return;

        index = next;
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null) items[i].SetFocused(i == index);

        PushToEventSystem();
    }

    /// <summary>Mouse hover. Moves the one highlight; it does not create another.</summary>
    public void FocusOn(MenuFocusItem item)
    {
        if (!open || Time.unscaledTime < inputUnlocksAt) return;

        int i = items.IndexOf(item);
        if (i < 0 || items[i] == null || !items[i].Interactable) return;
        SetIndex(i);
    }

    public void ClickOn(MenuFocusItem item)
    {
        if (!open || Time.unscaledTime < inputUnlocksAt) return;

        int i = items.IndexOf(item);
        if (i < 0 || items[i] == null || !items[i].Interactable) return;

        SetIndex(i);
        items[i].Activate();
    }

    void Step(int direction)
    {
        int next = NextInteractable(index + direction, direction);
        if (next >= 0) SetIndex(next);
    }

    /// <summary>
    /// Walks from <paramref name="start"/> in <paramref name="direction"/> to the next
    /// interactable item, honouring the wrap setting. -1 if nothing is selectable.
    /// </summary>
    int NextInteractable(int start, int direction)
    {
        if (items.Count == 0) return -1;

        for (int step = 0; step < items.Count; step++)
        {
            int i = start + direction * step;

            if (wrap) i = ((i % items.Count) + items.Count) % items.Count;
            else if (i < 0 || i >= items.Count) return -1;

            if (items[i] != null && items[i].Interactable) return i;
        }
        return -1;
    }

    /// <summary>The requested index if it is usable, otherwise the closest that is.</summary>
    int Nearest(int wanted)
    {
        wanted = Mathf.Clamp(wanted, 0, Mathf.Max(0, items.Count - 1));
        if (wanted < items.Count && items[wanted] != null && items[wanted].Interactable) return wanted;

        int forward = NextInteractable(wanted, +1);
        return forward >= 0 ? forward : Mathf.Max(0, wanted);
    }

    MenuFocusItem Current() => index >= 0 && index < items.Count ? items[index] : null;

    /// <summary>
    /// Keeps Unity's own selection in step with ours, so adding gamepad support later
    /// is a matter of switching the input source rather than rewriting this.
    /// </summary>
    void PushToEventSystem()
    {
        var system = EventSystem.current;
        var item = Current();
        if (system == null || item == null) return;

        system.SetSelectedGameObject(item.gameObject);
    }
}
