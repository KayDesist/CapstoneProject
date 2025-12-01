using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System;
using Unity.Collections;
using Unity.Services.Authentication;
using Unity.Services.Core;

public class LobbyManager : NetworkBehaviour
{
    [Header("UI Manager Reference")]
    [SerializeField] private LobbyUIManager lobbyUIManager;

    [Header("Debug Info")]
    [SerializeField] private TMP_Text debugText;

    // Network synchronized variables
    private NetworkList<LobbyPlayerData> lobbyPlayers;
    private string currentJoinCode;
    private RelayConnector relayConnector;
    private bool hasStartedConnection = false;

    private void Awake()
    {
        lobbyPlayers = new NetworkList<LobbyPlayerData>();
        relayConnector = GetComponent<RelayConnector>();
        if (relayConnector == null)
            relayConnector = gameObject.AddComponent<RelayConnector>();
    }

    private async void Start()
    {
        UpdateDebugText($"Lobby starting... Mode: {CrossSceneData.LobbyMode}");

        // Find UI Manager if not assigned
        if (lobbyUIManager == null)
            lobbyUIManager = FindObjectOfType<LobbyUIManager>();

        // Ensure NetworkManager is in a clean state
        await EnsureCleanNetworkState();

        // Prevent multiple connection attempts
        if (hasStartedConnection)
        {
            UpdateDebugText("Already started connection, skipping...");
            return;
        }

        hasStartedConnection = true;

        // Based on how we entered the lobby, start as host or client
        if (CrossSceneData.LobbyMode == "Host")
        {
            UpdateDebugText("Starting as HOST...");
            await StartHost();
        }
        else if (CrossSceneData.LobbyMode == "Client")
        {
            UpdateDebugText($"Starting as CLIENT with code: {CrossSceneData.JoinCode}");
            await StartClient();
        }
        else
        {
            UpdateDebugText("ERROR: Unknown lobby mode! Returning to main menu.");
            Debug.LogError("Unknown lobby mode!");
            ReturnToMainMenu();
        }
    }

    private async Task EnsureCleanNetworkState()
    {
        UpdateDebugText("Cleaning network state...");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            UpdateDebugText("Shutting down existing NetworkManager...");
            Debug.Log("NetworkManager was still listening - shutting down");
            NetworkManager.Singleton.Shutdown();
            // Wait a moment for shutdown to complete
            await Task.Delay(1000);
        }

