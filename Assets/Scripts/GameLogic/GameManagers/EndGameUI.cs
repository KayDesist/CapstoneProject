using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class EndGameUI : MonoBehaviour
{
    public static EndGameUI Instance;

    [Header("UI References")]
    [SerializeField] public GameObject endGamePanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button returnButton;

    [Header("Win Messages")]
    [SerializeField] private string survivorWinTitle = "SURVIVORS WIN!";
    [SerializeField] private string cultistWinTitle = "CULTISTS WIN!";

    private bool isEndGameActive = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Hide panel initially
        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        // Setup return button
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(OnReturnButtonClicked);
        }

        Debug.Log("EndGameUI initialized");

        // Subscribe to scene change event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Auto-destroy if we're in the MainMenu scene
        if (scene.name == "MainMenu")
        {
            Debug.Log("EndGameUI detected MainMenu scene - cleaning up");
            Cleanup();
        }
    }

    public void ShowEndGameScreen(EndGameManager.GameResult result)
    {
        if (endGamePanel == null)
        {
            Debug.LogError("EndGamePanel reference is null!");
            return;
        }

        isEndGameActive = true;

        // Force the gameObject to be active
        gameObject.SetActive(true);
        endGamePanel.SetActive(true);

        // Set content based on game result
        switch (result)
        {
            case EndGameManager.GameResult.SurvivorsWinByTasks:
                SetSurvivorWinUI("Survivors completed all their tasks and escaped!");
                break;

            case EndGameManager.GameResult.SurvivorsWinByKill:
                SetSurvivorWinUI("Survivors eliminated the cultist threat!");
                break;

            case EndGameManager.GameResult.CultistsWinByTasksAndKill:
                SetCultistWinUI("The cult completed their ritual and made a sacrifice!");
                break;

            case EndGameManager.GameResult.CultistsWinByElimination:
                SetCultistWinUI("The cult eliminated all survivors!");
                break;

            default:
                SetSurvivorWinUI("Game ended unexpectedly!");
                break;
        }

        // Disable player controls and enable mouse interaction
        DisablePlayerControls();

        Debug.Log($"End game UI shown for result: {result}");
    }

    private void SetSurvivorWinUI(string description)
    {
        if (titleText != null)
        {
            titleText.text = survivorWinTitle;
            titleText.color = Color.blue;
        }

        if (descriptionText != null)
            descriptionText.text = description;
    }

    private void SetCultistWinUI(string description)
    {
        if (titleText != null)
        {
            titleText.text = cultistWinTitle;
            titleText.color = Color.red;
        }

        if (descriptionText != null)
            descriptionText.text = description;
    }

    private void DisablePlayerControls()
    {
        // Find local player and disable controls
        var localPlayer = FindObjectOfType<NetworkPlayerController>();
        if (localPlayer != null && localPlayer.IsOwner)
        {
            localPlayer.enabled = false;

            // Also disable PlayerSpectator
            var spectator = localPlayer.GetComponent<PlayerSpectator>();
            if (spectator != null)
            {
                spectator.enabled = false;
            }
        }

        // Unlock cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Player controls disabled for end game UI");
    }

    private void OnReturnButtonClicked()
    {
        if (!isEndGameActive) return;

        Debug.Log("Return to main menu button clicked");

        // Prevent multiple clicks
        isEndGameActive = false;

        if (returnButton != null)
        {
            returnButton.interactable = false;
            returnButton.GetComponentInChildren<TMP_Text>().text = "Returning...";
        }

        // Request server to return to Main Menu
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // We are the host
            if (EndGameManager.Instance != null)
            {
                EndGameManager.Instance.ReturnToMainMenu();
            }
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            // We are a client, request the host
            RequestReturnToMainMenuServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestReturnToMainMenuServerRpc()
    {
        if (EndGameManager.Instance != null)
        {
            EndGameManager.Instance.ReturnToMainMenu();
        }
    }

    // Method to hide the UI (useful for testing)
    public void HideEndGameScreen()
    {
        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        isEndGameActive = false;
    }

    private void Cleanup()
    {
        Debug.Log("EndGameUI cleaning up in MainMenu scene");

        // Hide the UI
        HideEndGameScreen();

        // Destroy this gameObject
        if (Instance == this)
        {
            Instance = null;
        }

        Destroy(gameObject);
    }

    // Clean up when returning to main menu
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // Unsubscribe from scene change event
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}