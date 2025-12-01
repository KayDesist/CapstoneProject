using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string gameScene = "GameScene";

    [Header("Transition Effects")]
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private float minLoadTime = 1f;

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
    }

    public void LoadMainMenu()
    {
        Debug.Log("Loading Main Menu...");

        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        StartCoroutine(LoadSceneWithDelay(mainMenuScene, true));
    }

    public void LoadGameScene()
    {
        Debug.Log("Loading Game Scene...");

        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        // Clean up MainMenu completely
        CleanupBeforeGameLoad();

        StartCoroutine(LoadSceneWithDelay(gameScene, false));
    }

    private IEnumerator LoadSceneWithDelay(string sceneName, bool isMainMenu)
    {
        Debug.Log($"Starting scene load: {sceneName}");

        float startTime = Time.time;

        // Load scene async with SINGLE mode
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f || Time.time - startTime < minLoadTime)
        {
            yield return null;
        }

        asyncLoad.allowSceneActivation = true;

        // Wait for scene to fully load
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Debug.Log($"Scene {sceneName} loaded successfully");

        // Hide loading screen
        if (loadingScreen != null)
            loadingScreen.SetActive(false);

        // If we loaded MainMenu, ensure camera is properly set up
        if (isMainMenu)
        {
            yield return new WaitForSeconds(0.1f); // Small delay for initialization
            EnsureMainMenuCamera();
        }
    }

    private void CleanupBeforeGameLoad()
    {
        Debug.Log("Cleaning up before game load...");

        // Destroy any MainMenuCharacterManager instances
        MainMenuCharacterManager[] menuManagers = FindObjectsOfType<MainMenuCharacterManager>();
        foreach (var manager in menuManagers)
        {
            if (manager != null)
            {
                manager.ClearCharacters();
                Destroy(manager.gameObject);
            }
        }

        // Destroy any MainMenuCameraController instances
        MainMenuCameraController[] menuCameras = FindObjectsOfType<MainMenuCameraController>();
        foreach (var camController in menuCameras)
        {
            if (camController != null)
                Destroy(camController.gameObject);
        }
    }

    private void EnsureMainMenuCamera()
    {
        // Find or create main camera in MainMenu
        GameObject mainCamObj = GameObject.FindWithTag("MainCamera");
        if (mainCamObj == null)
        {
            Debug.LogWarning("No MainCamera found in MainMenu - creating one");
            mainCamObj = new GameObject("MainMenuCamera");
            mainCamObj.tag = "MainCamera";
            Camera cam = mainCamObj.AddComponent<Camera>();
            cam.enabled = true;
            mainCamObj.AddComponent<AudioListener>();

            // Add controller
            mainCamObj.AddComponent<MainMenuCameraController>();
        }
        else
        {
            // Ensure it has MainMenuCameraController
            if (mainCamObj.GetComponent<MainMenuCameraController>() == null)
            {
                mainCamObj.AddComponent<MainMenuCameraController>();
            }
        }
    }

    public void OnPlayButtonClicked()
    {
        LoadGameScene();
    }

    public void OnQuitButtonClicked()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}