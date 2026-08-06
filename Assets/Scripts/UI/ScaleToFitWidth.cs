using UnityEngine;

/// <summary>
/// Shrinks a UI block uniformly when the canvas is too narrow to show it at full
/// size. The Canvas Scaler is height-locked (Match 1) so the title screen's
/// column can never collide with its hint, but that leaves canvas *width* free to
/// fall below 960 units: the level-select card row reaches x 913, so anything
/// narrower than roughly 1.7:1 would push the last card off screen.
///
/// Scales about the rect's own pivot and never scales past 1, so at 16:9 and
/// wider this is a no-op. All numbers are 960x540 canvas units.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ScaleToFitWidth : MonoBehaviour
{
    [Tooltip("Right-most edge of the content inside this rect, in canvas units.")]
    [SerializeField] float contentRight = 913f;

    [Tooltip("Clear space to keep to the right of the content, in canvas units.")]
    [SerializeField] float rightMargin = 20f;

    [Tooltip("Floor on the scale, so a very narrow window shrinks the cards rather than erasing them.")]
    [SerializeField] float minScale = 0.5f;

    RectTransform rt;
    RectTransform canvasRect;

    void OnEnable() { Apply(); }

    void Update() { Apply(); }

    /// <summary>Public so an editor capture can settle the layout without waiting for a tick.</summary>
    public void Apply()
    {
        if (rt == null) rt = (RectTransform)transform;
        if (canvasRect == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas) canvasRect = (RectTransform)canvas.rootCanvas.transform;
        }
        if (canvasRect == null || contentRight <= 0f) return;

        float available = canvasRect.rect.width - rightMargin;
        float scale = Mathf.Clamp(available / contentRight, minScale, 1f);

        // Only write when it actually moves - an [ExecuteAlways] component that
        // assigns every frame would keep the scene permanently dirty in the editor.
        if (!Mathf.Approximately(rt.localScale.x, scale))
            rt.localScale = new Vector3(scale, scale, 1f);
    }
}
