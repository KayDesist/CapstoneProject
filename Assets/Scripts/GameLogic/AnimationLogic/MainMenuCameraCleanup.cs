using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuCameraCleanup : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float checkInterval = 0.5f;

    private Coroutine cleanupCoroutine;

    private void OnEnable()
    {
        // Only run in MainMenu scene
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            cleanupCoroutine = StartCoroutine(CleanupCamerasRoutine());
        }
    }

    private void OnDisable()
    {
        if (cleanupCoroutine != null)
        {
            StopCoroutine(cleanupCoroutine);
            cleanupCoroutine = null;
        }
    }

    private IEnumerator CleanupCamerasRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            CleanupPlayerCameras();
        }
    }

    private void CleanupPlayerCameras()
    {
        // Find all cameras in the scene
        Camera[] allCameras = FindObjectsOfType<Camera>(true); // Include inactive cameras

        // First, find the MainMenu camera controller
        MainMenuCameraController mainMenuCam = FindObjectOfType<MainMenuCameraController>();

        foreach (Camera cam in allCameras)
        {
            // Skip the MainMenu camera
            if (mainMenuCam != null && cam.gameObject == mainMenuCam.gameObject)
                continue;

            // Skip cameras that are children of MainMenuCameraController
            if (cam.transform.IsChildOf(mainMenuCam?.transform))
                continue;

            // Check if this looks like a player camera
            // Player cameras usually have a NetworkPlayerController or are on player prefabs
            NetworkPlayerController playerController = cam.GetComponentInParent<NetworkPlayerController>();

            if (playerController != null || cam.gameObject.name.Contains("Player") ||
                cam.gameObject.name.Contains("Character") || cam.gameObject.name.Contains("CameraPivot"))
            {
                Debug.Log($"Disabling player camera found in MainMenu: {cam.gameObject.name}");

                // Disable the camera
                cam.enabled = false;

                // Remove MainCamera tag if present
                if (cam.CompareTag("MainCamera"))
                    cam.tag = "Untagged";

                // Disable AudioListener
                AudioListener listener = cam.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = false;

                // Try to disable the entire camera object
                if (!cam.gameObject.CompareTag("DontDestroy"))
                    cam.gameObject.SetActive(false);
            }
        }

        // Ensure there's exactly one MainCamera tag in the scene
        EnsureSingleMainCamera();
    }

    private void EnsureSingleMainCamera()
    {
        // Find all objects with MainCamera tag
        GameObject[] mainCameras = GameObject.FindGameObjectsWithTag("MainCamera");

        if (mainCameras.Length > 1)
        {
            Debug.Log($"Found {mainCameras.Length} MainCameras, cleaning up...");

            // Keep the first one that has MainMenuCameraController
            MainMenuCameraController menuCamController = null;
            GameObject menuCamObject = null;

            foreach (GameObject cam in mainCameras)
            {
                MainMenuCameraController controller = cam.GetComponent<MainMenuCameraController>();
                if (controller != null)
                {
                    menuCamController = controller;
                    menuCamObject = cam;
                    break;
                }
            }

            // Remove MainCamera tag from all others
            foreach (GameObject cam in mainCameras)
            {
                if (cam != menuCamObject)
                {
                    cam.tag = "Untagged";
                    Debug.Log($"Removed MainCamera tag from: {cam.name}");
                }
            }
        }
    }

    [ContextMenu("Force Camera Cleanup")]
    public void ForceCameraCleanup()
    {
        CleanupPlayerCameras();

        // Log current camera state
        Debug.Log("=== FORCE CAMERA CLEANUP COMPLETE ===");
        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            Debug.Log($"Camera: {cam.name} | Enabled: {cam.enabled} | Tag: {cam.tag}");
        }
    }
}