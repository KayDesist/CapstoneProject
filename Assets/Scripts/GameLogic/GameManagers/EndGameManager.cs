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

    // Track deaths
    private NetworkVariable<int> survivorDeaths = new NetworkVariable<int>(0);
    private NetworkVariable<int> cultistDeaths = new NetworkVariable<int>(0);
    private NetworkVariable<bool> survivorTasksComplete = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> cultistTasksComplete = new NetworkVariable<bool>(false);

    // Track player states
    private Dictionary<ulong, bool> playerDeathStates = new Dictionary<ulong, bool>();
    private Dictionary<ulong, RoleManager.PlayerRole> playerRoles = new Dictionary<ulong, RoleManager.PlayerRole>();

    // For Update-based checking
    private float lastCheckTime = 0f;
    private float checkInterval = 1f;

    // Heartbeat for Relay
    private float lastHeartbeatTime = 0f;
    private float heartbeatInterval = 3f;

    // Game end event
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

        // Subscribe to changes - THIS IS CRITICAL FOR CLIENTS
        isGameEnded.OnValueChanged += OnGameEndedChanged;
        gameResult.OnValueChanged += OnGameResultChanged;

        Debug.Log($"EndGameManager spawned for {(IsServer ? "Server" : "Client")}");
    }

    private void Update()
    {
        // Only check win conditions on server and if game hasn't ended
        if (!IsServer || isGameEnded.Value) return;

        // Check win conditions
        CheckWinConditions();

        // Send heartbeat to keep Relay connection alive
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
        // Empty RPC - just keeps connection alive
    }

    private void OnGameEndedChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"GameEnded changed: {oldValue} -> {newValue}");
    }

    private void OnGameResultChanged(GameResult oldValue, GameResult newValue)
    {
        Debug.Log($"GameResult changed: {oldValue} -> {newValue} (Client: {!IsServer})");

        // Show UI immediately when result changes - THIS FIXES CLIENT UI
        if (newValue != GameResult.None)
        {
            // Show UI with a small delay to ensure it's ready
            Invoke(nameof(ShowEndGameUI), 0.5f);
            DisableAllPlayerControls();
        }
    }

    private void ShowEndGameUI()
    {
        if (EndGameUI.Instance != null)
        {
            EndGameUI.Instance.ShowEndGameScreen(gameResult.Value);
        }
        else
        {
            Debug.LogWarning("EndGameUI.Instance is null, trying to find it in scene...");
            // Try to find it if it hasn't been initialized yet
            var endGameUI = FindObjectOfType<EndGameUI>();
            if (endGameUI != null)
            {
                endGameUI.ShowEndGameScreen(gameResult.Value);
            }
        }
    }

    private void DisableAllPlayerControls()
    {
        // Find all players and disable their controls
        var players = FindObjectsOfType<NetworkPlayerController>();
        foreach (var player in players)
        {
            if (player.enabled && player.IsOwner)
            {
                player.enabled = false;

                // Also disable PlayerSpectator if it exists
                var spectator = player.GetComponent<PlayerSpectator>();
                if (spectator != null)
                {
                    spectator.enabled = false;
                }
            }
        }

        // Ensure cursor is visible for all local players
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

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

        // Count players and their status
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

        // WIN CONDITION CHECKS

        // 1. All survivors dead (cultists win by elimination)
        if (aliveSurvivors == 0 && totalSurvivors > 0)
        {
            Debug.Log("🎯 WIN CONDITION: Cultists win by eliminating all survivors!");
            EndGame(GameResult.CultistsWinByElimination);
            return;
        }

        // 2. All cultists dead (survivors win by kill)
        if (aliveCultists == 0 && totalCultists > 0)
        {
            Debug.Log("🎯 WIN CONDITION: Survivors win by killing all cultists!");
            EndGame(GameResult.SurvivorsWinByKill);
            return;
        }

        // 3. Survivor tasks complete
        if (survivorTasksComplete.Value)
        {
            Debug.Log("🎯 WIN CONDITION: Survivors win by tasks!");
            EndGame(GameResult.SurvivorsWinByTasks);
            return;
        }

        // 4. Cultist tasks complete (with at least one kill)
        if (cultistTasksComplete.Value && survivorDeaths.Value >= 1)
        {
            Debug.Log("🎯 WIN CONDITION: Cultists win by tasks and at least one kill!");
            EndGame(GameResult.CultistsWinByTasksAndKill);
            return;
        }
    }

    private void InitializeGameState()
    {
        Debug.Log("Initializing game state in EndGameManager");
        Invoke(nameof(DelayedInitialize), 1f);
    }

    private void DelayedInitialize()
    {
        if (!IsServer) return;

        playerDeathStates.Clear();
        playerRoles.Clear();

        // Initialize player tracking
        if (RoleManager.Instance != null)
        {
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var role = RoleManager.Instance.GetPlayerRole(clientId);
                playerRoles[clientId] = role;
                playerDeathStates[clientId] = false;
            }
        }

        Debug.Log($"EndGameManager initialized with {playerDeathStates.Count} players");
    }

    // Called when a player dies
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

    // Called when tasks are completed
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

    private void EndGame(GameResult result)
    {
        if (!IsServer || isGameEnded.Value) return;

        isGameEnded.Value = true;
        gameResult.Value = result;

        Debug.Log($"🎮 GAME ENDED: {result} 🎮");
        OnGameEnded?.Invoke();

        // Notify all clients
        EndGameClientRpc(result);
    }

    [ClientRpc]
    private void EndGameClientRpc(GameResult result)
    {
        Debug.Log($"Client received end game notification: {result}");

        // The NetworkVariable change will trigger OnGameResultChanged
        // which will show the UI
    }

    // NEW: Method to return to Main Menu (called by UI)
    public void ReturnToMainMenu()
    {
        if (!IsServer) return;

        Debug.Log("Returning to Main Menu...");

        // Notify all clients
        ReturnToMainMenuClientRpc();

        // Small delay before loading scene
        Invoke(nameof(LoadMainMenuScene), 1f);
    }

    [ClientRpc]
    private void ReturnToMainMenuClientRpc()
    {
        Debug.Log("Server is returning everyone to Main Menu...");
    }

    private void LoadMainMenuScene()
    {
        if (!IsServer) return;

        // Clean up cross-scene data
        CrossSceneData.Reset();

        // Load Main Menu scene for all clients
        NetworkManager.Singleton.SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }

    // Handle client disconnection
    public void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer || isGameEnded.Value) return;

        Debug.Log($"Client {clientId} disconnected - checking if game should end");

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

    // Method to manually add a player
    public void RegisterPlayer(ulong clientId, RoleManager.PlayerRole role)
    {
        if (!IsServer) return;

        if (!playerRoles.ContainsKey(clientId))
        {
            playerRoles[clientId] = role;
            playerDeathStates[clientId] = false;
            Debug.Log($"Registered player {clientId} with role {role}");
        }
    }

    // Reset static instance
    public static void ResetInstance()
    {
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
            Debug.Log("EndGameManager instance reset");
        }
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from changes
        if (isGameEnded != null)
            isGameEnded.OnValueChanged -= OnGameEndedChanged;

        if (gameResult != null)
            gameResult.OnValueChanged -= OnGameResultChanged;
    }
}