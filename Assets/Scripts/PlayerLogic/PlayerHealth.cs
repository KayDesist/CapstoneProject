using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class PlayerHealth : NetworkBehaviour
{
    public int maxHealth = 100;
    public int maxStamina = 100;
    public float staminaRegenRate = 15f;
    public float staminaRegenDelay = 2f;
    public float staminaRegenCooldown = 0f;
    public bool enableRagdollOnDeath = true;
    public bool enableDeathAnimation = true;
    public float ragdollForce = 10f;
    public float ragdollDestroyTime = 10f;
    public float deathAnimationDuration = 2f;
    private NetworkVariable<int> currentHealth = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> currentStamina = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkAnimationController networkAnimationController;
    private NetworkPlayerController playerController;
    private PlayerSpectator playerSpectator;
    private Rigidbody rb;
    private Animator animator;
    private Collider mainCollider;
    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;
    private bool isSprinting = false;
    private float lastStaminaUseTime = 0f;
    private float staminaAccumulator = 0f;
    private float deathAnimationEndTime = 0f;
    private bool isInDeathAnimation = false;

    // Initializes on awake
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

    // Initializes ragdoll components
    private void InitializeRagdoll()
    {
        if (!enableRagdollOnDeath) return;
        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();
        SetRagdollActive(false);
    }

    // Sets ragdoll active state
    private void SetRagdollActive(bool active)
    {
        if (!enableRagdollOnDeath) return;
        if (animator != null)
            animator.enabled = !active;
        if (rb != null)
        {
            rb.isKinematic = active;
            rb.detectCollisions = !active;
        }
        if (mainCollider != null)
            mainCollider.enabled = !active;
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
                collider.enabled = active;
        }
    }

    // Called when player spawns on network
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
    }

    // Updates every frame
    private void Update()
    {
        if (IsServer && !isDead.Value)
            HandleStaminaRegeneration();
        if (isInDeathAnimation && Time.time >= deathAnimationEndTime)
        {
            if (enableRagdollOnDeath)
                EnableRagdoll();
            isInDeathAnimation = false;
        }
    }

    // Handles stamina regeneration
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
                currentStamina.Value = newStamina;
            staminaAccumulator -= staminaToAdd;
        }
    }

    // Called when health value changes
    private void OnHealthChanged(int oldValue, int newValue)
    {
        if (IsOwner && GameHUDManager.Instance != null)
            GameHUDManager.Instance.UpdateHealth(newValue, maxHealth);
    }

    // Called when stamina value changes
    private void OnStaminaChanged(int oldValue, int newValue)
    {
        if (IsOwner && GameHUDManager.Instance != null)
            GameHUDManager.Instance.UpdateStamina(newValue, maxStamina);
    }

    // Called when death state changes
    private void OnDeathStateChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            HandleDeathVisuals();
            if (IsOwner)
                OnLocalPlayerDeath();
        }
    }

    // Applies damage to player
    public void TakeDamage(int damage, ulong attackerId)
    {
        if (!IsServer || isDead.Value) return;
        int newHealth = currentHealth.Value - damage;
        currentHealth.Value = Mathf.Max(0, newHealth);
        if (currentHealth.Value <= 0)
            Die(attackerId);
    }

    // Handles player death
    private void Die(ulong killerId)
    {
        if (!IsServer || isDead.Value) return;
        isDead.Value = true;
        if (networkAnimationController != null)
            networkAnimationController.SetDeathState(true);
        DisablePlayerControllerClientRpc();
        PlayDeathAnimationClientRpc(killerId);
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

    // Handles death visuals
    private void HandleDeathVisuals()
    {
    }

    // Client RPC to play death animation
    [ClientRpc]
    private void PlayDeathAnimationClientRpc(ulong killerId)
    {
        if (enableDeathAnimation && networkAnimationController != null)
        {
            networkAnimationController.TriggerDeath();
            isInDeathAnimation = true;
            deathAnimationEndTime = Time.time + deathAnimationDuration;
            if (enableRagdollOnDeath)
                StartCoroutine(ApplyDelayedRagdollForce(killerId));
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

    // Applies delayed ragdoll force
    private IEnumerator ApplyDelayedRagdollForce(ulong killerId)
    {
        yield return new WaitForSeconds(deathAnimationDuration - 0.1f);
        if (enableRagdollOnDeath)
            ApplyRagdollForce(killerId);
    }

    // Enables ragdoll physics
    private void EnableRagdoll()
    {
        if (!enableRagdollOnDeath) return;
        SetRagdollActive(true);
    }

    // Applies force to ragdoll
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
            rb.AddForce(forceDirection * ragdollForce, ForceMode.Impulse);
        foreach (Rigidbody ragdollRb in ragdollRigidbodies)
        {
            if (ragdollRb != rb)
            {
                Vector3 randomForce = new Vector3(Random.Range(-ragdollForce * 0.5f, ragdollForce * 0.5f), Random.Range(ragdollForce * 0.3f, ragdollForce * 0.7f), Random.Range(-ragdollForce * 0.5f, ragdollForce * 0.5f));
                ragdollRb.AddForce(randomForce, ForceMode.Impulse);
            }
        }
    }

    // Client RPC to disable player controller
    [ClientRpc]
    private void DisablePlayerControllerClientRpc()
    {
        if (!IsOwner) return;
        if (playerController != null)
            playerController.enabled = false;
        if (playerSpectator != null)
            Invoke(nameof(EnableSpectatorMode), deathAnimationDuration);
    }

    // Enables spectator mode
    private void EnableSpectatorMode()
    {
        if (playerSpectator != null && IsOwner)
        {
            playerSpectator.enabled = true;
            if (playerSpectator.enabled)
                playerSpectator.HandlePlayerDeath();
        }
    }

    // Called on local player death
    private void OnLocalPlayerDeath()
    {
        ShowDeathScreenOrSpectator();
    }

    // Shows death screen or switches to spectator
    private void ShowDeathScreenOrSpectator()
    {
    }

    // Consumes stamina
    public void ConsumeStamina(int amount)
    {
        if (!IsServer || isDead.Value) return;
        int newStamina = currentStamina.Value - amount;
        currentStamina.Value = Mathf.Max(0, newStamina);
        lastStaminaUseTime = Time.time;
    }

    // Sets sprinting state
    public void SetSprinting(bool sprinting)
    {
        isSprinting = sprinting;
        if (!sprinting)
            lastStaminaUseTime = Time.time;
    }

    // Gets current stamina
    public int GetStamina()
    {
        return currentStamina.Value;
    }

    // Checks if player is alive
    public bool IsAlive()
    {
        return !isDead.Value && currentHealth.Value > 0;
    }

    // Gets current health
    public int GetCurrentHealth()
    {
        return currentHealth.Value;
    }

    // Respawns player
    public void Respawn()
    {
        if (!IsServer) return;
        isDead.Value = false;
        currentHealth.Value = maxHealth;
        currentStamina.Value = maxStamina;
        isSprinting = false;
        lastStaminaUseTime = Time.time;
        staminaAccumulator = 0f;
        isInDeathAnimation = false;
        if (networkAnimationController != null)
        {
            networkAnimationController.SetDeathState(false);
            networkAnimationController.ResetDeathState();
        }
        SetRagdollActive(false);
        EnablePlayerClientRpc();
    }

    // Client RPC to enable player
    [ClientRpc]
    private void EnablePlayerClientRpc()
    {
        if (!IsOwner) return;
        SetRagdollActive(false);
        if (animator != null)
            animator.enabled = true;
        if (networkAnimationController != null)
            networkAnimationController.ResetDeathState();
        if (playerController != null)
            playerController.enabled = true;
        if (playerSpectator != null)
        {
            playerSpectator.StopSpectating();
            playerSpectator.enabled = false;
        }
    }

    // Called when player despawns from network
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentHealth.OnValueChanged -= OnHealthChanged;
        currentStamina.OnValueChanged -= OnStaminaChanged;
        isDead.OnValueChanged -= OnDeathStateChanged;
    }
}