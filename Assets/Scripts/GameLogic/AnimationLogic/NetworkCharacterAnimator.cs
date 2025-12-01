using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;

public class NetworkCharacterAnimator : NetworkBehaviour
{
    [Header("Animator References")]
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private NetworkAnimator networkAnimator;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeedThreshold = 0.1f;
    [SerializeField] private float animationSmoothTime = 0.1f;

    [Header("Animation Parameter Names")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string taskTrigger = "DoTask";
    [SerializeField] private string dieTrigger = "Die";
    [SerializeField] private string isDeadParam = "IsDead";
    [SerializeField] private string isDoingTaskParam = "IsDoingTask";

    // Components
    private Rigidbody rb;
    private NetworkPlayerController playerController;

    // Animation state
    private float currentSpeed = 0f;
    private bool isPerformingTask = false;
    private bool isDead = false;
    private Vector3 lastPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerController = GetComponent<NetworkPlayerController>();

        // Get animator if not assigned
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>();

        if (networkAnimator == null)
            networkAnimator = GetComponent<NetworkAnimator>();
    }

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (isDead) return; // Don't update movement if dead

        UpdateMovementAnimation();
    }

    private void UpdateMovementAnimation()
    {
        if (rb == null || characterAnimator == null) return;

        // Calculate speed (ignore vertical velocity)
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        float speed = horizontalVelocity.magnitude;

        // Smooth speed transition
        currentSpeed = Mathf.Lerp(currentSpeed, speed, animationSmoothTime * Time.deltaTime * 10f);

        // Update animator parameters
        characterAnimator.SetFloat(speedParam, currentSpeed);
        characterAnimator.SetBool(isMovingParam, speed > walkSpeedThreshold);

        // Sync across network
        if (IsServer)
        {
            UpdateAnimationParametersServerRpc(currentSpeed, speed > walkSpeedThreshold);
        }
        else
        {
            UpdateAnimationParametersServerRpc(currentSpeed, speed > walkSpeedThreshold);
        }
    }

    // ============ ANIMATION CONTROL METHODS ============

    public void PlayAttackAnimation()
    {
        if (!IsOwner) return;
        if (isDead) return;

        // Play locally first for responsiveness
        if (characterAnimator != null)
        {
            characterAnimator.SetTrigger(attackTrigger);
        }

        // Sync across network
        if (IsServer)
        {
            PlayAttackAnimationClientRpc();
        }
        else
        {
            RequestAttackAnimationServerRpc();
        }

        Debug.Log("Playing attack animation");
    }

    public void PlayTaskAnimation()
    {
        if (!IsOwner) return;
        if (isDead) return;

        if (characterAnimator != null && !isPerformingTask)
        {
            isPerformingTask = true;
            characterAnimator.SetBool(isDoingTaskParam, true);
            characterAnimator.SetTrigger(taskTrigger);
        }
    }

    public void StopTaskAnimation()
    {
        if (!IsOwner) return;

        if (characterAnimator != null && isPerformingTask)
        {
            isPerformingTask = false;
            characterAnimator.SetBool(isDoingTaskParam, false);
        }
    }

    public void PlayDieAnimation()
    {
        if (!IsOwner) return;

        isDead = true;

        // Play die animation
        if (characterAnimator != null)
        {
            characterAnimator.SetTrigger(dieTrigger);
            characterAnimator.SetBool(isDeadParam, true);

            // Stop all movement
            characterAnimator.SetFloat(speedParam, 0f);
            characterAnimator.SetBool(isMovingParam, false);
        }

        // Sync across network
        if (IsServer)
        {
            PlayDieAnimationClientRpc();
        }
        else
        {
            RequestDieAnimationServerRpc();
        }

        Debug.Log("Playing die animation");
    }

    public void Revive()
    {
        if (!IsOwner) return;

        isDead = false;

        if (characterAnimator != null)
        {
            characterAnimator.SetBool(isDeadParam, false);
        }

        // Sync across network
        if (IsServer)
        {
            ReviveClientRpc();
        }
        else
        {
            RequestReviveServerRpc();
        }
    }

    // ============ NETWORK RPC METHODS ============

    [ServerRpc]
    private void UpdateAnimationParametersServerRpc(float speed, bool isMoving)
    {
        UpdateAnimationParametersClientRpc(speed, isMoving);
    }

    [ClientRpc]
    private void UpdateAnimationParametersClientRpc(float speed, bool isMoving)
    {
        if (IsOwner) return; // Owner already has correct values

        if (characterAnimator != null)
        {
            characterAnimator.SetFloat(speedParam, speed);
            characterAnimator.SetBool(isMovingParam, isMoving);
        }
    }

    [ServerRpc]
    private void RequestAttackAnimationServerRpc()
    {
        PlayAttackAnimationClientRpc();
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        if (IsOwner) return; // Owner already played it

        if (characterAnimator != null && !isDead)
        {
            characterAnimator.SetTrigger(attackTrigger);
        }
    }

    [ServerRpc]
    private void RequestDieAnimationServerRpc()
    {
        PlayDieAnimationClientRpc();
    }

    [ClientRpc]
    private void PlayDieAnimationClientRpc()
    {
        if (IsOwner) return;

        if (characterAnimator != null)
        {
            isDead = true;
            characterAnimator.SetTrigger(dieTrigger);
            characterAnimator.SetBool(isDeadParam, true);
            characterAnimator.SetFloat(speedParam, 0f);
            characterAnimator.SetBool(isMovingParam, false);
        }
    }

    [ServerRpc]
    private void RequestReviveServerRpc()
    {
        ReviveClientRpc();
    }

    [ClientRpc]
    private void ReviveClientRpc()
    {
        if (IsOwner) return;

        if (characterAnimator != null)
        {
            isDead = false;
            characterAnimator.SetBool(isDeadParam, false);
        }
    }

    // ============ PUBLIC GETTERS ============

    public bool IsPerformingTask()
    {
        return isPerformingTask;
    }

    public bool IsDead()
    {
        return isDead;
    }

    public bool IsMoving()
    {
        return Vector3.Distance(transform.position, lastPosition) > 0.01f;
    }

    // ============ DEBUG METHODS ============

    [ContextMenu("Test Attack Animation")]
    public void TestAttackAnimation()
    {
        if (IsOwner)
        {
            PlayAttackAnimation();
        }
    }

    [ContextMenu("Test Task Animation")]
    public void TestTaskAnimation()
    {
        if (IsOwner)
        {
            PlayTaskAnimation();
        }
    }

    [ContextMenu("Test Die Animation")]
    public void TestDieAnimation()
    {
        if (IsOwner)
        {
            PlayDieAnimation();
        }
    }

    [ContextMenu("Test Revive")]
    public void TestRevive()
    {
        if (IsOwner)
        {
            Revive();
        }
    }

    private void LateUpdate()
    {
        // Update last position for movement detection
        lastPosition = transform.position;
    }
}