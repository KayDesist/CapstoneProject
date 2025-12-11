using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;

public class EndGameManager : NetworkBehaviour
{
    public static EndGameManager Instance;

    [Header("Game Settings")]
    [SerializeField] private float endGameDelay = 5f;

    private NetworkVariable<bool> isGameEnded = new NetworkVariable<bool>(false);
    private NetworkVariable<GameResult> gameResult = new NetworkVariable<GameResult>();

    private NetworkVariable<int> survivorDeaths = new NetworkVariable<int>(0);
    private NetworkVariable<int> cultistDeaths = new NetworkVariable<int>(0);
    private NetworkVariable<bool> survivorTasksComplete = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> cultistTasksComplete = new NetworkVariable<bool>(false);

    private Dictionary<ulong, bool> playerDeathStates = new Dictionary<ulong, bool>();
    private Dictionary<ulong, RoleManager.PlayerRole> playerRoles = new Dictionary<ulong, RoleManager.PlayerRole>();

    private float lastCheckTime = 0f;
    private float checkInterval = 1f;
    private float lastHeartbeatTime = 0f;
    private float heartbeatInterval = 3f;

    public event System.Action OnGameEnded;

    public enum GameResult
    {
        None,
        SurvivorsWinByTasks,
        SurvivorsWinByKill,
        CultistsWinByTasksAndKill,
        CultistsWinByElimination
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitializeGameState();
        }

