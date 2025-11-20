using UnityEngine;
using Unity.Netcode;

public class ConsumableItem : PickupableItem
{
    [Header("Consumable Settings")]
    public float healthRestore = 25f;
    public float staminaRestore = 30f;

    public override void Use(ulong userClientId)
    {
        Debug.Log($"ConsumableItem.Use called locally by client {userClientId} for item {ItemName}");

        // Call server RPC to consume the item
        ConsumeServerRpc(userClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ConsumeServerRpc(ulong userClientId, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"ConsumeServerRpc received from client {senderClientId} for user {userClientId}");

        // Verify the requesting client is the same as the user
        if (senderClientId != userClientId)
        {
            Debug.LogWarning($"Client {senderClientId} attempted to use item for client {userClientId}");
            return;
        }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(userClientId, out var client))
        {
            PlayerHealth health = client.PlayerObject.GetComponent<PlayerHealth>();
            InventorySystem inventory = client.PlayerObject.GetComponent<InventorySystem>();

            if (health != null)
            {
                Debug.Log($"Found PlayerHealth for client {userClientId}. Current health: {health.GetHealth()}, stamina: {health.GetStamina()}");

                bool wasUsed = false;

                // Only heal if not at full health
                if (healthRestore > 0 && health.GetHealth() < health.maxHealth)
                {
                    health.Heal(healthRestore);
                    Debug.Log($"Player {userClientId} consumed {ItemName} and restored {healthRestore} health. New health: {health.GetHealth()}");
                    wasUsed = true;
                }

                // Only restore stamina if not at full stamina
                if (staminaRestore > 0 && health.GetStamina() < health.maxStamina)
                {
                    health.RestoreStamina(staminaRestore);
                    Debug.Log($"Player {userClientId} consumed {ItemName} and restored {staminaRestore} stamina. New stamina: {health.GetStamina()}");
                    wasUsed = true;
                }

                if (wasUsed)
                {
                    // Remove from inventory first
                    if (inventory != null)
                    {
                        RemoveFromInventoryClientRpc(userClientId, NetworkObjectId);
                    }

                    // Destroy the consumable after use
                    if (NetworkObject != null)
                    {
                        Debug.Log($"Despawning consumable {ItemName}");
                        NetworkObject.Despawn(true);
                    }
                }
                else
                {
                    Debug.Log($"Consumable not used - player already at full health/stamina");
                }
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

    [ClientRpc]
    private void RemoveFromInventoryClientRpc(ulong clientId, ulong itemId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            InventorySystem inventory = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject().GetComponent<InventorySystem>();
            if (inventory != null)
            {
                // This will trigger the inventory to remove the item
                inventory.ForceRemoveItem(itemId);
            }
        }
    }
}