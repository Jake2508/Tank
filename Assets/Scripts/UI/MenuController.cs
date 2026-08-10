using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the four screens that sit on top of gameplay (MENUS_IMPLEMENTATION.md
/// section 6): upgrade, pause, game over and the win.
///
/// The screens draw and animate; this routes game events to them, takes the run
/// snapshot, and owns the three things that must live in exactly one place - the
/// timeScale, the cursor, and the HUD's alpha. Each screen deactivates its own root
/// when hidden, so nothing polls in the background.
/// </summary>
public class MenuController : MonoBehaviour
{
    public static MenuController Instance { get; private set; }

    [Header("Screens")]
    [SerializeField] UpgradeScreen upgradeScreen;
    [SerializeField] PauseScreen pauseScreen;
    [SerializeField] EndScreen endScreen;

    [Header("Model")]
    [SerializeField] UpgradeModel upgrades;

    [Header("Scene")]
    [SerializeField] LevelLoaderNew levelLoader;

    const int MainMenuBuildIndex = 0;

    /// <summary>HUD alpha while a menu is up - a quarter for the two mid-run screens.</summary>
    const float HudDim = 0.25f;

    void Awake()
    {
        Instance = this;

        if (upgrades == null) upgrades = GetComponentInChildren<UpgradeModel>(true);
        if (levelLoader == null) levelLoader = FindObjectOfType<LevelLoaderNew>();
    }

    void Start()
    {
        var game = GameManager.Instance;
        if (game != null)
        {
            game.OnLevelUp += OnLevelUp;
            game.OnGamePaused += OnPaused;
            game.OnGameResumed += OnResumed;
            game.OnDeath += OnDeath;
            game.OnWin += OnWin;
        }

        if (upgradeScreen != null)
        {
            upgradeScreen.Chosen += OnUpgradeChosen;
            upgradeScreen.Closed += OnUpgradeClosed;
            upgradeScreen.Hide();
        }

        if (pauseScreen != null)
        {
            pauseScreen.Resumed += OnPauseResumed;
            pauseScreen.Restarted += RestartRun;
            pauseScreen.MenuRequested += GoToMainMenu;
            pauseScreen.Hide();
        }

        if (endScreen != null)
        {
            endScreen.Primary += OnEndPrimary;
            endScreen.MenuRequested += GoToMainMenu;
            endScreen.Hide();
        }
    }

    void OnDestroy()
    {
        // GameManager survives scene loads, so leaving these attached would keep
        // invoking a controller that has already been destroyed.
        var game = GameManager.Instance;
        if (game != null)
        {
            game.OnLevelUp -= OnLevelUp;
            game.OnGamePaused -= OnPaused;
            game.OnGameResumed -= OnResumed;
            game.OnDeath -= OnDeath;
            game.OnWin -= OnWin;

            game.MenuOwnsInput = false;
        }

        if (Instance == this) Instance = null;
    }

    // =========================================================================
    // Upgrade
    // =========================================================================

    void OnLevelUp(object sender, System.EventArgs e)
    {
        Cursor.visible = true;
        DimHud(HudDim);
        if (upgradeScreen != null) upgradeScreen.Show(upgrades);
    }

    void OnUpgradeChosen(UpgradeType type)
    {
        if (upgrades == null) return;

        int tier = upgrades.Take(type);
        if (tier <= 0) return;

        // The slot punch and pip flash already live in HudController.
        var hud = HudController.Instance;
        if (hud != null) hud.SetUpgrade(type, tier);
    }

    void OnUpgradeClosed()
    {
        // Gameplay resumes only once the cards have finished leaving; ToggleUpgradePause
        // is what restores timeScale and clears the upgrade lockout in GameManager.
        var game = GameManager.Instance;
        if (game != null) game.ToggleUpgradePause();

        DimHud(1f);
    }

    // =========================================================================
    // Pause
    // =========================================================================

    void OnPaused(object sender, System.EventArgs e)
    {
        Cursor.visible = true;
        DimHud(HudDim);

        // Escape is bound to the Pause axis as well as being this menu's back key, so
        // without this the same press would both open the band and resume through it.
        var game = GameManager.Instance;
        if (game != null) game.MenuOwnsInput = true;

        if (pauseScreen != null) pauseScreen.Show(RunStats.Capture());
    }

    void OnResumed(object sender, System.EventArgs e)
    {
        DimHud(1f);

        var game = GameManager.Instance;
        if (game != null) game.MenuOwnsInput = false;
    }

    /// <summary>The band has finished sliding out; hand the clock back.</summary>
    void OnPauseResumed()
    {
        var game = GameManager.Instance;
        if (game != null && game.IsGamePaused()) game.TogglePauseGame();
    }

    // =========================================================================
    // End of run
    // =========================================================================

    void OnDeath(object sender, System.EventArgs e)
    {
        Cursor.visible = true;

        // Zero, not a quarter: the run is finished and the hull bar is noise.
        var hud = HudController.Instance;
        if (hud != null) hud.SetHidden(true);

        var music = MusicDirector.Instance;
        if (music != null) music.SetDucked(true);

        Time.timeScale = 0f;
        if (endScreen != null) endScreen.ShowGameOver(RunStats.Capture());
    }

    void OnWin(object sender, System.EventArgs e)
    {
        Cursor.visible = true;

        var hud = HudController.Instance;
        if (hud != null) hud.SetHidden(true);

        var music = MusicDirector.Instance;
        if (music != null) music.SetDucked(true);

        Time.timeScale = 0f;
        if (endScreen != null) endScreen.ShowWin(RunStats.Capture());
    }

    /// <summary>RETRY after a death, CONTINUE after a win - the same button slot.</summary>
    void OnEndPrimary()
    {
        var game = GameManager.Instance;
        if (game != null && game.IsDead())
        {
            RestartRun();
            return;
        }

        // Won, and chose to keep playing: drop the screen and give the clock back.
        Cursor.visible = false;
        Time.timeScale = 1f;

        var hud = HudController.Instance;
        if (hud != null) hud.SetHidden(false);

        var music = MusicDirector.Instance;
        if (music != null) music.SetDucked(false);
    }

    // =========================================================================
    // Scene changes
    // =========================================================================

    void RestartRun()
    {
        Load(SceneManager.GetActiveScene().buildIndex);
    }

    void GoToMainMenu()
    {
        Load(MainMenuBuildIndex);
    }

    void Load(int buildIndex)
    {
        if (levelLoader == null) levelLoader = FindObjectOfType<LevelLoaderNew>();

        if (levelLoader != null)
        {
            // LoadNextLevel restores timeScale before it starts its wipe, which matters
            // because the transition animation would otherwise be frozen too.
            levelLoader.LoadNextLevel(buildIndex);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    static void DimHud(float alpha)
    {
        var hud = HudController.Instance;
        if (hud != null) hud.SetDimmed(alpha < 1f);

        // The music drops under any menu and comes back with gameplay, so a screen that
        // freezes the game does not leave the track competing with it.
        var music = MusicDirector.Instance;
        if (music != null) music.SetDucked(alpha < 1f);
    }
}
