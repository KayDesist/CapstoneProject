using UnityEngine;
using Unity.Netcode;

public class ConsumableItem : PickupableItem
{
    [Header("Consumable Settings")]
    public float healthRestore = 25f;
    public float staminaRestore = 30f;

    // ADD 'override' keyword to properly override the base method
    public override void Use(ulong userClientId)
    {
        // Send use request to server
        ConsumeServerRpc(userClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ConsumeServerRpc(ulong userClientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(userClientId, out var client))
        {
            PlayerHealth health = client.PlayerObject.GetComponent<PlayerHealth>();
            if (health != null)
            {
                if (healthRestore > 0)
                {
                    health.Heal(healthRestore);
                    Debug.Log($"Player {userClientId} consumed {ItemName} and restored {healthRestore} health");
                }

                if (staminaRestore > 0)
                {
                    health.RestoreStamina(staminaRestore);
                    Debug.Log($"Player {userClientId} consumed {ItemName} and restored {staminaRestore} stamina");
                }

                // Destroy the consumable after use
                if (NetworkObject != null)
                    NetworkObject.Despawn(true);
            }
            else
            {
                Debug.LogError($"PlayerHealth component not found on player {userClientId}");
            }
        }
        else
        {
            Debug.LogError($"Client {userClientId} not found in connected clients");
        }
    }
}