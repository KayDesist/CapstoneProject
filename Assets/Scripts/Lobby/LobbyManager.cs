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

    private NetworkList<NetworkPlayerInfo> lobbyPlayers;
    private string currentJoinCode;
    private RelayConnector relayConnector;

    // Initialize components
    private void Awake()
    {
        lobbyPlayers = new NetworkList<NetworkPlayerInfo>();
        relayConnector = GetComponent<RelayConnector>();
        if (relayConnector == null)
            relayConnector = gameObject.AddComponent<RelayConnector>();
    }

    // Start lobby based on mode
    private async void Start()
    {
        if (lobbyUIManager == null)
            lobbyUIManager = FindObjectOfType<LobbyUIManager>();

        await EnsureCleanNetworkState();

        try
        {
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
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize lobby: {e.Message}");
            ShowErrorToUser($"Connection failed: {e.Message}");
            ReturnToMainMenu();
        }
    }

    // Ensure clean network state
    private async Task EnsureCleanNetworkState()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("NetworkManager was still listening - shutting down");
            NetworkManager.Singleton.Shutdown();
            await Task.Delay(100);
        }

        CleanupLeftoverManagers();
    }

    // Clean up leftover manager instances
    private void CleanupLeftoverManagers()
    {
        RoleManager.ResetInstance();
        TaskManager.ResetInstance();
        GameHUDManager.ResetInstance();
        EndGameManager.ResetInstance();

        Debug.Log("Cleaned up leftover manager instances");
    }

    // Initialize Unity services
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

    // Start as host
    private async Task StartHost()
    {
        Debug.Log("Starting as host...");

        try
        {
            await InitializeUnityServices();

            string joinCode = await StartRelayWithRetry(3);

            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("Failed to create lobby after retries! Relay returned null join code.");
                ShowErrorToUser("Failed to create lobby. Please check your internet connection and try again.");
                ReturnToMainMenu();
                return;
            }

            currentJoinCode = joinCode;

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

    // Retry logic for Relay connection
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
                    await Task.Delay(attempt * 2000);
                }
            }
        }

        return null;
    }

    // Start as client
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
            await InitializeUnityServices();

            bool success = await JoinRelayWithRetry(CrossSceneData.JoinCode, 3);

            if (!success)
            {
                Debug.LogError("Failed to join lobby after retries!");
                ShowErrorToUser("Failed to join lobby! Please check the join code and try again.");
                ReturnToMainMenu();
                return;
            }

            currentJoinCode = CrossSceneData.JoinCode;

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

    // Retry logic for client Relay connection
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
                    await Task.Delay(attempt * 2000);
                }
            }
        }

        return false;
    }

    // Show error messages to user
    private void ShowErrorToUser(string message)
    {
        Debug.LogError($"USER ERROR: {message}");
    }

    // Network spawn callback
    public override void OnNetworkSpawn()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        lobbyPlayers.OnListChanged += OnLobbyPlayersChanged;

        if (IsServer)
        {
            AddPlayerToLobby(NetworkManager.Singleton.LocalClientId, "Player");
        }
        else if (IsClient)
        {
            AddPlayerToLobbyServerRpc(NetworkManager.Singleton.LocalClientId, "Player");
        }

        UpdateUI();
    }

    // Handle lobby players changed
    private void OnLobbyPlayersChanged(NetworkListEvent<NetworkPlayerInfo> changeEvent)
    {
        UpdateUI();
    }

    // Server RPC to add player to lobby
    [ServerRpc(RequireOwnership = false)]
    private void AddPlayerToLobbyServerRpc(ulong clientId, string playerName, ServerRpcParams rpcParams = default)
    {
        AddPlayerToLobby(clientId, $"{playerName} {clientId}");
    }

    // Add player to lobby
    private void AddPlayerToLobby(ulong clientId, string playerName)
    {
        foreach (var player in lobbyPlayers)
        {
            if (player.ClientId == clientId)
                return;
        }

        lobbyPlayers.Add(new NetworkPlayerInfo
        {
            ClientId = clientId,
            PlayerName = playerName,
            IsReady = false,
            CharacterIndex = -1
        });

        Debug.Log($"Added player to lobby: {playerName} (ID: {clientId})");
    }

    // Remove player from lobby
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

    // Update UI
    private void UpdateUI()
    {
        if (!IsSpawned) return;

        List<NetworkPlayerInfo> playersList = new List<NetworkPlayerInfo>();
        foreach (var player in lobbyPlayers)
        {
            playersList.Add(player);
        }

        if (lobbyUIManager != null)
        {
            lobbyUIManager.UpdatePlayerList(playersList);
            lobbyUIManager.UpdatePlayerCountDisplay();

            if (IsHost)
            {
                lobbyUIManager.SetStartButtonVisible(lobbyPlayers.Count >= 2);
            }
        }
    }

    // Get current player count
    public int GetCurrentPlayerCount()
    {
        return lobbyPlayers.Count;
    }

    // Leave lobby
    public void LeaveLobby()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        ReturnToMainMenu();
    }

    // Return to main menu
    private void ReturnToMainMenu()
    {
        CrossSceneData.Reset();

        SceneManager.LoadScene("MainMenu");
    }

    // Host-only start game
    public void StartGame()
    {
        if (!IsHost)
        {
            Debug.LogWarning("[LobbyManager] Only host can start the game");
            return;
        }

        if (lobbyPlayers.Count < 2)
        {
            Debug.LogWarning("[LobbyManager] Need at least 2 players to start");
            return;
        }

        Debug.Log($"[LobbyManager] Starting GameScene for {lobbyPlayers.Count} players...");

        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);

        Debug.Log($"Current connected clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Debug.Log($" - Client {clientId}");
        }
    }

    // Client connected callback
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[LobbyManager] Client connected: {clientId}");

        if (IsServer)
        {
            AddPlayerToLobby(clientId, $"Player {clientId}");
        }
    }

    // Client disconnected callback
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[LobbyManager] Client disconnected: {clientId}");

        if (IsServer)
        {
            RemovePlayerFromLobby(clientId);
        }

        if (!IsServer && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ReturnToMainMenu();
        }
    }

    // Network despawn callback
    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        lobbyPlayers.OnListChanged -= OnLobbyPlayersChanged;
    }

    // Debug lobby state
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

    // Force start game
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