        CleanupLeftoverManagers();
        UpdateDebugText("Network cleanup completed");
    }

    private void CleanupLeftoverManagers()
    {
        RoleManager.ResetInstance();
        TaskManager.ResetInstance();
        GameHUDManager.ResetInstance();
        EndGameManager.ResetInstance();
        Debug.Log("Cleaned up leftover manager instances");
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            UpdateDebugText("Initializing Unity Services...");
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                UpdateDebugText("Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("Signed in to Unity Services");
            }
            UpdateDebugText("Unity Services ready");
        }
        catch (System.Exception e)
        {
            UpdateDebugText($"ERROR: Services init failed: {e.Message}");
            Debug.LogError($"Services initialization failed: {e.Message}");
        }
    }

    private async Task StartHost()
    {
        Debug.Log("Starting as host...");

        try
        {
            // Initialize Unity Services first
            await InitializeUnityServices();

            // Add timeout and retry logic
            string joinCode = await StartRelayWithRetry(3);

            if (string.IsNullOrEmpty(joinCode))
            {
                UpdateDebugText("ERROR: Failed to create lobby after retries!");
                Debug.LogError("Failed to create lobby after retries! Relay returned null join code.");
                ReturnToMainMenu();
                return;
            }

            currentJoinCode = joinCode;

            // Update UI with join code
            if (lobbyUIManager != null)
            {
                lobbyUIManager.UpdateLobbyCodeDisplay(joinCode);
            }

            UpdateDebugText($"Lobby created! Join code: {joinCode}");
            Debug.Log($"[LobbyManager] Lobby created with code {joinCode}");
        }
        catch (System.Exception e)
        {
            UpdateDebugText($"ERROR: Host start failed: {e.Message}");
            Debug.LogError($"Exception while starting host: {e.Message}");
            ReturnToMainMenu();
        }
    }

    private async Task<string> StartRelayWithRetry(int maxRetries)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                UpdateDebugText($"Relay attempt {attempt}/{maxRetries}...");
                Debug.Log($"Relay connection attempt {attempt}/{maxRetries}");
                string joinCode = await relayConnector.StartHostWithRelay(maxConnections: 10, connectionType: "wss");

                if (!string.IsNullOrEmpty(joinCode))
                {
                    return joinCode;
                }
            }
            catch (System.Exception e)
            {
                UpdateDebugText($"Relay attempt {attempt} failed: {e.Message}");
                Debug.LogWarning($"Relay attempt {attempt} failed: {e.Message}");

                if (attempt < maxRetries)
                {
                    UpdateDebugText($"Retrying in {attempt * 2} seconds...");
                    Debug.Log($"Retrying in {attempt * 2} seconds...");
                    await Task.Delay(attempt * 2000); // Exponential backoff
                }
            }
        }

        return null;
    }

    private async Task StartClient()
    {
        if (string.IsNullOrEmpty(CrossSceneData.JoinCode))
        {
            UpdateDebugText("ERROR: No join code provided!");
            Debug.LogError("No join code provided!");
            ReturnToMainMenu();
            return;
        }

        Debug.Log("Starting as client...");

        try
        {
            // Initialize Unity Services first
            await InitializeUnityServices();

            // Add retry logic for client connection
            bool success = await JoinRelayWithRetry(CrossSceneData.JoinCode, 3);

            if (!success)
            {
                UpdateDebugText("ERROR: Failed to join lobby after retries!");
                Debug.LogError("Failed to join lobby after retries!");
                ReturnToMainMenu();
                return;
            }

            currentJoinCode = CrossSceneData.JoinCode;

            // Update UI with join code
            if (lobbyUIManager != null)
            {
                lobbyUIManager.UpdateLobbyCodeDisplay(CrossSceneData.JoinCode);
            }

            UpdateDebugText("Successfully joined lobby!");
            Debug.Log("[LobbyManager] Successfully joined relay session");
        }
        catch (System.Exception e)
        {
            UpdateDebugText($"ERROR: Client join failed: {e.Message}");
            Debug.LogError($"Exception while starting client: {e.Message}");
            ReturnToMainMenu();
        }
    }

    private async Task<bool> JoinRelayWithRetry(string joinCode, int maxRetries)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                UpdateDebugText($"Joining attempt {attempt}/{maxRetries}...");
                Debug.Log($"Client Relay connection attempt {attempt}/{maxRetries}");
                bool success = await relayConnector.StartClientWithRelay(joinCode, "wss");

                if (success)
                {
                    UpdateDebugText("Join successful!");
                    return true;
                }
            }
            catch (System.Exception e)
            {
                UpdateDebugText($"Join attempt {attempt} failed: {e.Message}");
                Debug.LogWarning($"Client Relay attempt {attempt} failed: {e.Message}");

                if (attempt < maxRetries)
                {
                    UpdateDebugText($"Retrying in {attempt * 2} seconds...");
                    Debug.Log($"Retrying in {attempt * 2} seconds...");
                    await Task.Delay(attempt * 2000); // Exponential backoff
                }
            }
        }

        return false;
    }

    public override void OnNetworkSpawn()
    {
        UpdateDebugText($"Network spawned - IsServer: {IsServer}, IsClient: {IsClient}");

        // Register network callbacks
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        lobbyPlayers.OnListChanged += OnLobbyPlayersChanged;

        // Add the local player to the lobby
        if (IsServer)
        {
            UpdateDebugText("I am the SERVER - Adding host to lobby");
            // Host adds themselves
            int characterIndex = CrossSceneData.GetCharacterIndexForClient(NetworkManager.Singleton.LocalClientId);
            AddPlayerToLobby(NetworkManager.Singleton.LocalClientId, "Host", characterIndex);
        }
        else if (IsClient)
        {
            UpdateDebugText("I am a CLIENT - Requesting to join lobby");
            // Client requests to be added
            AddPlayerToLobbyServerRpc(NetworkManager.Singleton.LocalClientId, "Player");
        }

        UpdateUI();
    }

    private void OnLobbyPlayersChanged(NetworkListEvent<LobbyPlayerData> changeEvent)
    {
        UpdateDebugText($"Lobby players changed: {lobbyPlayers.Count} players");
        UpdateUI();
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPlayerToLobbyServerRpc(ulong clientId, string playerName, ServerRpcParams rpcParams = default)
    {
        UpdateDebugText($"Client {clientId} requesting to join lobby");
        int characterIndex = CrossSceneData.GetCharacterIndexForClient(clientId);
        AddPlayerToLobby(clientId, $"{playerName} {clientId}", characterIndex);
    }

    private void AddPlayerToLobby(ulong clientId, string playerName, int characterIndex)
    {
        // Check if player already exists
        foreach (var player in lobbyPlayers)
        {
            if (player.ClientId == clientId)
            {
                UpdateDebugText($"Player {clientId} already in lobby, skipping");
                return;
            }
        }

        lobbyPlayers.Add(new LobbyPlayerData
        {
            ClientId = clientId,
            PlayerName = playerName,
            IsReady = true, // Auto-ready for now
            CharacterIndex = characterIndex
        });

        string characterName = CrossSceneData.GetCharacterName(characterIndex);
        UpdateDebugText($"Added {playerName} as {characterName}");
        Debug.Log($"Added player to lobby: {playerName} (ID: {clientId}) as {characterName}");
    }

    private void RemovePlayerFromLobby(ulong clientId)
    {
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i].ClientId == clientId)
            {
                lobbyPlayers.RemoveAt(i);
                UpdateDebugText($"Removed player {clientId} from lobby");
                break;
            }
        }
    }

    private void UpdateUI()
    {
        if (!IsSpawned) return;

        // Convert NetworkList to regular List for UI
        List<LobbyPlayerData> playersList = new List<LobbyPlayerData>();
        foreach (var player in lobbyPlayers)
        {
            playersList.Add(player);
        }

        // Update UI on ALL clients
        if (lobbyUIManager != null)
        {
            lobbyUIManager.UpdatePlayerList(playersList);
            lobbyUIManager.UpdatePlayerCountDisplay();

            // Show start button only for host and when we have at least 2 players
            if (IsHost)
            {
                // FIXED: Show start button only when we have 2+ players
                bool canStart = lobbyPlayers.Count >= 2;
                lobbyUIManager.SetStartButtonVisible(canStart);
                UpdateDebugText($"Host UI - {lobbyPlayers.Count} players, start button: {canStart}");
            }
            else
            {
                UpdateDebugText($"Client UI - {lobbyPlayers.Count} players in lobby");
            }
        }
    }

    public int GetCurrentPlayerCount()
    {
        return lobbyPlayers.Count;
    }

    // Simple leave / shutdown
    public void LeaveLobby()
    {
        UpdateDebugText("Leaving lobby...");
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        UpdateDebugText("Returning to main menu...");
        // Clear cross-scene data
        CrossSceneData.Reset();

        SceneManager.LoadScene("MainMenu");
    }

    // Host-only StartGame - UPDATED for better scene loading
    public void StartGame()
    {
        if (!IsHost)
        {
            UpdateDebugText("ERROR: Only host can start the game!");
            Debug.LogWarning("[LobbyManager] Only host can start the game");
            return;
        }

        // FIXED: Require at least 2 players (host + 1 other)
        if (lobbyPlayers.Count < 2) // Changed from 1 to 2
        {
            UpdateDebugText("ERROR: Need at least 2 players to start!");
            Debug.LogWarning("[LobbyManager] Need at least 2 players to start");
            return;
        }

        UpdateDebugText($"Starting game with {lobbyPlayers.Count} players...");
        Debug.Log($"[LobbyManager] Starting GameScene for {lobbyPlayers.Count} players...");

        // Log character assignments
        foreach (var player in lobbyPlayers)
        {
            Debug.Log($"Client {player.ClientId} -> {CrossSceneData.GetCharacterName(player.CharacterIndex)}");
        }

        // Ensure all clients are synchronized before loading scene
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);

        UpdateDebugText("Game scene load initiated!");
    }

    // Network callbacks
    private void OnClientConnected(ulong clientId)
    {
        UpdateDebugText($"Client {clientId} connected to lobby");
        Debug.Log($"[LobbyManager] Client connected: {clientId}");

        if (IsServer)
        {
            int characterIndex = CrossSceneData.GetCharacterIndexForClient(clientId);
            AddPlayerToLobby(clientId, $"Player {clientId}", characterIndex);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        UpdateDebugText($"Client {clientId} disconnected from lobby");
        Debug.Log($"[LobbyManager] Client disconnected: {clientId}");

        if (IsServer)
        {
            RemovePlayerFromLobby(clientId);
        }

        // If we get disconnected as client, return to main menu
        if (!IsServer && clientId == NetworkManager.Singleton.LocalClientId)
        {
            UpdateDebugText("I was disconnected - returning to main menu");
            ReturnToMainMenu();
        }
    }

    public override void OnNetworkDespawn()
    {
        UpdateDebugText("Network despawned - cleaning up");
        // Clean up callbacks
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        lobbyPlayers.OnListChanged -= OnLobbyPlayersChanged;
    }

    private void UpdateDebugText(string message)
    {
        if (debugText != null)
        {
            debugText.text = $"[Lobby] {message}";
        }
        Debug.Log($"[LobbyManager] {message}");
    }

    // Debug methods
    [ContextMenu("Debug Lobby State")]
    public void DebugLobbyState()
    {
        Debug.Log("=== LOBBY STATE ===");
        Debug.Log($"Players: {lobbyPlayers.Count}");
        Debug.Log($"Join Code: {currentJoinCode}");
        Debug.Log($"Is Host: {IsHost}");
        Debug.Log($"Is Server: {IsServer}");
        Debug.Log($"CrossSceneData - Mode: {CrossSceneData.LobbyMode}, JoinCode: {CrossSceneData.JoinCode}");

        foreach (var player in lobbyPlayers)
        {
            string characterName = CrossSceneData.GetCharacterName(player.CharacterIndex);
            Debug.Log($"- Player {player.ClientId}: {player.PlayerName} -> {characterName}");
        }
    }

    [ContextMenu("Force Start Game")]
    public void ForceStartGame()
    {
        if (IsHost)
        {
            StartGame();
        }
        else
        {
            Debug.LogWarning("Only host can force start game");
        }
    }
}