using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetworkPlayerController : NetworkBehaviour
{
    public float walkSpeed = 7f;
    public float sprintSpeed = 11f;
    public float mouseSensitivity = 3f;
    public int sprintStaminaCost = 15;
    private float staminaAccumulator = 0f;
    public Transform playerCamera;
    public Transform cameraPivot;
    public Animator animator;
    private NetworkAnimationController networkAnimationController;
    public AudioClip[] footstepSounds;
    public float footstepInterval = 0.5f;
    public float sprintIntervalMultiplier = 0.7f;
    private float nextFootstepTime = 0f;
    private AudioSource audioSource;
    private Rigidbody rb;
    private PlayerHealth playerHealth;
    private float xRotation = 0f;
    private bool isSprinting = false;
    private float currentSpeed;
    private Vector3 lastPosition;
    private string currentCharacterName = "";
    private bool isInitialized = false;

    // Initializes components on start
    private void Start()
    {
        InitializeComponents();
        if (networkAnimationController == null)
            networkAnimationController = GetComponent<NetworkAnimationController>();
    }

    // Sets up all required components
    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody>();
        playerHealth = GetComponent<PlayerHealth>();
        currentSpeed = walkSpeed;
        lastPosition = transform.position;
        if (networkAnimationController == null)
            networkAnimationController = GetComponent<NetworkAnimationController>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (cameraPivot == null)
        {
            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(transform);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraPivot = pivot.transform;
        }
        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // Called when player spawns on the network
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(true);
            enabled = true;
            UpdateCursorState();
        }
        else
        {
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
            enabled = false;
        }
        isInitialized = true;
    }

    // Updates every frame
    private void Update()
    {
        if (!IsOwner) return;
        if (SceneManager.GetActiveScene().name != "GameScene") return;
        if (playerHealth != null && !playerHealth.IsAlive()) return;
        HandleMouseLook();
        HandleSprint();
        HandleFootsteps();
        lastPosition = transform.position;
    }

    // Fixed update for physics
    private void FixedUpdate()
    {
        if (!IsOwner) return;
        if (SceneManager.GetActiveScene().name != "GameScene") return;
        if (playerHealth != null && !playerHealth.IsAlive()) return;
        HandleMovement();
    }

    // Handles footstep sounds
    private void HandleFootsteps()
    {
        if (!IsMoving() || Time.time < nextFootstepTime || footstepSounds == null || footstepSounds.Length == 0)
            return;
        if (audioSource != null && IsOwner)
        {
            int randomIndex = Random.Range(0, footstepSounds.Length);
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(footstepSounds[randomIndex]);
            PlayFootstepServerRpc(randomIndex);
        }
        float interval = footstepInterval;
        if (IsSprinting())
            interval *= sprintIntervalMultiplier;
        nextFootstepTime = Time.time + interval;
    }

    // Server RPC to play footstep
    [ServerRpc]
    private void PlayFootstepServerRpc(int soundIndex)
    {
        PlayFootstepClientRpc(soundIndex);
    }

    // Client RPC to play footstep on all clients
    [ClientRpc]
    private void PlayFootstepClientRpc(int soundIndex)
    {
        if (IsOwner) return;
        if (audioSource != null && footstepSounds != null && soundIndex < footstepSounds.Length)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(footstepSounds[soundIndex]);
        }
    }

    // Updates cursor state based on scene
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

    // Handles mouse look input
    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    // Handles sprinting logic
    private void HandleSprint()
    {
        if (Input.GetKey(KeyCode.LeftShift) && playerHealth != null && playerHealth.GetStamina() > 0)
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            bool isTryingToMove = (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f);
            if (isTryingToMove)
            {
                if (!isSprinting)
                {
                    isSprinting = true;
                    currentSpeed = sprintSpeed;
                    if (playerHealth != null)
                        playerHealth.SetSprinting(true);
                }
                staminaAccumulator += sprintStaminaCost * Time.deltaTime;
                if (staminaAccumulator >= 1f)
                {
                    int staminaToConsume = Mathf.FloorToInt(staminaAccumulator);
                    if (IsServer)
                    {
                        if (playerHealth != null)
                            playerHealth.ConsumeStamina(staminaToConsume);
                    }
                    else
                        RequestStaminaConsumptionServerRpc(staminaToConsume);
                    staminaAccumulator -= staminaToConsume;
                }
            }
            else
            {
                if (isSprinting)
                {
                    isSprinting = false;
                    currentSpeed = walkSpeed;
                    if (playerHealth != null)
                        playerHealth.SetSprinting(false);
                }
            }
        }
        else
        {
            if (isSprinting)
            {
                isSprinting = false;
                currentSpeed = walkSpeed;
                if (playerHealth != null)
                    playerHealth.SetSprinting(false);
            }
        }
        if (isSprinting && playerHealth != null && playerHealth.GetStamina() <= 0)
        {
            isSprinting = false;
            currentSpeed = walkSpeed;
            playerHealth.SetSprinting(false);
        }
    }

    // Handles player movement
    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 moveDir = (transform.forward * vertical + transform.right * horizontal).normalized;
        if (rb != null)
        {
            Vector3 moveVelocity = new Vector3(moveDir.x * currentSpeed, rb.linearVelocity.y, moveDir.z * currentSpeed);
            rb.linearVelocity = moveVelocity;
        }
    }

    // Server RPC to request stamina consumption
    [ServerRpc]
    private void RequestStaminaConsumptionServerRpc(int staminaCost)
    {
        if (playerHealth != null)
            playerHealth.ConsumeStamina(staminaCost);
    }

    // Checks if player is moving
    public bool IsMoving()
    {
        return (transform.position - lastPosition).sqrMagnitude > 0.001f;
    }

    // Checks if player is sprinting
    public bool IsSprinting()
    {
        return isSprinting;
    }

    // Sets up character with name
    public void SetupCharacter(string characterName)
    {
        currentCharacterName = characterName;
        isInitialized = true;
    }

    // Applies role-specific settings
    public void ApplyRoleSpecificSettings(RoleManager.PlayerRole role)
    {
        switch (role)
        {
            case RoleManager.PlayerRole.Survivor:
                walkSpeed += 0f;
                sprintSpeed += 0f;
                break;
            case RoleManager.PlayerRole.Cultist:
                walkSpeed += 1f;
                sprintSpeed += 2f;
                break;
        }
        currentSpeed = walkSpeed;
    }

    // Plays attack animation
    public void PlayAttackAnimation()
    {
        if (networkAnimationController != null)
            networkAnimationController.TriggerAttack();
        else if (animator != null)
            animator.SetTrigger("Attack");
    }

    // Sets performing task state
    public void SetPerformingTask(bool isPerformingTask)
    {
        if (networkAnimationController != null)
            networkAnimationController.SetPerformingTask(isPerformingTask);
        else if (animator != null)
            animator.SetBool("IsPerformingTask", isPerformingTask);
    }

    // Plays task animation
    public void PlayTaskAnimation(bool isPerformingTask)
    {
        SetPerformingTask(isPerformingTask);
    }

    // Gets character name
    public string GetCharacterName()
    {
        return currentCharacterName;
    }

    // Called when player despawns from network
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}