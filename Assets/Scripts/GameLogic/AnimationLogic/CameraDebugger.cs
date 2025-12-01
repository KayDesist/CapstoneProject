using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraDebugger : MonoBehaviour
{
    void Update()
    {
        // Press F1 to debug cameras
        if (Input.GetKeyDown(KeyCode.F1))
        {
            DebugCameras();
        }

        // Press F2 to check Camera.main
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Debug.Log($"Camera.main is: {mainCam.name} at position {mainCam.transform.position}");

                // Highlight it in scene view
                Debug.DrawRay(mainCam.transform.position, mainCam.transform.forward * 10f, Color.green, 5f);
            }
            else
            {
                Debug.LogError("Camera.main is NULL!");
            }
        }
    }

    private void DebugCameras()
    {
        Debug.Log("=== CAMERA DEBUGGER ===");
        Debug.Log($"Scene: {SceneManager.GetActiveScene().name}");

        Camera[] allCameras = FindObjectsOfType<Camera>();
        Debug.Log($"Total Cameras Found: {allCameras.Length}");

        foreach (Camera cam in allCameras)
        {
            string status = cam.enabled ? "ENABLED" : "disabled";
            string tagInfo = cam.CompareTag("MainCamera") ? "[MAIN CAMERA]" : "";
            Debug.Log($"- {cam.name}: {status} {tagInfo} (Depth: {cam.depth}, Pos: {cam.transform.position})");

            // Also check for AudioListener
            AudioListener listener = cam.GetComponent<AudioListener>();
            if (listener != null)
                Debug.Log($"  Has AudioListener: {listener.enabled}");
        }

        // Check which camera is actually rendering
        Camera renderingCam = null;
        foreach (Camera cam in allCameras)
        {
            if (cam.enabled && cam.targetTexture == null) // Not a render texture camera
            {
                renderingCam = cam;
                break;
            }
        }

        if (renderingCam != null)
            Debug.Log($"Currently rendering camera: {renderingCam.name}");
        else
            Debug.LogWarning("No camera appears to be rendering!");
    }
}