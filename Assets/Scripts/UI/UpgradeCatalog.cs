using UnityEngine;

/// <summary>
/// Display names and per-tier copy for the four upgrades.
///
/// The old screen only wrote a description *after* a tier was bought, and the strings
/// had drifted a tier out of step with what GameManager actually applied - buying
/// Armour I called IncreaseArmour() but printed the ranged-resistance line, which is
/// tier II. The cards have to say what you get *before* you commit, so this table is
/// written straight off GameManager.Apply*Upgrade and TankController.
///
/// Kept under ~60 characters each: the card's description column is 164px, and Saira
/// Condensed 17/20 gets three lines out of that and no more.
/// </summary>
public static class UpgradeCatalog
{
    /// <summary>
    /// Fixed order, everywhere in the game: Armour, Turret, Speed, Demolition.
    /// Players learn positions, so this never gets reordered or shuffled.
    /// </summary>
    public static readonly UpgradeType[] Order =
    {
        UpgradeType.Armor, UpgradeType.Turret, UpgradeType.Tracks, UpgradeType.Magnet,
    };

    public const int MaxTier = 3;

    /// <summary>
    /// The enum predates the design language: Tracks is shown as SPEED and Magnet as
    /// DEMOLITION. Renaming the enum would churn every scene reference for nothing, so
    /// the mapping lives here instead.
    /// </summary>
    public static string DisplayName(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.Armor:  return "ARMOUR";
            case UpgradeType.Turret: return "TURRET";
            case UpgradeType.Tracks: return "SPEED";
            case UpgradeType.Magnet: return "DEMOLITION";
            default:                 return type.ToString().ToUpperInvariant();
        }
    }

    static readonly string[] Armour =
    {
        "Plate the hull. Every hit lands one point softer.",
        "Angled plating. Incoming shellfire does less damage.",
        "Field repairs. The hull mends itself as you drive.",
    };

    static readonly string[] Turret =
    {
        "Autoloader. Cuts the reload to a third of a second.",
        "Servo drive. The turret swings onto target faster.",
        "Heavy shells. A bigger round with a wider blast.",
    };

    static readonly string[] Speed =
    {
        "Wider tracks. The tank comes around much faster.",
        "Tuned engine. Raises your top speed across the map.",
        "Overdrive. Maximum speed and the sharpest turning yet.",
    };

    static readonly string[] Demolition =
    {
        "Salvage magnet. Pulls pickups in from further out.",
        "Stronger field. Pickups come to you faster.",
        "Full draw. Maximum pull range and force.",
    };

    const string Maxed = "Fully upgraded. Nothing left to take here.";

    /// <summary>
    /// What the player gets if they take this card now. <paramref name="ownedTier"/> is
    /// how many tiers they already hold, so index 0 describes tier I.
    /// </summary>
    public static string NextDescription(UpgradeType type, int ownedTier)
    {
        var table = Table(type);
        if (table == null) return string.Empty;
        if (ownedTier < 0) ownedTier = 0;
        return ownedTier >= table.Length ? Maxed : table[ownedTier];
    }

    static string[] Table(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.Armor:  return Armour;
            case UpgradeType.Turret: return Turret;
            case UpgradeType.Tracks: return Speed;
            case UpgradeType.Magnet: return Demolition;
            default:                 return null;
        }
    }

    /// <summary>Position of a type in the fixed order, which is also its number key.</summary>
    public static int IndexOf(UpgradeType type)
    {
        for (int i = 0; i < Order.Length; i++)
            if (Order[i] == type) return i;
        return 0;
    }
}
