// PickupableItem.cs
using UnityEngine;
using Unity.Netcode;

public class PickupableItem : NetworkBehaviour
{
    [Header("Item Settings")]
    [SerializeField] private string itemName = "Item";
    [SerializeField] private GameObject heldPrefab;
    [SerializeField] private GameObject worldModel;

    private NetworkVariable<bool> isPickedUp = new NetworkVariable<bool>(false);
    private Collider itemCollider;

    public string ItemName => itemName;
    public GameObject HeldPrefab => heldPrefab;
    public bool CanBePickedUp => !isPickedUp.Value;
    public bool IsPickedUp => isPickedUp.Value;

    public override void OnNetworkSpawn()
    {
        itemCollider = GetComponent<Collider>();

        // Auto-find world model if not assigned
        if (worldModel == null)
        {
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                worldModel = meshRenderer.gameObject;
            }
            else
            {
                worldModel = gameObject;
            }
        }

        UpdateVisuals();
        isPickedUp.OnValueChanged += OnPickedUpStateChanged;

        Debug.Log($"PickupableItem {itemName} spawned");
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
        Debug.Log($"{itemName} picked up by {pickerUpperId}");
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

        Debug.Log($"{itemName} dropped at {dropPosition}");
        UpdateVisualsClientRpc();
    }

    [ClientRpc]
    private void UpdateVisualsClientRpc()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        // Show/hide world model
        if (worldModel != null)
        {
            worldModel.SetActive(!isPickedUp.Value);
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

    public override void OnNetworkDespawn()
    {
        isPickedUp.OnValueChanged -= OnPickedUpStateChanged;
    }
}