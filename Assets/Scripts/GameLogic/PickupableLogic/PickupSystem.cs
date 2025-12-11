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

    // Called when object spawns on network
    public override void OnNetworkSpawn()
    {
        carriedItemId.OnValueChanged += OnCarriedItemChanged;
    }

    // Main update loop
    private void Update()
    {
        if (!IsOwner) return;

        CheckForPickupItems();
        HandleInput();
    }

    // Checks for pickup items in range
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

    // Handles player input
    private void HandleInput()
    {
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

        if (Input.GetKeyDown(dropKey) && carriedItemId.Value != 0)
        {
            Vector3 dropPosition = transform.position;
            Vector3 forwardDirection = transform.forward;
            if (IsServer)
            {
                DropItem(dropPosition, forwardDirection);
            }
            else
            {
                DropItemServerRpc(dropPosition, forwardDirection);
            }
        }
    }

    // Server RPC to pickup item
    [ServerRpc]
    private void PickupItemServerRpc(ulong itemId)
    {
        PickupItem(itemId);
    }

    // Picks up item
    private void PickupItem(ulong itemId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(itemId))
        {
            return;
        }

        NetworkObject itemNetObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[itemId];
        PickupableItem item = itemNetObject.GetComponent<PickupableItem>();

        if (item == null || !item.CanBePickedUp || item.IsPickedUp)
        {
            return;
        }

        carriedItemId.Value = itemId;
        item.PickupItem(NetworkObjectId);
    }

    // Server RPC to drop item
    [ServerRpc]
    private void DropItemServerRpc(Vector3 dropPosition, Vector3 forwardDirection)
    {
        DropItem(dropPosition, forwardDirection);
    }

    // Drops item
    private void DropItem(Vector3 dropPosition, Vector3 forwardDirection)
    {
        if (carriedItemId.Value == 0)
        {
            return;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(carriedItemId.Value, out NetworkObject itemNetObject))
        {
            PickupableItem item = itemNetObject.GetComponent<PickupableItem>();
            if (item != null)
            {
                item.DropItem(dropPosition, forwardDirection);
                carriedItemId.Value = 0;
            }
        }
    }

    // Called when carried item changes
    private void OnCarriedItemChanged(ulong oldValue, ulong newValue)
    {
        UpdateHeldItemVisuals();
    }

    // Updates held item visuals
    private void UpdateHeldItemVisuals()
    {
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
        }

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
                }
            }
        }

        if (GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.HideInteractionPrompt();
        }
    }

    // Draws gizmos in editor
    private void OnDrawGizmos()
    {
        if (!IsOwner) return;

        Gizmos.color = itemInRange != null ? Color.green : Color.yellow;
        Vector3 start = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawRay(start, transform.forward * 3f);
    }

    // Called when object despawns from network
    public override void OnNetworkDespawn()
    {
        carriedItemId.OnValueChanged -= OnCarriedItemChanged;

        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
        }
    }
}