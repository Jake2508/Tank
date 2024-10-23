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

    private enum State
    {
        WaitingToStart,
        CountdownToStart,
        GamePlaying,
        GameOver,
    }


    private void Awake()
    {
        // Ensures there's only one instance of GameManager.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); 
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        Cursor.visible = false;
        TankInputs.Instance.OnPauseAction += Instance_OnPauseAction;
    }

    private void Instance_OnPauseAction(object sender, EventArgs e)
    {
        if(upgradeMenuActive || dead)
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
