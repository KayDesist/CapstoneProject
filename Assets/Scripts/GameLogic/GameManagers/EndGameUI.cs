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

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        if (returnButton != null)
        {
            returnButton.onClick.AddListener(OnReturnButtonClicked);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
        {
            Cleanup();
        }
    }

    // Display end game screen
    public void ShowEndGameScreen(EndGameManager.GameResult result)
    {
        if (endGamePanel == null) return;

        isEndGameActive = true;
        gameObject.SetActive(true);
        endGamePanel.SetActive(true);

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

        DisablePlayerControls();
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

    // Disable player controls
    private void DisablePlayerControls()
    {
        var localPlayer = FindObjectOfType<NetworkPlayerController>();
        if (localPlayer != null && localPlayer.IsOwner)
        {
            localPlayer.enabled = false;

            var spectator = localPlayer.GetComponent<PlayerSpectator>();
            if (spectator != null)
            {
                spectator.enabled = false;
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Handle return button click
    private void OnReturnButtonClicked()
    {
        if (!isEndGameActive) return;

        isEndGameActive = false;

        if (returnButton != null)
        {
            returnButton.interactable = false;
            returnButton.GetComponentInChildren<TMP_Text>().text = "Returning...";
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (EndGameManager.Instance != null)
            {
                EndGameManager.Instance.ReturnToMainMenu();
            }
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
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

    // Hide end game screen
    public void HideEndGameScreen()
    {
        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        isEndGameActive = false;
    }

    // Clean up UI
    private void Cleanup()
    {
        HideEndGameScreen();

        if (Instance == this)
        {
            Instance = null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}