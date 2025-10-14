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

    private void Start()
    {
        joinPanel.SetActive(false);

        // Button events
        hostButton.GetComponent<Button>().onClick.AddListener(OnHostClicked);
        joinButton.GetComponent<Button>().onClick.AddListener(OnJoinClicked);
        confirmJoinButton.onClick.AddListener(OnConfirmJoinClicked);
        cancelJoinButton.onClick.AddListener(OnCancelJoinClicked);
    }

    private void OnHostClicked()
    {
        PlayerPrefs.SetString("LobbyMode", "Host");
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

        PlayerPrefs.SetString("LobbyMode", "Client");
        PlayerPrefs.SetString("JoinCode", code);
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
