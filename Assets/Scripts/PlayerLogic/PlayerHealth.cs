using UnityEngine;
using Unity.Netcode;
using TMPro;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float maxStamina = 100f;
    public float staminaRegenRate = 10f;

    // Network-synced health variable - only server can write, all clients can read
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Network-synced stamina variable
    public NetworkVariable<float> currentStamina = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GameHUDManager hudManager;
    private NetworkPlayerController playerController;

    // Events for health changes
    public System.Action<float, float> OnHealthChanged;
    public System.Action<float> OnDamageTaken;
    public System.Action OnDeath;

    public override void OnNetworkSpawn()
    {
        // Server sets initial values
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            currentStamina.Value = maxStamina;
        }

        // All clients subscribe to changes
        currentHealth.OnValueChanged += OnHealthChangedCallback;
        currentStamina.OnValueChanged += OnStaminaChangedCallback;

        if (IsOwner)
        {
            // Find HUD manager for local player
            hudManager = GameHUDManager.Instance;
            playerController = GetComponent<NetworkPlayerController>();

            // Update HUD with initial values
            if (hudManager != null)
            {
                hudManager.UpdateHealth(currentHealth.Value, maxHealth);
                hudManager.UpdateStamina(currentStamina.Value, maxStamina);
            }
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Regenerate stamina when not sprinting
        if (currentStamina.Value < maxStamina && (!playerController.IsSprinting() || playerController == null))
        {
            // Request stamina regen from server
            RequestStaminaRegenServerRpc();
        }
    }

    [ServerRpc]
    private void RequestStaminaRegenServerRpc()
    {
        if (currentStamina.Value < maxStamina)
        {
            float newStamina = Mathf.Min(maxStamina, currentStamina.Value + staminaRegenRate * Time.deltaTime);
            currentStamina.Value = newStamina;
        }
    }

    private void OnHealthChangedCallback(float oldHealth, float newHealth)
    {
        // Update HUD for local player
        if (IsOwner && hudManager != null)
        {
            hudManager.UpdateHealth(newHealth, maxHealth);
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

    private void OnStaminaChangedCallback(float oldStamina, float newStamina)
    {
        // Update HUD for local player
        if (IsOwner && hudManager != null)
        {
            hudManager.UpdateStamina(newStamina, maxStamina);
        }
    }

    [ClientRpc]
    private void TakeDamageClientRpc(float damage, ulong damagerId)
    {
        // Visual/audio feedback for damage on all clients
        if (IsOwner)
        {
            Debug.Log($"You took {damage} damage from player {damagerId}");
            // Add screen shake, blood effects, etc.
        }
    }


    // Server-only damage method
    public void TakeDamage(float damage, ulong damagerId = 0)
    {
        if (!IsServer)
        {
            Debug.LogWarning("TakeDamage called on client! This should only be called on server.");
            return;
        }

        if (currentHealth.Value <= 0) return; // Already dead

        float newHealth = Mathf.Max(0, currentHealth.Value - damage);
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

    // Server-only heal method
    public void Heal(float healAmount)
    {
        if (!IsServer)
        {
            Debug.LogWarning("Heal called on client! This should only be called on server.");
            return;
        }

        float newHealth = Mathf.Min(maxHealth, currentHealth.Value + healAmount);
        currentHealth.Value = newHealth;

        Debug.Log($"Player {OwnerClientId} healed for {healAmount}. Health: {newHealth}");
    }

    // Server-only stamina methods
    public void ConsumeStamina(float staminaCost)
    {
        if (!IsServer) return;

        currentStamina.Value = Mathf.Max(0, currentStamina.Value - staminaCost);
    }

    public void RestoreStamina(float staminaAmount)
    {
        if (!IsServer) return;

        currentStamina.Value = Mathf.Min(maxStamina, currentStamina.Value + staminaAmount);
    }

    private void HandleDeath()
    {
        Debug.Log($"Player {OwnerClientId} has died!");
        OnDeath?.Invoke();

        if (IsOwner)
        {
            // Show death screen or disable controls
            if (playerController != null)
            {
                // playerController.enabled = false;
            }
        }

       
    }

    // Public getters for client-side checks
    public float GetHealth()
    {
        return currentHealth.Value;
    }

    public float GetStamina()
    {
        return currentStamina.Value;
    }

    public bool IsAlive()
    {
        return currentHealth.Value > 0;
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChangedCallback;
        currentStamina.OnValueChanged -= OnStaminaChangedCallback;
    }
}