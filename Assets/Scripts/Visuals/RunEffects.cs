using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The non-UI half of the old GameOverUI and WinnerUI: the colour-grading swing and
/// the player's explosion.
///
/// Those two screens were doing real work alongside their layout, so retiring their
/// hierarchies would have quietly taken the desaturate-on-death effect with them.
/// The builder lifts the Volume and explosion references off the old components
/// before deleting them and re-hosts them here.
/// </summary>
public class RunEffects : MonoBehaviour
{
    [Tooltip("The scene's global post-processing Volume.")]
    [SerializeField] GameObject globalVolumeGO;

    [Tooltip("Spawned at the player's position on death.")]
    [SerializeField] GameObject characterExplosion;

    const float DeathSaturation = -100f;
    const float WinSaturation = 100f;

    void Start()
    {
        var game = GameManager.Instance;
        if (game == null) return;

        game.OnDeath += OnDeath;
        game.OnWin += OnWin;
    }

    void OnDestroy()
    {
        var game = GameManager.Instance;
        if (game == null) return;

        game.OnDeath -= OnDeath;
        game.OnWin -= OnWin;
    }

    void OnDeath(object sender, System.EventArgs e)
    {
        SetSaturation(DeathSaturation);

        if (characterExplosion == null) return;

        var player = GameManager.Instance != null ? GameManager.Instance.playerTransform : null;
        if (player != null)
            Instantiate(characterExplosion, player.position, Quaternion.identity);
    }

    void OnWin(object sender, System.EventArgs e)
    {
        SetSaturation(WinSaturation);
    }

    void SetSaturation(float value)
    {
        if (globalVolumeGO == null) return;

        var volume = globalVolumeGO.GetComponent<Volume>();
        if (volume == null || volume.profile == null) return;

        if (volume.profile.TryGet(out ColorAdjustments adjustments))
            adjustments.saturation.value = value;
    }

#if UNITY_EDITOR
    /// <summary>Builder hook - carries the references over from the retired screens.</summary>
    public void EditorBind(GameObject volume, GameObject explosion)
    {
        if (volume != null) globalVolumeGO = volume;
        if (explosion != null) characterExplosion = explosion;
    }
#endif
}
