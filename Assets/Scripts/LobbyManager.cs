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

        // Ensure Unity Services are initialized
        await InitializeUnityServices();

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

        // FIXED: Changed from "dtls" to "wss" for WebSockets compatibility
        string joinCode = await relayConnector.StartHostWithRelay(maxConnections: 10, connectionType: "wss");

        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogError("Failed to create lobby!");
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

    private async Task StartClient()
    {
        if (string.IsNullOrEmpty(CrossSceneData.JoinCode))
        {
            Debug.LogError("No join code provided!");
            ReturnToMainMenu();
            return;
        }

        Debug.Log("Starting as client...");

        // FIXED: Changed from "dtls" to "wss" for WebSockets compatibility
        bool success = await relayConnector.StartClientWithRelay(CrossSceneData.JoinCode, "wss");

        if (!success)
        {
            Debug.LogError("Failed to join lobby!");
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

    // Host-only StartGame
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

        Debug.Log("[LobbyManager] Starting GameScene for everyone...");
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
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