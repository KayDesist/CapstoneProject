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

    private bool gameEnded = false;

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

        // Ensure hitbox reference is set
        if (playerHitbox == null)
        {
            playerHitbox = GetComponentInChildren<PlayerHitboxDamage>(true);
            if (playerHitbox == null)
            {
                Debug.LogError("PlayerHitboxDamage not found on player!");
            }
            else
            {
                Debug.Log($"Hitbox found and assigned: {playerHitbox.gameObject.name}");
            }
        }

        inventorySlots.OnListChanged += OnInventoryChanged;
        currentSlotIndex.OnValueChanged += OnCurrentSlotChanged;

        if (EndGameManager.Instance != null)
        {
            EndGameManager.Instance.OnGameEnded += HandleGameEnded;
        }

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
   
        if (!IsOwner || gameEnded) return;

        HandleInput();

        // Check if the current itemInRange is still valid and pickupable
        if (itemInRange != null && (!itemInRange.CanBePickedUp || itemInRange.IsPickedUp))
        {
            itemInRange = null;
            GameHUDManager.Instance?.HideInteractionPrompt();
        }
    }

    private void HandleInput()
    {
     
        if (gameEnded) return;

        // Slot switching - FIXED: Handle locally for immediate response
        for (int i = 0; i < 3; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < inventorySlots.Count)
            {
                // Switch locally first for immediate visual feedback
                int newSlotIndex = i;

                // Update visuals immediately
                if (newSlotIndex >= 0 && newSlotIndex < inventorySlots.Count)
                {
                    UpdateHeldItemVisualsForSlot(newSlotIndex);
                }

                // Then sync with server
                SwitchToSlotServerRpc(newSlotIndex);
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
            // Send both position and forward direction
            DropCurrentItemServerRpc(transform.position, transform.forward);
        }

        // Weapon attack (left click) - store input for FixedUpdate
        if (Input.GetMouseButtonDown(0))
        {
            attackInput = true;
        }

        if (Input.GetMouseButtonDown(1))
        {
            useInput = true;
        }
    }

    private void FixedUpdate()
    {
        if (gameEnded) return;

        // Handle attack input in FixedUpdate for consistent physics
        if (attackInput)
        {
            HandleWeaponAttack();
            attackInput = false;
        }

        // Handle use input - REMOVED: Consumable usage
        if (useInput)
        {
            useInput = false;
        }
    }

    // Collision-based detection for items
    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner || gameEnded) return;

        PickupableItem item = other.GetComponent<PickupableItem>();
        if (item != null && item.CanBePickedUp)
        {
            itemInRange = item;
            UpdateInteractionPrompt();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner || gameEnded) return;

        PickupableItem item = other.GetComponent<PickupableItem>();
        if (item != null && item == itemInRange)
        {
            itemInRange = null;
            GameHUDManager.Instance?.HideInteractionPrompt();
        }
    }

    private void UpdateInteractionPrompt()
    {
        if (itemInRange != null && itemInRange.CanBePickedUp)
        {
            if (HasEmptySlots())
            {
                GameHUDManager.Instance?.ShowInteractionPrompt($"Press {pickupKey} to pickup {itemInRange.ItemName}");
            }
            else
            {
                GameHUDManager.Instance?.ShowInteractionPrompt("Inventory full!");
            }
        }
        else
        {
            GameHUDManager.Instance?.HideInteractionPrompt();
        }
    }

    [ServerRpc]
    private void SwitchToSlotServerRpc(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < inventorySlots.Count)
        {
            currentSlotIndex.Value = slotIndex;
            Debug.Log($"Server updated current slot to {slotIndex}");
        }
    }

    [ServerRpc]
    private void PickupItemServerRpc(ulong itemId)
    {
    
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(itemId))
        {
            Debug.LogWarning($"Cannot pickup item - network object {itemId} not found");
            return;
        }

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
                        if (playerHitbox == null)
                        {
                            Debug.LogError("PlayerHitboxDamage not found on player!");
                        }
                    }

                    // Initialize weapon with all three required parameters
                    weapon.Initialize(OwnerClientId, health, playerHitbox);
                    Debug.Log($"Weapon {weapon.weaponName} initialized for player {OwnerClientId}");
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

            // Update the interaction prompt after picking up an item
            // This will show "Inventory full!" if we just filled the last slot
            UpdateInteractionPrompt();
        }
    }

    [ServerRpc]
    private void DropCurrentItemServerRpc(Vector3 dropPosition, Vector3 forwardDirection)
    {
        if (currentSlotIndex.Value == -1) return;

        var currentSlot = inventorySlots[currentSlotIndex.Value];
        if (currentSlot.isEmpty) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
        {
            PickupableItem item = itemNetObject.GetComponent<PickupableItem>();
            if (item != null)
            {
                item.DropItem(dropPosition, forwardDirection);
            }
        }
        else
        {
            Debug.LogWarning($"Cannot drop item - network object {currentSlot.itemNetworkId} not found");
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
        if (currentSlotIndex.Value == -1)
        {
            Debug.Log("No slot selected");
            return;
        }

        var currentSlot = inventorySlots[currentSlotIndex.Value];
        if (currentSlot.isEmpty)
        {
            Debug.Log("Current slot is empty");
            return;
        }

        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.SpawnManager == null ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(currentSlot.itemNetworkId))
        {
            Debug.LogWarning($"Cannot attack - network object {currentSlot.itemNetworkId} not found or network manager unavailable");

            // Clear the invalid slot
            if (IsServer)
            {
                ClearInvalidSlot(currentSlotIndex.Value);
            }
            else
            {
                ClearInvalidSlotServerRpc(currentSlotIndex.Value);
            }
            return;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
        {
            Weapon weapon = itemNetObject.GetComponent<Weapon>();
            if (weapon != null)
            {
                Debug.Log($"Attempting attack with {weapon.weaponName}");
                weapon.Attack();
            }
            else
            {
                Debug.Log("Item in slot is not a weapon");
            }
        }
        else
        {
            Debug.LogWarning($"Could not find network object with ID: {currentSlot.itemNetworkId}");

            // Clear the invalid slot
            if (IsServer)
            {
                ClearInvalidSlot(currentSlotIndex.Value);
            }
            else
            {
                ClearInvalidSlotServerRpc(currentSlotIndex.Value);
            }
        }
    }

    [ServerRpc]
    private void ClearInvalidSlotServerRpc(int slotIndex)
    {
        ClearInvalidSlot(slotIndex);
    }

    private void ClearInvalidSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < inventorySlots.Count)
        {
            inventorySlots[slotIndex] = new InventorySlot
            {
                itemNetworkId = 0,
                isEmpty = true,
                itemName = "Empty"
            };
            Debug.Log($"Cleared invalid slot {slotIndex}");
        }
    }

    private void OnInventoryChanged(NetworkListEvent<InventorySlot> changeEvent)
    {
        if (IsOwner)
        {
            UpdateHeldItemVisuals();
            UpdateInteractionPrompt(); // Update prompt when inventory changes
        }
    }

    private void OnCurrentSlotChanged(int oldValue, int newValue)
    {
        if (IsOwner)
        {
            Debug.Log($"Slot changed from {oldValue} to {newValue}");
            UpdateHeldItemVisuals();
        }
    }

    private void UpdateHeldItemVisualsForSlot(int slotIndex)
    {
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
        }

        if (slotIndex != -1 && !inventorySlots[slotIndex].isEmpty)
        {
            var targetSlot = inventorySlots[slotIndex];

           
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.SpawnManager != null &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetSlot.itemNetworkId, out NetworkObject itemNetObject))
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

                    Debug.Log($"Immediately updated visuals for slot {slotIndex}");
                }
            }
            else
            {
                Debug.LogWarning($"Cannot update visuals - network object {targetSlot.itemNetworkId} not found");
            }
        }
        else
        {
            Debug.Log($"Slot {slotIndex} is empty - no item to show");
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

         
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.SpawnManager != null &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
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

                    Debug.Log($"Updated held item visuals for slot {currentSlotIndex.Value}");
                }
            }
            else
            {
                Debug.LogWarning($"Cannot update held item - network object {currentSlot.itemNetworkId} not found");
            }
        }
        else
        {
            Debug.Log($"Current slot {currentSlotIndex.Value} is empty - no item to show");
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

 
    private void HandleGameEnded()
    {
        gameEnded = true;

        // Clear held item visuals
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
        }

        // Hide interaction prompts
        GameHUDManager.Instance?.HideInteractionPrompt();

        Debug.Log("InventorySystem: Game ended - disabled input and cleared visuals");
    }

    public override void OnNetworkDespawn()
    {
        inventorySlots.OnListChanged -= OnInventoryChanged;
        currentSlotIndex.OnValueChanged -= OnCurrentSlotChanged;

       
        if (EndGameManager.Instance != null)
        {
            EndGameManager.Instance.OnGameEnded -= HandleGameEnded;
        }

        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
        }
    }

    [ContextMenu("Debug Weapon Initialization")]
    private void DebugWeaponInitialization()
    {
        Debug.Log("=== WEAPON INITIALIZATION DEBUG ===");
        Debug.Log($"PlayerHitbox reference: {playerHitbox != null}");
        Debug.Log($"PlayerHealth reference: {GetComponent<PlayerHealth>() != null}");

        if (currentSlotIndex.Value != -1 && !inventorySlots[currentSlotIndex.Value].isEmpty)
        {
            var currentSlot = inventorySlots[currentSlotIndex.Value];
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(currentSlot.itemNetworkId, out NetworkObject itemNetObject))
            {
                Weapon weapon = itemNetObject.GetComponent<Weapon>();
                if (weapon != null)
                {
                    Debug.Log($"Current weapon: {weapon.weaponName}");
                    Debug.Log($"Weapon owner ID: {weapon.ownerId}");
                    Debug.Log($"Weapon hitbox reference: {weapon.playerHitbox != null}");
                }
            }
        }
    }
}