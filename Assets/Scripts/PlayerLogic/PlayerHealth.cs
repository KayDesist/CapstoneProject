using UnityEngine;
using Unity.Netcode;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public int maxStamina = 100;

    [Header("Stamina Settings")]
    public float staminaRegenRate = 15f; // Stamina per second
    public float staminaRegenDelay = 2f; // Seconds before regen starts after using stamina
    public float staminaRegenCooldown = 0f; // Time when we can start regenerating

    // Network variables - Server writes, everyone reads
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

    // Local stamina tracking
    private bool isSprinting = false;
    private float lastStaminaUseTime = 0f;
    private float staminaAccumulator = 0f;

    private void Awake()
    {
        networkAnimationController = GetComponent<NetworkAnimationController>();
        playerController = GetComponent<NetworkPlayerController>();
        playerSpectator = GetComponent<PlayerSpectator>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Set initial values on server
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            currentStamina.Value = maxStamina;
        }

        // Subscribe to value changes for UI updates
        currentHealth.OnValueChanged += OnHealthChanged;
        currentStamina.OnValueChanged += OnStaminaChanged;
        isDead.OnValueChanged += OnDeathStateChanged;

        // Debug logging
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
        // Don't regenerate if we're sprinting or recently used stamina
        if (isSprinting) return;

        float timeSinceLastUse = Time.time - lastStaminaUseTime;
        if (timeSinceLastUse < staminaRegenDelay) return;

        // Regenerate stamina
        staminaAccumulator += staminaRegenRate * Time.deltaTime;

        if (staminaAccumulator >= 1f)
        {
            int staminaToAdd = Mathf.FloorToInt(staminaAccumulator);
            int newStamina = Mathf.Min(maxStamina, currentStamina.Value + staminaToAdd);

            if (newStamina != currentStamina.Value)
            {
                currentStamina.Value = newStamina;
                Debug.Log($"Regenerated {staminaToAdd} stamina, now at {currentStamina.Value}");
            }

            staminaAccumulator -= staminaToAdd;
        }
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
        Debug.Log($"Player {OwnerClientId} health changed: {oldValue} -> {newValue}");

        // Update UI for local player
        if (IsOwner && GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.UpdateHealth(newValue, maxHealth);
        }
    }

    private void OnStaminaChanged(int oldValue, int newValue)
    {
        Debug.Log($"Player {OwnerClientId} stamina changed: {oldValue} -> {newValue}");

        // Update UI for local player
        if (IsOwner && GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.UpdateStamina(newValue, maxStamina);
        }
    }

    private void OnDeathStateChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"Player {OwnerClientId} death state changed: {oldValue} -> {newValue}");

        if (newValue && IsOwner)
        {
            // Handle local player death
            OnLocalPlayerDeath();
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

        // Disable player controller (handled by client via OnDeathStateChanged)
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

        // Notify player died to all clients (for spectating, etc.)
        PlayerDiedClientRpc(killerId);
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

        // Enable spectator mode after a delay
        if (playerSpectator != null)
        {
            Invoke(nameof(EnableSpectatorMode), 1f);
        }
    }

    private void EnableSpectatorMode()
    {
        if (playerSpectator != null && IsOwner)
        {
            playerSpectator.enabled = true;
            Debug.Log("Enabling spectator mode");
        }
    }

    [ClientRpc]
    private void PlayerDiedClientRpc(ulong killerId)
    {
        Debug.Log($"Player {OwnerClientId} died (killed by {killerId}) - notified all clients");

        // No death animation - just disable the character visually
        if (IsOwner)
        {
            // For the dead player, hide or disable their model
            HandleLocalDeathVisuals();
        }
        else
        {
            // For other players, they might see a ragdoll or just leave the body
            HandleRemoteDeathVisuals();
        }
    }

    private void HandleLocalDeathVisuals()
    {
        // For local player who died - disable rendering
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = false;
        }

        // Disable colliders
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }

        Debug.Log("Disabled local player visuals due to death");
    }

    private void HandleRemoteDeathVisuals()
    {
        // For remote players - you might want to show a ragdoll or just leave the body
        // For now, we'll just disable the animator to freeze them
        if (networkAnimationController != null && networkAnimationController.enabled)
        {
            networkAnimationController.enabled = false;
        }

        Debug.Log("Disabled remote player animator due to death");
    }

    private void OnLocalPlayerDeath()
    {
        Debug.Log("Local player death handler called");

        // Disable player controller
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // Show death screen or spectator mode
        ShowDeathScreenOrSpectator();
    }

    private void ShowDeathScreenOrSpectator()
    {
        // You might want to show a death screen here
        // For now, we'll just log and enable spectator mode
        Debug.Log("You died! Waiting for game to end or enabling spectator mode...");

        // If there are enough players, auto-enable spectator after a delay
        if (NetworkManager.Singleton.ConnectedClientsIds.Count > 2)
        {
            Invoke(nameof(EnableSpectatorMode), 2f);
        }
    }

    public void ConsumeStamina(int amount)
    {
        if (!IsServer || isDead.Value) return;

        int newStamina = currentStamina.Value - amount;
        currentStamina.Value = Mathf.Max(0, newStamina);

        // Record when we last used stamina
        lastStaminaUseTime = Time.time;

        Debug.Log($"Consumed {amount} stamina, now at {currentStamina.Value}");
    }

    public void SetSprinting(bool sprinting)
    {
        // Update sprinting state for stamina regeneration
        isSprinting = sprinting;

        // If we just stopped sprinting, record the time
        if (!sprinting)
        {
            lastStaminaUseTime = Time.time;
        }

        Debug.Log($"Player {OwnerClientId} sprinting: {sprinting}");
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

        // Enable player on all clients
        EnablePlayerClientRpc();
    }

    [ClientRpc]
    private void EnablePlayerClientRpc()
    {
        if (!IsOwner) return;

        // Re-enable visuals for local player
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = true;
        }

        // Re-enable colliders
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = true;
        }

        // Re-enable player controller
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        // Re-enable animator
        if (networkAnimationController != null)
        {
            networkAnimationController.enabled = true;
        }

        Debug.Log("Player respawned and re-enabled");
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // Unsubscribe from events
        currentHealth.OnValueChanged -= OnHealthChanged;
        currentStamina.OnValueChanged -= OnStaminaChanged;
        isDead.OnValueChanged -= OnDeathStateChanged;
    }
}