        isGameEnded.OnValueChanged += OnGameEndedChanged;
        gameResult.OnValueChanged += OnGameResultChanged;
    }

    private void Update()
    {
        if (!IsServer || isGameEnded.Value) return;

        CheckWinConditions();

        if (Time.time - lastHeartbeatTime >= heartbeatInterval)
        {
            SendHeartbeatClientRpc();
            lastHeartbeatTime = Time.time;
        }

        if (Time.time - lastCheckTime >= checkInterval)
        {
            lastCheckTime = Time.time;
        }
    }

    [ClientRpc]
    private void SendHeartbeatClientRpc()
    {
    }

    private void OnGameEndedChanged(bool oldValue, bool newValue)
    {
    }

    private void OnGameResultChanged(GameResult oldValue, GameResult newValue)
    {
        if (newValue != GameResult.None)
        {
            ShowEndGameUI();
        }
    }

    // Display end game UI
    private void ShowEndGameUI()
    {
        if (EndGameUI.Instance != null)
        {
            EndGameUI.Instance.ShowEndGameScreen(gameResult.Value);
        }
        else
        {
            var endGameUI = FindObjectOfType<EndGameUI>();
            if (endGameUI != null)
            {
                endGameUI.ShowEndGameScreen(gameResult.Value);
            }
            else
            {
                CreateEndGameUIForDeadPlayers();
            }
        }

        if (GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.gameObject.SetActive(true);
            GameHUDManager.Instance.ResetHUD();
        }
    }

    // Create UI for dead players
    private void CreateEndGameUIForDeadPlayers()
    {
        GameObject endGameUIPrefab = Resources.Load<GameObject>("EndGameUI");
        if (endGameUIPrefab != null)
        {
            GameObject endGameUIObject = Instantiate(endGameUIPrefab);
            EndGameUI endGameUI = endGameUIObject.GetComponent<EndGameUI>();
            if (endGameUI != null)
            {
                endGameUI.ShowEndGameScreen(gameResult.Value);
            }
        }
    }

    // Check win conditions
    private void CheckWinConditions()
    {
        if (isGameEnded.Value)
        {
            return;
        }

        int totalSurvivors = 0;
        int totalCultists = 0;
        int aliveSurvivors = 0;
        int aliveCultists = 0;

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            RoleManager.PlayerRole role = RoleManager.PlayerRole.Survivor;

            if (RoleManager.Instance != null)
            {
                role = RoleManager.Instance.GetPlayerRole(clientId);
            }

            bool isDead = playerDeathStates.ContainsKey(clientId) && playerDeathStates[clientId];

            if (role == RoleManager.PlayerRole.Survivor)
            {
                totalSurvivors++;
                if (!isDead) aliveSurvivors++;
            }
            else if (role == RoleManager.PlayerRole.Cultist)
            {
                totalCultists++;
                if (!isDead) aliveCultists++;
            }
        }

        if (aliveSurvivors == 0 && totalSurvivors > 0)
        {
            EndGame(GameResult.CultistsWinByElimination);
            return;
        }

        if (aliveCultists == 0 && totalCultists > 0)
        {
            EndGame(GameResult.SurvivorsWinByKill);
            return;
        }

        if (survivorTasksComplete.Value)
        {
            EndGame(GameResult.SurvivorsWinByTasks);
            return;
        }

        if (cultistTasksComplete.Value && survivorDeaths.Value >= 1)
        {
            EndGame(GameResult.CultistsWinByTasksAndKill);
            return;
        }
    }

    // Initialize game state
    private void InitializeGameState()
    {
        Invoke(nameof(DelayedInitialize), 1f);
    }

    private void DelayedInitialize()
    {
        if (!IsServer) return;

        playerDeathStates.Clear();
        playerRoles.Clear();

        if (RoleManager.Instance != null)
        {
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var role = RoleManager.Instance.GetPlayerRole(clientId);
                playerRoles[clientId] = role;
                playerDeathStates[clientId] = false;
            }
        }
    }

    // Handle player death
    public void OnPlayerDied(ulong clientId, RoleManager.PlayerRole role)
    {
        if (!IsServer || isGameEnded.Value) return;

        if (playerDeathStates.ContainsKey(clientId))
        {
            playerDeathStates[clientId] = true;

            if (role == RoleManager.PlayerRole.Survivor)
            {
                survivorDeaths.Value++;
            }
            else if (role == RoleManager.PlayerRole.Cultist)
            {
                cultistDeaths.Value++;
            }
        }

        CheckWinConditions();
    }

    // Handle task completion
    public void OnTasksCompleted(RoleManager.PlayerRole role)
    {
        if (!IsServer || isGameEnded.Value) return;

        if (role == RoleManager.PlayerRole.Survivor)
        {
            survivorTasksComplete.Value = true;
        }
        else if (role == RoleManager.PlayerRole.Cultist)
        {
            cultistTasksComplete.Value = true;
        }

        CheckWinConditions();
    }

    // End the game
    private void EndGame(GameResult result)
    {
        if (!IsServer || isGameEnded.Value) return;

        isGameEnded.Value = true;
        gameResult.Value = result;

        OnGameEnded?.Invoke();
        EndGameClientRpc(result);
    }

    [ClientRpc]
    private void EndGameClientRpc(GameResult result)
    {
        ShowEndGameUI();
    }

    // Return to main menu
    public void ReturnToMainMenu()
    {
        CrossSceneData.Reset();
        NetworkManager.Singleton.SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }

    // Handle client disconnection
    public void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer || isGameEnded.Value) return;

        if (playerRoles.ContainsKey(clientId))
        {
            var role = playerRoles[clientId];
            playerRoles.Remove(clientId);
            playerDeathStates.Remove(clientId);

            if (role == RoleManager.PlayerRole.Survivor)
            {
                survivorDeaths.Value++;
            }
            else if (role == RoleManager.PlayerRole.Cultist)
            {
                cultistDeaths.Value++;
            }
        }

        CheckWinConditions();
    }

    // Register player
    public void RegisterPlayer(ulong clientId, RoleManager.PlayerRole role)
    {
        if (!IsServer) return;

        if (!playerRoles.ContainsKey(clientId))
        {
            playerRoles[clientId] = role;
            playerDeathStates[clientId] = false;
        }
    }

    // Reset static instance
    public static void ResetInstance()
    {
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (isGameEnded != null)
            isGameEnded.OnValueChanged -= OnGameEndedChanged;

        if (gameResult != null)
            gameResult.OnValueChanged -= OnGameResultChanged;
    }
}