using UnityEngine;

/// <summary>
/// Keeps the controls plate on screen and clear of the band at any aspect ratio.
///
/// The design places it at a fixed X +448 with a width of 430, which needs 878 units of
/// canvas. At 16:9 there are 960 and it fits with room to spare; at 4:3 the scaler
/// leaves about 830, and a fixed X would push the plate's right edge off the screen.
///
/// So it anchors to the right edge instead of a fixed X, and shrinks only if the band
/// would otherwise be underneath it. At 16:9 that produces exactly the specified
/// position, and narrower ratios degrade rather than clip.
/// </summary>
[ExecuteAlways]
public class PlateFit : MonoBehaviour
{
    [SerializeField] RectTransform plate;

    [Tooltip("Width at 16:9, where the design's fixed position and this agree.")]
    [SerializeField] float designWidth = 430f;

    [Tooltip("Gap from the right edge. 960 - (448 + 430) at the reference resolution.")]
    [SerializeField] float rightMargin = 82f;

    [Tooltip("The plate's left edge may not come closer than this to the canvas left, " +
             "which is where the band's slanted edge reaches at its widest.")]
    [SerializeField] float bandClearance = 330f;

    [Tooltip("Never shrink below this - past it the 9px sub-labels stop being readable " +
             "and hiding the plate would be better than squeezing it.")]
    [SerializeField] float minWidth = 300f;

    RectTransform canvasRect;
    float lastCanvasWidth = -1f;

    void OnEnable() => Fit();

    void LateUpdate()
    {
        // Only recompute when the canvas actually changes size; this runs every frame
        // and the layout is otherwise static.
        float width = CanvasWidth();
        if (Mathf.Approximately(width, lastCanvasWidth)) return;

        lastCanvasWidth = width;
        Fit();
    }

    float CanvasWidth()
    {
        if (canvasRect == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvasRect = canvas.transform as RectTransform;
        }

        return canvasRect != null ? canvasRect.rect.width : 0f;
    }

    /// <summary>
    /// Public so the screen can force it the moment it opens. Relying on LateUpdate
    /// alone leaves the plate a frame stale, and in an editor render - where LateUpdate
    /// may never tick at all - it never runs.
    /// </summary>
    public void Fit()
    {
        if (plate == null) return;

        float canvas = CanvasWidth();
        if (canvas <= 0f) return;

        // Room between the band's reach and the right margin.
        float available = canvas - bandClearance - rightMargin;
        float width = Mathf.Clamp(Mathf.Min(designWidth, available), minWidth, designWidth);

        plate.anchorMin = new Vector2(1f, 1f);
        plate.anchorMax = new Vector2(1f, 1f);
        plate.pivot = new Vector2(1f, 1f);
        plate.sizeDelta = new Vector2(width, plate.sizeDelta.y);

        var position = plate.anchoredPosition;
        plate.anchoredPosition = new Vector2(-rightMargin, position.y);
    }
}
