// InventorySystem.cs
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;
using Unity.Collections;

public class InventorySystem : NetworkBehaviour
{
    [Header("Inventory Settings")]
    [SerializeField] private int maxSlots = 3;
    [SerializeField] private Transform itemHoldPoint;

    [Header("Input Settings")]
    [SerializeField] private KeyCode pickupKey = KeyCode.F;
    [SerializeField] private KeyCode dropKey = KeyCode.Q;
    [SerializeField] private KeyCode useKey = KeyCode.Mouse1;

    private NetworkList<InventorySlot> inventorySlots;
    private NetworkVariable<int> currentSlotIndex = new NetworkVariable<int>(0); // Start with slot 0 selected

    private GameObject currentHeldItem;
    private PickupableItem itemInRange;

    public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
    {
        public ulong itemNetworkId;
        public bool isEmpty;
        public FixedString32Bytes itemName; // Track item name for debugging

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemNetworkId);
            serializer.SerializeValue(ref isEmpty);
            serializer.SerializeValue(ref itemName);
        }

        public bool Equals(InventorySlot other)
        {
            return itemNetworkId == other.itemNetworkId && isEmpty == other.isEmpty;
        }

        public override bool Equals(object obj)
        {
            return obj is InventorySlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + itemNetworkId.GetHashCode();
                hash = hash * 23 + isEmpty.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(InventorySlot left, InventorySlot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InventorySlot left, InventorySlot right)
        {
            return !left.Equals(right);
        }
    }

    private void Awake()
    {
        inventorySlots = new NetworkList<InventorySlot>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitializeEmptySlots();
        }

        inventorySlots.OnListChanged += OnInventoryChanged;
        currentSlotIndex.OnValueChanged += OnCurrentSlotChanged;

        // Initialize visuals
        if (IsOwner)
        {
            Invoke(nameof(UpdateHeldItemVisuals), 0.5f);
        }
    }

    private void InitializeEmptySlots()
    {
        for (int i = 0; i < maxSlots; i++)
        {
            inventorySlots.Add(new InventorySlot
            {
                itemNetworkId = 0,
                isEmpty = true,
                itemName = "Empty"
            });
        }
        Debug.Log("Initialized empty slots");
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

                if (itemInRange != null && itemInRange.CanBePickedUp && HasEmptySlots())
                {
                    if (GameHUDManager.Instance != null)
                    {
                        GameHUDManager.Instance.ShowInteractionPrompt($"Press {pickupKey} to pickup {itemInRange.ItemName}");
                    }
                }
                else if (itemInRange != null && !HasEmptySlots())
                {
                    if (GameHUDManager.Instance != null)
                    {
                        GameHUDManager.Instance.ShowInteractionPrompt("Inventory full!");
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
        // Fixed slot switching: Key 1 = Slot 0, Key 2 = Slot 1, Key 3 = Slot 2
        for (int i = 0; i < 3; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < inventorySlots.Count)
            {
                SwitchToSlot(i);
            }
        }

        // Pick up item
        if (Input.GetKeyDown(pickupKey) && itemInRange != null && HasEmptySlots())
        {
            PickupItemServerRpc(itemInRange.NetworkObjectId);
        }

        // Drop current item
        if (Input.GetKeyDown(dropKey) && currentSlotIndex.Value != -1)
        {
            Vector3 dropPosition = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
            DropCurrentItemServerRpc(dropPosition);
        }

        // Use item (right click)
        if (Input.GetKeyDown(useKey) && currentSlotIndex.Value != -1)
        {
            UseCurrentItemServerRpc();
        }
    }

    [ServerRpc]
    private void SwitchToSlotServerRpc(int slotIndex)
    {
        SwitchToSlot(slotIndex);
    }

    private void SwitchToSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < inventorySlots.Count)
        {
            currentSlotIndex.Value = slotIndex;
            Debug.Log($"Switched to slot {slotIndex}");
        }
    }

    [ServerRpc]
    private void PickupItemServerRpc(ulong itemId)
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

        // Find first empty slot
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i].isEmpty)
            {
                // Update inventory slot with item details
                inventorySlots[i] = new InventorySlot
                {
                    itemNetworkId = itemId,
                    isEmpty = false,
                    itemName = item.ItemName
                };

                // Pick up the item
                item.PickupItem(NetworkObjectId);

                // AUTO-EQUIP FIX: Always update visuals when picking up an item
                // This ensures the mesh shows up even if no slot was previously selected
                UpdateHeldItemVisualsClientRpc();

                Debug.Log($"Picked up {item.ItemName} into slot {i}");

                // Log inventory state for debugging
                LogInventoryState();
                return;
            }
        }
    }

    [ClientRpc]
    private void UpdateHeldItemVisualsClientRpc()
    {
        if (IsOwner)
        {
            UpdateHeldItemVisuals();
        }
    }

    [ServerRpc]
    private void DropCurrentItemServerRpc(Vector3 dropPosition)
    {
        if (currentSlotIndex.Value == -1) return;

        var currentSlot = inventorySlots[currentSlotIndex.Value];
        if (currentSlot.isEmpty) return;

        // Drop the item
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
        {
            PickupableItem item = itemNetObject.GetComponent<PickupableItem>();
            if (item != null)
            {
                item.DropItem(dropPosition);
                Debug.Log($"Dropped {item.ItemName}");
            }
        }

        // Clear the slot
        inventorySlots[currentSlotIndex.Value] = new InventorySlot
        {
            itemNetworkId = 0,
            isEmpty = true,
            itemName = "Empty"
        };

        // Update visuals after dropping
        UpdateHeldItemVisualsClientRpc();

        // Log inventory state for debugging
        LogInventoryState();
    }

    [ServerRpc]
    private void UseCurrentItemServerRpc()
    {
        if (currentSlotIndex.Value == -1) return;

        var currentSlot = inventorySlots[currentSlotIndex.Value];
        if (currentSlot.isEmpty) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
        {
            PickupableItem item = itemNetObject.GetComponent<PickupableItem>();
            if (item != null)
            {
                Debug.Log($"Used item: {item.ItemName}");
            }
        }
    }

    private void OnInventoryChanged(NetworkListEvent<InventorySlot> changeEvent)
    {
        Debug.Log($"Inventory changed: Slot {changeEvent.Index} - {inventorySlots[changeEvent.Index].itemName}");

        if (IsOwner)
        {
            UpdateHeldItemVisuals();
        }
    }

    private void OnCurrentSlotChanged(int oldValue, int newValue)
    {
        Debug.Log($"Current slot changed from {oldValue} to {newValue}");

        if (IsOwner)
        {
            UpdateHeldItemVisuals();
        }
    }

    private void UpdateHeldItemVisuals()
    {
        Debug.Log("Updating held item visuals...");

        // Remove current held item
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
            Debug.Log("Destroyed previous held item");
        }

        // Check if we have a valid slot and item
        if (currentSlotIndex.Value != -1 && !inventorySlots[currentSlotIndex.Value].isEmpty)
        {
            var currentSlot = inventorySlots[currentSlotIndex.Value];
            Debug.Log($"Trying to display item from slot {currentSlotIndex.Value}: {currentSlot.itemName}");

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
            {
                PickupableItem item = itemNetObject.GetComponent<PickupableItem>();
                if (item != null && item.HeldPrefab != null)
                {
                    // Instantiate the held prefab
                    currentHeldItem = Instantiate(item.HeldPrefab, itemHoldPoint);
                    currentHeldItem.transform.localPosition = Vector3.zero;
                    currentHeldItem.transform.localRotation = Quaternion.identity;

                    // Force enable all renderers
                    Renderer[] allRenderers = currentHeldItem.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer renderer in allRenderers)
                    {
                        renderer.enabled = true;
                    }

                    // Ensure the item is active
                    currentHeldItem.SetActive(true);

                    Debug.Log($"Successfully instantiated held item: {item.ItemName} in slot {currentSlotIndex.Value}");

                    // Call configuration if available
                    item.ConfigureHeldItem(currentHeldItem);
                }
                else
                {
                    Debug.LogWarning($"Item or held prefab is null - Item: {item != null}, HeldPrefab: {item?.HeldPrefab != null}");
                }
            }
            else
            {
                Debug.LogWarning($"Could not find network object for item ID {currentSlot.itemNetworkId}");
            }
        }
        else
        {
            Debug.Log("No item to display - slot is empty or invalid");
        }

        LogInventoryState();
    }

    private void LogInventoryState()
    {
        Debug.Log("=== CURRENT INVENTORY STATE ===");
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            var slot = inventorySlots[i];
            string slotInfo = $"Slot {i}: ";

            if (slot.isEmpty)
            {
                slotInfo += "Empty";
            }
            else
            {
                slotInfo += $"{slot.itemName} (ID: {slot.itemNetworkId})";
            }

            if (i == currentSlotIndex.Value)
            {
                slotInfo += " [CURRENTLY EQUIPPED]";
            }

            Debug.Log(slotInfo);
        }
        Debug.Log("===============================");
    }

    private bool HasEmptySlots()
    {
        foreach (var slot in inventorySlots)
        {
            if (slot.isEmpty) return true;
        }
        return false;
    }

    public override void OnNetworkDespawn()
    {
        inventorySlots.OnListChanged -= OnInventoryChanged;
        currentSlotIndex.OnValueChanged -= OnCurrentSlotChanged;

        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
        }
    }
}