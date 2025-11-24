using UnityEngine;
using Unity.Netcode;
using TMPro;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float maxStamina = 100f;
    public float staminaRegenRate = 10f;

    // Network-synced health variable with faster updates
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Network-synced stamina variable with faster updates
    public NetworkVariable<float> currentStamina = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private GameHUDManager hudManager;
    private NetworkPlayerController playerController;

    // Client-side prediction for immediate feedback
    private float localHealth;
    private float localStamina;
    private bool initialized = false;
    private bool isDead = false;

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
        if (!IsOwner || !initialized || isDead) return;

        // Regenerate stamina when not sprinting - immediate local update
        if (localStamina < maxStamina && (!playerController.IsSprinting() || playerController == null))
        {
            float newStamina = Mathf.Min(maxStamina, localStamina + staminaRegenRate * Time.deltaTime);
            if (newStamina != localStamina)
            {
                localStamina = newStamina;
                hudManager?.UpdateStamina(localStamina, maxStamina);

                // Sync with server periodically or on significant changes
                if (Mathf.Abs(localStamina - currentStamina.Value) > 5f)
                {
                    RequestStaminaRegenServerRpc();
                }
            }
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

    private void OnStaminaChangedCallback(float oldStamina, float newStamina)
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

        if (currentHealth.Value <= 0 || isDead)
        {
            Debug.Log($"Player {OwnerClientId} is already dead, ignoring damage");
            return;
        }

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

            // Hide HUD for dead players
            if (GameHUDManager.Instance != null)
            {
                GameHUDManager.Instance.gameObject.SetActive(false);
            }

            // Unlock cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("Local player died - controls disabled");
        }


    }

    // Public getters for client-side checks
    public float GetHealth()
    {
        return IsOwner ? localHealth : currentHealth.Value;
    }

    public float GetStamina()
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