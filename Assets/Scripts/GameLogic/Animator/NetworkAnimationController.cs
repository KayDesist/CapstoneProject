using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkAnimationController : NetworkBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float smoothTime = 0.1f;
    [SerializeField] private Animator animator;

    [Header("Component References")]
    [SerializeField] private NetworkPlayerController playerController;
    [SerializeField] private PlayerHealth playerHealth;

    // Network variables for animation synchronization
    private NetworkVariable<float> networkSpeed = new NetworkVariable<float>(0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private NetworkVariable<bool> networkIsSprinting = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private NetworkVariable<bool> networkIsPerformingTask = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private NetworkVariable<bool> networkAttackTrigger = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    // Local animation state
    private Vector3 lastPosition;
    private float currentVelocity;
    private bool isInitialized = false;

    private void Awake()
    {
        // Get references if not set
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerController == null)
            playerController = GetComponent<NetworkPlayerController>();

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        lastPosition = transform.position;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Owner sends animation data
            isInitialized = true;
            Debug.Log("NetworkAnimationController: Owner initialized");
        }
        else
        {
            // Non-owners receive animation data
            isInitialized = true;

            // Subscribe to network variable changes
            networkSpeed.OnValueChanged += OnSpeedChanged;
            networkIsSprinting.OnValueChanged += OnSprintingChanged;
            networkIsPerformingTask.OnValueChanged += OnTaskChanged;
            networkAttackTrigger.OnValueChanged += OnAttackTriggered;

            Debug.Log("NetworkAnimationController: Client initialized");
        }
    }

    private void Update()
    {
        if (!isInitialized || animator == null) return;

        if (IsOwner)
        {
            UpdateOwnerAnimations();
        }
        else
        {
            UpdateAnimatorFromNetwork();
        }
    }

    private void UpdateOwnerAnimations()
    {
        if (animator == null || playerController == null) return;

        // Check if player is alive
        if (playerHealth != null && !playerHealth.IsAlive())
        {
            // Player is dead, stop animations
            networkSpeed.Value = 0f;
            networkIsSprinting.Value = false;
            networkIsPerformingTask.Value = false;
            return;
        }

        // Calculate speed based on movement
        Vector3 currentPosition = transform.position;
        float distance = Vector3.Distance(currentPosition, lastPosition);
        float speed = distance / Time.deltaTime;

        // Smooth speed value
        float smoothedSpeed = Mathf.SmoothDamp(animator.GetFloat("Speed"), speed, ref currentVelocity, smoothTime);

        // Normalize speed
        float normalizedSpeed = Mathf.Clamp01(smoothedSpeed / playerController.walkSpeed);

        // Update network variables
        if (Mathf.Abs(networkSpeed.Value - normalizedSpeed) > 0.05f)
        {
            networkSpeed.Value = normalizedSpeed;
        }

        bool isSprinting = playerController.IsSprinting();
        if (networkIsSprinting.Value != isSprinting)
        {
            networkIsSprinting.Value = isSprinting;
        }

        // Update local animator
        animator.SetFloat("Speed", normalizedSpeed);
        animator.SetBool("IsSprinting", isSprinting);
        animator.SetBool("IsPerformingTask", networkIsPerformingTask.Value);

        lastPosition = currentPosition;
    }

    private void UpdateAnimatorFromNetwork()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", networkSpeed.Value);
        animator.SetBool("IsSprinting", networkIsSprinting.Value);
        animator.SetBool("IsPerformingTask", networkIsPerformingTask.Value);
    }

    // ============ EVENT HANDLERS ============

    private void OnSpeedChanged(float oldValue, float newValue)
    {
        if (!IsOwner && animator != null)
        {
            animator.SetFloat("Speed", newValue);
        }
    }

    private void OnSprintingChanged(bool oldValue, bool newValue)
    {
        if (!IsOwner && animator != null)
        {
            animator.SetBool("IsSprinting", newValue);
        }
    }

    private void OnTaskChanged(bool oldValue, bool newValue)
    {
        if (!IsOwner && animator != null)
        {
            animator.SetBool("IsPerformingTask", newValue);
        }
    }

    private void OnAttackTriggered(bool oldValue, bool newValue)
    {
        if (newValue && !IsOwner && animator != null)
        {
            animator.SetTrigger("Attack");
            // Reset on server
            if (IsServer)
            {
                StartCoroutine(ResetAttackTrigger());
            }
        }
    }

    private IEnumerator ResetAttackTrigger()
    {
        yield return new WaitForSeconds(0.1f);
        if (networkAttackTrigger.Value)
        {
            networkAttackTrigger.Value = false;
        }
    }

    // ============ PUBLIC METHODS ============

    public void SetPerformingTask(bool isPerforming)
    {
        if (!IsOwner || (playerHealth != null && !playerHealth.IsAlive())) return;

        networkIsPerformingTask.Value = isPerforming;
        if (animator != null)
        {
            animator.SetBool("IsPerformingTask", isPerforming);
        }
    }

    public void TriggerAttack()
    {
        if (!IsOwner || (playerHealth != null && !playerHealth.IsAlive())) return;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
        networkAttackTrigger.Value = true;

        // Reset after a moment
        StartCoroutine(ResetAttackTrigger());
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (!IsOwner && isInitialized)
        {
            networkSpeed.OnValueChanged -= OnSpeedChanged;
            networkIsSprinting.OnValueChanged -= OnSprintingChanged;
            networkIsPerformingTask.OnValueChanged -= OnTaskChanged;
            networkAttackTrigger.OnValueChanged -= OnAttackTriggered;
        }
    }
}