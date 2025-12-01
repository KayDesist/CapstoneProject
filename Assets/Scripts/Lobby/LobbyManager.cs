using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System;

public class LobbyManager : NetworkBehaviour
{
    [Header("UI Manager Reference")]
    [SerializeField] private LobbyUIManager lobbyUIManager;

    [Header("Debug Info")]
    [SerializeField] private TMP_Text debugText;

    // Network synchronized variables
    private NetworkList<LobbyPlayerData> lobbyPlayers;
    private string currentJoinCode;

    private void Awake()
    {
        lobbyPlayers = new NetworkList<LobbyPlayerData>();
    }

    private void Start()
    {
        UpdateDebugText($"Lobby loaded. Mode: {CrossSceneData.LobbyMode}, Code: {CrossSceneData.JoinCode}");

        // Find UI Manager if not assigned
        if (lobbyUIManager == null)
        {
            lobbyUIManager = FindObjectOfType<LobbyUIManager>();
            if (lobbyUIManager == null)
                Debug.LogWarning("[LobbyManager] LobbyUIManager not found!");
        }

        // Display join code if we have one
        if (!string.IsNullOrEmpty(CrossSceneData.JoinCode) && lobbyUIManager != null)
        {
            lobbyUIManager.UpdateLobbyCodeDisplay(CrossSceneData.JoinCode);
            currentJoinCode = CrossSceneData.JoinCode;
        }

        // Check if we're already connected
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            UpdateDebugText($"Already connected as {NetworkManager.Singleton.LocalClientId}");
        }
        else
        {
            UpdateDebugText("ERROR: Not connected to network! Connection should have been established in MainMenu.");

            // If we somehow got here without a connection, go back to main menu
            if (Application.isPlaying)
            {
                UpdateDebugText("Returning to main menu...");
                StartCoroutine(LoadMainMenuAsync());
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        UpdateDebugText($"Network spawned - IsServer: {IsServer}, IsClient: {IsClient}, IsHost: {IsHost}");

        // Register network callbacks
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        lobbyPlayers.OnListChanged += OnLobbyPlayersChanged;

        // Add the local player to the lobby
        if (IsServer)
        {
            UpdateDebugText("I am the SERVER/HOST - Adding host to lobby");
            // Host adds themselves
            int characterIndex = CrossSceneData.GetCharacterIndexForClient(NetworkManager.Singleton.LocalClientId);
            AddPlayerToLobby(NetworkManager.Singleton.LocalClientId, "Host", characterIndex);
        }
        else if (IsClient)
        {
            UpdateDebugText("I am a CLIENT - Adding to lobby");
            // Client adds themselves via ServerRPC
            int characterIndex = CrossSceneData.GetCharacterIndexForClient(NetworkManager.Singleton.LocalClientId);
            AddPlayerToLobbyServerRpc(NetworkManager.Singleton.LocalClientId, "Player", characterIndex);
        }

        UpdateUI();
    }

    private void OnLobbyPlayersChanged(NetworkListEvent<LobbyPlayerData> changeEvent)
    {
        UpdateDebugText($"Lobby players changed: {lobbyPlayers.Count} players");
        UpdateUI();
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPlayerToLobbyServerRpc(ulong clientId, string playerName, int characterIndex, ServerRpcParams rpcParams = default)
    {
        UpdateDebugText($"Client {clientId} requesting to join lobby");
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
            IsReady = true,
            CharacterIndex = characterIndex
        });

        string characterName = CrossSceneData.GetCharacterName(characterIndex);
        UpdateDebugText($"Added {playerName} as {characterName}");
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
        if (!IsSpawned)
        {
            UpdateDebugText("Not spawned yet, skipping UI update");
            return;
        }

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
                bool canStart = lobbyPlayers.Count >= 2;
                lobbyUIManager.SetStartButtonVisible(canStart);
                UpdateDebugText($"Host UI - {lobbyPlayers.Count} players, start button: {canStart}");
            }
            else
            {
                lobbyUIManager.SetStartButtonVisible(false);
                UpdateDebugText($"Client UI - {lobbyPlayers.Count} players in lobby");
            }
        }
        else
        {
            UpdateDebugText("LobbyUIManager is null, cannot update UI");
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

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Return to main menu
        CrossSceneData.Reset();
        StartCoroutine(LoadMainMenuAsync());
    }

    private System.Collections.IEnumerator LoadMainMenuAsync()
    {
        yield return null; // Wait one frame
        SceneManager.LoadScene("MainMenu");
    }

    // Host-only StartGame
    public void StartGame()
    {
        if (!IsHost)
        {
            UpdateDebugText("ERROR: Only host can start the game!");
            return;
        }

        if (lobbyPlayers.Count < 2)
        {
            UpdateDebugText("ERROR: Need at least 2 players to start!");
            return;
        }

        UpdateDebugText($"Starting game with {lobbyPlayers.Count} players...");

        // Log character assignments
        foreach (var player in lobbyPlayers)
        {
            Debug.Log($"[LobbyManager] Client {player.ClientId} -> {CrossSceneData.GetCharacterName(player.CharacterIndex)}");
        }

        // Load game scene
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }

    // Network callbacks
    private void OnClientConnected(ulong clientId)
    {
        UpdateDebugText($"Client {clientId} connected to lobby");

        if (IsServer)
        {
            UpdateDebugText($"Server: Client {clientId} connected, waiting for join request...");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        UpdateDebugText($"Client {clientId} disconnected from lobby");

        if (IsServer)
        {
            RemovePlayerFromLobby(clientId);
        }

        // If we get disconnected as client, return to main menu
        if (!IsServer && clientId == NetworkManager.Singleton.LocalClientId)
        {
            UpdateDebugText("I was disconnected - returning to main menu");
            CrossSceneData.Reset();
            StartCoroutine(LoadMainMenuAsync());
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

        if (lobbyPlayers != null)
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

    [ContextMenu("Debug Lobby State")]
    public void DebugLobbyState()
    {
        Debug.Log("=== LOBBY STATE ===");
        Debug.Log($"Is Host: {IsHost}");
        Debug.Log($"Is Server: {IsServer}");
        Debug.Log($"Is Client: {IsClient}");
        Debug.Log($"Is Spawned: {IsSpawned}");
        Debug.Log($"Players: {lobbyPlayers?.Count ?? 0}");
        Debug.Log($"Join Code: {currentJoinCode}");

        if (lobbyPlayers != null)
        {
            foreach (var player in lobbyPlayers)
            {
                string characterName = CrossSceneData.GetCharacterName(player.CharacterIndex);
                Debug.Log($"- Player {player.ClientId}: {player.PlayerName} -> {characterName}");
            }
        }
    }
}