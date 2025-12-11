using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

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

    // Initialize UI
    private void Start()
    {
        joinPanel.SetActive(false);

        hostButton.GetComponent<Button>().onClick.AddListener(OnHostClicked);
        joinButton.GetComponent<Button>().onClick.AddListener(OnJoinClicked);
        confirmJoinButton.onClick.AddListener(OnConfirmJoinClicked);
        cancelJoinButton.onClick.AddListener(OnCancelJoinClicked);
    }

    // Host button clicked
    private void OnHostClicked()
    {
        CrossSceneData.LobbyMode = "Host";
        SceneManager.LoadScene("Lobby");
    }

    // Join button clicked
    private void OnJoinClicked()
    {
        hostButton.SetActive(false);
        joinButton.SetActive(false);
        joinPanel.SetActive(true);
        joinCodeInput.text = "";
    }

    // Confirm join clicked
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

    // Cancel join clicked
    private void OnCancelJoinClicked()
    {
        joinPanel.SetActive(false);
        hostButton.SetActive(true);
        joinButton.SetActive(true);
    }
}