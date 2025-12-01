using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class LobbyUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button leaveButton;

    [Header("Player Slots - Drag ALL player slots here")]
    [SerializeField] private List<GameObject> playerSlots;

    [Header("Character Sprites - Assign in order: 0=Jaxen, 1=Sam, 2=Mizuki, 3=Elijah, 4=Clint")]
    [SerializeField] private Sprite[] characterSprites;

    [Header("Manager Reference")]
    [SerializeField] private LobbyManager lobbyManager;

    private void Awake()
    {
        if (lobbyManager == null)
            lobbyManager = FindObjectOfType<LobbyManager>();

        SetupUI();
        HideAllPlayerSlots();
    }

    private void SetupUI()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveClicked);

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

        // Sort players by client ID for consistent order
        var sortedPlayers = players.OrderBy(p => p.ClientId).ToList();

        // Then show and update slots for current players
        for (int i = 0; i < sortedPlayers.Count && i < playerSlots.Count; i++)
        {
            if (playerSlots[i] != null)
            {
                playerSlots[i].SetActive(true);
                SetupPlayerSlot(playerSlots[i], sortedPlayers[i]);
            }
        }
    }

    private void SetupPlayerSlot(GameObject playerSlot, LobbyPlayerData playerData)
    {
        // Find the child UI components
        TMP_Text playerNameText = playerSlot.transform.Find("Player Name")?.GetComponent<TMP_Text>();
        TMP_Text readyStateText = playerSlot.transform.Find("Ready State")?.GetComponent<TMP_Text>();
        TMP_Text characterNameText = playerSlot.transform.Find("Character Name")?.GetComponent<TMP_Text>();
        Image characterImage = playerSlot.transform.Find("Character Image")?.GetComponent<Image>();

        // Get character info
        string characterName = CrossSceneData.GetCharacterName(playerData.CharacterIndex);
        Sprite characterSprite = GetCharacterSprite(playerData.CharacterIndex);

        if (playerNameText != null)
        {
            // Convert FixedString to regular string
            string name = playerData.PlayerName.ToString();

            // Add (Host) indicator if this is the host (clientId 0)
            if (playerData.ClientId == 0)
            {
                playerNameText.text = name + " (Host)";
                playerNameText.color = Color.yellow; // Optional: Make host name stand out
            }
            else
            {
                playerNameText.text = name;
                playerNameText.color = Color.white;
            }
        }

        if (readyStateText != null)
        {
            readyStateText.text = playerData.IsReady ? "Ready ✓" : "Not Ready";
            readyStateText.color = playerData.IsReady ? Color.green : Color.gray;
        }

        // Update character name and image
        if (characterNameText != null)
        {
            characterNameText.text = characterName;
        }

        if (characterImage != null)
        {
            if (characterSprite != null)
            {
                characterImage.sprite = characterSprite;
                characterImage.color = Color.white;
            }
            else
            {
                characterImage.color = Color.clear;
                Debug.LogWarning($"No character sprite found for index {playerData.CharacterIndex}");
            }
        }
    }

    private Sprite GetCharacterSprite(int characterIndex)
    {
        if (characterSprites != null && characterIndex >= 0 && characterIndex < characterSprites.Length)
        {
            return characterSprites[characterIndex];
        }
        return null;
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

    [ContextMenu("Test UI Update")]
    public void TestUIUpdate()
    {
        // Create test data
        var testPlayers = new List<LobbyPlayerData>
        {
            new LobbyPlayerData(0, "Test Host", true, 2), // Mizuki
            new LobbyPlayerData(1, "Test Player 1", true, 1), // Sam
            new LobbyPlayerData(2, "Test Player 2", false, 3) // Elijah
        };

        UpdatePlayerList(testPlayers);
        Debug.Log("Test UI update completed");
    }
}