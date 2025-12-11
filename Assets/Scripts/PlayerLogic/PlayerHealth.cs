using UnityEngine;
using Unity.Netcode;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public int maxStamina = 100;

    [Header("Stamina Settings")]
    public float staminaRegenRate = 15f;
    public float staminaRegenDelay = 2f;
    public float staminaRegenCooldown = 0f;

    [Header("Ragdoll Settings")]
    public bool enableRagdollOnDeath = true;
    public float ragdollForce = 10f;
    public float ragdollDestroyTime = 10f;

    // Network variables
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> currentStamina = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Local references
    private NetworkAnimationController networkAnimationController;
    private NetworkPlayerController playerController;
    private PlayerSpectator playerSpectator;
    private Rigidbody rb;
    private Animator animator;
    private Collider mainCollider;
    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;

    // Local stamina tracking
    private bool isSprinting = false;
    private float lastStaminaUseTime = 0f;
    private float staminaAccumulator = 0f;

    private void Awake()
    {
        networkAnimationController = GetComponent<NetworkAnimationController>();
        playerController = GetComponent<NetworkPlayerController>();
        playerSpectator = GetComponent<PlayerSpectator>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        mainCollider = GetComponent<Collider>();

        // Initialize ragdoll
        InitializeRagdoll();
    }

    private void InitializeRagdoll()
    {
        if (!enableRagdollOnDeath) return;

        // Get all rigidbodies and colliders for ragdoll
        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        // Disable ragdoll by default
        SetRagdollActive(false);
    }

    private void SetRagdollActive(bool active)
    {
        if (!enableRagdollOnDeath) return;

        // Enable/disable animator
        if (animator != null)
        {
            animator.enabled = !active;
        }

        // Enable/disable main rigidbody and collider
        if (rb != null)
        {
            rb.isKinematic = active;
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = !active;
        }

        // Enable/disable ragdoll rigidbodies and colliders
        foreach (Rigidbody ragdollRb in ragdollRigidbodies)
        {
            if (ragdollRb != rb) // Don't touch the main rigidbody twice
            {
                ragdollRb.isKinematic = !active;
                ragdollRb.useGravity = active;
                ragdollRb.detectCollisions = active;
            }
        }

        foreach (Collider collider in ragdollColliders)
        {
            if (collider != mainCollider) // Don't touch the main collider twice
            {
                collider.enabled = active;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            currentStamina.Value = maxStamina;
        }

        currentHealth.OnValueChanged += OnHealthChanged;
        currentStamina.OnValueChanged += OnStaminaChanged;
        isDead.OnValueChanged += OnDeathStateChanged;

        Debug.Log($"PlayerHealth spawned for client {OwnerClientId} (IsOwner: {IsOwner}, IsServer: {IsServer})");
    }

    private void Update()
    {
        if (IsServer && !isDead.Value)
        {
            HandleStaminaRegeneration();
        }
    }

    private void HandleStaminaRegeneration()
    {
        if (isSprinting) return;

        float timeSinceLastUse = Time.time - lastStaminaUseTime;
        if (timeSinceLastUse < staminaRegenDelay) return;

        staminaAccumulator += staminaRegenRate * Time.deltaTime;

        if (staminaAccumulator >= 1f)
        {
            int staminaToAdd = Mathf.FloorToInt(staminaAccumulator);
            int newStamina = Mathf.Min(maxStamina, currentStamina.Value + staminaToAdd);

            if (newStamina != currentStamina.Value)
            {
                currentStamina.Value = newStamina;
            }

            staminaAccumulator -= staminaToAdd;
        }
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
        Debug.Log($"Player {OwnerClientId} health changed: {oldValue} -> {newValue}");

        if (IsOwner && GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.UpdateHealth(newValue, maxHealth);
        }
    }

    private void OnStaminaChanged(int oldValue, int newValue)
    {
        Debug.Log($"Player {OwnerClientId} stamina changed: {oldValue} -> {newValue}");

        if (IsOwner && GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.UpdateStamina(newValue, maxStamina);
        }
    }

    private void OnDeathStateChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"Player {OwnerClientId} death state changed: {oldValue} -> {newValue}");

        if (newValue)
        {
            // Handle death visuals on all clients
            HandleDeathVisuals();

            // Handle local player death
            if (IsOwner)
            {
                OnLocalPlayerDeath();
            }
        }
    }

    public void TakeDamage(int damage, ulong attackerId)
    {
        if (!IsServer || isDead.Value) return;

        Debug.Log($"Player {OwnerClientId} taking {damage} damage from {attackerId}");

        int newHealth = currentHealth.Value - damage;
        currentHealth.Value = Mathf.Max(0, newHealth);

        if (currentHealth.Value <= 0)
        {
            Die(attackerId);
        }
    }

    private void Die(ulong killerId)
    {
        if (!IsServer || isDead.Value) return;

        Debug.Log($"Player {OwnerClientId} died (killed by {killerId})");

        // Set death state
        isDead.Value = true;

        // Disable player controller
        DisablePlayerControllerClientRpc();

        // Notify EndGameManager
        if (EndGameManager.Instance != null)
        {
            var roleManager = FindObjectOfType<RoleManager>();
            if (roleManager != null)
            {
                var role = roleManager.GetPlayerRole(OwnerClientId);
                EndGameManager.Instance.OnPlayerDied(OwnerClientId, role);
            }
        }

        // Apply ragdoll force
        ApplyRagdollForceClientRpc(killerId);
    }

    private void HandleDeathVisuals()
    {
        if (!enableRagdollOnDeath) return;

        // Enable ragdoll
        SetRagdollActive(true);

        // Apply some force to make ragdoll more dramatic
        if (rb != null)
        {
            Vector3 randomForce = new Vector3(
                Random.Range(-ragdollForce, ragdollForce),
                Random.Range(ragdollForce * 0.5f, ragdollForce),
                Random.Range(-ragdollForce, ragdollForce)
            );
            rb.AddForce(randomForce, ForceMode.Impulse);
        }

        Debug.Log("Enabled ragdoll on death");
    }

    [ClientRpc]
    private void ApplyRagdollForceClientRpc(ulong killerId)
    {
        if (!enableRagdollOnDeath) return;

        // Find the killer to apply force away from them
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (NetworkPlayerController player in allPlayers)
        {
            if (player.NetworkObject != null && player.NetworkObject.OwnerClientId == killerId)
            {
                if (rb != null)
                {
                    Vector3 direction = (transform.position - player.transform.position).normalized;
                    rb.AddForce(direction * ragdollForce, ForceMode.Impulse);
                }
                break;
            }
        }
    }

    [ClientRpc]
    private void DisablePlayerControllerClientRpc()
    {
        if (!IsOwner) return;

        Debug.Log("Disabling player controller due to death");

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // Enable spectator mode after a short delay
        if (playerSpectator != null)
        {
            Invoke(nameof(EnableSpectatorMode), 0.5f);
        }
    }

    private void EnableSpectatorMode()
    {
        if (playerSpectator != null && IsOwner)
        {
            playerSpectator.enabled = true;

            // Call the death handler to start spectating
            if (playerSpectator.enabled)
            {
                playerSpectator.HandlePlayerDeath();
            }

            Debug.Log("Enabled spectator mode");
        }
    }

    private void OnLocalPlayerDeath()
    {
        Debug.Log("Local player death handler called");

        // Show death screen or spectator mode
        ShowDeathScreenOrSpectator();
    }

    private void ShowDeathScreenOrSpectator()
    {
        Debug.Log("You died! Enabling spectator mode...");
    }

    public void ConsumeStamina(int amount)
    {
        if (!IsServer || isDead.Value) return;

        int newStamina = currentStamina.Value - amount;
        currentStamina.Value = Mathf.Max(0, newStamina);

        lastStaminaUseTime = Time.time;
    }

    public void SetSprinting(bool sprinting)
    {
        isSprinting = sprinting;

        if (!sprinting)
        {
            lastStaminaUseTime = Time.time;
        }
    }

    public int GetStamina()
    {
        return currentStamina.Value;
    }

    public bool IsAlive()
    {
        return !isDead.Value && currentHealth.Value > 0;
    }

    public int GetCurrentHealth()
    {
        return currentHealth.Value;
    }

    public void Respawn()
    {
        if (!IsServer) return;

        Debug.Log($"Respawning player {OwnerClientId}");

        isDead.Value = false;
        currentHealth.Value = maxHealth;
        currentStamina.Value = maxStamina;
        isSprinting = false;
        lastStaminaUseTime = Time.time;
        staminaAccumulator = 0f;

        // Disable ragdoll
        SetRagdollActive(false);

        // Enable player on all clients
        EnablePlayerClientRpc();
    }

    [ClientRpc]
    private void EnablePlayerClientRpc()
    {
        if (!IsOwner) return;

        // Disable ragdoll
        SetRagdollActive(false);

        // Re-enable player controller
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        // Re-enable animator
        if (animator != null)
        {
            animator.enabled = true;
        }

        // Disable spectator if active
        if (playerSpectator != null)
        {
            playerSpectator.StopSpectating();
            playerSpectator.enabled = false;
        }

        Debug.Log("Player respawned and re-enabled");
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        currentHealth.OnValueChanged -= OnHealthChanged;
        currentStamina.OnValueChanged -= OnStaminaChanged;
        isDead.OnValueChanged -= OnDeathStateChanged;
    }
}