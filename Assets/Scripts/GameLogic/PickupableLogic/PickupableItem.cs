using UnityEngine;
using Unity.Netcode;
using System;

public class PickupableItem : NetworkBehaviour
{
    public enum ItemType
    {
        Generic,
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

    // Virtual use method
    public virtual void Use(ulong userClientId)
    {
    }

    // Called when object spawns on network
    public override void OnNetworkSpawn()
    {
        itemCollider = GetComponent<Collider>();

        if (worldModel == null)
        {
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                worldModel = meshRenderer.gameObject;
            }
        }

        if (worldModel == null)
        {
            worldModel = gameObject;
        }

        UpdateVisuals();
        isPickedUp.OnValueChanged += OnPickedUpStateChanged;
    }

    // Picks up item
    public void PickupItem(ulong pickerUpperId)
    {
        PickupItemServerRpc(pickerUpperId);
    }

    // Server RPC to pickup item
    [ServerRpc(RequireOwnership = false)]
    public void PickupItemServerRpc(ulong pickerUpperId)
    {
        if (isPickedUp.Value) return;

        isPickedUp.Value = true;
        UpdateVisualsClientRpc();
    }

    // Drops item (backward compatibility)
    public void DropItem(Vector3 dropPosition)
    {
        DropItem(dropPosition, Vector3.forward);
    }

    // Drops item with direction
    public void DropItem(Vector3 dropPosition, Vector3 forwardDirection)
    {
        DropItemServerRpc(dropPosition, forwardDirection);
    }

    // Server RPC to drop item
    [ServerRpc(RequireOwnership = false)]
    public void DropItemServerRpc(Vector3 dropPosition, Vector3 forwardDirection)
    {
        if (!isPickedUp.Value) return;

        isPickedUp.Value = false;

        Vector3 finalDropPosition = dropPosition + forwardDirection * 1.5f + Vector3.up * 0.5f;
        transform.position = finalDropPosition;

        UpdateDropPositionClientRpc(finalDropPosition);
    }

    // Client RPC to update drop position
    [ClientRpc]
    private void UpdateDropPositionClientRpc(Vector3 newPosition)
    {
        transform.position = newPosition;
        UpdateVisuals();
    }

    // Client RPC to update visuals
    [ClientRpc]
    private void UpdateVisualsClientRpc()
    {
        UpdateVisuals();
    }

    // Updates item visuals
    private void UpdateVisuals()
    {
        if (worldModel != null)
        {
            worldModel.SetActive(!isPickedUp.Value);

            if (!isPickedUp.Value)
            {
                MeshRenderer meshRenderer = worldModel.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = true;
                }

                MeshRenderer[] childRenderers = worldModel.GetComponentsInChildren<MeshRenderer>();
                foreach (MeshRenderer renderer in childRenderers)
                {
                    renderer.enabled = true;
                }
            }
        }

        if (itemCollider != null)
        {
            itemCollider.enabled = !isPickedUp.Value;
        }
    }

    // Called when picked up state changes
    private void OnPickedUpStateChanged(bool oldValue, bool newValue)
    {
        UpdateVisuals();
    }

    // Configures held item
    public void ConfigureHeldItem(GameObject heldInstance)
    {
        if (heldInstance != null)
        {
            heldInstance.transform.localPosition = heldPositionOffset;
            heldInstance.transform.localRotation = Quaternion.Euler(heldRotationOffset);
            heldInstance.transform.localScale = Vector3.one * heldScale;

            MeshRenderer[] renderers = heldInstance.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                renderer.enabled = true;
            }
        }
    }

    // Called when object despawns from network
    public override void OnNetworkDespawn()
    {
        isPickedUp.OnValueChanged -= OnPickedUpStateChanged;
    }
}