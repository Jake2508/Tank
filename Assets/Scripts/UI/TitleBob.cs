using UnityEngine;

/// <summary>
/// Slow vertical ping-pong for the title lockup. Runs in LateUpdate so it wins
/// against the Animator, which otherwise pins the transform after MainMenuFade ends.
/// </summary>
public class TitleBob : MonoBehaviour
{
    [SerializeField] float amplitude = 2.5f;
    [SerializeField] float period = 5f;

    RectTransform rt;
    Vector2 home;

    void Awake()
    {
        rt = (RectTransform)transform;
        home = rt.anchoredPosition;
    }

    void LateUpdate()
    {
        rt.anchoredPosition = home + Vector2.up * (Mathf.Sin(Time.unscaledTime / period * Mathf.PI * 2f) * amplitude);
    }
}
