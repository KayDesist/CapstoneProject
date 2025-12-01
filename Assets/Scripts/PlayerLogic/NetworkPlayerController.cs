using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 7f;
    public float sprintSpeed = 11f;
    public float mouseSensitivity = 3f;
    public float jumpForce = 7f;

    [Header("Stamina Settings")]
    public int sprintStaminaCost = 15;
    private float staminaAccumulator = 0f;

    [Header("References")]
    public Transform playerCamera;
    public Transform cameraPivot;

    private Rigidbody rb;
    private PlayerHealth playerHealth;
    private float xRotation = 0f;
    private bool isGrounded = true;
    private bool isSprinting = false;
    private float currentSpeed;
    private Vector3 lastPosition;
    private bool wasMoving = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerHealth = GetComponent<PlayerHealth>();
        currentSpeed = walkSpeed;
        lastPosition = transform.position;

        Debug.Log($"PlayerController Start - ClientID: {NetworkManager.Singleton.LocalClientId}, IsOwner: {IsOwner}");

        // Find camera pivot in character hierarchy
        if (cameraPivot == null)
        {
            Transform foundPivot = transform.Find("CameraPivot");
            if (foundPivot != null)
            {
                cameraPivot = foundPivot;
                Debug.Log("Found existing CameraPivot");
            }
            else
            {
                GameObject pivot = new GameObject("CameraPivot");
                pivot.transform.SetParent(transform);
                pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                cameraPivot = pivot.transform;
                Debug.Log("Created new CameraPivot");
            }
        }

        // Find main camera in children
        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                playerCamera = cam.transform;
                Debug.Log("Found player camera in children");
            }
            else
            {
                Debug.LogError("No camera found in character prefab! Creating one...");

                // Create camera as fallback
                GameObject cameraObj = new GameObject("PlayerCamera");
                cameraObj.transform.SetParent(cameraPivot);
                cameraObj.transform.localPosition = Vector3.zero;
                cameraObj.transform.localRotation = Quaternion.identity;

                Camera newCam = cameraObj.AddComponent<Camera>();
                cameraObj.AddComponent<AudioListener>(); // Add audio listener for local player

                playerCamera = cameraObj.transform;
                Debug.Log("Created fallback camera");
            }
        }

        if (rb != null)
            rb.freezeRotation = true;

        // CRITICAL: Only enable camera and controls for owner
        if (!IsOwner)
        {
            Debug.Log($"Disabling controls for non-owner client: {NetworkManager.Singleton.LocalClientId}");

            // Disable camera
            if (playerCamera != null)
            {
                Camera cam = playerCamera.GetComponent<Camera>();
                if (cam != null) cam.enabled = false;

                AudioListener audioListener = playerCamera.GetComponent<AudioListener>();
                if (audioListener != null) audioListener.enabled = false;

                playerCamera.gameObject.SetActive(false);
            }

            // Disable this script
            enabled = false;
            return;
        }

        // Enable camera and controls for owner
        Debug.Log($"Enabling controls for owner client: {NetworkManager.Singleton.LocalClientId}");

        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            Camera cam = playerCamera.GetComponent<Camera>();
            if (cam != null) cam.enabled = true;

            AudioListener audioListener = playerCamera.GetComponent<AudioListener>();
            if (audioListener != null) audioListener.enabled = true;
        }

        UpdateCursorState();
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Skip controls in non-game scenes
        if (SceneManager.GetActiveScene().name != "GameScene") return;

        HandleMouseLook();
        HandleSprint();
        HandleMovement();

        // Update movement state
        wasMoving = IsMoving();
        lastPosition = transform.position;
    }

    private void UpdateCursorState()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "GameScene")
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("Cursor locked for gameplay");
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleSprint()
    {
        // Check if player is trying to sprint and has ANY stamina left
        if (Input.GetKey(KeyCode.LeftShift) && playerHealth != null && playerHealth.GetStamina() > 0)
        {
            // Only allow sprinting if actually moving
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            bool isTryingToMove = (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f);

            if (isTryingToMove)
            {
                if (!isSprinting)
                {
                    isSprinting = true;
                    currentSpeed = sprintSpeed;
                    // Notify PlayerHealth about sprinting state
                    if (playerHealth != null) playerHealth.SetSprinting(true);
                }

                // Accumulate stamina cost and consume when it reaches at least 1
                staminaAccumulator += sprintStaminaCost * Time.deltaTime;

                if (staminaAccumulator >= 1f)
                {
                    int staminaToConsume = Mathf.FloorToInt(staminaAccumulator);

                    // Use ServerRpc to consume stamina on the server
                    if (IsServer)
                    {
                        if (playerHealth != null) playerHealth.ConsumeStamina(staminaToConsume);
                    }
                    else
                    {
                        RequestStaminaConsumptionServerRpc(staminaToConsume);
                    }

                    staminaAccumulator -= staminaToConsume;
                }
            }
            else
            {
                // Not moving, stop sprinting
                if (isSprinting)
                {
                    isSprinting = false;
                    currentSpeed = walkSpeed;
                    // Notify PlayerHealth about sprinting state
                    if (playerHealth != null) playerHealth.SetSprinting(false);
                }
            }
        }
        else
        {
            // Not sprinting or out of stamina
            if (isSprinting)
            {
                isSprinting = false;
                currentSpeed = walkSpeed;
                // Notify PlayerHealth about sprinting state
                if (playerHealth != null) playerHealth.SetSprinting(false);
            }
        }

        // Force stop sprinting if stamina reaches exactly 0
        if (isSprinting && playerHealth != null && playerHealth.GetStamina() <= 0)
        {
            isSprinting = false;
            currentSpeed = walkSpeed;
            // Notify PlayerHealth about sprinting state
            if (playerHealth != null) playerHealth.SetSprinting(false);
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDir = (transform.forward * vertical + transform.right * horizontal).normalized;
        Vector3 moveVelocity = new Vector3(moveDir.x * currentSpeed, rb.linearVelocity.y, moveDir.z * currentSpeed);

        rb.linearVelocity = moveVelocity;
    }

    [ServerRpc]
    private void RequestStaminaConsumptionServerRpc(int staminaCost)
    {
        if (playerHealth != null)
        {
            playerHealth.ConsumeStamina(staminaCost);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    public bool IsMoving()
    {
        return (transform.position - lastPosition).sqrMagnitude > 0.001f;
    }

    public bool IsSprinting()
    {
        return isSprinting;
    }

    // Role-based movement speed adjustment
    public void ApplyRoleSpecificSettings(RoleManager.PlayerRole role)
    {
        Debug.Log($"Applying role settings: {role}");

        switch (role)
        {
            case RoleManager.PlayerRole.Survivor:
                // Survivor-specific settings
                walkSpeed = 7f;
                sprintSpeed = 10f;
                break;
            case RoleManager.PlayerRole.Cultist:
                // Cultist-specific settings
                walkSpeed = 8f;
                sprintSpeed = 12f; // Cultist is faster
                break;
        }

        currentSpeed = walkSpeed;
        Debug.Log($"Role settings applied - Walk: {walkSpeed}, Sprint: {sprintSpeed}");
    }

    // Debug method to check camera status
    [ContextMenu("Debug Camera Status")]
    public void DebugCameraStatus()
    {
        Debug.Log($"=== CAMERA STATUS ===");
        Debug.Log($"IsOwner: {IsOwner}");
        Debug.Log($"PlayerCamera: {playerCamera}");
        if (playerCamera != null)
        {
            Debug.Log($"Camera active: {playerCamera.gameObject.activeInHierarchy}");
            Camera cam = playerCamera.GetComponent<Camera>();
            if (cam != null) Debug.Log($"Camera enabled: {cam.enabled}");
        }
    }
}