using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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

    // FIXED: Added game end event
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

        // Check win conditions EVERY FRAME for immediate response
        CheckWinConditions();

        // Optional: Keep the periodic check for debugging
        if (Time.time - lastCheckTime >= checkInterval)
        {
            Debug.Log("Periodic win condition check");
            lastCheckTime = Time.time;
        }
    }

    private void CheckWinConditions()
    {
        if (isGameEnded.Value)
        {
            return;
        }

        Debug.Log("=== REAL-TIME WIN CONDITION CHECK ===");

        int totalSurvivors = 0;
        int totalCultists = 0;
        int aliveSurvivors = 0;
        int aliveCultists = 0;

        // Count players and their status - REAL-TIME role checking
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // ALWAYS get role from RoleManager to ensure accuracy
            RoleManager.PlayerRole role = RoleManager.PlayerRole.Survivor; // Default

            if (RoleManager.Instance != null)
            {
                role = RoleManager.Instance.GetPlayerRole(clientId);
            }
            else
            {
                Debug.LogWarning("RoleManager.Instance is null during win condition check!");
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

            Debug.Log($"Player {clientId}: {role}, Dead: {isDead}");
        }

        Debug.Log($"REAL-TIME STATUS - Survivors: {aliveSurvivors}/{totalSurvivors} alive, Cultists: {aliveCultists}/{totalCultists} alive");
        Debug.Log($"Tasks - Survivor: {survivorTasksComplete.Value}, Cultist: {cultistTasksComplete.Value}");

        // IMMEDIATE WIN CONDITION CHECKS

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

        // Wait a moment for RoleManager to be ready
        Invoke(nameof(DelayedInitialize), 1f);
    }

    private void DelayedInitialize()
    {
        if (!IsServer) return;

        playerDeathStates.Clear();
        playerRoles.Clear();

        // Initialize player tracking - FIXED: Get roles from RoleManager directly
        if (RoleManager.Instance != null)
        {
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var role = RoleManager.Instance.GetPlayerRole(clientId);
                playerRoles[clientId] = role;
                playerDeathStates[clientId] = false;
                Debug.Log($"Initialized player {clientId} with role: {role}");
            }
        }
        else
        {
            Debug.LogError("RoleManager.Instance is null! Cannot initialize player roles.");
            // Fallback: initialize with default roles
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                playerRoles[clientId] = RoleManager.PlayerRole.Survivor;
                playerDeathStates[clientId] = false;
                Debug.LogWarning($"Fallback: Initialized player {clientId} as Survivor (RoleManager missing)");
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
            // Don't update role here - keep the original role

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


    private void EndGame(GameResult result)
    {
        if (!IsServer || isGameEnded.Value) return;

        isGameEnded.Value = true;
        gameResult.Value = result;

        Debug.Log($"🎮 GAME ENDED: {result} 🎮");

        // FIXED: Trigger game end event
        OnGameEnded?.Invoke();

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

    public void ReturnToMainMenuImmediately()
    {
        if (!IsServer) return;

        Debug.Log("Manual return to main menu requested");

        // Cancel any pending automatic return
        CancelInvoke(nameof(ReturnToMainMenu));

        // Return immediately
        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        if (!IsServer) return;

        Debug.Log("Returning to main menu...");

        // Clean up cross-scene data
        CrossSceneData.Reset();

        // Reset all static instances
        ResetAllManagers();

        // FIXED: Use NetworkManager's SceneManager to load scene for all clients
        NetworkManager.Singleton.SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }

    // NEW: Reset all manager instances
    private void ResetAllManagers()
    {
        RoleManager.ResetInstance();
        TaskManager.ResetInstance();
        GameHUDManager.ResetInstance();
        ResetInstance(); // Reset self

        Debug.Log("All manager instances reset for new game session");
    }

    // NEW: Reset static instance
    public static void ResetInstance()
    {
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
            Debug.Log("EndGameManager instance reset");
        }
    }

    // Handle client disconnection gracefully
    public void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer || isGameEnded.Value) return;

        Debug.Log($"Client {clientId} disconnected - checking if game should end");

        // Remove the disconnected player from tracking
        if (playerRoles.ContainsKey(clientId))
        {
            var role = playerRoles[clientId];
            if (role == RoleManager.PlayerRole.Cultist)
            {
                Debug.Log($"Cultist {clientId} disconnected - cultists can no longer win by tasks");
                // If cultist disconnects, they can't complete tasks, but survivors can still win
            }

            playerRoles.Remove(clientId);
            playerDeathStates.Remove(clientId);
        }

        // Check if game should continue or end
        CheckWinConditions();
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

        if (!playerRoles.ContainsKey(clientId))
        {
            playerRoles[clientId] = role;
            playerDeathStates[clientId] = false;
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

    // Debug method to check scene state
    [ContextMenu("Debug Scene Check")]
    public void DebugSceneCheck()
    {
        Debug.Log($"Current scene: {SceneManager.GetActiveScene().name}");
        Debug.Log($"IsServer: {IsServer}");
        Debug.Log($"NetworkManager exists: {NetworkManager.Singleton != null}");
        Debug.Log($"EndGameUI exists: {EndGameUI.Instance != null}");
        Debug.Log($"GameHUDManager exists: {GameHUDManager.Instance != null}");

        // Debug player roles
        Debug.Log("=== PLAYER ROLES ===");
        foreach (var kvp in playerRoles)
        {
            bool isDead = playerDeathStates.ContainsKey(kvp.Key) && playerDeathStates[kvp.Key];
            Debug.Log($"Player {kvp.Key}: {kvp.Value} (Dead: {isDead})");
        }
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

    // NEW: Test method to simulate cultist killing survivor
    [ContextMenu("Test Cultist Kills Survivor")]
    private void TestCultistKillsSurvivor()
    {
        if (IsServer)
        {
            Debug.Log("TEST: Simulating cultist killing survivor");

            // Find one survivor and one cultist
            ulong survivorId = 0;
            ulong cultistId = 0;

            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (playerRoles.ContainsKey(clientId))
                {
                    if (playerRoles[clientId] == RoleManager.PlayerRole.Survivor && survivorId == 0)
                    {
                        survivorId = clientId;
                    }
                    else if (playerRoles[clientId] == RoleManager.PlayerRole.Cultist && cultistId == 0)
                    {
                        cultistId = clientId;
                    }
                }
            }

            if (survivorId != 0)
            {
                OnPlayerDied(survivorId, RoleManager.PlayerRole.Survivor);
                Debug.Log($"Killed survivor {survivorId}");
            }
            else
            {
                Debug.LogWarning("No survivor found to kill");
            }
        }
    }
}