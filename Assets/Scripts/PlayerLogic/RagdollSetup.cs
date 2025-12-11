using UnityEngine;

public class RagdollSetup : MonoBehaviour
{
    [Header("Ragdoll Settings")]
    public bool enableRagdollByDefault = false;
    public float ragdollForceMultiplier = 1f;

    private Rigidbody[] rigidbodies;
    private Collider[] colliders;
    private Animator animator;
    private Rigidbody mainRigidbody;
    private Collider mainCollider;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        mainRigidbody = GetComponent<Rigidbody>();
        mainCollider = GetComponent<Collider>();

        // Get all rigidbodies and colliders
        rigidbodies = GetComponentsInChildren<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();

        // Set up ragdoll
        SetupRagdoll();
    }

    private void SetupRagdoll()
    {
        // Disable ragdoll by default
        SetRagdollActive(enableRagdollByDefault);
    }

    public void SetRagdollActive(bool active)
    {
        // Enable/disable animator
        if (animator != null)
        {
            animator.enabled = !active;
        }

        // Enable/disable main rigidbody and collider
        if (mainRigidbody != null)
        {
            mainRigidbody.isKinematic = active;
            mainRigidbody.useGravity = !active;
        }

        if (mainCollider != null)
        {
            mainCollider.enabled = !active;
        }

        // Enable/disable ragdoll rigidbodies and colliders
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb != mainRigidbody) // Skip main rigidbody
            {
                rb.isKinematic = !active;
                rb.useGravity = active;
                rb.detectCollisions = active;
            }
        }

        foreach (Collider col in colliders)
        {
            if (col != mainCollider) // Skip main collider
            {
                col.enabled = active;
            }
        }
    }

    public void ApplyForce(Vector3 force, ForceMode forceMode = ForceMode.Impulse)
    {
        if (mainRigidbody != null)
        {
            mainRigidbody.AddForce(force * ragdollForceMultiplier, forceMode);
        }
    }

    public void ApplyForceAtPosition(Vector3 force, Vector3 position, ForceMode forceMode = ForceMode.Impulse)
    {
        if (mainRigidbody != null)
        {
            mainRigidbody.AddForceAtPosition(force * ragdollForceMultiplier, position, forceMode);
        }
    }
}