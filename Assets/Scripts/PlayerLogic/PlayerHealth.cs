using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public int maxStamina = 100;

    [Header("Stamina Settings")]
    public float staminaRegenRate = 15f;
    public float staminaRegenDelay = 2f;
    public float staminaRegenCooldown = 0f;

    [Header("Death Settings")]
    public bool enableRagdollOnDeath = true;
    public bool enableDeathAnimation = true;
    public float ragdollForce = 10f;
    public float ragdollDestroyTime = 10f;
    public float deathAnimationDuration = 2f;

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

    // Death animation tracking
    private float deathAnimationEndTime = 0f;
    private bool isInDeathAnimation = false;

    private void Awake()
    {
        networkAnimationController = GetComponent<NetworkAnimationController>();
        playerController = GetComponent<NetworkPlayerController>();
        playerSpectator = GetComponent<PlayerSpectator>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        mainCollider = GetComponent<Collider>();

        InitializeRagdoll();
    }

    private void InitializeRagdoll()
    {
        if (!enableRagdollOnDeath) return;

        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        SetRagdollActive(false);
    }

    private void SetRagdollActive(bool active)
    {
        if (!enableRagdollOnDeath) return;

        if (animator != null)
        {
            animator.enabled = !active;
        }

        if (rb != null)
        {
            rb.isKinematic = active;
            rb.detectCollisions = !active;
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = !active;
        }

        foreach (Rigidbody ragdollRb in ragdollRigidbodies)
        {
            if (ragdollRb != rb)
            {
                ragdollRb.isKinematic = !active;
                ragdollRb.useGravity = active;
                ragdollRb.detectCollisions = active;
            }
        }

        foreach (Collider collider in ragdollColliders)
        {
            if (collider != mainCollider)
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

        if (isInDeathAnimation && Time.time >= deathAnimationEndTime)
        {
            if (enableRagdollOnDeath)
            {
                EnableRagdoll();
            }
            isInDeathAnimation = false;
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
            HandleDeathVisuals();

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

        // Trigger death animation through NetworkAnimationController
        if (networkAnimationController != null)
        {
            networkAnimationController.SetDeathState(true);
        }

        // Disable player controller
        DisablePlayerControllerClientRpc();

        // Play death animation on all clients
        PlayDeathAnimationClientRpc(killerId);

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
    }

    private void HandleDeathVisuals()
    {
        Debug.Log("Death state changed - waiting for death animation");
    }

    [ClientRpc]
    private void PlayDeathAnimationClientRpc(ulong killerId)
    {
        Debug.Log($"Playing death animation for player {OwnerClientId}");

        // Trigger death animation through NetworkAnimationController
        if (enableDeathAnimation && networkAnimationController != null)
        {
            networkAnimationController.TriggerDeath();
            isInDeathAnimation = true;
            deathAnimationEndTime = Time.time + deathAnimationDuration;

            if (enableRagdollOnDeath)
            {
                StartCoroutine(ApplyDelayedRagdollForce(killerId));
            }
        }
        else
        {
            if (enableRagdollOnDeath)
            {
                EnableRagdoll();
                ApplyRagdollForce(killerId);
            }
        }
    }

    private IEnumerator ApplyDelayedRagdollForce(ulong killerId)
    {
        yield return new WaitForSeconds(deathAnimationDuration - 0.1f);

        if (enableRagdollOnDeath)
        {
            ApplyRagdollForce(killerId);
        }
    }

    private void EnableRagdoll()
    {
        if (!enableRagdollOnDeath) return;

        Debug.Log("Enabling ragdoll");
        SetRagdollActive(true);
    }

    private void ApplyRagdollForce(ulong killerId)
    {
        if (!enableRagdollOnDeath || rb == null) return;

        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        Vector3 forceDirection = Vector3.up;

        foreach (NetworkPlayerController player in allPlayers)
        {
            if (player.NetworkObject != null && player.NetworkObject.OwnerClientId == killerId)
            {
                forceDirection = (transform.position - player.transform.position).normalized;
                forceDirection.y = Mathf.Abs(forceDirection.y);
                forceDirection.Normalize();
                break;
            }
        }

        if (rb != null)
        {
            rb.AddForce(forceDirection * ragdollForce, ForceMode.Impulse);
            Debug.Log($"Applied ragdoll force: {forceDirection * ragdollForce}");
        }

        foreach (Rigidbody ragdollRb in ragdollRigidbodies)
        {
            if (ragdollRb != rb)
            {
                Vector3 randomForce = new Vector3(
                    Random.Range(-ragdollForce * 0.5f, ragdollForce * 0.5f),
                    Random.Range(ragdollForce * 0.3f, ragdollForce * 0.7f),
                    Random.Range(-ragdollForce * 0.5f, ragdollForce * 0.5f)
                );
                ragdollRb.AddForce(randomForce, ForceMode.Impulse);
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

        if (playerSpectator != null)
        {
            Invoke(nameof(EnableSpectatorMode), deathAnimationDuration);
        }
    }

    private void EnableSpectatorMode()
    {
        if (playerSpectator != null && IsOwner)
        {
            playerSpectator.enabled = true;

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
        isInDeathAnimation = false;

        // Reset death animation
        if (networkAnimationController != null)
        {
            networkAnimationController.SetDeathState(false);
            networkAnimationController.ResetDeathState();
        }

        // Disable ragdoll
        SetRagdollActive(false);

        // Enable player on all clients
        EnablePlayerClientRpc();
    }

    [ClientRpc]
    private void EnablePlayerClientRpc()
    {
        if (!IsOwner) return;

        SetRagdollActive(false);

        if (animator != null)
        {
            animator.enabled = true;
        }

        if (networkAnimationController != null)
        {
            networkAnimationController.ResetDeathState();
        }

        if (playerController != null)
        {
            playerController.enabled = true;
        }

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