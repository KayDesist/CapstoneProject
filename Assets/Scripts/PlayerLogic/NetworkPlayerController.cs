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
    public float sprintStaminaCost = 15f;
    private float staminaAccumulator = 0f;

    [Header("References")]
    public Transform playerCamera;

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

        if (rb != null)
            rb.freezeRotation = true;

        // Disable camera and controls for non-owners
        if (!IsOwner)
        {
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        // Cursor control
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

    private void Update()
    {
        if (!IsOwner) return;

        // Skip controls in non-game scenes
        if (SceneManager.GetActiveScene().name != "GameScene")
            return;

        HandleMouseLook();
        HandleSprint();
        HandleMovement();
       

        // Update movement state
        wasMoving = IsMoving();
        lastPosition = transform.position;
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
        // Check if player is trying to sprint and has enough stamina
        if (Input.GetKey(KeyCode.LeftShift) && playerHealth != null && playerHealth.GetStamina() > 5f)
        {
            // Only allow sprinting if actually moving
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            bool isTryingToMove = (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f);

            if (isTryingToMove)
            {
                if (!isSprinting)
                {
                    Debug.Log("Started sprinting");
                    isSprinting = true;
                    currentSpeed = sprintSpeed;
                }

                // Accumulate stamina cost and consume when it reaches at least 1
                staminaAccumulator += sprintStaminaCost * Time.deltaTime;

                if (staminaAccumulator >= 1f)
                {
                    int staminaToConsume = Mathf.FloorToInt(staminaAccumulator);
                    playerHealth.ConsumeStaminaServerRpc(staminaToConsume);
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
                }
            }
        }
        else
        {
            // Not sprinting or out of stamina
            if (isSprinting)
            {
                Debug.Log("Stopped sprinting");
                isSprinting = false;
                currentSpeed = walkSpeed;
            }
        }

        // Force stop sprinting if stamina is too low
        if (isSprinting && playerHealth != null && playerHealth.GetStamina() <= 0)
        {
            isSprinting = false;
            currentSpeed = walkSpeed;
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
}