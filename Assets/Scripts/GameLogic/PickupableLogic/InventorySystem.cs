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

    [Header("Audio")]
    public AudioClip pickupSound;
    public AudioClip dropSound;
    private AudioSource audioSource;

    private NetworkList<InventorySlot> inventorySlots;
    private NetworkVariable<int> currentSlotIndex = new NetworkVariable<int>(0);

    private GameObject currentHeldItem;
    private PickupableItem itemInRange;

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

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (playerHitbox == null)
            playerHitbox = GetComponentInChildren<PlayerHitboxDamage>(true);
    }

    // Called when object spawns on network
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitializeEmptySlots();
        }

        if (playerHitbox == null)
        {
            playerHitbox = GetComponentInChildren<PlayerHitboxDamage>(true);
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

    // Initializes empty inventory slots
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

    // Main update loop
    private void Update()
    {
        if (!IsOwner || gameEnded) return;

        HandleInput();
        CheckForItemsInRange();
    }

    // Checks for items in pickup range
    private void CheckForItemsInRange()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 3f);
        PickupableItem nearestItem = null;
        float nearestDistance = float.MaxValue;

        foreach (var collider in hitColliders)
        {
            PickupableItem item = collider.GetComponent<PickupableItem>();
            if (item != null && item.CanBePickedUp && !item.IsPickedUp)
            {
                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestItem = item;
                }
            }
        }

        if (itemInRange != nearestItem)
        {
            itemInRange = nearestItem;
            UpdateInteractionPrompt();
        }
    }

    // Handles player input
    private void HandleInput()
    {
        if (gameEnded) return;

        for (int i = 0; i < 3; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < inventorySlots.Count)
            {
                int newSlotIndex = i;
                if (newSlotIndex >= 0 && newSlotIndex < inventorySlots.Count)
                {
                    UpdateHeldItemVisualsForSlot(newSlotIndex);
                }
                SwitchToSlotServerRpc(newSlotIndex);
            }
        }

        if (Input.GetKeyDown(pickupKey) && itemInRange != null && HasEmptySlots())
        {
            if (audioSource != null && pickupSound != null && IsOwner)
            {
                audioSource.PlayOneShot(pickupSound);
            }

            PickupItemServerRpc(itemInRange.NetworkObjectId);
        }

        if (Input.GetKeyDown(dropKey) && currentSlotIndex.Value != -1)
        {
            if (audioSource != null && dropSound != null && IsOwner)
            {
                audioSource.PlayOneShot(dropSound);
            }

            DropCurrentItemServerRpc(transform.position, transform.forward);
        }

        if (Input.GetMouseButtonDown(0))
        {
            attackInput = true;
        }

        if (Input.GetMouseButtonDown(1))
        {
            useInput = true;
        }
    }

    // Fixed update for physics-based actions
    private void FixedUpdate()
    {
        if (gameEnded) return;

        if (attackInput)
        {
            HandleWeaponAttack();
            attackInput = false;
        }

        if (useInput)
        {
            useInput = false;
        }
    }

    // Trigger enter handler
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

    // Trigger exit handler
    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner || gameEnded) return;

        PickupableItem item = other.GetComponent<PickupableItem>();
        if (item != null && item == itemInRange)
        {
            itemInRange = null;
            UpdateInteractionPrompt();
        }
    }

    // Updates interaction prompt UI
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

    // Server RPC to switch inventory slot
    [ServerRpc]
    private void SwitchToSlotServerRpc(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < inventorySlots.Count)
        {
            currentSlotIndex.Value = slotIndex;
        }
    }

    // Server RPC to pickup item
    [ServerRpc]
    private void PickupItemServerRpc(ulong itemId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(itemId))
        {
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

                Weapon weapon = itemNetObject.GetComponent<Weapon>();
                if (weapon != null)
                {
                    PlayerHealth health = GetComponent<PlayerHealth>();

                    if (playerHitbox == null)
                    {
                        playerHitbox = GetComponentInChildren<PlayerHitboxDamage>(true);
                    }

                    weapon.Initialize(OwnerClientId, health, playerHitbox);
                }

                UpdateHeldItemVisualsClientRpc();
                PlayPickupSoundClientRpc();
                return;
            }
        }
    }

    // Client RPC to play pickup sound
    [ClientRpc]
    private void PlayPickupSoundClientRpc()
    {
        if (!IsOwner && audioSource != null && pickupSound != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }
    }

    // Client RPC to update held item visuals
    [ClientRpc]
    private void UpdateHeldItemVisualsClientRpc()
    {
        if (IsOwner)
        {
            UpdateHeldItemVisuals();
            UpdateInteractionPrompt();
        }
    }

    // Server RPC to drop current item
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

        inventorySlots[currentSlotIndex.Value] = new InventorySlot
        {
            itemNetworkId = 0,
            isEmpty = true,
            itemName = "Empty"
        };

        UpdateHeldItemVisualsClientRpc();
        PlayDropSoundClientRpc();
    }

    // Client RPC to play drop sound
    [ClientRpc]
    private void PlayDropSoundClientRpc()
    {
        if (!IsOwner && audioSource != null && dropSound != null)
        {
            audioSource.PlayOneShot(dropSound);
        }
    }

    // Handles weapon attack
    private void HandleWeaponAttack()
    {
        if (currentSlotIndex.Value == -1)
        {
            return;
        }

        var currentSlot = inventorySlots[currentSlotIndex.Value];
        if (currentSlot.isEmpty)
        {
            return;
        }

        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.SpawnManager == null ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(currentSlot.itemNetworkId))
        {
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
                weapon.Attack();
            }
        }
        else
        {
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

    // Server RPC to clear invalid slot
    [ServerRpc]
    private void ClearInvalidSlotServerRpc(int slotIndex)
    {
        ClearInvalidSlot(slotIndex);
    }

    // Clears invalid inventory slot
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
        }
    }

    // Called when inventory changes
    private void OnInventoryChanged(NetworkListEvent<InventorySlot> changeEvent)
    {
        if (IsOwner)
        {
            UpdateHeldItemVisuals();
            UpdateInteractionPrompt();
        }
    }

    // Called when current slot changes
    private void OnCurrentSlotChanged(int oldValue, int newValue)
    {
        if (IsOwner)
        {
            UpdateHeldItemVisuals();
        }
    }

    // Updates held item visuals for specific slot
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

                    Renderer[] allRenderers = currentHeldItem.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer renderer in allRenderers)
                    {
                        renderer.enabled = true;
                    }

                    currentHeldItem.SetActive(true);
                    item.ConfigureHeldItem(currentHeldItem);

                    Weapon weapon = itemNetObject.GetComponent<Weapon>();
                    if (weapon != null)
                    {
                        weapon.OnEquipped();
                    }
                }
            }
        }
    }

    // Updates held item visuals
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

                    Renderer[] allRenderers = currentHeldItem.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer renderer in allRenderers)
                    {
                        renderer.enabled = true;
                    }

                    currentHeldItem.SetActive(true);
                    item.ConfigureHeldItem(currentHeldItem);

                    Weapon weapon = itemNetObject.GetComponent<Weapon>();
                    if (weapon != null)
                    {
                        weapon.OnEquipped();
                    }
                }
            }
        }
    }

    // Checks if inventory has empty slots
    private bool HasEmptySlots()
    {
        foreach (var slot in inventorySlots)
        {
            if (slot.isEmpty) return true;
        }
        return false;
    }

    // Handles game ended event
    private void HandleGameEnded()
    {
        gameEnded = true;

        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
            currentHeldItem = null;
        }

        GameHUDManager.Instance?.HideInteractionPrompt();
    }

    // Called when object despawns from network
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
}