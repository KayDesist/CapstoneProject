using UnityEngine;
using Unity.Netcode;

public class InteractableObject : NetworkBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] protected string interactionText = "Press E to interact";
    [SerializeField] protected float interactionTime = 3f;
    [SerializeField] protected bool requiresSurvivor = true;

    protected bool isInteractable = true;
    protected bool isBeingInteracted = false;

    public virtual void Interact(PlayerController player)
    {
        if (!isInteractable) return;

        // Check role requirements
        if (requiresSurvivor && player.GetRole() != GameManager.PlayerRole.Survivor)
        {
            if (player.IsOwner)
            {
                InGameHUD.Instance.ShowNotification("Only survivors can interact with this");
            }
            return;
        }

        // Start interaction
        if (player.IsOwner)
        {
            StartInteractionServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    protected virtual void StartInteractionServerRpc()
    {
        if (!isInteractable) return;

        isBeingInteracted = true;
        BeginInteractionClientRpc();
    }

    [ClientRpc]
    protected virtual void BeginInteractionClientRpc()
    {
        // Show progress bar, play animation, etc.
        Debug.Log($"Started interacting with {gameObject.name}");

        // For now, complete immediately
        CompleteInteraction();
    }

    protected virtual void CompleteInteraction()
    {
        if (IsServer)
        {
            // Notify GameManager if this is a survivor task
            if (requiresSurvivor)
            {
                GameManager.Instance.CompleteTaskServerRpc();
            }

            InteractionCompletedClientRpc();
        }
    }

    [ClientRpc]
    protected virtual void InteractionCompletedClientRpc()
    {
        Debug.Log($"Completed interaction with {gameObject.name}");
        isInteractable = false;
        isBeingInteracted = false;

        // Visual feedback
        // You can add particle effects, sound, etc.
    }
}