using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class MainMenuUI : MonoBehaviour
{
    [Header("Main Buttons")]
    [SerializeField] private GameObject hostButton;
    [SerializeField] private GameObject joinButton;

    [Header("Join Panel")]
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button confirmJoinButton;
    [SerializeField] private Button cancelJoinButton;

    [Header("Debug Info")]
    [SerializeField] private TMP_Text debugText;

    private void Start()
    {
        joinPanel.SetActive(false);

        // Clear any previous data
        CrossSceneData.Reset();

        // Button events
        hostButton.GetComponent<Button>().onClick.AddListener(OnHostClicked);
        joinButton.GetComponent<Button>().onClick.AddListener(OnJoinClicked);
        confirmJoinButton.onClick.AddListener(OnConfirmJoinClicked);
        cancelJoinButton.onClick.AddListener(OnCancelJoinClicked);

        UpdateDebugText("MainMenu loaded - Ready");
    }

    private void Update()
    {
        // Press F3 to debug cameras
        if (Input.GetKeyDown(KeyCode.F3))
        {
            Debug.Log("=== MAIN MENU CAMERA DEBUG ===");

            Camera[] cameras = FindObjectsOfType<Camera>();
            Debug.Log($"Total cameras: {cameras.Length}");

            foreach (Camera cam in cameras)
            {
                string status = cam.enabled ? "ENABLED" : "disabled";
                string tagInfo = cam.CompareTag("MainCamera") ? "[MAIN]" : "";
                Debug.Log($"- {cam.name}: {status} {tagInfo} (Depth: {cam.depth})");
            }

            // Check which camera is actually rendering
            if (Camera.main != null)
            {
                Debug.Log($"Camera.main is: {Camera.main.name}");
            }
            else
            {
                Debug.LogError("Camera.main is NULL!");
            }
        }
    }

    private void OnHostClicked()
    {
        UpdateDebugText("Host button clicked - Setting up as Host");
        CrossSceneData.LobbyMode = "Host";
        CrossSceneData.JoinCode = ""; // Clear any previous join code
        Debug.Log($"MAIN MENU: Starting as HOST. LobbyMode: {CrossSceneData.LobbyMode}");
        SceneManager.LoadScene("Lobby");
    }

    private void OnJoinClicked()
    {
        UpdateDebugText("Join button clicked - Showing join panel");
        // Hide menu buttons, show join UI
        hostButton.SetActive(false);
        joinButton.SetActive(false);
        joinPanel.SetActive(true);
        joinCodeInput.text = "";
        joinCodeInput.Select();
        joinCodeInput.ActivateInputField();

        // Ensure the panel is fully visible and interactive
        joinPanel.transform.SetAsLastSibling(); // Bring to front

        UpdateDebugText("Join panel activated - Ready for code input");
    }

    private void OnConfirmJoinClicked()
    {
        string code = joinCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            UpdateDebugText("ERROR: No lobby code entered!");
            Debug.LogWarning("No lobby code entered!");

            // Visual feedback - make input field flash red
            StartCoroutine(FlashInputFieldRed());
            return;
        }

        if (code.Length < 4)
        {
            UpdateDebugText("ERROR: Join code too short!");
            Debug.LogWarning($"Join code too short: {code}");
            StartCoroutine(FlashInputFieldRed());
            return;
        }

        UpdateDebugText($"Joining lobby with code: {code}");
        CrossSceneData.LobbyMode = "Client";
        CrossSceneData.JoinCode = code;
        Debug.Log($"MAIN MENU: Starting as CLIENT. LobbyMode: {CrossSceneData.LobbyMode}, JoinCode: {CrossSceneData.JoinCode}");
        SceneManager.LoadScene("Lobby");
    }

    private void OnCancelJoinClicked()
    {
        UpdateDebugText("Cancelled join - Returning to main menu");
        // Bring buttons back, hide join panel
        joinPanel.SetActive(false);
        hostButton.SetActive(true);
        joinButton.SetActive(true);
    }

    private System.Collections.IEnumerator FlashInputFieldRed()
    {
        if (joinCodeInput != null)
        {
            Color originalColor = joinCodeInput.image.color;
            joinCodeInput.image.color = Color.red;
            yield return new WaitForSeconds(0.5f);
            joinCodeInput.image.color = originalColor;
        }
    }

    private void UpdateDebugText(string message)
    {
        if (debugText != null)
        {
            debugText.text = $"[Debug] {message}";
        }
        Debug.Log($"[MainMenu] {message}");
    }

    // Debug method to check current state
    [ContextMenu("Debug Current State")]
    public void DebugCurrentState()
    {
        Debug.Log($"=== MAIN MENU STATE ===");
        Debug.Log($"LobbyMode: {CrossSceneData.LobbyMode}");
        Debug.Log($"JoinCode: {CrossSceneData.JoinCode}");
        Debug.Log($"Host Button Active: {hostButton.activeInHierarchy}");
        Debug.Log($"Join Button Active: {joinButton.activeInHierarchy}");
        Debug.Log($"Join Panel Active: {joinPanel.activeInHierarchy}");
    }
}