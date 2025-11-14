// PickupableItem.cs
using UnityEngine;
using Unity.Netcode;
using System;

public class PickupableItem : NetworkBehaviour
{
    public enum ItemType
    {
        Generic,
        Consumable,
        Weapon,
        Tool,
        Evidence
    }

    [Header("Item Settings")]
    [SerializeField] private string itemName = "Item";
    [SerializeField] private ItemType itemType = ItemType.Generic;
    [SerializeField] private GameObject heldPrefab;
    [SerializeField] private GameObject worldModel;

    [Header("Visual Settings")]
    [SerializeField] private Vector3 heldPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 heldRotationOffset = Vector3.zero;
    [SerializeField] private float heldScale = 1.0f;

    private NetworkVariable<bool> isPickedUp = new NetworkVariable<bool>(false);
    private Collider itemCollider;

    public string ItemName => itemName;
    public ItemType Type => itemType;
    public GameObject HeldPrefab => heldPrefab;
    public bool CanBePickedUp => !isPickedUp.Value;
    public bool IsPickedUp => isPickedUp.Value;

    public override void OnNetworkSpawn()
    {
        itemCollider = GetComponent<Collider>();

        // Ensure world model is properly set
        if (worldModel == null)
        {
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                worldModel = meshRenderer.gameObject;
            }
        }

        // If still null, use the root object
        if (worldModel == null)
        {
            worldModel = gameObject;
        }

        UpdateVisuals();
        isPickedUp.OnValueChanged += OnPickedUpStateChanged;
    }

    public void PickupItem(ulong pickerUpperId)
    {
        PickupItemServerRpc(pickerUpperId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PickupItemServerRpc(ulong pickerUpperId)
    {
        if (isPickedUp.Value) return;

        isPickedUp.Value = true;
        UpdateVisualsClientRpc();
    }

    public void DropItem(Vector3 dropPosition)
    {
        DropItemServerRpc(dropPosition);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DropItemServerRpc(Vector3 dropPosition)
    {
        if (!isPickedUp.Value) return;

        isPickedUp.Value = false;
        transform.position = dropPosition;

        UpdateVisualsClientRpc();
    }

    [ClientRpc]
    private void UpdateVisualsClientRpc()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        // Show/hide world model with proper mesh visibility
        if (worldModel != null)
        {
            worldModel.SetActive(!isPickedUp.Value);

            // Force enable mesh renderer when active
            if (!isPickedUp.Value)
            {
                MeshRenderer meshRenderer = worldModel.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = true;
                }

                // Also check children
                MeshRenderer[] childRenderers = worldModel.GetComponentsInChildren<MeshRenderer>();
                foreach (MeshRenderer renderer in childRenderers)
                {
                    renderer.enabled = true;
                }
            }
        }

        // Enable/disable collider
        if (itemCollider != null)
        {
            itemCollider.enabled = !isPickedUp.Value;
        }
    }

    private void OnPickedUpStateChanged(bool oldValue, bool newValue)
    {
        UpdateVisuals();
    }

    // Method to configure the held item properly
    public void ConfigureHeldItem(GameObject heldInstance)
    {
        if (heldInstance != null)
        {
            heldInstance.transform.localPosition = heldPositionOffset;
            heldInstance.transform.localRotation = Quaternion.Euler(heldRotationOffset);
            heldInstance.transform.localScale = Vector3.one * heldScale;

            // Ensure all mesh renderers are enabled
            MeshRenderer[] renderers = heldInstance.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                renderer.enabled = true;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        isPickedUp.OnValueChanged -= OnPickedUpStateChanged;
    }
}