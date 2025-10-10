using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LobbyUIManager : MonoBehaviour
{
    [Header("Main Menu UI")]
    [SerializeField] private GameObject backgroundPanel;   // Title background stays visible
    [SerializeField] private Button playButton;            // Play button (gets hidden after click)

    [Header("Lobby UI Panels")]
    [SerializeField] private GameObject lobbyPanel;        // Contains Create/Join/Start/Ready buttons
    [SerializeField] private GameObject playerGridPanel;   // Contains Player Slots
    [SerializeField] private Button backButton;            // The new "Back" button

    [Header("Lobby Elements")]
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button readyUpButton;

    [Header("Player Slots")]
    [SerializeField] private List<GameObject> playerSlots;

    private List<bool> playerReadyStates = new();
    private int currentPlayers = 0;
    private string lobbyCode = "";

    void Start()
    {
        // Hide lobby UI on start, show only title + play button
        lobbyPanel.SetActive(false);
        playerGridPanel.SetActive(false);
        foreach (var slot in playerSlots)
            slot.SetActive(false);

        // Button listeners
        playButton.onClick.AddListener(OpenLobbyUI);
        backButton.onClick.AddListener(CloseLobbyUI);
        createLobbyButton.onClick.AddListener(CreateLobby);
        joinLobbyButton.onClick.AddListener(JoinLobby);
        readyUpButton.onClick.AddListener(ToggleReadyUp);
        startGameButton.onClick.AddListener(StartGame);

        startGameButton.interactable = false;
    }

    // When Play is clicked
    public void OpenLobbyUI()
    {
        playButton.gameObject.SetActive(false); // Hide Play button only
        lobbyPanel.SetActive(true);
        playerGridPanel.SetActive(true);
        Debug.Log("Lobby UI opened.");
    }

    // When Back is clicked
    public void CloseLobbyUI()
    {
        // Hide all lobby-related UI
        lobbyPanel.SetActive(false);
        playerGridPanel.SetActive(false);

        // Reset player slots and data
        foreach (var slot in playerSlots)
            slot.SetActive(false);
        playerReadyStates.Clear();
        currentPlayers = 0;
        lobbyCodeText.text = "Lobby Code: ----";

        // Show Play button again
        playButton.gameObject.SetActive(true);
        Debug.Log("Returned to title screen.");
    }

    public void CreateLobby()
    {
        // Generate a random 4-digit code
        lobbyCode = Random.Range(1000, 9999).ToString();
        lobbyCodeText.text = $"Lobby Code: {lobbyCode}";
        AddPlayer("Player 1");
    }

    public void JoinLobby()
    {
        string enteredCode = joinCodeInput.text;

        if (enteredCode == lobbyCode && !string.IsNullOrEmpty(lobbyCode))
        {
            AddPlayer($"Player {currentPlayers + 1}");
        }
        else
        {
            Debug.LogWarning("Incorrect or missing lobby code!");
        }
    }

    public void AddPlayer(string playerName)
    {
        if (currentPlayers >= playerSlots.Count)
            return;

        var slot = playerSlots[currentPlayers];
        slot.SetActive(true);

        var nameText = slot.transform.Find("PlayerName").GetComponent<TMP_Text>();
        var readyText = slot.transform.Find("ReadyUp").GetComponent<TMP_Text>();

        nameText.text = playerName;
        readyText.text = "Not Ready";
        readyText.color = Color.gray;

        playerReadyStates.Add(false);
        currentPlayers++;
    }

    public void ToggleReadyUp()
    {
        // Toggle readiness for the first player (for now)
        if (currentPlayers == 0) return;

        var slot = playerSlots[0];
        var readyText = slot.transform.Find("ReadyUp").GetComponent<TMP_Text>();

        bool isReady = playerReadyStates[0];
        playerReadyStates[0] = !isReady;

        if (playerReadyStates[0])
        {
            readyText.text = "Ready";
            readyText.color = Color.green;
        }
        else
        {
            readyText.text = "Not Ready";
            readyText.color = Color.gray;
        }

        CheckAllReady();
    }

    public void CheckAllReady()
    {
        bool allReady = true;
        foreach (bool ready in playerReadyStates)
        {
            if (!ready)
            {
                allReady = false;
                break;
            }
        }

        startGameButton.interactable = allReady && currentPlayers > 0;
    }

    public void StartGame()
    {
        Debug.Log("All players ready — starting game...");
        // Placeholder for future scene transition
    }
}
