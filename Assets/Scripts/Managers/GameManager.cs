using System;
using Tank;
using UnityEngine;


public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public event EventHandler OnStateChanged;
    public event EventHandler OnGamePaused;
    public event EventHandler OnGameResumed;
    public event EventHandler OnLevelUp;
    public event EventHandler OnLevelUpSelected;
    public event EventHandler OnDeath;
    public event EventHandler OnWin;
    
    public Transform playerTransform;
    public Camera mainCamera;
    public GameObject particleContainer;

    [SerializeField] private GameObject[] uiElements;
    [SerializeField] private TankController player;
    [Header("WINNER")]
    public int winTime = 10;

    private bool lowerRangedDamage= false;

    private State state = State.WaitingToStart;
    private bool isGamePaused = false;

    private float gamePlayingTimer;
    private float gamePlayingTimerMax;
    private float countdownToStartTimer;

    private bool upgradeMenuActive;
    private bool dead = false;

    /// <summary>
    /// Set by MenuController while the pause band is up. Escape is bound to the Pause
    /// axis *and* is the menus' back key, so without this one press would both open the
    /// band and immediately resume back through it.
    /// </summary>
    public bool MenuOwnsInput { get; set; }

    private enum State
    {
        WaitingToStart,
        CountdownToStart,
        GamePlaying,
        GameOver,
    }


    private void Awake()
    {
        // Newest wins, not first.
        //
        // This used to keep the first GameManager ever loaded and destroy every later
        // one, which broke any level loaded second in a session. The survivor kept
        // pointing playerTransform, player and mainCamera at objects from the previous
        // scene - Unity had destroyed them, so ObjectDispose threw a
        // MissingReferenceException every frame reading playerTransform.position.
        //
        // Worse, it also kept its run state. Dying in one level left `dead` true, and
        // TankController gates all movement on IsDead(), so the tank in the next level
        // simply never moved.
        //
        // Keeping the current scene's instance means the references and the state always
        // belong to the run being played.
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResetRunState();
    }

    /// <summary>
    /// Puts the manager back into a fresh-run condition. Also clears the timeScale,
    /// which a menu or a death screen may have left at zero when the scene changed.
    /// </summary>
    private void ResetRunState()
    {
        dead = false;
        isGamePaused = false;
        upgradeMenuActive = false;
        MenuOwnsInput = false;
        lowerRangedDamage = false;
        state = State.WaitingToStart;

        Time.timeScale = 1f;
    }

    private void Start()
    {
        Cursor.visible = false;

        // Guarded: a scene without a player - or one still spawning it - would otherwise
        // take the manager down on load.
        if (TankInputs.Instance != null)
            TankInputs.Instance.OnPauseAction += Instance_OnPauseAction;
    }

    private void Instance_OnPauseAction(object sender, EventArgs e)
    {
        if(upgradeMenuActive || dead || MenuOwnsInput)
        {
            return;
        }
        else
        {
            TogglePauseGame();
        }

    }

    private void Update()
    {
        switch (state)
        {
            case State.WaitingToStart:
                break;
            case State.CountdownToStart:
                countdownToStartTimer -= Time.deltaTime;
                if (countdownToStartTimer < 0f)
                {
                    state = State.GamePlaying;
                    gamePlayingTimer = gamePlayingTimerMax;
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
                break;
            case State.GamePlaying:
                gamePlayingTimer -= Time.deltaTime;
                if (gamePlayingTimer < 0f)
                {
                    state = State.GameOver;

                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
                break;
            case State.GameOver:
                break;
        }

    }


    public bool IsGamePlaying()
    {
        return state == State.GamePlaying;
    }

    public bool IsGameOver()
    {
        return state == State.GameOver;
    }

    public bool IsDead()
    {
        return dead;
    }
    public bool IsGamePaused()
    {
        return isGamePaused;
    }


    public void TogglePauseGame()
    {
        isGamePaused = !isGamePaused;
        if (isGamePaused)
        {
            Cursor.visible = true;
            Time.timeScale = 0f;
            OnGamePaused?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Cursor.visible = false;
            Time.timeScale = 1f;
            OnGameResumed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ToggleUpgradePause()
    {
        isGamePaused = !isGamePaused;
        if (isGamePaused)
        {
            Cursor.visible = true;
            upgradeMenuActive = true;
            Time.timeScale = 0f;
            OnLevelUp?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Cursor.visible = false;
            upgradeMenuActive = false;
            Time.timeScale = 1f;
            OnLevelUpSelected?.Invoke(this, EventArgs.Empty);

        }
    }

    public void Death()
    {
        dead = true;
        Cursor.visible = true;
        OnDeath?.Invoke(this, EventArgs.Empty);

        // Disable UI Elements
        foreach (GameObject ui in uiElements)
        {
            ui.SetActive(false);    
        }
    }

    public void  ApplyArmorUpgrade(int tier)
    {
        switch(tier)
        {
            case 1:
                player.IncreaseArmour();
                break;
            case 2:
                lowerRangedDamage = true;
                break;
            case 3:
                player.ArmorRegen();
                break;
        }
    }

    public void ApplyTurretUpgrade(int tier)
    {
        switch (tier)
        {
            case 1:
                player.ReduceReload();
                break;
            case 2:
                player.IncreaseTurretRotation();
                break;
            case 3:
                player.ImproveTurretShell();
                break;
        }
    }

    public void ApplySpeedUpgrade(int tier)
    {
        switch (tier)
        {
            case 1:
                player.IncreaseRotationSpeed();
                break;
            case 2:
                player.IncreaseSpeed();
                break;
            case 3:
                player.HyperSpeed();
                break;
        }
    }

    public void ApplyDemolitionUpgrade(int tier)
    {
        switch (tier)
        {
            case 1:
                Magnet.Instance.IncreasePickupRange();
                break;
            case 2:
                Magnet.Instance.IncreasePickupStrength();
                break;
            case 3:
                Magnet.Instance.MaxForce();
                break;
        }
    }

    public bool reduceRangedDamage()
    {
        return lowerRangedDamage;
    }
    public bool CanFire()
    {
        return player.CanFireP();
    }

    public void CheckWinCondition()
    {
        int currentMin = TimerManager.Instance.GetMin();
        if(currentMin == winTime)
        {
            // Win UI
            OnWin?.Invoke(this, EventArgs.Empty);
            Cursor.visible = true;
        }
    }

    public void ChestOpenedAddHealth()
    {
        player.Heal(6);
    }
}
