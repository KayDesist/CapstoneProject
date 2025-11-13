// SimplePickupSystem.cs
using UnityEngine;
using Unity.Netcode;

public class SimplePickupSystem : NetworkBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private Transform itemHoldPoint;
    [SerializeField] private KeyCode pickupKey = KeyCode.F;
    [SerializeField] private KeyCode dropKey = KeyCode.Q;

    private NetworkVariable<ulong> carriedItemId = new NetworkVariable<ulong>(0);
    private GameObject currentHeldItem;
    private PickupableItem itemInRange;

    public override void OnNetworkSpawn()
    {
        carriedItemId.OnValueChanged += OnCarriedItemChanged;
    }

    private void Update()
    {
        if (!IsOwner) return;

        CheckForPickupItems();
        HandleInput();
    }

    private void CheckForPickupItems()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 3f))
        {
            PickupableItem newItem = hit.collider.GetComponent<PickupableItem>();
            if (newItem != itemInRange)
            {
                itemInRange = newItem;

                if (itemInRange != null && itemInRange.CanBePickedUp && carriedItemId.Value == 0)
                {
                    if (GameHUDManager.Instance != null)
                    {
                        GameHUDManager.Instance.ShowInteractionPrompt($"Press {pickupKey} to pickup {itemInRange.ItemName}");
                    }
                }
            }
        }
        else
        {
            if (itemInRange != null)
            {
                itemInRange = null;
                if (GameHUDManager.Instance != null)
                {
                    GameHUDManager.Instance.HideInteractionPrompt();
                }
            }
        }
    }

    private void HandleInput()
    {
        // Pick up item
        if (Input.GetKeyDown(pickupKey) && itemInRange != null && carriedItemId.Value == 0)
        {
            if (IsServer)
            {
                PickupItem(itemInRange.NetworkObjectId);
            }
            else
            {
                PickupItemServerRpc(itemInRange.NetworkObjectId);
            }
        }

        // Drop item
        if (Input.GetKeyDown(dropKey) && carriedItemId.Value != 0)
        {
            Vector3 dropPosition = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
            if (IsServer)
            {
                DropItem(dropPosition);
            }
            else
            {
                DropItemServerRpc(dropPosition);
            }
        }
    }

    [ServerRpc]
    private void PickupItemServerRpc(ulong itemId)
    {
        PickupItem(itemId);
    }

    private void PickupItem(ulong itemId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(itemId))
        {
            Debug.LogError($"Item with network ID {itemId} not found!");
            return;
        }

        NetworkObject itemNetObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[itemId];
        PickupableItem item = itemNetObject.GetComponent<PickupableItem>();

        if (item == null || !item.CanBePickedUp || item.IsPickedUp)
        {
            Debug.Log("Item cannot be picked up");
            return;
        }

        carriedItemId.Value = itemId;
        item.PickupItem(NetworkObjectId);

        Debug.Log($"Picked up {item.ItemName}");
    }

    [ServerRpc]
    private void DropItemServerRpc(Vector3 dropPosition)
    {
        DropItem(dropPosition);
    }

    private void DropItem(Vector3 dropPosition)
    {
        if (carriedItemId.Value == 0)
        {
            Debug.Log("No item to drop");
            return;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(carriedItemId.Value, out NetworkObject itemNetObject))
        {
            PickupableItem item = itemNetObject.GetComponent<PickupableItem>();
            if (item != null)
            {
                item.DropItem(dropPosition);
                carriedItemId.Value = 0;
                Debug.Log($"Dropped {item.ItemName}");
            }
        }
    }

    private void OnCarriedItemChanged(ulong oldValue, ulong newValue)
    {
        UpdateHeldItemVisuals();
    }

    private void UpdateHeldItemVisuals()
    {
        // Remove current held item
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
        }

        // Add new held item if we're carrying something
        if (carriedItemId.Value != 0)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(carriedItemId.Value, out NetworkObject itemNetObject))
            {
                PickupableItem item = itemNetObject.GetComponent<PickupableItem>();
                if (item != null && item.HeldPrefab != null)
                {
                    currentHeldItem = Instantiate(item.HeldPrefab, itemHoldPoint);
                    currentHeldItem.transform.localPosition = Vector3.zero;
                    currentHeldItem.transform.localRotation = Quaternion.identity;

                    Debug.Log($"Now holding: {item.ItemName}");
                }
            }
        }
        else
        {
            Debug.Log("No item carried");
        }

        // Update UI
        if (GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.HideInteractionPrompt();
        }
    }

    private void OnDrawGizmos()
    {
        if (!IsOwner) return;

        Gizmos.color = itemInRange != null ? Color.green : Color.yellow;
        Vector3 start = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawRay(start, transform.forward * 3f);
    }

    public override void OnNetworkDespawn()
    {
        carriedItemId.OnValueChanged -= OnCarriedItemChanged;

        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
        }
    }
}