using UnityEngine;
using Unity.Netcode;

public class MainMenuCleanup : MonoBehaviour
{
    private void Start()
    {
        CleanupPreviousSession();
    }

    private void CleanupPreviousSession()
    {
        Debug.Log("Cleaning up previous game session...");

        // Ensure NetworkManager is shut down
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsListening)
            {
                Debug.Log("Shutting down NetworkManager from previous session...");
                NetworkManager.Singleton.Shutdown();
            }
        }

        // Destroy any leftover manager objects
        DestroyManagers();

        // Clean up EndGameUI specifically
        CleanupEndGameUI();

        // Reset cross-scene data
        CrossSceneData.Reset();

        Debug.Log("Previous session cleanup completed");
    }

    private void DestroyManagers()
    {
        // Destroy any leftover manager objects from previous game
        RoleManager[] roleManagers = FindObjectsOfType<RoleManager>();
        foreach (RoleManager manager in roleManagers)
        {
            if (manager.gameObject != null)
            {
                Debug.Log($"Destroying leftover RoleManager: {manager.gameObject.name}");
                Destroy(manager.gameObject);
            }
        }

        TaskManager[] taskManagers = FindObjectsOfType<TaskManager>();
        foreach (TaskManager manager in taskManagers)
        {
            if (manager.gameObject != null)
            {
                Debug.Log($"Destroying leftover TaskManager: {manager.gameObject.name}");
                Destroy(manager.gameObject);
            }
        }

        GameHUDManager[] hudManagers = FindObjectsOfType<GameHUDManager>();
        foreach (GameHUDManager manager in hudManagers)
        {
            if (manager.gameObject != null)
            {
                Debug.Log($"Destroying leftover GameHUDManager: {manager.gameObject.name}");
                Destroy(manager.gameObject);
            }
        }

        EndGameManager[] endGameManagers = FindObjectsOfType<EndGameManager>();
        foreach (EndGameManager manager in endGameManagers)
        {
            if (manager.gameObject != null)
            {
                Debug.Log($"Destroying leftover EndGameManager: {manager.gameObject.name}");
                Destroy(manager.gameObject);
            }
        }

        // Clean up RoleDisplayUI if it exists
        RoleDisplayUI[] roleDisplays = FindObjectsOfType<RoleDisplayUI>();
        foreach (RoleDisplayUI display in roleDisplays)
        {
            if (display.gameObject != null && display.gameObject != gameObject)
            {
                Debug.Log($"Destroying leftover RoleDisplayUI: {display.gameObject.name}");
                Destroy(display.gameObject);
            }
        }

        // Reset static instances using the new methods
        ResetStaticInstances();
    }

    private void CleanupEndGameUI()
    {
        // Clean up any EndGameUI instances
        EndGameUI[] endGameUIs = FindObjectsOfType<EndGameUI>();
        foreach (EndGameUI ui in endGameUIs)
        {
            if (ui.gameObject != null && ui.gameObject != gameObject)
            {
                Debug.Log($"Destroying leftover EndGameUI: {ui.gameObject.name}");
                Destroy(ui.gameObject);
            }
        }

        // Also clean up any temporary end game canvases
        GameObject tempCanvas = GameObject.Find("TempEndGameCanvas");
        if (tempCanvas != null)
        {
            Debug.Log($"Destroying temporary end game canvas");
            Destroy(tempCanvas);
        }
    }

    private void ResetStaticInstances()
    {
        RoleManager.ResetInstance();
        TaskManager.ResetInstance();
        GameHUDManager.ResetInstance();
        EndGameManager.ResetInstance();

        Debug.Log("All static manager instances reset");
    }
}