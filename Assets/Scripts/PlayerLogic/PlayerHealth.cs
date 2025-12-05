using UnityEngine;
using Unity.Netcode;
using TMPro;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public int maxStamina = 100;
    public int staminaRegenRate = 15; // Increased regen rate for faster recovery

    // Network-synced health variable
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Network-synced stamina variable
    public NetworkVariable<int> currentStamina = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GameHUDManager hudManager;
    private NetworkPlayerController playerController;
    private bool isSprinting = false;

    // For server-side regeneration timing
    private float staminaRegenTimer = 0f;
    private const float STAMINA_REGEN_INTERVAL = 0.1f; // Regenerate every 0.1 seconds

    // Client-side values for immediate feedback
    private int localHealth;
    private int localStamina;
    private bool initialized = false;
    private bool isDead = false;

    // Events for health changes
    public System.Action<int, int> OnHealthChanged;
    public System.Action<int> OnDamageTaken;
    public System.Action OnDeath;

    public override void OnNetworkSpawn()
    {
        // Server sets initial values
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            currentStamina.Value = maxStamina;
            isDead = false;
        }

        // Initialize local values
        localHealth = currentHealth.Value;
        localStamina = currentStamina.Value;

        // All clients subscribe to changes
        currentHealth.OnValueChanged += OnHealthChangedCallback;
        currentStamina.OnValueChanged += OnStaminaChangedCallback;

        if (IsOwner)
        {
            // Find HUD manager for local player
            hudManager = GameHUDManager.Instance;
            playerController = GetComponent<NetworkPlayerController>();

            // Update HUD with initial values immediately
            if (hudManager != null)
            {
                hudManager.UpdateHealth(localHealth, maxHealth);
                hudManager.UpdateStamina(localStamina, maxStamina);
            }

            initialized = true;
        }
    }

    private void Update()
    {
        // Server handles stamina regeneration
        if (IsServer && !isDead)
        {
            HandleStaminaRegeneration();
        }
    }

    private void HandleStaminaRegeneration()
    {
        // Only regenerate if not sprinting
        if (!isSprinting && currentStamina.Value < maxStamina)
        {
            // Use timer-based regeneration for whole numbers
            staminaRegenTimer += Time.deltaTime;

            if (staminaRegenTimer >= STAMINA_REGEN_INTERVAL)
            {
                // Calculate how many stamina points to add
                int pointsToAdd = Mathf.RoundToInt(staminaRegenRate * STAMINA_REGEN_INTERVAL);
                if (pointsToAdd < 1) pointsToAdd = 1; // Ensure at least 1 point

                currentStamina.Value = Mathf.Min(maxStamina, currentStamina.Value + pointsToAdd);
                staminaRegenTimer = 0f;
            }
        }
        else
        {
            // Reset timer when sprinting or at max stamina
            staminaRegenTimer = 0f;
        }
    }

    // Method to update sprinting state from NetworkPlayerController
    public void SetSprinting(bool sprinting)
    {
        if (IsServer)
        {
            isSprinting = sprinting;
        }
        else
        {
            SetSprintingServerRpc(sprinting);
        }
    }

    [ServerRpc]
    private void SetSprintingServerRpc(bool sprinting)
    {
        isSprinting = sprinting;
    }

    private void OnHealthChangedCallback(int oldHealth, int newHealth)
    {
        // Update local health immediately
        localHealth = newHealth;

        // Update HUD for local player immediately
        if (IsOwner && hudManager != null)
        {
            hudManager.UpdateHealth(localHealth, maxHealth);
        }

        // Invoke events for other systems
        OnHealthChanged?.Invoke(newHealth, maxHealth);

        // Check if damage was taken
        if (newHealth < oldHealth)
        {
            OnDamageTaken?.Invoke(oldHealth - newHealth);
            Debug.Log($"Player {OwnerClientId} health changed: {oldHealth} -> {newHealth}");
        }

        // Check for death
        if (newHealth <= 0 && oldHealth > 0)
        {
            HandleDeath();
        }
    }

    private void OnStaminaChangedCallback(int oldStamina, int newStamina)
    {
        // Update local stamina immediately
        localStamina = newStamina;

        // Update HUD for local player immediately
        if (IsOwner && hudManager != null)
        {
            hudManager.UpdateStamina(localStamina, maxStamina);
        }
    }

    [ClientRpc]
    private void TakeDamageClientRpc(int damage, ulong damagerId)
    {
        // Visual/audio feedback for damage on all clients
        if (IsOwner)
        {
            Debug.Log($"You took {damage} damage from player {damagerId}");
            // Add screen shake, blood effects, etc.
        }
    }

    // Server-only damage method
    public void TakeDamage(int damage, ulong damagerId = 0)
    {
        if (!IsServer)
        {
            Debug.LogWarning("TakeDamage called on client! This should only be called on server.");
            return;
        }

        if (currentHealth.Value <= 0 || isDead)
        {
            Debug.Log($"Player {OwnerClientId} is already dead, ignoring damage");
            return;
        }

        int newHealth = Mathf.Max(0, currentHealth.Value - damage);
        currentHealth.Value = newHealth;

        // Notify all clients about the damage for visual feedback
        TakeDamageClientRpc(damage, damagerId);

        Debug.Log($"Player {OwnerClientId} took {damage} damage from {damagerId}. Health: {newHealth}");

        // Check for death after damage
        if (newHealth <= 0)
        {
            HandleDeath();
        }
    }

    // Server-only stamina methods
    public void ConsumeStamina(int staminaCost)
    {
        if (!IsServer) return;

        // Allow stamina to go to 0 or negative, then clamp to 0
        int newStamina = currentStamina.Value - staminaCost;
        currentStamina.Value = Mathf.Max(0, newStamina);
    }

    public void RestoreStamina(int staminaAmount)
    {
        if (!IsServer) return;

        currentStamina.Value = Mathf.Min(maxStamina, currentStamina.Value + staminaAmount);
    }

    private void HandleDeath()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log($"Player {OwnerClientId} has died!");

        PlayerSpectator spectator = GetComponent<PlayerSpectator>();
        if (spectator != null && IsOwner)
        {
            spectator.HandlePlayerDeath();
        }

        // Notify EndGameManager about this death
        if (EndGameManager.Instance != null && RoleManager.Instance != null)
        {
            var role = RoleManager.Instance.GetPlayerRole(OwnerClientId);
            Debug.Log($"Notifying EndGameManager about death - Player {OwnerClientId}, Role: {role}");
            EndGameManager.Instance.OnPlayerDied(OwnerClientId, role);
        }
        else
        {
            Debug.LogError($"Cannot notify EndGameManager - Instance: {EndGameManager.Instance != null}, RoleManager: {RoleManager.Instance != null}");
        }

        OnDeath?.Invoke();

        if (IsOwner)
        {
            // For all dead players, disable controls and show cursor
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            // IMPORTANT: Don't hide the HUD completely for dead players
            // They still need to see the end game UI
            if (GameHUDManager.Instance != null)
            {
                // Instead of hiding, just show minimal HUD or keep it visible
                GameHUDManager.Instance.ResetHUD();
            }

            // Unlock cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("Local player died - controls disabled (but HUD kept for end game)");
        }
    }

    // Public getters for client-side checks
    public int GetHealth()
    {
        return IsOwner ? localHealth : currentHealth.Value;
    }

    public int GetStamina()
    {
        return IsOwner ? localStamina : currentStamina.Value;
    }

    public bool IsAlive()
    {
        return currentHealth.Value > 0 && !isDead;
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChangedCallback;
        currentStamina.OnValueChanged -= OnStaminaChangedCallback;
    }
}