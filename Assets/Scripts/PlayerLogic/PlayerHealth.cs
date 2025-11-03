using UnityEngine;
using Unity.Netcode;
using TMPro;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float maxStamina = 100f;
    public float staminaRegenRate = 10f;

    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(100f);
    private NetworkVariable<float> currentStamina = new NetworkVariable<float>(100f);

    private GameHUDManager hudManager;
    private NetworkPlayerController playerController;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Find HUD manager
            hudManager = GameHUDManager.Instance;
            playerController = GetComponent<NetworkPlayerController>();

            // Set initial values
            currentHealth.Value = maxHealth;
            currentStamina.Value = maxStamina;

            // Update HUD
            if (hudManager != null)
            {
                hudManager.UpdateHealth(currentHealth.Value, maxHealth);
                hudManager.UpdateStamina(currentStamina.Value, maxStamina);

                // Removed SetPlayerName call since we don't have player name display in current HUD design
            }

            // Subscribe to health changes
            currentHealth.OnValueChanged += OnHealthChanged;
            currentStamina.OnValueChanged += OnStaminaChanged;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Regenerate stamina
        if (currentStamina.Value < maxStamina)
        {
            float newStamina = Mathf.Min(maxStamina, currentStamina.Value + staminaRegenRate * Time.deltaTime);
            if (IsServer)
            {
                currentStamina.Value = newStamina;
            }
            else
            {
                UpdateStaminaServerRpc(newStamina);
            }
        }

        // Consume stamina when moving (example)
        if (playerController != null && playerController.IsMoving() && currentStamina.Value > 0)
        {
            float staminaCost = 15f * Time.deltaTime;
            if (IsServer)
            {
                currentStamina.Value = Mathf.Max(0, currentStamina.Value - staminaCost);
            }
            else
            {
                ConsumeStaminaServerRpc(staminaCost);
            }
        }
    }

    [ServerRpc]
    private void UpdateStaminaServerRpc(float newStamina)
    {
        currentStamina.Value = newStamina;
    }

    [ServerRpc]
    public void ConsumeStaminaServerRpc(float staminaCost)
    {
        currentStamina.Value = Mathf.Max(0, currentStamina.Value - staminaCost);
    }

    private void OnHealthChanged(float oldHealth, float newHealth)
    {
        if (IsOwner && hudManager != null)
        {
            hudManager.UpdateHealth(newHealth, maxHealth);
        }

        // Check for death
        if (newHealth <= 0)
        {
            HandleDeath();
        }
    }

    private void OnStaminaChanged(float oldStamina, float newStamina)
    {
        if (IsOwner && hudManager != null)
        {
            hudManager.UpdateStamina(newStamina, maxStamina);
        }
    }

    [ServerRpc]
    public void TakeDamageServerRpc(float damage)
    {
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
    }

    [ServerRpc]
    public void HealServerRpc(float healAmount)
    {
        currentHealth.Value = Mathf.Min(maxHealth, currentHealth.Value + healAmount);
    }

    private void HandleDeath()
    {
        // Handle player death
        Debug.Log($"Player {OwnerClientId} has died!");

        // You can add respawn logic or game over logic here
        if (IsOwner)
        {
            // Show death screen or disable controls
            if (playerController != null)
            {
                // playerController.enabled = false;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            currentHealth.OnValueChanged -= OnHealthChanged;
            currentStamina.OnValueChanged -= OnStaminaChanged;
        }
    }

    // Public methods for other systems to interact with health
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
}