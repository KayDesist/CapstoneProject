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

    // Network synchronized variables
    private NetworkList<LobbyPlayerData> lobbyPlayers;
    private string currentJoinCode;
    private RelayConnector relayConnector;

    private void Awake()
    {
        lobbyPlayers = new NetworkList<LobbyPlayerData>();
        relayConnector = GetComponent<RelayConnector>();
        if (relayConnector == null)
            relayConnector = gameObject.AddComponent<RelayConnector>();
    }

    private async void Start()
    {
        // Find UI Manager if not assigned
        if (lobbyUIManager == null)
            lobbyUIManager = FindObjectOfType<LobbyUIManager>();

        // Ensure NetworkManager is in a clean state
        await EnsureCleanNetworkState();

        // Based on how we entered the lobby, start as host or client
        if (CrossSceneData.LobbyMode == "Host")
        {
            await StartHost();
        }
        else if (CrossSceneData.LobbyMode == "Client")
        {
            await StartClient();
        }
        else
        {
            Debug.LogError("Unknown lobby mode!");
            ReturnToMainMenu();
        }
    }

    // NEW: Ensure NetworkManager is in clean state
    private async Task EnsureCleanNetworkState()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("NetworkManager was still listening - shutting down");
            NetworkManager.Singleton.Shutdown();
            // Wait a moment for shutdown to complete
            await Task.Delay(100);
        }

        // Clean up any leftover manager instances
        CleanupLeftoverManagers();
    }

    // NEW: Clean up leftover manager instances
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
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("Signed in to Unity Services");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Services initialization failed: {e.Message}");
        }
    }

    private async Task StartHost()
    {
        Debug.Log("Starting as host...");

        try
        {
            // FIXED: Initialize Unity Services first
            await InitializeUnityServices();

            // FIXED: Add timeout and retry logic
            string joinCode = await StartRelayWithRetry(3); // 3 retries

            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("Failed to create lobby after retries! Relay returned null join code.");
                ShowErrorToUser("Failed to create lobby. Please check your internet connection and try again.");
                ReturnToMainMenu();
                return;
            }

            currentJoinCode = joinCode;

            // Update UI with join code
            if (lobbyUIManager != null)
            {
                lobbyUIManager.UpdateLobbyCodeDisplay(joinCode);
            }

            Debug.Log($"[LobbyManager] Lobby created with code {joinCode}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception while starting host: {e.Message}");
            ShowErrorToUser($"Failed to create lobby: {e.Message}");
            ReturnToMainMenu();
        }
    }

    // NEW: Add retry logic for Relay connection
    private async Task<string> StartRelayWithRetry(int maxRetries)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Debug.Log($"Relay connection attempt {attempt}/{maxRetries}");
                string joinCode = await relayConnector.StartHostWithRelay(maxConnections: 10, connectionType: "wss");

                if (!string.IsNullOrEmpty(joinCode))
                {
                    return joinCode;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Relay attempt {attempt} failed: {e.Message}");

                if (attempt < maxRetries)
                {
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
            Debug.LogError("No join code provided!");
            ShowErrorToUser("No join code provided!");
            ReturnToMainMenu();
            return;
        }

        Debug.Log("Starting as client...");

        try
        {
            // FIXED: Initialize Unity Services first
            await InitializeUnityServices();

            // FIXED: Add retry logic for client connection
            bool success = await JoinRelayWithRetry(CrossSceneData.JoinCode, 3);

            if (!success)
            {
                Debug.LogError("Failed to join lobby after retries!");
                ShowErrorToUser("Failed to join lobby! Please check the join code and try again.");
                ReturnToMainMenu();
                return;
            }

            currentJoinCode = CrossSceneData.JoinCode;

            // Update UI with join code
            if (lobbyUIManager != null)
            {
                lobbyUIManager.UpdateLobbyCodeDisplay(CrossSceneData.JoinCode);
            }

            Debug.Log("[LobbyManager] Successfully joined relay session");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception while starting client: {e.Message}");
            ShowErrorToUser($"Failed to join lobby: {e.Message}");
            ReturnToMainMenu();
        }
    }

    // NEW: Add retry logic for client Relay connection
    private async Task<bool> JoinRelayWithRetry(string joinCode, int maxRetries)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Debug.Log($"Client Relay connection attempt {attempt}/{maxRetries}");
                bool success = await relayConnector.StartClientWithRelay(joinCode, "wss");

                if (success)
                {
                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Client Relay attempt {attempt} failed: {e.Message}");

                if (attempt < maxRetries)
                {
                    Debug.Log($"Retrying in {attempt * 2} seconds...");
                    await Task.Delay(attempt * 2000); // Exponential backoff
                }
            }
        }

        return false;
    }

    // NEW: Show error messages to user
    private void ShowErrorToUser(string message)
    {
        Debug.LogError($"USER ERROR: {message}");
        // You can implement this to show error messages in UI
        // Example: if you have an error text UI element
        // errorText.text = message;
        // errorPanel.SetActive(true);
    }

    public override void OnNetworkSpawn()
    {
        // Register network callbacks
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        lobbyPlayers.OnListChanged += OnLobbyPlayersChanged;

        // Add the local player to the lobby
        if (IsServer)
        {
            // Host adds themselves
            AddPlayerToLobby(NetworkManager.Singleton.LocalClientId, "Player");
        }
        else if (IsClient)
        {
            // Client requests to be added
            AddPlayerToLobbyServerRpc(NetworkManager.Singleton.LocalClientId, "Player");
        }

        UpdateUI();
    }

    private void OnLobbyPlayersChanged(NetworkListEvent<LobbyPlayerData> changeEvent)
    {
        UpdateUI();
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPlayerToLobbyServerRpc(ulong clientId, string playerName, ServerRpcParams rpcParams = default)
    {
        AddPlayerToLobby(clientId, $"{playerName} {clientId}");
    }

    private void AddPlayerToLobby(ulong clientId, string playerName)
    {
        // Check if player already exists
        foreach (var player in lobbyPlayers)
        {
            if (player.ClientId == clientId)
                return;
        }

        lobbyPlayers.Add(new LobbyPlayerData
        {
            ClientId = clientId,
            PlayerName = playerName,
            IsReady = false
        });

        Debug.Log($"Added player to lobby: {playerName} (ID: {clientId})");
    }

    private void RemovePlayerFromLobby(ulong clientId)
    {
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i].ClientId == clientId)
            {
                lobbyPlayers.RemoveAt(i);
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

        // Update UI
        if (lobbyUIManager != null)
        {
            lobbyUIManager.UpdatePlayerList(playersList);
            lobbyUIManager.UpdatePlayerCountDisplay();

            // Show start button only for host and when we have enough players
            if (IsHost)
            {
                lobbyUIManager.SetStartButtonVisible(lobbyPlayers.Count >= 2); // Change to 5 for final
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
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        // Clear cross-scene data
        CrossSceneData.Reset();

        SceneManager.LoadScene("MainMenu");
    }

    // Host-only StartGame - UPDATED for better scene loading
    public void StartGame()
    {
        if (!IsHost)
        {
            Debug.LogWarning("[LobbyManager] Only host can start the game");
            return;
        }

        if (lobbyPlayers.Count < 2) // Change to 5 for your final version
        {
            Debug.LogWarning("[LobbyManager] Need at least 2 players to start");
            return;
        }

        Debug.Log($"[LobbyManager] Starting GameScene for {lobbyPlayers.Count} players...");

        // Ensure all clients are synchronized before loading scene
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);

        // Log for debugging
        Debug.Log($"Current connected clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Debug.Log($" - Client {clientId}");
        }
    }

    // Network callbacks
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[LobbyManager] Client connected: {clientId}");

        if (IsServer)
        {
            AddPlayerToLobby(clientId, $"Player {clientId}");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[LobbyManager] Client disconnected: {clientId}");

        if (IsServer)
        {
            RemovePlayerFromLobby(clientId);
        }

        // If we get disconnected as client, return to main menu
        if (!IsServer && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ReturnToMainMenu();
        }
    }

    public override void OnNetworkDespawn()
    {
        // Clean up callbacks
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        lobbyPlayers.OnListChanged -= OnLobbyPlayersChanged;
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

        foreach (var player in lobbyPlayers)
        {
            Debug.Log($"- Player {player.ClientId}: {player.PlayerName} (Ready: {player.IsReady})");
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

// Network-serializable player data
public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName;
    public bool IsReady;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref IsReady);
    }

    public bool Equals(LobbyPlayerData other)
    {
        return ClientId == other.ClientId &&
               PlayerName.Equals(other.PlayerName) &&
               IsReady == other.IsReady;
    }

    public override bool Equals(object obj)
    {
        return obj is LobbyPlayerData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ClientId, PlayerName, IsReady);
    }
}