using System;
using UnityEngine;

/// <summary>
/// What the player has taken, and what taking another tier does. This is the half of
/// the old UpgradeUI that was not actually UI - the tier counters and the calls into
/// GameManager - lifted out so the new card screen can ask "what tier am I on?"
/// before it draws, which the old screen never needed to do.
///
/// Nothing here knows about sprites or layout. UpgradeScreen renders, this decides.
/// </summary>
public class UpgradeModel : MonoBehaviour
{
    public static UpgradeModel Instance { get; private set; }

    /// <summary>Raised after a tier is applied, with the type and its new tier.</summary>
    public event Action<UpgradeType, int> Taken;

    readonly int[] tiers = new int[4];

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int Tier(UpgradeType type)
    {
        int i = UpgradeCatalog.IndexOf(type);
        return i >= 0 && i < tiers.Length ? tiers[i] : 0;
    }

    public bool IsMaxed(UpgradeType type) => Tier(type) >= UpgradeCatalog.MaxTier;

    /// <summary>A copy, so nothing outside can quietly rewrite the run's build.</summary>
    public int[] Snapshot() => (int[])tiers.Clone();

    /// <summary>
    /// Applies the next tier of <paramref name="type"/> and reports the new level.
    /// Returns 0 if the type was already maxed, in which case nothing was applied.
    /// </summary>
    public int Take(UpgradeType type)
    {
        if (IsMaxed(type)) return 0;

        int i = UpgradeCatalog.IndexOf(type);
        int next = ++tiers[i];

        Apply(type, next);
        Taken?.Invoke(type, next);
        return next;
    }

    /// <summary>
    /// The effect table. GameManager owns what each tier actually does to the tank;
    /// this only picks which one to call.
    /// </summary>
    static void Apply(UpgradeType type, int tier)
    {
        var game = GameManager.Instance;
        if (game == null) return;

        switch (type)
        {
            case UpgradeType.Armor:
                // Tier 3 was commented out on the old screen, so armour quietly stopped
                // paying out at the top of its track. It is wired up here.
                game.ApplyArmorUpgrade(tier);
                break;
            case UpgradeType.Turret:
                game.ApplyTurretUpgrade(tier);
                break;
            case UpgradeType.Tracks:
                game.ApplySpeedUpgrade(tier);
                break;
            case UpgradeType.Magnet:
                game.ApplyDemolitionUpgrade(tier);
                break;
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: force a build for a layout capture.</summary>
    public void EditorPose(int[] values)
    {
        for (int i = 0; i < tiers.Length && values != null && i < values.Length; i++)
            tiers[i] = Mathf.Clamp(values[i], 0, UpgradeCatalog.MaxTier);
    }
#endif
}
