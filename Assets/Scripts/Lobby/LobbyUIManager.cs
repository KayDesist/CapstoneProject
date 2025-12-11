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

    [Header("Player Slots")]
    [SerializeField] private List<GameObject> playerSlots;

    [Header("Manager Reference")]
    [SerializeField] private LobbyManager lobbyManager;

    // Initialize components
    private void Awake()
    {
        if (lobbyManager == null)
            lobbyManager = FindObjectOfType<LobbyManager>();

        SetupUI();

        HideAllPlayerSlots();
    }

    // Setup UI components
    private void SetupUI()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveClicked);

        if (startButton != null)
            startButton.gameObject.SetActive(false);
    }

    // Hide all player slots
    private void HideAllPlayerSlots()
    {
        foreach (var slot in playerSlots)
        {
            if (slot != null)
                slot.SetActive(false);
        }
    }

    // Update each frame
    private void Update()
    {
        UpdatePlayerCountDisplay();
    }

    // Update player count display
    public void UpdatePlayerCountDisplay()
    {
        if (playerCountText != null && lobbyManager != null)
        {
            int playerCount = lobbyManager.GetCurrentPlayerCount();
            playerCountText.text = $"Players: {playerCount}/10";
        }
    }

    // Update lobby code display
    public void UpdateLobbyCodeDisplay(string joinCode)
    {
        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = $"Join Code: {joinCode}";
        }
    }

    // Update player list display
    public void UpdatePlayerList(List<NetworkPlayerInfo> players)
    {
        HideAllPlayerSlots();

        for (int i = 0; i < players.Count && i < playerSlots.Count; i++)
        {
            if (playerSlots[i] != null)
            {
                playerSlots[i].SetActive(true);
                SetupPlayerSlot(playerSlots[i], players[i]);
            }
        }
    }

    // Setup individual player slot
    private void SetupPlayerSlot(GameObject playerSlot, NetworkPlayerInfo playerData)
    {
        TMP_Text playerNameText = playerSlot.transform.Find("Player Name")?.GetComponent<TMP_Text>();
        TMP_Text readyStateText = playerSlot.transform.Find("Ready State")?.GetComponent<TMP_Text>();

        if (playerNameText != null)
        {
            playerNameText.text = playerData.PlayerName.ToString();

            if (playerData.ClientId == 0)
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

    // Show/hide start button
    public void SetStartButtonVisible(bool visible)
    {
        if (startButton != null)
        {
            startButton.gameObject.SetActive(visible);
        }
    }

    // Start button clicked
    private void OnStartClicked()
    {
        if (lobbyManager != null)
            lobbyManager.StartGame();
    }

    // Leave button clicked
    private void OnLeaveClicked()
    {
        if (lobbyManager != null)
            lobbyManager.LeaveLobby();
    }
}