using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private Transform lookAtTarget;
    [SerializeField] private float cameraHeight = 3f;
    [SerializeField] private float cameraDistance = 5f;

    private Camera mainCamera;
    private float currentAngle = 0f;
    private bool isInitialized = false;

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
        {
            mainCamera = gameObject.AddComponent<Camera>();
            Debug.Log("Added Camera component to MainMenuCamera");
        }
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            Debug.LogWarning($"MainMenuCameraController in wrong scene: {SceneManager.GetActiveScene().name}");
            gameObject.SetActive(false);
            return;
        }

        InitializeMainMenuCamera();
    }

    private void InitializeMainMenuCamera()
    {
        if (isInitialized) return;

        // Step 1: Tag this as MainCamera
        gameObject.tag = "MainCamera";

        // Step 2: Ensure AudioListener
        AudioListener listener = GetComponent<AudioListener>();
        if (listener == null)
        {
            listener = gameObject.AddComponent<AudioListener>();
            Debug.Log("Added AudioListener to MainMenuCamera");
        }
        listener.enabled = true;

        // Step 3: Enable this camera
        if (mainCamera != null)
        {
            mainCamera.enabled = true;
            mainCamera.depth = 0; // Ensure priority
            Debug.Log($"MainMenu Camera enabled: {mainCamera.name}");
        }

        // Step 4: Position camera
        if (lookAtTarget != null)
        {
            Vector3 targetPos = lookAtTarget.position + Vector3.up * 0.5f;
            Vector3 cameraPos = targetPos + new Vector3(0, cameraHeight, -cameraDistance);
            transform.position = cameraPos;
            transform.LookAt(targetPos);
        }

        // Step 5: Disable any other cameras in MainMenu
        DisableOtherCameras();

        isInitialized = true;
        Debug.Log("MainMenuCamera initialized successfully");
    }

    private void DisableOtherCameras()
    {
        Camera[] allCameras = FindObjectsOfType<Camera>();
        int disabledCount = 0;

        foreach (Camera cam in allCameras)
        {
            // Skip self
            if (cam == mainCamera) continue;

            // Only disable cameras that are in MainMenu scene
            if (cam.gameObject.scene.name == "MainMenu" && cam.enabled)
            {
                cam.enabled = false;

                // Remove MainCamera tag from other cameras
                if (cam.CompareTag("MainCamera"))
                    cam.tag = "Untagged";

                disabledCount++;
                Debug.Log($"Disabled other camera: {cam.name}");
            }
        }

        Debug.Log($"Disabled {disabledCount} other cameras in MainMenu");
    }

    private void Update()
    {
        // Only run in MainMenu
        if (SceneManager.GetActiveScene().name != "MainMenu") return;

        // Slowly rotate around target
        if (lookAtTarget != null && mainCamera != null && mainCamera.enabled)
        {
            currentAngle += rotationSpeed * Time.deltaTime;

            Vector3 targetPos = lookAtTarget.position + Vector3.up * 0.5f;
            Vector3 offset = new Vector3(0, cameraHeight, -cameraDistance);
            Vector3 rotatedOffset = Quaternion.Euler(0, currentAngle, 0) * offset;

            transform.position = targetPos + rotatedOffset;
            transform.LookAt(targetPos);
        }

        // Safety check: re-enable if somehow disabled
        if (mainCamera != null && !mainCamera.enabled)
        {
            mainCamera.enabled = true;
            Debug.LogWarning("MainMenu camera was disabled - re-enabled");
        }
    }

    private void OnEnable()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            InitializeMainMenuCamera();
        }
    }

    private void OnDisable()
    {
        if (mainCamera != null)
        {
            mainCamera.enabled = false;
            Debug.Log("MainMenuCamera disabled");
        }
    }

    [ContextMenu("Debug Camera Info")]
    public void DebugCameraInfo()
    {
        Debug.Log($"=== MAIN MENU CAMERA DEBUG ===");
        Debug.Log($"Scene: {SceneManager.GetActiveScene().name}");
        Debug.Log($"Camera enabled: {mainCamera != null && mainCamera.enabled}");
        Debug.Log($"Camera tag: {gameObject.tag}");
        Debug.Log($"Camera position: {transform.position}");

        // Check Camera.main
        Camera unityMainCam = Camera.main;
        if (unityMainCam != null)
            Debug.Log($"Unity Camera.main: {unityMainCam.name} (should be this camera)");
        else
            Debug.LogError("Unity Camera.main is NULL!");

        // List all cameras
        Camera[] allCams = FindObjectsOfType<Camera>();
        Debug.Log($"Total cameras in scene: {allCams.Length}");
        foreach (Camera cam in allCams)
        {
            Debug.Log($"- {cam.name}: Enabled={cam.enabled}, Tag={cam.tag}");
        }
    }
}