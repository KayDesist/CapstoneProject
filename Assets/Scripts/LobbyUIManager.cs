using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LobbyUIManager : MonoBehaviour
{
    [Header("Lobby UI")]
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private List<GameObject> playerSlots;
    [SerializeField] private Button startButton;
    [SerializeField] private Button forceStartButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button leaveButton;

    private bool isReady = false;
    private LobbyManager lobby;

    private void Start()
    {
        lobby = FindObjectOfType<LobbyManager>();

        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (forceStartButton != null) forceStartButton.onClick.AddListener(OnForceStartClicked);
        if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);

        if (readyButton != null)
            readyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Ready";
    }

    public void UpdateLobbyCode(string code)
    {
        if (lobbyCodeText != null)
            lobbyCodeText.text = $"Lobby Code: {code}";
    }

    public void UpdatePlayerList(List<string> names, List<bool> readyStates)
    {
        for (int i = 0; i < playerSlots.Count; i++)
        {
            var slot = playerSlots[i];
            if (i < names.Count)
            {
                slot.SetActive(true);
                var nameText = slot.GetComponentInChildren<TextMeshProUGUI>();
                nameText.text = names[i] + (readyStates[i] ? " (Ready)" : "");
            }
            else
            {
                slot.SetActive(false);
            }
        }
    }

    public void SetStartInteractable(bool interactable)
    {
        if (startButton != null)
            startButton.interactable = interactable;
    }

    public void SetForceStartVisible(bool visible)
    {
        if (forceStartButton != null)
            forceStartButton.gameObject.SetActive(visible);
    }

    private void OnStartClicked()
    {
        if (lobby != null)
            lobby.StartGame(false);
    }

    private void OnForceStartClicked()
    {
        if (lobby != null)
            lobby.StartGame(true);
    }

    private void OnReadyClicked()
    {
        if (lobby == null || Unity.Netcode.NetworkManager.Singleton == null ||
            !Unity.Netcode.NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.LogWarning("[UI] Not connected yet – ignoring ready press.");
            return;
        }

        isReady = !isReady;
        readyButton.GetComponentInChildren<TextMeshProUGUI>().text = isReady ? "Unready" : "Ready";
        lobby.SetReadyServerRpc(Unity.Netcode.NetworkManager.Singleton.LocalClientId, isReady);
    }

    private void OnLeaveClicked()
    {
        if (lobby != null)
            lobby.LeaveLobby();
    }

    // Auto refresh patch
    public void AutoRefreshOnConnect(LobbyManager lobby)
    {
        if (lobby != null)
            lobby.Invoke(nameof(lobby.RefreshUI), 0.3f);
    }
}
