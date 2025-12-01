using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Threading;
using System;
using Unity.Netcode;

public class MainMenuUI : MonoBehaviour
{
    [Header("Main Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Header("Join Panel")]
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button confirmJoinButton;
    [SerializeField] private Button cancelJoinButton;

    [Header("Debug Info")]
    [SerializeField] private TMP_Text debugText;

    [Header("Character Display")]
    [SerializeField] private MainMenuCharacterManager characterManager;

    private RelayConnector relayConnector;
    private CancellationTokenSource cancellationTokenSource;
    private bool isConnecting = false;

    private void Start()
    {
        // Initialize UI
        if (joinPanel != null)
            joinPanel.SetActive(false);

        // Clear any previous data
        CrossSceneData.Reset();

        // Setup button listeners
        if (hostButton != null)
            hostButton.onClick.AddListener(OnHostClicked);

        if (joinButton != null)
            joinButton.onClick.AddListener(OnJoinClicked);

        if (confirmJoinButton != null)
            confirmJoinButton.onClick.AddListener(OnConfirmJoinClicked);

        if (cancelJoinButton != null)
            cancelJoinButton.onClick.AddListener(OnCancelJoinClicked);

        // Create cancellation token source
        cancellationTokenSource = new CancellationTokenSource();

        UpdateDebugText("MainMenu ready");

        // Ensure character manager continues to work
        if (characterManager != null)
        {
            characterManager.enabled = true;
        }
    }

    private void Update()
    {
        // Simple debug key
        if (Input.GetKeyDown(KeyCode.F3))
        {
            CrossSceneData.LogCurrentData();
        }
    }

    private async void OnHostClicked()
    {
        if (isConnecting) return;

        try
        {
            isConnecting = true;
            UpdateDebugText("Creating lobby...");

            // Disable buttons during connection
            SetButtonsInteractable(false);

            // Clean up any existing network state
            await CleanupNetworkState();

            // Setup lobby data
            CrossSceneData.LobbyMode = "Host";
            CrossSceneData.JoinCode = "";

            // Create and use relay connector
            relayConnector = gameObject.AddComponent<RelayConnector>();

            string joinCode = await relayConnector.StartHostWithRelay(10, "wss");

            if (string.IsNullOrEmpty(joinCode))
            {
                UpdateDebugText("Failed to create lobby");
                SetButtonsInteractable(true);
                return;
            }

            CrossSceneData.JoinCode = joinCode;
            UpdateDebugText($"Lobby created! Code: {joinCode}");

            // Load lobby scene immediately without delay
            SceneManager.LoadScene("Lobby");
        }
        catch (Exception e)
        {
            UpdateDebugText($"Error: {e.Message}");
            SetButtonsInteractable(true);
        }
        finally
        {
            isConnecting = false;
        }
    }

    private void OnJoinClicked()
    {
        if (isConnecting) return;

        // Show join panel
        if (hostButton != null) hostButton.gameObject.SetActive(false);
        if (joinButton != null) joinButton.gameObject.SetActive(false);
        if (joinPanel != null)
        {
            joinPanel.SetActive(true);
            joinPanel.transform.SetAsLastSibling();
        }

        if (joinCodeInput != null)
        {
            joinCodeInput.text = "";
            joinCodeInput.Select();
            joinCodeInput.ActivateInputField();
        }

        UpdateDebugText("Enter 6-character lobby code");
    }

    private async void OnConfirmJoinClicked()
    {
        if (isConnecting) return;

        string code = joinCodeInput?.text?.Trim().ToUpper() ?? "";

        if (string.IsNullOrEmpty(code) || code.Length < 6)
        {
            UpdateDebugText("Code must be at least 6 characters");
            FlashInputFieldRed();
            return;
        }

        try
        {
            isConnecting = true;
            UpdateDebugText($"Joining lobby: {code}");

            // Disable buttons during connection
            SetButtonsInteractable(false);

            // Clean up any existing network state
            await CleanupNetworkState();

            // Setup lobby data
            CrossSceneData.LobbyMode = "Client";
            CrossSceneData.JoinCode = code;

            // Create and use relay connector
            relayConnector = gameObject.AddComponent<RelayConnector>();

            bool joined = await relayConnector.StartClientWithRelay(code, "wss");

            if (!joined)
            {
                UpdateDebugText("Failed to join lobby");
                SetButtonsInteractable(true);
                return;
            }

            UpdateDebugText("Successfully joined!");

            // Load lobby scene immediately without delay
            SceneManager.LoadScene("Lobby");
        }
        catch (Exception e)
        {
            UpdateDebugText($"Error: {e.Message}");
            SetButtonsInteractable(true);
        }
        finally
        {
            isConnecting = false;
        }
    }

    private void OnCancelJoinClicked()
    {
        // Return to main buttons
        if (hostButton != null) hostButton.gameObject.SetActive(true);
        if (joinButton != null) joinButton.gameObject.SetActive(true);
        if (joinPanel != null) joinPanel.SetActive(false);

        UpdateDebugText("Join cancelled");
    }

    private async System.Threading.Tasks.Task CleanupNetworkState()
    {
        try
        {
            // Shut down any existing NetworkManager
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                await System.Threading.Tasks.Task.Delay(100);
            }

            // Clean up any leftover RelayConnector
            var oldConnectors = GetComponents<RelayConnector>();
            foreach (var connector in oldConnectors)
            {
                if (connector != relayConnector)
                    Destroy(connector);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Cleanup warning: {e.Message}");
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (hostButton != null)
        {
            hostButton.interactable = interactable;
            hostButton.gameObject.SetActive(interactable);
        }

        if (joinButton != null)
        {
            joinButton.interactable = interactable;
            joinButton.gameObject.SetActive(interactable);
        }

        if (confirmJoinButton != null)
            confirmJoinButton.interactable = interactable;

        if (cancelJoinButton != null)
            cancelJoinButton.interactable = interactable;
    }

    private async void FlashInputFieldRed()
    {
        if (joinCodeInput != null && joinCodeInput.image != null)
        {
            Color originalColor = joinCodeInput.image.color;
            joinCodeInput.image.color = Color.red;

            // Use Task.Delay instead of coroutine for async method
            await System.Threading.Tasks.Task.Delay(500);

            if (joinCodeInput != null && joinCodeInput.image != null)
                joinCodeInput.image.color = originalColor;
        }
    }

    private void UpdateDebugText(string message)
    {
        if (debugText != null)
        {
            debugText.text = $"[MainMenu] {message}";
        }
        Debug.Log($"[MainMenuUI] {message}");
    }

    private void OnDestroy()
    {
        // Cancel any ongoing operations
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();

        // Clean up relay connector
        if (relayConnector != null)
            Destroy(relayConnector);
    }
}