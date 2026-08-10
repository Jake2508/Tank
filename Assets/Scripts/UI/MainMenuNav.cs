using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keyboard + mouse selection for the title-screen menu column.
/// Mouse hover and keyboard selection are the same visual state, so only one
/// slab is ever highlighted. Enter/Space fires the selected slab's existing Button.
/// </summary>
public class MainMenuNav : MonoBehaviour
{
    public static MainMenuNav Instance { get; private set; }

    [SerializeField] List<MenuSlab> slabs = new List<MenuSlab>();   // Play, Settings, Quit - visual order

    int index;

    void Awake() { Instance = this; }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        index = FirstInteractable(0, +1);
        Apply();
    }

    public void Select(MenuSlab slab)
    {
        int i = slabs.IndexOf(slab);
        if (i < 0 || !slabs[i].Interactable) return;
        index = i;
        Apply();
    }

    void Apply()
    {
        for (int i = 0; i < slabs.Count; i++)
            if (slabs[i]) slabs[i].SetSelected(i == index);
    }

    void Update()
    {
        if (!AnyInteractable()) return;

        // The menu can be locked out while the level-select popup is open; pick the
        // highlight back up when it comes home.
        var current = Current();
        if (current == null || !current.Interactable)
        {
            index = FirstInteractable(index, +1);
            Apply();
        }
        else if (!current.IsSelected)
        {
            Apply();
        }

        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Step(+1);
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) Step(-1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            var slab = Current();
            if (slab != null && slab.Interactable && slab.Button != null)
                slab.Button.onClick.Invoke();
        }
    }

    void Step(int d)
    {
        index = FirstInteractable(index + d, d);
        Apply();
    }

    MenuSlab Current() => index >= 0 && index < slabs.Count ? slabs[index] : null;

    bool AnyInteractable()
    {
        for (int i = 0; i < slabs.Count; i++)
            if (slabs[i] && slabs[i].Interactable) return true;
        return false;
    }

    /// <summary>Walks from <paramref name="start"/> in direction d to the next interactable slab.</summary>
    int FirstInteractable(int start, int d)
    {
        if (slabs.Count == 0) return 0;
        for (int step = 0; step < slabs.Count; step++)
        {
            int i = ((start + d * step) % slabs.Count + slabs.Count) % slabs.Count;
            if (slabs[i] && slabs[i].Interactable) return i;
        }
        return ((start % slabs.Count) + slabs.Count) % slabs.Count;
    }
}
