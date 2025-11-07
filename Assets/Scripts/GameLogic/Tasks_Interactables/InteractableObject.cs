using UnityEngine;
using Unity.Netcode;
using TMPro;

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

    [Header("UI Prompt")]
    [SerializeField] private GameObject interactionPromptUI;
    [SerializeField] private TMP_Text interactionTextUI;

    private NetworkVariable<bool> isInteractable = new NetworkVariable<bool>(true);
    private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(false);
    private NetworkVariable<float> interactionProgress = new NetworkVariable<float>(0f);

    private bool playerInRange = false;
    private bool isInteracting = false;
    private float localInteractionProgress = 0f;

    public override void OnNetworkSpawn()
    {
        // Initialize visual feedback
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && incompleteMaterial != null)
        {
            meshRenderer.material = incompleteMaterial;
        }

        // Initialize UI prompt
        if (interactionPromptUI != null)
        {
            interactionPromptUI.SetActive(false);
        }

        if (interactionTextUI != null)
        {
            interactionTextUI.text = interactionText;
        }

        // Subscribe to network variable changes
        isCompleted.OnValueChanged += OnCompletedChanged;
        interactionProgress.OnValueChanged += OnProgressChanged;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Handle interaction input
        if (playerInRange && Input.GetKey(KeyCode.E) && isInteractable.Value && !isCompleted.Value)
        {
            if (!isInteracting)
            {
                // Start interaction
                isInteracting = true;
                StartInteractionServerRpc();
            }

            // Continue interaction
            localInteractionProgress += Time.deltaTime;
            UpdateInteractionProgressServerRpc(localInteractionProgress);

            if (localInteractionProgress >= interactionTime)
            {
                CompleteInteractionServerRpc();
                isInteracting = false;
                localInteractionProgress = 0f;
            }
        }
        else if (isInteracting)
        {
            // Cancel interaction if E key is released or player moves away
            isInteracting = false;
            localInteractionProgress = 0f;
            CancelInteractionServerRpc();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        // Check if it's a player and if they are the local player
        NetworkPlayerController player = other.GetComponent<NetworkPlayerController>();
        if (player != null && player.IsOwner)
        {
            playerInRange = true;

            // Check role requirements
            if (requiresSurvivor && RoleManager.Instance != null)
            {
                var playerRole = RoleManager.Instance.GetLocalPlayerRole();
                if (playerRole != RoleManager.PlayerRole.Survivor)
                {
                    // Cultist can't interact with survivor tasks
                    if (interactionTextUI != null)
                    {
                        interactionTextUI.text = "Only survivors can interact with this";
                    }
                }
            }

            ShowInteractionPromptClientRpc(true);
            Debug.Log("Player entered interaction zone");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;

        NetworkPlayerController player = other.GetComponent<NetworkPlayerController>();
        if (player != null && player.IsOwner)
        {
            playerInRange = false;
            isInteracting = false;
            localInteractionProgress = 0f;
            ShowInteractionPromptClientRpc(false);
            Debug.Log("Player left interaction zone");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartInteractionServerRpc(ServerRpcParams rpcParams = default)
    {
        var clientId = rpcParams.Receive.SenderClientId;

        // Check role requirements on server
        if (requiresSurvivor && RoleManager.Instance != null)
        {
            var playerRole = RoleManager.Instance.GetPlayerRole(clientId);
            if (playerRole != RoleManager.PlayerRole.Survivor)
            {
                Debug.Log($"Client {clientId} cannot interact - requires survivor role");
                return;
            }
        }

        Debug.Log($"Client {clientId} started interacting with {gameObject.name}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateInteractionProgressServerRpc(float progress)
    {
        interactionProgress.Value = progress;
    }

    [ServerRpc(RequireOwnership = false)]
    private void CompleteInteractionServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isCompleted.Value) return;

        // Update task progress
        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.UpdateTaskProgressServerRpc(taskIndex, isSurvivorTask, progressToAdd);
        }

        // Mark as completed
        isCompleted.Value = true;
        isInteractable.Value = false;

        Debug.Log($"Client {rpcParams.Receive.SenderClientId} completed interaction with {gameObject.name}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void CancelInteractionServerRpc()
    {
        interactionProgress.Value = 0f;
    }

    [ClientRpc]
    private void ShowInteractionPromptClientRpc(bool show)
    {
        if (interactionPromptUI != null)
        {
            interactionPromptUI.SetActive(show && !isCompleted.Value);
        }
    }

    private void OnCompletedChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            // Change material to green when completed
            if (meshRenderer != null && completeMaterial != null)
            {
                meshRenderer.material = completeMaterial;
            }

            // Hide UI prompt
            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetActive(false);
            }

            Debug.Log($"{gameObject.name} interaction completed!");
        }
    }

    private void OnProgressChanged(float oldValue, float newValue)
    {
        // Show progress in console for debugging
        if (IsOwner && isInteracting)
        {
            Debug.Log($"Interaction progress: {newValue:F1}/{interactionTime:F1}");
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
            else if (collider is SphereCollider sphereCollider)
            {
                Gizmos.DrawWireSphere(transform.position + sphereCollider.center, sphereCollider.radius);
            }
        }
    }
}