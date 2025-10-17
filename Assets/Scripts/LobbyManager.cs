using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    [SerializeField] private LobbyUIManager ui;

    private NetworkList<FixedString64Bytes> playerNames;
    private NetworkList<bool> playerReadyStates;

    private readonly Dictionary<ulong, int> clientSlotMap = new();
    private string lobbyCode;

    private const int MinPlayers = 5;
    private const int MaxPlayers = 10;

    private void Awake()
    {
        playerNames = new NetworkList<FixedString64Bytes>();
        playerReadyStates = new NetworkList<bool>();

        playerNames.OnListChanged += _ => RefreshUI();
        playerReadyStates.OnListChanged += _ => RefreshUI();
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[Lobby] No NetworkManager found in scene!");
            return;
        }

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        string mode = PlayerPrefs.GetString("LobbyMode");
        if (mode == "Host") StartAsHost();
        else if (mode == "Client") StartAsClient(PlayerPrefs.GetString("JoinCode"));
    }

    // -----------------------------
    //  HOST / CLIENT SETUP
    // -----------------------------
    private void StartAsHost()
    {
        lobbyCode = Random.Range(100000, 999999).ToString("000000");

        if (!NetworkManager.Singleton.StartHost())
        {
            Debug.LogError("[Lobby] Failed to start host!");
            return;
        }

        ui.UpdateLobbyCode(lobbyCode);

        // Directly register host in server list (no RPC needed)
        AddPlayer_Internal(NetworkManager.Singleton.LocalClientId, $"Host_{NetworkManager.Singleton.LocalClientId}");

        RefreshUI();
        ui.AutoRefreshOnConnect(this);
    }

    private void StartAsClient(string code)
    {
        lobbyCode = code;
        ui.UpdateLobbyCode(lobbyCode);

        if (!NetworkManager.Singleton.StartClient())
        {
            Debug.LogError("[Lobby] Failed to start client!");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedToServer;
    }

    private void OnClientConnectedToServer(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            AddPlayerServerRpc(clientId, $"Player_{clientId}");
        }

        ui.AutoRefreshOnConnect(this);
    }

    // -----------------------------
    //  PLAYER MANAGEMENT
    // -----------------------------
    [ServerRpc(RequireOwnership = false)]
    private void AddPlayerServerRpc(ulong clientId, string name)
    {
        AddPlayer_Internal(clientId, name);
    }

    private void AddPlayer_Internal(ulong clientId, string name)
    {
        if (playerNames.Count >= MaxPlayers)
        {
            Debug.LogWarning($"[Lobby] Max players reached; rejecting {clientId}");
            return;
        }

        int slotIndex = playerNames.Count;
        clientSlotMap[clientId] = slotIndex;

        playerNames.Add(new FixedString64Bytes(name));
        playerReadyStates.Add(false);

        Debug.Log($"[Lobby] Added {name} in slot {slotIndex + 1}");
        RefreshUI();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(ulong clientId, bool ready)
    {
        if (!clientSlotMap.TryGetValue(clientId, out int index))
        {
            Debug.LogWarning($"[Lobby] Client {clientId} not in slot map; ignoring ready.");
            return;
        }

        if (index < playerReadyStates.Count)
        {
            playerReadyStates[index] = ready;
            Debug.Log($"[Lobby] {playerNames[index]} ready = {ready}");
        }

        RefreshUI();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (!clientSlotMap.TryGetValue(clientId, out int slot)) return;

        Debug.Log($"[Lobby] Client {clientId} disconnected – removing slot {slot + 1}");

        // Remove from lists & compact
        playerNames.RemoveAt(slot);
        playerReadyStates.RemoveAt(slot);
        clientSlotMap.Remove(clientId);

        // Rebuild mapping after compression
        clientSlotMap.Clear();
        for (int i = 0; i < playerNames.Count; i++)
            clientSlotMap[NetworkManager.Singleton.ConnectedClientsIds[i]] = i;

        RefreshUI();
    }

    public void LeaveLobby()
    {
        Debug.Log("[Lobby] Leaving lobby...");
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("MainMenu");
    }

    // -----------------------------
    //  GAME START LOGIC
    // -----------------------------
    private bool AllPlayersReady()
    {
        int count = Mathf.Min(playerNames.Count, playerReadyStates.Count);
        if (count < MinPlayers) return false;

        for (int i = 0; i < count; i++)
        {
            if (playerNames[i].ToString() == "Empty") return false;
            if (!playerReadyStates[i]) return false;
        }
        return true;
    }

    public void StartGame(bool forceStart = false)
    {
        if (!IsServer) return;

        if (!forceStart && !AllPlayersReady())
        {
            Debug.Log("[Lobby] Not all ready or not enough players; cannot start.");
            return;
        }

        Debug.Log(forceStart ? "[Lobby] Host force-started game." : "[Lobby] All ready – starting game.");
        // SceneManager.LoadScene("GameScene");  // Or use ServerChangeScene when ready
    }

    public void RefreshUI()
    {
        if (ui == null) return;

        int count = Mathf.Max(playerNames.Count, playerReadyStates.Count);
        List<string> names = new(count);
        List<bool> ready = new(count);

        for (int i = 0; i < count; i++)
        {
            names.Add(i < playerNames.Count ? playerNames[i].ToString() : "Empty");
            ready.Add(i < playerReadyStates.Count && playerReadyStates[i]);
        }

        ui.UpdatePlayerList(names, ready);

        bool canStart = AllPlayersReady();
        ui.SetStartInteractable(canStart);
        ui.SetForceStartVisible(IsServer);
    }
}
