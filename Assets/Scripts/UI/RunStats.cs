using UnityEngine;

/// <summary>
/// A snapshot of the run, for the pause and end screens (MENUS_IMPLEMENTATION.md
/// section 6). Taken once when a menu opens rather than polled, so the numbers cannot
/// shift under a count-up animation.
/// </summary>
public struct RunStats
{
    /// <summary>Seconds survived. Held as a float so the end screen can count it up.</summary>
    public float time;

    public int kills;
    public int level;

    /// <summary>Four entries, 0-3, in the fixed upgrade order.</summary>
    public int[] tiers;

    public int bronze, silver, gold;

    public int Coins => bronze + silver + gold;

    /// <summary>mm:ss. Minutes are not clamped - a long run should read 12:04, not 02:04.</summary>
    public static string Clock(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int total = Mathf.FloorToInt(seconds);
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    /// <summary>
    /// Reads the run's managers. Each is optional: a scene opened on its own for a
    /// layout check should still bring a menu up rather than throw.
    /// </summary>
    public static RunStats Capture()
    {
        var stats = new RunStats { tiers = new int[4] };

        var timer = TimerManager.Instance;
        if (timer != null) stats.time = timer.GetMin() * 60f + timer.GetSec();

        var kills = KillCounter.Instance;
        if (kills != null) stats.kills = kills.GetKillCount();

        var level = Level.Instance;
        if (level != null)
        {
            // The raw level, matching the HUD's "LV n" chip. The old end screen showed
            // level - 1 as "levels gained", which read as an off-by-one next to the HUD.
            stats.level = level.GetLevel();
            stats.bronze = level.GetBronzeTotal();
            stats.silver = level.GetSilverTotal();
            stats.gold = level.GetGoldTotal();
        }

        var upgrades = UpgradeModel.Instance;
        if (upgrades != null) stats.tiers = upgrades.Snapshot();

        return stats;
    }
}
