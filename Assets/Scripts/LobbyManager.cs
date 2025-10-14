using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;
using Unity.Collections;

public class LobbyManager : NetworkBehaviour
{
    [SerializeField] private LobbyUIManager ui;

    private NetworkList<FixedString64Bytes> playerNames;
    private string lobbyCode;

    private const int MinPlayers = 5;
    private const int MaxPlayers = 10;

    private void Awake()
    {
        playerNames = new NetworkList<FixedString64Bytes>();
        playerNames.OnListChanged += OnPlayerListChanged;
    }

    private void Start()
    {
        // Handle disconnect cleanup
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        string mode = PlayerPrefs.GetString("LobbyMode");
        if (mode == "Host") StartAsHost();
        else if (mode == "Client") StartAsClient(PlayerPrefs.GetString("JoinCode"));
    }

    private void StartAsHost()
    {
        lobbyCode = Random.Range(100000, 999999).ToString();
        NetworkManager.Singleton.StartHost();
        ui.UpdateLobbyCode(lobbyCode);
        AddPlayerServerRpc(NetworkManager.Singleton.LocalClientId, "Host");
    }

    private void StartAsClient(string code)
    {
        lobbyCode = code;
        ui.UpdateLobbyCode(lobbyCode);
        NetworkManager.Singleton.StartClient();
        AddPlayerServerRpc(NetworkManager.Singleton.LocalClientId, "Client");
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPlayerServerRpc(ulong clientId, string name)
    {
        if (playerNames.Count >= MaxPlayers)
            return;

        playerNames.Add(name);
    }

    private void OnPlayerListChanged(NetworkListEvent<FixedString64Bytes> change)
    {
        var names = new List<string>();
        foreach (var n in playerNames)
            names.Add(n.ToString());

        ui.UpdatePlayerList(names);
        ui.SetStartInteractable(playerNames.Count >= MinPlayers);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer && playerNames.Count > 0)
            playerNames.RemoveAt(playerNames.Count - 1);
    }

    public void LeaveLobby()
    {
        if (NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
