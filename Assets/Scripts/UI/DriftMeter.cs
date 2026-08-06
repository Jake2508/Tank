using UnityEngine;

/// <summary>
/// Feeds the HUD's drift readout.
///
/// There is no drift state machine in the game yet, so this derives drift the only
/// place it actually exists: the angle between where the tank is pointing and where
/// it is travelling. Read-only - it looks at the player's Rigidbody and changes
/// nothing about how the tank handles.
///
/// When a real drift mechanic lands, delete this and call
/// <see cref="HudController.SetDrift"/> from it instead.
/// </summary>
public class DriftMeter : MonoBehaviour
{
    [SerializeField] Rigidbody player;

    [Header("Detection")]
    [Tooltip("Below this speed the tank is manoeuvring, not sliding.")]
    [SerializeField] float minSpeed = 4f;
    [Tooltip("Degrees between heading and travel before it counts as a slide.")]
    [SerializeField] float minSlipAngle = 18f;

    [Header("Meter")]
    [Tooltip("Seconds of sustained slide to fill all three pips.")]
    [SerializeField] float chargeTime = 1.6f;
    [Tooltip("Seconds for the meter to empty once the slide stops.")]
    [SerializeField] float decayTime = 1.1f;

    float charge;

    void Start()
    {
        if (player != null) return;

        var tank = GameManager.Instance != null ? GameManager.Instance.playerTransform : null;
        if (tank != null) player = tank.GetComponent<Rigidbody>();
    }

    void Update()
    {
        var hud = HudController.Instance;
        if (hud == null) return;

        bool drifting = IsDrifting();
        charge = Mathf.Clamp01(charge + Time.deltaTime / (drifting ? chargeTime : -decayTime));
        hud.SetDrift(drifting, charge);
    }

    bool IsDrifting()
    {
        if (player == null) return false;

        // Not IsGamePlaying: that state is never entered in this build, so it would
        // hold the meter at zero for the whole run.
        var game = GameManager.Instance;
        if (game != null && (game.IsGamePaused() || game.IsDead())) return false;

        var travel = player.velocity;
        travel.y = 0f;
        if (travel.magnitude < minSpeed) return false;

        var heading = player.transform.forward;
        heading.y = 0f;

        // Reversing is not drifting, so fold the angle into the first quadrant pair.
        float slip = Vector3.Angle(heading, travel);
        if (slip > 90f) slip = 180f - slip;

        return slip >= minSlipAngle;
    }
}
