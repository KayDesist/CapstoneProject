using UnityEngine;
using Unity.Netcode;

public class InteractableObject : NetworkBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private string interactionText = "Press E to interact";
    [SerializeField] private float interactionTime = 3f;
    [SerializeField] private bool requiresSurvivor = true;
    [SerializeField] private int taskIndex = 0;
    [SerializeField] private bool isSurvivorTask = true;
    [SerializeField] private int progressToAdd = 1;

    [Header("Visual Feedback")]
    [SerializeField] private Material incompleteMaterial;
    [SerializeField] private Material completeMaterial;
    private MeshRenderer meshRenderer;

    [Header("Animation Settings")]
    [SerializeField] private bool triggerTaskAnimation = true;

    private NetworkVariable<bool> isInteractable = new NetworkVariable<bool>(true);
    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(false);
    private NetworkVariable<float> interactionProgress = new NetworkVariable<float>(0f);

    private bool playerInRange = false;
    private bool isInteracting = false;
    private float localInteractionProgress = 0f;

    // Reference to current interacting player's animator
    private NetworkCharacterAnimator currentPlayerAnimator;

    public override void OnNetworkSpawn()
    {
        // Initialize visual feedback
        meshRenderer = GetComponent<MeshRenderer>();
        UpdateVisuals();

        // Subscribe to network variable changes
        isCompleted.OnValueChanged += OnCompletedChanged;
        interactionProgress.OnValueChanged += OnProgressChanged;

        // Initialize completed state
        if (isCompleted.Value)
        {
            UpdateVisuals();
        }
    }

    private void Update()
    {
        // Only handle input for local player
        if (!IsClient) return;

        // Check if player can interact at all (role check)
        if (!CanInteract(NetworkManager.Singleton.LocalClientId))
        {
            // If they can't interact due to role, don't process any input
            if (isInteracting)
            {
                CancelInteraction();
            }
            return;
        }

        // If player can interact, handle the input
        if (playerInRange && Input.GetKey(KeyCode.E) && isInteractable.Value && !isCompleted.Value)
        {
            if (!isInteracting)
            {
                // Start interaction
                StartInteraction();
            }

            // Continue interaction
            localInteractionProgress += Time.deltaTime;

            // Update progress UI
            if (GameHUDManager.Instance != null)
            {
                GameHUDManager.Instance.ShowInteractionProgress(localInteractionProgress, interactionTime);
            }

            // Update server with progress
            if (IsServer)
            {
                interactionProgress.Value = localInteractionProgress;
            }
            else
            {
                UpdateInteractionProgressServerRpc(localInteractionProgress);
            }

            // Check if interaction is complete
            if (localInteractionProgress >= interactionTime)
            {
                CompleteInteraction();
            }
        }
        else if (isInteracting && (Input.GetKeyUp(KeyCode.E) || !playerInRange))
        {
            // Cancel interaction if E key is released or player moves away
            CancelInteraction();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsClient) return;

        // Check if it's a player and if they are the local player
        NetworkPlayerController player = other.GetComponent<NetworkPlayerController>();
        if (player != null && player.IsOwner)
        {
            playerInRange = true;

            // Check if player can interact based on role
            if (CanInteract(NetworkManager.Singleton.LocalClientId))
            {
                if (GameHUDManager.Instance != null)
                {
                    GameHUDManager.Instance.ShowInteractionPrompt(interactionText);
                }
                Debug.Log("Player entered interaction zone - can interact");
            }
            else
            {
                if (GameHUDManager.Instance != null)
                {
                    GameHUDManager.Instance.ShowInteractionPrompt("You cannot interact with this");
                }
                Debug.Log("Player entered interaction zone - cannot interact (wrong role)");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsClient) return;

        NetworkPlayerController player = other.GetComponent<NetworkPlayerController>();
        if (player != null && player.IsOwner)
        {
            playerInRange = false;
            CancelInteraction();

            if (GameHUDManager.Instance != null)
            {
                GameHUDManager.Instance.HideInteractionPrompt();
                GameHUDManager.Instance.HideInteractionProgress();
            }
            Debug.Log("Player left interaction zone");
        }
    }

    private void StartInteraction()
    {
        if (!CanInteract(NetworkManager.Singleton.LocalClientId))
        {
            Debug.Log("Cannot start interaction - role check failed");
            return;
        }

        isInteracting = true;
        localInteractionProgress = 0f;

        // Get player's animator for task animation
        NetworkPlayerController playerController = FindLocalPlayerController();
        if (playerController != null && triggerTaskAnimation)
        {
            currentPlayerAnimator = playerController.GetComponent<NetworkCharacterAnimator>();
            if (currentPlayerAnimator != null)
            {
                currentPlayerAnimator.PlayTaskAnimation();
            }
        }

        Debug.Log($"Local player started interacting with {gameObject.name}");

        // Notify server
        if (!IsServer)
        {
            StartInteractionServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }

    private NetworkPlayerController FindLocalPlayerController()
    {
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (NetworkPlayerController player in allPlayers)
        {
            if (player.IsOwner)
            {
                return player;
            }
        }
        return null;
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartInteractionServerRpc(ulong clientId)
    {
        // Server-side role validation - this should never be called if client check worked, but just in case
        if (!CanInteract(clientId))
        {
            Debug.Log($"Server rejected interaction start from client {clientId} - role check failed");
            return;
        }

        Debug.Log($"Server approved interaction start from client {clientId} with {gameObject.name}");
        interactionProgress.Value = 0f;
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateInteractionProgressServerRpc(float progress)
    {
        interactionProgress.Value = progress;
    }

    private void CompleteInteraction()
    {
        Debug.Log($"Local interaction completed with {gameObject.name}");

        // Stop task animation
        if (currentPlayerAnimator != null)
        {
            currentPlayerAnimator.StopTaskAnimation();
        }

        if (IsServer)
        {
            CompleteInteractionServerRpc(NetworkManager.Singleton.LocalClientId);
        }
        else
        {
            CompleteInteractionServerRpc(NetworkManager.Singleton.LocalClientId);
        }

        // Reset local state
        isInteracting = false;
        localInteractionProgress = 0f;
        currentPlayerAnimator = null;

        // Hide UI
        if (GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.HideInteractionPrompt();
            GameHUDManager.Instance.HideInteractionProgress();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CompleteInteractionServerRpc(ulong clientId)
    {
        if (isCompleted.Value) return;

        // Final server-side role validation
        if (!CanInteract(clientId))
        {
            Debug.Log($"Server rejected completion from client {clientId} - role check failed");
            return;
        }

        // Update task progress
        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.UpdateTaskProgressServerRpc(taskIndex, isSurvivorTask, progressToAdd);
        }

        // Mark as completed
        isCompleted.Value = true;
        isInteractable.Value = false;

        // Update visuals
        UpdateVisuals();

        Debug.Log($"Server confirmed completion from client {clientId} for {gameObject.name}");
    }

    private void CancelInteraction()
    {
        if (isInteracting)
        {
            Debug.Log("Interaction cancelled");

            // Stop task animation
            if (currentPlayerAnimator != null)
            {
                currentPlayerAnimator.StopTaskAnimation();
            }

            isInteracting = false;
            localInteractionProgress = 0f;
            currentPlayerAnimator = null;

            // Reset progress on server
            if (IsServer)
            {
                interactionProgress.Value = 0f;
            }
            else
            {
                CancelInteractionServerRpc();
            }

            // Hide progress UI
            if (GameHUDManager.Instance != null)
            {
                GameHUDManager.Instance.HideInteractionProgress();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CancelInteractionServerRpc()
    {
        interactionProgress.Value = 0f;
    }

    private bool CanInteract(ulong clientId)
    {
        if (!isInteractable.Value || isCompleted.Value)
            return false;

        // Role check - this is the key fix!
        if (RoleManager.Instance != null)
        {
            var playerRole = RoleManager.Instance.GetPlayerRole(clientId);
            Debug.Log($"CanInteract check - Client {clientId} has role {playerRole}, requiresSurvivor: {requiresSurvivor}, isSurvivorTask: {isSurvivorTask}");

            // If this requires a survivor and player is not survivor, cannot interact
            if (requiresSurvivor && playerRole != RoleManager.PlayerRole.Survivor)
            {
                Debug.Log($"Cannot interact: requires survivor but player is {playerRole}");
                return false;
            }

            // Additional safety: if this is a survivor task and player is cultist, cannot interact
            if (isSurvivorTask && playerRole == RoleManager.PlayerRole.Cultist)
            {
                Debug.Log($"Cannot interact: is survivor task but player is cultist");
                return false;
            }

            // Additional safety: if this is NOT a survivor task and player is survivor, cannot interact
            if (!isSurvivorTask && playerRole == RoleManager.PlayerRole.Survivor)
            {
                Debug.Log($"Cannot interact: is cultist task but player is survivor");
                return false;
            }
        }
        else
        {
            Debug.Log("CanInteract: RoleManager instance is null");
            return false; // Be safe - if no RoleManager, don't allow interaction
        }

        return true;
    }

    private void OnCompletedChanged(bool oldValue, bool newValue)
    {
        UpdateVisuals();

        if (newValue)
        {
            Debug.Log($"{gameObject.name} interaction completed!");
        }
    }

    private void OnProgressChanged(float oldValue, float newValue)
    {
        // Show progress in console for debugging
        if (isInteracting)
        {
            Debug.Log($"Interaction progress: {newValue:F1}/{interactionTime:F1}");
        }
    }

    private void UpdateVisuals()
    {
        if (meshRenderer != null)
        {
            if (isCompleted.Value && completeMaterial != null)
            {
                meshRenderer.material = completeMaterial;
            }
            else if (!isCompleted.Value && incompleteMaterial != null)
            {
                meshRenderer.material = incompleteMaterial;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        isCompleted.OnValueChanged -= OnCompletedChanged;
        interactionProgress.OnValueChanged -= OnProgressChanged;
    }

    // Visualize interaction zone in editor
    private void OnDrawGizmos()
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            Gizmos.color = playerInRange ? Color.green : (isInteractable.Value ? Color.yellow : Color.gray);
            if (collider is BoxCollider boxCollider)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            }
        }
    }
}