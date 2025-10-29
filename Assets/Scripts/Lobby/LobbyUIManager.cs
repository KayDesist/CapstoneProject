using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LobbyUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button leaveButton;

    [Header("Player Slots - Drag ALL player slots here")]
    [SerializeField] private List<GameObject> playerSlots; // Drag Player_1, Player_2, etc. here

    [Header("Manager Reference")]
    [SerializeField] private LobbyManager lobbyManager;

    private void Awake()
    {
        if (lobbyManager == null)
            lobbyManager = FindObjectOfType<LobbyManager>();

        SetupUI();

        // Initially hide all player slots
        HideAllPlayerSlots();
    }

    private void SetupUI()
    {
        // Button listeners
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveClicked);

        // Initially hide start button
        if (startButton != null)
            startButton.gameObject.SetActive(false);
    }

    private void HideAllPlayerSlots()
    {
        foreach (var slot in playerSlots)
        {
            if (slot != null)
                slot.SetActive(false);
        }
    }

    private void Update()
    {
        UpdatePlayerCountDisplay();
    }

    public void UpdatePlayerCountDisplay()
    {
        if (playerCountText != null && lobbyManager != null)
        {
            int playerCount = lobbyManager.GetCurrentPlayerCount();
            playerCountText.text = $"Players: {playerCount}/10";
        }
    }

    public void UpdateLobbyCodeDisplay(string joinCode)
    {
        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = $"Join Code: {joinCode}";
        }
    }

    public void UpdatePlayerList(List<LobbyPlayerData> players)
    {
        // First hide all slots
        HideAllPlayerSlots();

        // Then show and update slots for current players
        for (int i = 0; i < players.Count && i < playerSlots.Count; i++)
        {
            if (playerSlots[i] != null)
            {
                playerSlots[i].SetActive(true);
                SetupPlayerSlot(playerSlots[i], players[i]);
            }
        }
    }

    private void SetupPlayerSlot(GameObject playerSlot, LobbyPlayerData playerData)
    {
        // Find the child text components and update them
        TMP_Text playerNameText = playerSlot.transform.Find("Player Name")?.GetComponent<TMP_Text>();
        TMP_Text readyStateText = playerSlot.transform.Find("Ready State")?.GetComponent<TMP_Text>();

        if (playerNameText != null)
        {
            playerNameText.text = playerData.PlayerName.ToString();

            // Add (Host) indicator if this is the host
            if (playerData.ClientId == 0) // Host is usually client ID 0
            {
                playerNameText.text += " (Host)";
            }
        }

        if (readyStateText != null)
        {
            readyStateText.text = playerData.IsReady ? "Ready ✓" : "Not Ready";
            readyStateText.color = playerData.IsReady ? Color.green : Color.gray;
        }
    }

    public void SetStartButtonVisible(bool visible)
    {
        if (startButton != null)
        {
            startButton.gameObject.SetActive(visible);
        }
    }

    private void OnStartClicked()
    {
        if (lobbyManager != null)
            lobbyManager.StartGame();
    }

    private void OnLeaveClicked()
    {
        if (lobbyManager != null)
            lobbyManager.LeaveLobby();
    }
}