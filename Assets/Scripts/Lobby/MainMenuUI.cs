using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

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

    private void Start()
    {
        // FIXED: Clean up NetworkManager and services when returning to main menu
        CleanupNetworkManager();

        joinPanel.SetActive(false);

        // Button events
        hostButton.GetComponent<Button>().onClick.AddListener(OnHostClicked);
        joinButton.GetComponent<Button>().onClick.AddListener(OnJoinClicked);
        confirmJoinButton.onClick.AddListener(OnConfirmJoinClicked);
        cancelJoinButton.onClick.AddListener(OnCancelJoinClicked);
    }

    private void CleanupNetworkManager()
    {
        Debug.Log("Cleaning up NetworkManager for new session...");

        // Shutdown NetworkManager if it exists and is running
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsListening)
            {
                Debug.Log("Shutting down existing NetworkManager...");
                NetworkManager.Singleton.Shutdown();
            }

            // FIXED: Important - Destroy the NetworkManager to allow fresh start
            Destroy(NetworkManager.Singleton.gameObject);
        }

        // Find and destroy any remaining NetworkManager instances
        NetworkManager[] networkManagers = FindObjectsOfType<NetworkManager>();
        foreach (NetworkManager nm in networkManagers)
        {
            if (nm != null && nm.gameObject != null)
            {
                Debug.Log($"Destroying NetworkManager: {nm.gameObject.name}");
                Destroy(nm.gameObject);
            }
        }

        // Clean up any remaining network objects
        NetworkObject[] networkObjects = FindObjectsOfType<NetworkObject>();
        foreach (NetworkObject no in networkObjects)
        {
            if (no != null && no.IsSpawned)
            {
                Debug.Log($"Destroying spawned NetworkObject: {no.name}");
                if (no.gameObject != null)
                {
                    Destroy(no.gameObject);
                }
            }
        }

        // Reset cross-scene data
        CrossSceneData.Reset();

        Debug.Log("Network cleanup completed");
    }

    private void OnHostClicked()
    {
        CrossSceneData.LobbyMode = "Host";
        SceneManager.LoadScene("Lobby");
    }

    private void OnJoinClicked()
    {
        // Hide menu buttons, show join UI
        hostButton.SetActive(false);
        joinButton.SetActive(false);
        joinPanel.SetActive(true);
        joinCodeInput.text = "";
    }

    private void OnConfirmJoinClicked()
    {
        string code = joinCodeInput.text.Trim();

        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("No lobby code entered!");
            return;
        }

        CrossSceneData.LobbyMode = "Client";
        CrossSceneData.JoinCode = code;
        SceneManager.LoadScene("Lobby");
    }

    private void OnCancelJoinClicked()
    {
        // Bring buttons back, hide join panel
        joinPanel.SetActive(false);
        hostButton.SetActive(true);
        joinButton.SetActive(true);
    }
}