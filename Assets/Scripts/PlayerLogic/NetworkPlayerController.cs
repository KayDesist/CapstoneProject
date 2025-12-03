using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 7f;
    public float sprintSpeed = 11f;
    public float mouseSensitivity = 3f;

    [Header("Stamina Settings")]
    public int sprintStaminaCost = 15;
    private float staminaAccumulator = 0f;

    [Header("References")]
    public Transform playerCamera;
    public Transform cameraPivot;

    [Header("Animation")]
    public Animator animator;

    private Rigidbody rb;
    private PlayerHealth playerHealth;
    private float xRotation = 0f;
    private bool isSprinting = false;
    private float currentSpeed;
    private Vector3 lastPosition;
    private string currentCharacterName = "";
    private bool isInitialized = false;

    private void Start()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody>();
        playerHealth = GetComponent<PlayerHealth>();
        currentSpeed = walkSpeed;
        lastPosition = transform.position;

        // Try to find animator if not assigned
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Create camera pivot if it doesn't exist
        if (cameraPivot == null)
        {
            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(transform);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraPivot = pivot.transform;
            Debug.Log("Created CameraPivot for player");
        }

        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            // Reset any existing velocity
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            Debug.LogError("Rigidbody component not found on player!");
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"Player spawned - IsOwner: {IsOwner}, OwnerClientId: {OwnerClientId}");

        // Setup camera and controls based on ownership
        if (IsOwner)
        {
            // Enable camera for owner
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(true);
            }

            // Enable this script
            enabled = true;

            // Cursor control
            UpdateCursorState();

            Debug.Log("Enabled camera and controls for owner player");
        }
        else
        {
            // Disable camera for non-owners
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(false);
            }

            // Disable this script for non-owners
            enabled = false;
            Debug.Log("Disabled camera and controls for non-owner player");
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Skip controls in non-game scenes
        if (SceneManager.GetActiveScene().name != "GameScene") return;

        HandleMouseLook();
        HandleSprint();

        // Update movement state before handling movement
        lastPosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        if (SceneManager.GetActiveScene().name != "GameScene") return;

        HandleMovement();
        UpdateAnimations();
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        // Calculate movement speed for animation
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        float speed = horizontalVelocity.magnitude;

        // Normalize speed for animation (0-1 range based on walk speed)
        float normalizedSpeed = Mathf.Clamp01(speed / walkSpeed);

        // Set animation parameters
        animator.SetFloat("Speed", normalizedSpeed);
        animator.SetBool("IsSprinting", isSprinting);
    }

    private void UpdateCursorState()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "GameScene")
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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

        if (playerCamera != null)
        {
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

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
                    if (playerHealth != null)
                    {
                        playerHealth.SetSprinting(true);
                    }
                }

                // Accumulate stamina cost and consume when it reaches at least 1
                staminaAccumulator += sprintStaminaCost * Time.deltaTime;

                if (staminaAccumulator >= 1f)
                {
                    int staminaToConsume = Mathf.FloorToInt(staminaAccumulator);

                    // Use ServerRpc to consume stamina on the server
                    if (IsServer)
                    {
                        if (playerHealth != null)
                        {
                            playerHealth.ConsumeStamina(staminaToConsume);
                        }
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
                    if (playerHealth != null)
                    {
                        playerHealth.SetSprinting(false);
                    }
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
                if (playerHealth != null)
                {
                    playerHealth.SetSprinting(false);
                }
            }
        }

        // Force stop sprinting if stamina reaches exactly 0
        if (isSprinting && playerHealth != null && playerHealth.GetStamina() <= 0)
        {
            isSprinting = false;
            currentSpeed = walkSpeed;
            // Notify PlayerHealth about sprinting state
            playerHealth.SetSprinting(false);
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDir = (transform.forward * vertical + transform.right * horizontal).normalized;

        if (rb != null)
        {
            // Keep vertical velocity (gravity)
            Vector3 moveVelocity = new Vector3(moveDir.x * currentSpeed, rb.linearVelocity.y, moveDir.z * currentSpeed);
            rb.linearVelocity = moveVelocity;
        }
    }

    [ServerRpc]
    private void RequestStaminaConsumptionServerRpc(int staminaCost)
    {
        if (playerHealth != null)
        {
            playerHealth.ConsumeStamina(staminaCost);
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

    // Character-specific initialization (called from GameManager)
    public void SetupCharacter(string characterName)
    {
        currentCharacterName = characterName;
        Debug.Log($"Character setup: {characterName}");

        // Character-specific settings are now set by GameManager directly
        // GameManager sets walkSpeed and sprintSpeed directly on this component

        isInitialized = true;
        Debug.Log($"Character {characterName} is ready - Walk: {walkSpeed}, Sprint: {sprintSpeed}");
    }

    // Role-based movement speed adjustment
    public void ApplyRoleSpecificSettings(RoleManager.PlayerRole role)
    {
        Debug.Log($"Applying role settings: {role} on character: {currentCharacterName}");

        switch (role)
        {
            case RoleManager.PlayerRole.Survivor:
                // Survivor-specific settings
                walkSpeed += 0f;
                sprintSpeed += 0f;
                break;
            case RoleManager.PlayerRole.Cultist:
                // Cultist-specific settings
                walkSpeed += 1f;
                sprintSpeed += 2f;
                break;
        }

        currentSpeed = walkSpeed;
        Debug.Log($"Role settings applied - Walk: {walkSpeed}, Sprint: {sprintSpeed}");
    }

    // Simple animation methods
    public void PlayAttackAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    public void PlayTaskAnimation(bool isPerformingTask)
    {
        if (animator != null)
        {
            animator.SetBool("IsPerformingTask", isPerformingTask);
        }
    }

    public string GetCharacterName()
    {
        return currentCharacterName;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // Reset cursor when player is destroyed
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // Debug method to check player state
    [ContextMenu("Debug Player State")]
    public void DebugPlayerState()
    {
        Debug.Log($"=== PLAYER STATE ===");
        Debug.Log($"Character: {currentCharacterName}");
        Debug.Log($"IsOwner: {IsOwner}");
        Debug.Log($"OwnerClientId: {OwnerClientId}");
        Debug.Log($"IsInitialized: {isInitialized}");
        Debug.Log($"Speed: {currentSpeed} (Walk: {walkSpeed}, Sprint: {sprintSpeed})");
        Debug.Log($"Camera Active: {playerCamera != null && playerCamera.gameObject.activeInHierarchy}");
        Debug.Log($"Controller Enabled: {enabled}");
        Debug.Log($"Animator: {animator != null}");
        Debug.Log($"Rigidbody: {rb != null}");
        if (rb != null)
        {
            Debug.Log($"Rigidbody Velocity: {rb.linearVelocity}");
            Debug.Log($"Rigidbody Use Gravity: {rb.useGravity}");
        }
    }

    [ContextMenu("Test Movement")]
    public void TestMovement()
    {
        if (rb != null)
        {
            Debug.Log("Testing movement - applying forward force");
            rb.linearVelocity = transform.forward * 5f;
        }
    }
}