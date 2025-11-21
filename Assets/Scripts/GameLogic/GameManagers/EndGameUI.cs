using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class EndGameUI : MonoBehaviour
{
    public static EndGameUI Instance;

    [Header("UI References")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button returnButton;

    [Header("Win Messages")]
    [SerializeField] private string survivorWinTitle = "SURVIVORS WIN!";
    [SerializeField] private string cultistWinTitle = "CULTISTS WIN!";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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

            if (playerController != null)
                playerController.enabled = false;

            if (inventory != null)
                inventory.enabled = false;

            // Hide HUD
            if (GameHUDManager.Instance != null)
            {
                GameHUDManager.Instance.gameObject.SetActive(false);
            }
        }

        // Unlock cursor for ALL clients (not just player owner)
        // This is crucial for being able to click the return button
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Player controls disabled and cursor unlocked for UI interaction");
    }

    private void OnReturnButtonClicked()
    {
        Debug.Log("Return to main menu button clicked");

        // Show immediate feedback that button was clicked
        if (returnButton != null)
        {
            returnButton.interactable = false;
            returnButton.GetComponentInChildren<TMP_Text>().text = "Returning...";
        }

        // If we're the server, trigger immediate return
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (EndGameManager.Instance != null)
            {
                EndGameManager.Instance.ReturnToMainMenuImmediately();
            }
            else
            {
                Debug.LogError("EndGameManager.Instance is null!");
                FallbackReturnToMainMenu();
            }
        }
        else
        {
            // If we're a client, request the server to return
            RequestReturnToMainMenuServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestReturnToMainMenuServerRpc()
    {
        if (EndGameManager.Instance != null)
        {
            EndGameManager.Instance.ReturnToMainMenuImmediately();
        }
        else
        {
            Debug.LogError("EndGameManager.Instance is null on server!");
        }
    }

    private void FallbackReturnToMainMenu()
    {
        Debug.Log("Using fallback method to return to main menu");

        // Clean up cross-scene data
        CrossSceneData.Reset();

        // Load main menu directly
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    // Method to hide the UI (useful for testing)
    public void HideEndGameScreen()
    {
        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        // Re-enable cursor just in case
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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