using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("First Person Components")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundLayerMask = 1;

    [Header("Player Stats")]
    [SerializeField] private float health = 100f;
    [SerializeField] private float stamina = 100f;
    [SerializeField] private float staminaDrainRate = 20f;
    [SerializeField] private float staminaRegenRate = 15f;

    [Header("Character Models")]
    [SerializeField] private GameObject[] characterModels;
    [SerializeField] private NetworkVariable<byte> characterModelIndex = new NetworkVariable<byte>();

    // First Person Variables
    private float xRotation = 0f;

    // Movement Variables
    private Vector3 velocity;
    private bool isGrounded;
    private bool isRunning = false;

    // Player State
    private GameManager.PlayerRole playerRole;
    private bool isAlive = true;

    // Network Variables
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>();
    private NetworkVariable<float> currentStamina = new NetworkVariable<float>();

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            InitializeFirstPerson();
        }
        else
        {
            // Disable camera and audio listener for other players
            if (playerCamera != null) playerCamera.enabled = false;
            if (audioListener != null) audioListener.enabled = false;
        }

        currentHealth.OnValueChanged += OnHealthChanged;
        currentStamina.OnValueChanged += OnStaminaChanged;
        characterModelIndex.OnValueChanged += OnCharacterModelChanged;

        // Set initial values
        if (IsServer)
        {
            currentHealth.Value = health;
            currentStamina.Value = stamina;
        }

        // Set character model
        UpdateCharacterModel();
    }

    private void InitializeFirstPerson()
    {
        // Enable camera and audio listener for local player
        if (playerCamera != null) playerCamera.enabled = true;
        if (audioListener != null) audioListener.enabled = true;

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize HUD
        if (InGameHUD.Instance != null)
        {
            InGameHUD.Instance.SetPlayerController(this);
        }

        Debug.Log("First-person player initialized");
    }

    private void Update()
    {
        if (!IsOwner || !isAlive) return;

        HandleMouseLook();
        HandleGroundCheck();
        HandleMovement();
        HandleJump();
        HandleStamina();
        HandleInteraction();
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleGroundCheck()
    {
        // Simple raycast ground check
        Vector3 rayStart = transform.position + Vector3.up * 0.1f; // Slightly above feet
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, groundLayerMask);

        // Debug visualization
        Debug.DrawRay(rayStart, Vector3.down * groundCheckDistance, isGrounded ? Color.green : Color.red);
    }

    private void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Check for running
        isRunning = Input.GetKey(KeyCode.LeftShift) && currentStamina.Value > 0 && vertical > 0;
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        // Calculate movement direction relative to player's rotation
        Vector3 moveDirection = (transform.right * horizontal + transform.forward * vertical).normalized;

        // Apply movement
        Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;
        transform.Translate(movement, Space.World);

        // Handle gravity
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep player grounded
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        // Apply vertical movement (jumping/falling)
        transform.Translate(velocity * Time.deltaTime, Space.World);
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    private void HandleStamina()
    {
        if (!IsOwner) return;

        if (isRunning && (Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0))
        {
            // Drain stamina when running
            currentStamina.Value = Mathf.Max(0, currentStamina.Value - staminaDrainRate * Time.deltaTime);
        }
        else
        {
            // Regenerate stamina when not running
            currentStamina.Value = Mathf.Min(stamina, currentStamina.Value + staminaRegenRate * Time.deltaTime);
        }

        // Update HUD
        if (InGameHUD.Instance != null)
        {
            InGameHUD.Instance.UpdateStaminaBar(currentStamina.Value, stamina);
        }
    }

    private void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Raycast from camera center for interaction
            Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 3f)) // 3 meter interaction distance
            {
                InteractableObject interactable = hit.collider.GetComponent<InteractableObject>();
                if (interactable != null)
                {
                    interactable.Interact(this);
                }
            }
        }
    }

    private void UpdateCharacterModel()
    {
        // Hide all models first
        foreach (var model in characterModels)
        {
            if (model != null)
                model.SetActive(false);
        }

        // Show the assigned model
        if (characterModelIndex.Value < characterModels.Length && characterModels[characterModelIndex.Value] != null)
        {
            characterModels[characterModelIndex.Value].SetActive(true);
        }
    }

    [ServerRpc]
    public void AssignCharacterModelServerRpc(byte modelIndex)
    {
        if (modelIndex < characterModels.Length)
        {
            characterModelIndex.Value = modelIndex;
        }
    }

    private void OnCharacterModelChanged(byte oldIndex, byte newIndex)
    {
        UpdateCharacterModel();
    }

    public void SetRole(GameManager.PlayerRole role)
    {
        playerRole = role;
        Debug.Log($"Role set to: {role}");

        // Update HUD with role information
        if (IsOwner && InGameHUD.Instance != null)
        {
            InGameHUD.Instance.UpdateRoleIndicator(role);
        } 

       
    }

    [ServerRpc]
    public void TakeDamageServerRpc(float damage, ulong attackerId = 0)
    {
        currentHealth.Value -= damage;

        if (currentHealth.Value <= 0)
        {
            Die(attackerId);
        }
    }

    private void Die(ulong killerId = 0)
    {
        isAlive = false;

        // Notify GameManager
        GameManager.Instance.PlayerDiedServerRpc(OwnerClientId);

        // Handle death on all clients
        PlayerDiedClientRpc(killerId);
    }

    [ClientRpc]
    private void PlayerDiedClientRpc(ulong killerId)
    {
        if (IsOwner)
        {
            // Disable controls for local player
            playerCamera.enabled = false;
            audioListener.enabled = false;

            // Show death screen or spectator mode
            Debug.Log("You died!");

            // You could enable a death camera here
        }

        // Hide the character model when dead
        foreach (var model in characterModels)
        {
            if (model != null)
                model.SetActive(false);
        }
    }

    private void OnHealthChanged(float oldHealth, float newHealth)
    {
        // Update HUD health bar
        if (IsOwner && InGameHUD.Instance != null)
        {
            InGameHUD.Instance.UpdateHealthBar(newHealth, health);
        }

        // Visual/audio feedback for taking damage
        if (newHealth < oldHealth && IsOwner)
        {
            // Play hurt sound, screen effect, etc.
            InGameHUD.Instance.ShowNotification("You took damage!");
        }
    }

    private void OnStaminaChanged(float oldStamina, float newStamina)
    {
        // Already handled in HandleStamina for local player
    }

    // Simple collision detection to prevent going through walls
    private void OnControllerColliderHit(UnityEngine.CharacterController hit) { } // Empty but keeps the method for compatibility

    // Alternative collision handling with Rigidbody (if you want physics)
    private void OnCollisionEnter(Collision collision)
    {
        // Optional: Add custom collision handling here
    }

    public GameManager.PlayerRole GetRole() => playerRole;
    public bool IsAlive() => isAlive;
    public byte GetCharacterModelIndex() => characterModelIndex.Value;
}