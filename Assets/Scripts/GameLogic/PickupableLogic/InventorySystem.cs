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

    [Header("Combat References")]
    [SerializeField] private PlayerHitboxDamage playerHitbox;

    private NetworkList<InventorySlot> inventorySlots;
    private NetworkVariable<int> currentSlotIndex = new NetworkVariable<int>(0);

    private GameObject currentHeldItem;
    private PickupableItem itemInRange;

    // Input handling
    private bool attackInput = false;
    private bool useInput = false;

    public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
    {
        public ulong itemNetworkId;
        public bool isEmpty;
        public FixedString32Bytes itemName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemNetworkId);
            serializer.SerializeValue(ref isEmpty);
            serializer.SerializeValue(ref itemName);
        }

        public bool Equals(InventorySlot other) => itemNetworkId == other.itemNetworkId && isEmpty == other.isEmpty;
        public override bool Equals(object obj) => obj is InventorySlot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(itemNetworkId, isEmpty);
        public static bool operator ==(InventorySlot left, InventorySlot right) => left.Equals(right);
        public static bool operator !=(InventorySlot left, InventorySlot right) => !left.Equals(right);
    }

    private void Awake()
    {
        inventorySlots = new NetworkList<InventorySlot>();

        // Get reference to player's hitbox if not set
        if (playerHitbox == null)
        {
            playerHitbox = GetComponentInChildren<PlayerHitboxDamage>(true);
        }

    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitializeEmptySlots();
        }

        inventorySlots.OnListChanged += OnInventoryChanged;
        currentSlotIndex.OnValueChanged += OnCurrentSlotChanged;

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
                    GameHUDManager.Instance?.ShowInteractionPrompt($"Press {pickupKey} to pickup {itemInRange.ItemName}");
                }
                else if (itemInRange != null && !HasEmptySlots())
                {
                    GameHUDManager.Instance?.ShowInteractionPrompt("Inventory full!");
                }
            }
        }
        else
        {
            if (itemInRange != null)
            {
                itemInRange = null;
                GameHUDManager.Instance?.HideInteractionPrompt();
            }
        }
    }

    private void HandleInput()
    {
        // Slot switching
        for (int i = 0; i < 3; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < inventorySlots.Count)
            {
                SwitchToSlotServerRpc(i);
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

        // Weapon attack (left click) - store input for FixedUpdate
        if (Input.GetMouseButtonDown(0))
        {
            attackInput = true;
        }

        // Use item (right click)
        if (Input.GetMouseButtonDown(1))
        {
            useInput = true;
        }
    }

    private void FixedUpdate()
    {
        // Handle attack input in FixedUpdate for consistent physics
        if (attackInput)
        {
            HandleWeaponAttack();
            attackInput = false;
        }

        // Handle use input
        if (useInput)
        {
            HandleItemUse();
            useInput = false;
        }
    }

    [ServerRpc]
    private void SwitchToSlotServerRpc(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < inventorySlots.Count)
        {
            currentSlotIndex.Value = slotIndex;
        }
    }

    [ServerRpc]

    private void PickupItemServerRpc(ulong itemId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(itemId)) return;

        NetworkObject itemNetObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[itemId];
        PickupableItem item = itemNetObject.GetComponent<PickupableItem>();

        if (item == null || !item.CanBePickedUp || item.IsPickedUp) return;

        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i].isEmpty)
            {
                inventorySlots[i] = new InventorySlot
                {
                    itemNetworkId = itemId,
                    isEmpty = false,
                    itemName = item.ItemName
                };

                item.PickupItem(NetworkObjectId);

                // Initialize weapon with player's hitbox
                Weapon weapon = itemNetObject.GetComponent<Weapon>();
                if (weapon != null)
                {
                    PlayerHealth health = GetComponent<PlayerHealth>();

                    // Get reference to player's hitbox if not already set
                    if (playerHitbox == null)
                    {
                        playerHitbox = GetComponentInChildren<PlayerHitboxDamage>(true);
                    }

                    // Initialize weapon with all three required parameters
                    weapon.Initialize(OwnerClientId, health, playerHitbox);
                }

                UpdateHeldItemVisualsClientRpc();
                Debug.Log($"Picked up {item.ItemName} into slot {i}");
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

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
        {
            PickupableItem item = itemNetObject.GetComponent<PickupableItem>();
            if (item != null)
            {
                item.DropItem(dropPosition);
            }
        }

        inventorySlots[currentSlotIndex.Value] = new InventorySlot
        {
            itemNetworkId = 0,
            isEmpty = true,
            itemName = "Empty"
        };

        UpdateHeldItemVisualsClientRpc();
    }

    private void HandleWeaponAttack()
    {
        if (currentSlotIndex.Value == -1) return;

        var currentSlot = inventorySlots[currentSlotIndex.Value];
        if (currentSlot.isEmpty) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
        {
            Weapon weapon = itemNetObject.GetComponent<Weapon>();
            if (weapon != null)
            {
                // Call the non-RPC attack method
                weapon.Attack();
            }
        }
    }

    private void HandleItemUse()
    {
        if (currentSlotIndex.Value == -1) return;

        var currentSlot = inventorySlots[currentSlotIndex.Value];
        if (currentSlot.isEmpty) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
        {
            PickupableItem item = itemNetObject.GetComponent<PickupableItem>();
            if (item != null)
            {
                item.Use(OwnerClientId);
            }
        }
    }

    private void OnInventoryChanged(NetworkListEvent<InventorySlot> changeEvent)
    {
        if (IsOwner)
        {
            UpdateHeldItemVisuals();
        }
    }

    private void OnCurrentSlotChanged(int oldValue, int newValue)
    {
        if (IsOwner)
        {
            UpdateHeldItemVisuals();
        }
    }

    private void UpdateHeldItemVisuals()
    {
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
        }

        if (currentSlotIndex.Value != -1 && !inventorySlots[currentSlotIndex.Value].isEmpty)
        {
            var currentSlot = inventorySlots[currentSlotIndex.Value];

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
            {
                PickupableItem item = itemNetObject.GetComponent<PickupableItem>();
                if (item != null && item.HeldPrefab != null)
                {
                    currentHeldItem = Instantiate(item.HeldPrefab, itemHoldPoint);
                    currentHeldItem.transform.localPosition = Vector3.zero;
                    currentHeldItem.transform.localRotation = Quaternion.identity;

                    // Enable renderers
                    Renderer[] allRenderers = currentHeldItem.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer renderer in allRenderers)
                    {
                        renderer.enabled = true;
                    }

                    currentHeldItem.SetActive(true);
                    item.ConfigureHeldItem(currentHeldItem);

                    // Notify weapon it's equipped
                    Weapon weapon = itemNetObject.GetComponent<Weapon>();
                    if (weapon != null)
                    {
                        weapon.OnEquipped();
                    }
                }
            }
        }
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