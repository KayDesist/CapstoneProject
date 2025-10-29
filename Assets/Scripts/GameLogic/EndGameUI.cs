using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class EndGameUI : MonoBehaviour
{
    public static EndGameUI Instance;

    [Header("End Game UI")]
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private Button quitButton;

    [Header("Results")]
    [SerializeField] private string survivorsWinText = "SURVIVORS ESCAPE!";
    [SerializeField] private string survivorsWinDescription = "The survivors completed their tasks and escaped the woods.";
    [SerializeField] private string cultistWinText = "CULTIST VICTORY!";
    [SerializeField] private string cultistWinDescription = "The cultist sacrificed all survivors to their dark ritual.";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        endGamePanel.SetActive(false);

        // Button listeners
        returnToLobbyButton.onClick.AddListener(ReturnToLobby);
        quitButton.onClick.AddListener(QuitGame);
    }

    public void ShowEndGameScreen(bool survivorsWin)
    {
        endGamePanel.SetActive(true);

        if (survivorsWin)
        {
            resultText.text = survivorsWinText;
            descriptionText.text = survivorsWinDescription;
            resultText.color = Color.green;
        }
        else
        {
            resultText.text = cultistWinText;
            descriptionText.text = cultistWinDescription;
            resultText.color = Color.red;
        }

        // Add fade-in animation
        CanvasGroup canvasGroup = endGamePanel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            LeanTween.alphaCanvas(canvasGroup, 1f, 1.5f);
        }
    }

    private void ReturnToLobby()
    {
        // Shutdown network and return to lobby
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene("MainMenu");
        Destroy(gameObject); // Remove this EndGameUI instance
    }

    private void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}