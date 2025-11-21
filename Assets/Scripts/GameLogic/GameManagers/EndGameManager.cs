using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class EndGameManager : NetworkBehaviour
{
    public static EndGameManager Instance;

    [Header("Game Settings")]
    [SerializeField] private float endGameDelay = 5f;

    private NetworkVariable<bool> isGameEnded = new NetworkVariable<bool>(false);
    private NetworkVariable<GameResult> gameResult = new NetworkVariable<GameResult>();

    // Track deaths - using NetworkVariable for proper sync
    private NetworkVariable<int> survivorDeaths = new NetworkVariable<int>(0);
    private NetworkVariable<int> cultistDeaths = new NetworkVariable<int>(0);
    private NetworkVariable<bool> survivorTasksComplete = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> cultistTasksComplete = new NetworkVariable<bool>(false);

    // Track player states
    private Dictionary<ulong, bool> playerDeathStates = new Dictionary<ulong, bool>();
    private Dictionary<ulong, RoleManager.PlayerRole> playerRoles = new Dictionary<ulong, RoleManager.PlayerRole>();

    // For Update-based checking
    private float lastCheckTime = 0f;
    private float checkInterval = 1f; // Check every second

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

        // Subscribe to changes
        isGameEnded.OnValueChanged += OnGameEndedChanged;
        gameResult.OnValueChanged += OnGameResultChanged;
        survivorDeaths.OnValueChanged += OnSurvivorDeathsChanged;
        cultistDeaths.OnValueChanged += OnCultistDeathsChanged;
        survivorTasksComplete.OnValueChanged += OnSurvivorTasksCompleteChanged;
        cultistTasksComplete.OnValueChanged += OnCultistTasksCompleteChanged;

        Debug.Log("EndGameManager spawned and ready");
    }

    private void Update()
    {
        // Only check win conditions on server and if game hasn't ended
        if (!IsServer || isGameEnded.Value) return;

        CheckWinConditions();
    }

    private void InitializeGameState()
    {
        Debug.Log("Initializing game state in EndGameManager");

        // Wait a moment for RoleManager to be ready
        Invoke(nameof(DelayedInitialize), 1f);
    }

    private void DelayedInitialize()
    {
        if (!IsServer) return;

        playerDeathStates.Clear();
        playerRoles.Clear();

        // Initialize player tracking
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            playerDeathStates[clientId] = false;

            // Get role from RoleManager with null check
            if (RoleManager.Instance != null)
            {
                var role = RoleManager.Instance.GetPlayerRole(clientId);
                playerRoles[clientId] = role;
                Debug.Log($"Initialized player {clientId} with role: {role}");
            }
            else
            {
                Debug.LogWarning($"RoleManager.Instance is null! Defaulting to Survivor for client {clientId}");
                playerRoles[clientId] = RoleManager.PlayerRole.Survivor;
            }
        }

        Debug.Log($"EndGameManager initialized with {playerDeathStates.Count} players");
    }

    // Called when a player dies
    public void OnPlayerDied(ulong clientId, RoleManager.PlayerRole role)
    {
        if (!IsServer || isGameEnded.Value)
        {
            Debug.Log($"OnPlayerDied called but server={IsServer}, gameEnded={isGameEnded.Value}");
            return;
        }

        Debug.Log($"Player {clientId} died with role: {role}");

        if (playerDeathStates.ContainsKey(clientId))
        {
            playerDeathStates[clientId] = true;
            playerRoles[clientId] = role; // Update role

            if (role == RoleManager.PlayerRole.Survivor)
            {
                survivorDeaths.Value++;
                Debug.Log($"Survivor {clientId} died. Total survivor deaths: {survivorDeaths.Value}");
            }
            else if (role == RoleManager.PlayerRole.Cultist)
            {
                cultistDeaths.Value++;
                Debug.Log($"Cultist {clientId} died. Total cultist deaths: {cultistDeaths.Value}");
            }
        }
        else
        {
            Debug.LogWarning($"Player {clientId} not found in death states dictionary");
        }

        // Force immediate win condition check when someone dies
        CheckWinConditions();
    }

    // Called when tasks are completed
    public void OnTasksCompleted(RoleManager.PlayerRole role)
    {
        if (!IsServer || isGameEnded.Value)
        {
            Debug.Log($"OnTasksCompleted called but server={IsServer}, gameEnded={isGameEnded.Value}");
            return;
        }

        Debug.Log($"Tasks completed for role: {role}");

        if (role == RoleManager.PlayerRole.Survivor)
        {
            survivorTasksComplete.Value = true;
            Debug.Log("ALL SURVIVOR TASKS COMPLETED! Setting survivorTasksComplete to true");
        }
        else if (role == RoleManager.PlayerRole.Cultist)
        {
            cultistTasksComplete.Value = true;
            Debug.Log("ALL CULTIST TASKS COMPLETED! Setting cultistTasksComplete to true");
        }

        // Force immediate win condition check when tasks complete
        CheckWinConditions();
    }

    private void CheckWinConditions()
    {
        if (isGameEnded.Value)
        {
            return;
        }

        Debug.Log("=== CHECKING WIN CONDITIONS ===");

        int totalSurvivors = 0;
        int totalCultists = 0;
        int aliveSurvivors = 0;
        int aliveCultists = 0;

        // Count players and their status
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (playerRoles.TryGetValue(clientId, out RoleManager.PlayerRole role))
            {
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
            else
            {
                Debug.LogWarning($"Player {clientId} not found in roles dictionary");
            }
        }

        Debug.Log($"Player Status - Survivors: {aliveSurvivors}/{totalSurvivors} alive, Cultists: {aliveCultists}/{totalCultists} alive");
        Debug.Log($"Tasks - Survivor: {survivorTasksComplete.Value}, Cultist: {cultistTasksComplete.Value}");
        Debug.Log($"Deaths - Survivors: {survivorDeaths.Value}, Cultists: {cultistDeaths.Value}");

        // Survivor win conditions
        if (survivorTasksComplete.Value)
        {
            Debug.Log("WIN CONDITION: Survivors win by tasks!");
            EndGame(GameResult.SurvivorsWinByTasks);
            return;
        }

        // Check if all cultists are dead
        if (aliveCultists == 0 && totalCultists > 0)
        {
            Debug.Log("WIN CONDITION: Survivors win by killing all cultists!");
            EndGame(GameResult.SurvivorsWinByKill);
            return;
        }

        // Cultist win conditions
        if (cultistTasksComplete.Value && survivorDeaths.Value >= 1)
        {
            Debug.Log("WIN CONDITION: Cultists win by tasks and at least one kill!");
            EndGame(GameResult.CultistsWinByTasksAndKill);
            return;
        }

        // Check if all survivors are dead
        if (aliveSurvivors == 0 && totalSurvivors > 0)
        {
            Debug.Log("WIN CONDITION: Cultists win by eliminating all survivors!");
            EndGame(GameResult.CultistsWinByElimination);
            return;
        }
    }

    private void EndGame(GameResult result)
    {
        if (!IsServer || isGameEnded.Value) return;

        isGameEnded.Value = true;
        gameResult.Value = result;

        Debug.Log($"🎮 GAME ENDED: {result} 🎮");

        // Notify all clients
        EndGameClientRpc(result);

        // Return to main menu after delay
        Invoke(nameof(ReturnToMainMenu), endGameDelay);
    }

    [ClientRpc]
    private void EndGameClientRpc(GameResult result)
    {
        Debug.Log($"Client received end game notification: {result}");

        // Show end game UI on all clients
        if (EndGameUI.Instance != null)
        {
            EndGameUI.Instance.ShowEndGameScreen(result);
        }
        else
        {
            Debug.LogError("EndGameUI.Instance is null! Cannot show end game screen.");
        }
    }

    private void ReturnToMainMenu()
    {
        if (IsServer)
        {
            Debug.Log("Returning to main menu...");
            // Load main menu scene
            NetworkManager.Singleton.SceneManager.LoadScene("MainMenu", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    // NetworkVariable change handlers for debugging
    private void OnGameEndedChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"GameEnded changed: {oldValue} -> {newValue}");
    }

    private void OnGameResultChanged(GameResult oldValue, GameResult newValue)
    {
        Debug.Log($"GameResult changed: {oldValue} -> {newValue}");
    }

    private void OnSurvivorDeathsChanged(int oldValue, int newValue)
    {
        Debug.Log($"SurvivorDeaths changed: {oldValue} -> {newValue}");
        if (IsServer) CheckWinConditions();
    }

    private void OnCultistDeathsChanged(int oldValue, int newValue)
    {
        Debug.Log($"CultistDeaths changed: {oldValue} -> {newValue}");
        if (IsServer) CheckWinConditions();
    }

    private void OnSurvivorTasksCompleteChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"SurvivorTasksComplete changed: {oldValue} -> {newValue}");
        if (IsServer) CheckWinConditions();
    }

    private void OnCultistTasksCompleteChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"CultistTasksComplete changed: {oldValue} -> {newValue}");
        if (IsServer) CheckWinConditions();
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from changes
        isGameEnded.OnValueChanged -= OnGameEndedChanged;
        gameResult.OnValueChanged -= OnGameResultChanged;
        survivorDeaths.OnValueChanged -= OnSurvivorDeathsChanged;
        cultistDeaths.OnValueChanged -= OnCultistDeathsChanged;
        survivorTasksComplete.OnValueChanged -= OnSurvivorTasksCompleteChanged;
        cultistTasksComplete.OnValueChanged -= OnCultistTasksCompleteChanged;
    }

    // Method to manually add a player (for testing)
    public void RegisterPlayer(ulong clientId, RoleManager.PlayerRole role)
    {
        if (!IsServer) return;

        if (!playerDeathStates.ContainsKey(clientId))
        {
            playerDeathStates[clientId] = false;
            playerRoles[clientId] = role;
            Debug.Log($"Registered player {clientId} with role {role}");
        }
    }

    // Debug method to check current state
    [ContextMenu("Debug Current Game State")]
    public void DebugCurrentState()
    {
        Debug.Log("=== CURRENT GAME STATE ===");
        Debug.Log($"Game Ended: {isGameEnded.Value}");
        Debug.Log($"Game Result: {gameResult.Value}");
        Debug.Log($"Survivor Tasks Complete: {survivorTasksComplete.Value}");
        Debug.Log($"Cultist Tasks Complete: {cultistTasksComplete.Value}");
        Debug.Log($"Survivor Deaths: {survivorDeaths.Value}");
        Debug.Log($"Cultist Deaths: {cultistDeaths.Value}");

        CheckWinConditions();
    }

    // Test methods for development
    [ContextMenu("Test Survivor Win By Tasks")]
    private void TestSurvivorWinTasks()
    {
        if (IsServer)
        {
            Debug.Log("TEST: Forcing Survivor Win By Tasks");
            survivorTasksComplete.Value = true;
        }
    }

    [ContextMenu("Test Survivor Win By Kill")]
    private void TestSurvivorWinKill()
    {
        if (IsServer)
        {
            Debug.Log("TEST: Forcing Survivor Win By Kill");
            // Mark all cultists as dead
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (playerRoles.ContainsKey(clientId) && playerRoles[clientId] == RoleManager.PlayerRole.Cultist)
                {
                    playerDeathStates[clientId] = true;
                    cultistDeaths.Value++;
                }
            }
            CheckWinConditions();
        }
    }

    [ContextMenu("Test Cultist Win By Tasks And Kill")]
    private void TestCultistWinTasksKill()
    {
        if (IsServer)
        {
            Debug.Log("TEST: Forcing Cultist Win By Tasks And Kill");
            cultistTasksComplete.Value = true;
            survivorDeaths.Value = 1; // At least one survivor dead
        }
    }

    [ContextMenu("Test Cultist Win By Elimination")]
    private void TestCultistWinElimination()
    {
        if (IsServer)
        {
            Debug.Log("TEST: Forcing Cultist Win By Elimination");
            // Mark all survivors as dead
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (playerRoles.ContainsKey(clientId) && playerRoles[clientId] == RoleManager.PlayerRole.Survivor)
                {
                    playerDeathStates[clientId] = true;
                    survivorDeaths.Value++;
                }
            }
            CheckWinConditions();
        }
    }
}