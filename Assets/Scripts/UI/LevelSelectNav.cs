using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keyboard + mouse selection for the level-select card row.
/// Mouse hover and keyboard selection are the same visual state, so exactly one
/// card is ever highlighted. The row is a difficulty ladder, so stepping does
/// not wrap. LevelSelectUI owns what Confirm/Back actually do.
/// </summary>
public class LevelSelectNav : MonoBehaviour
{
    public static LevelSelectNav Instance { get; private set; }

    [SerializeField] List<LevelCard> cards = new List<LevelCard>();   // Woodlands, Desert, Snow - visual order

    int index;
    bool open;
    int openedFrame = -1;

    void Awake() { Instance = this; }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public LevelCard Current => index >= 0 && index < cards.Count ? cards[index] : null;

    /// <summary>Takes input and highlights the first card. Called by LevelSelectUI.Show.</summary>
    public void Open()
    {
        open = true;
        openedFrame = Time.frameCount;
        index = 0;
        Apply();
    }

    public void Close() { open = false; }

    public void Select(LevelCard card)
    {
        if (!open) return;
        int i = cards.IndexOf(card);
        if (i < 0) return;
        index = i;
        Apply();
    }

    public void Confirm()
    {
        if (!open) return;
        var card = Current;
        if (card == null) return;
        card.Punch();
        if (LevelSelectUI.instance) LevelSelectUI.instance.Deploy(card);
    }

    void Apply()
    {
        for (int i = 0; i < cards.Count; i++)
            if (cards[i]) cards[i].SetSelected(i == index);
    }

    void Update()
    {
        if (!open) return;

        // The title screen fires PLAY on Enter, and GetKeyDown stays true for the
        // rest of that frame - so without this the same press would fall straight
        // through to Confirm and deploy the first level on sight.
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Step(+1);
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            Confirm();

        if (Input.GetKeyDown(KeyCode.Escape) && LevelSelectUI.instance)
            LevelSelectUI.instance.Back();
    }

    void Step(int d)
    {
        if (cards.Count == 0) return;
        index = Mathf.Clamp(index + d, 0, cards.Count - 1);   // no wrap: the row is a ladder
        Apply();
    }
}
