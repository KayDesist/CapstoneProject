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

    private NetworkVariable<bool> networkIsDead = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Vector3 lastPosition;
    private float currentVelocity;
    private bool isInitialized = false;
    private bool deathAnimationTriggered = false;

    private void Awake()
    {
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
            isInitialized = true;
        }
        else
        {
            isInitialized = true;

            networkSpeed.OnValueChanged += OnSpeedChanged;
            networkIsSprinting.OnValueChanged += OnSprintingChanged;
            networkIsPerformingTask.OnValueChanged += OnTaskChanged;
            networkAttackTrigger.OnValueChanged += OnAttackTriggered;
            networkIsDead.OnValueChanged += OnDeathStateChanged;
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

    // Update animations for owner
    private void UpdateOwnerAnimations()
    {
        if (animator == null || playerController == null || playerHealth == null) return;

        bool isDead = networkIsDead.Value || (playerHealth != null && !playerHealth.IsAlive());

        if (isDead)
        {
            if (!deathAnimationTriggered)
            {
                TriggerDeath();
                deathAnimationTriggered = true;
            }

            networkSpeed.Value = 0f;
            networkIsSprinting.Value = false;
            networkIsPerformingTask.Value = false;
            return;
        }

        deathAnimationTriggered = false;

        Vector3 currentPosition = transform.position;
        float distance = Vector3.Distance(currentPosition, lastPosition);
        float speed = distance / Time.deltaTime;

        float smoothedSpeed = Mathf.SmoothDamp(animator.GetFloat("Speed"), speed, ref currentVelocity, smoothTime);
        float normalizedSpeed = Mathf.Clamp01(smoothedSpeed / playerController.walkSpeed);

        if (Mathf.Abs(networkSpeed.Value - normalizedSpeed) > 0.05f)
        {
            networkSpeed.Value = normalizedSpeed;
        }

        bool isSprinting = playerController.IsSprinting();
        if (networkIsSprinting.Value != isSprinting)
        {
            networkIsSprinting.Value = isSprinting;
        }

        animator.SetFloat("Speed", normalizedSpeed);
        animator.SetBool("IsSprinting", isSprinting);
        animator.SetBool("IsPerformingTask", networkIsPerformingTask.Value);

        lastPosition = currentPosition;
    }

    // Update animations from network variables
    private void UpdateAnimatorFromNetwork()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", networkSpeed.Value);
        animator.SetBool("IsSprinting", networkIsSprinting.Value);
        animator.SetBool("IsPerformingTask", networkIsPerformingTask.Value);
    }

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
            if (IsServer)
            {
                StartCoroutine(ResetAttackTrigger());
            }
        }
    }

    private void OnDeathStateChanged(bool oldValue, bool newValue)
    {
        if (newValue && animator != null)
        {
            TriggerDeath();
        }
        else if (!newValue && animator != null)
        {
            ResetDeathState();
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

    // Trigger death animation
    public void TriggerDeath()
    {
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        if (IsServer)
        {
            networkIsDead.Value = true;
        }
    }

    // Reset death state
    public void ResetDeathState()
    {
        if (animator != null)
        {
            animator.ResetTrigger("Die");
            animator.SetBool("IsDead", false);

            if (animator.isActiveAndEnabled)
            {
                animator.Play("Idle", 0, 0f);
            }
        }

        if (IsServer)
        {
            networkIsDead.Value = false;
        }

        deathAnimationTriggered = false;
    }

    // Set task performance state
    public void SetPerformingTask(bool isPerforming)
    {
        if (!IsOwner || (playerHealth != null && !playerHealth.IsAlive())) return;

        networkIsPerformingTask.Value = isPerforming;
        if (animator != null)
        {
            animator.SetBool("IsPerformingTask", isPerforming);
        }
    }

    // Trigger attack animation
    public void TriggerAttack()
    {
        if (!IsOwner || (playerHealth != null && !playerHealth.IsAlive())) return;

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
        networkAttackTrigger.Value = true;

        StartCoroutine(ResetAttackTrigger());
    }

    // Set death state
    public void SetDeathState(bool isDead)
    {
        if (IsServer)
        {
            networkIsDead.Value = isDead;
        }
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
            networkIsDead.OnValueChanged -= OnDeathStateChanged;
        }
    }
}