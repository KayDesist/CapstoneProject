using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

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
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Make it persist
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
    }

    public void ShowEndGameScreen(EndGameManager.GameResult result)
    {
        if (endGamePanel == null)
        {
            Debug.LogError("EndGamePanel reference is null!");
            return;
        }

        isEndGameActive = true;
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
            // Disable various components
            var playerController = localPlayer.GetComponent<NetworkPlayerController>();
            var inventory = localPlayer.GetComponent<InventorySystem>();
            var spectator = localPlayer.GetComponent<PlayerSpectator>();

            if (playerController != null)
                playerController.enabled = false;

            if (inventory != null)
                inventory.enabled = false;

            if (spectator != null)
                spectator.enabled = false;

            // Hide HUD
            if (GameHUDManager.Instance != null)
            {
                GameHUDManager.Instance.gameObject.SetActive(false);
            }
        }

        // Unlock cursor for ALL clients
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Player controls disabled and cursor unlocked for UI interaction");
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

        // Clean up cross-scene data
        CrossSceneData.Reset();

        // Reset all manager instances
        ResetManagers();

        // Load main menu directly
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");

        // Destroy this instance after scene load
        Destroy(gameObject, 0.5f);
    }

    private void ResetManagers()
    {
        // Destroy all manager instances
        var endGameManager = FindObjectOfType<EndGameManager>();
        if (endGameManager != null) Destroy(endGameManager.gameObject);

        var roleManager = FindObjectOfType<RoleManager>();
        if (roleManager != null) Destroy(roleManager.gameObject);

        var taskManager = FindObjectOfType<TaskManager>();
        if (taskManager != null) Destroy(taskManager.gameObject);

        var hudManager = FindObjectOfType<GameHUDManager>();
        if (hudManager != null) Destroy(hudManager.gameObject);

        // Shutdown network if it exists
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }

    // Method to hide the UI (useful for testing)
    public void HideEndGameScreen()
    {
        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        isEndGameActive = false;
    }

    // Debug method to check UI state
    [ContextMenu("Debug UI State")]
    public void DebugUIState()
    {
        Debug.Log($"EndGamePanel active: {endGamePanel != null && endGamePanel.activeInHierarchy}");
        Debug.Log($"Return button interactable: {returnButton != null && returnButton.interactable}");
        Debug.Log($"Cursor lock state: {Cursor.lockState}, visible: {Cursor.visible}");
        Debug.Log($"NetworkManager exists: {NetworkManager.Singleton != null}");
        if (NetworkManager.Singleton != null)
        {
            Debug.Log($"IsServer: {NetworkManager.Singleton.IsServer}");
            Debug.Log($"IsClient: {NetworkManager.Singleton.IsClient}");
        }
    }
}
