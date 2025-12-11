using UnityEngine;

public class RagdollSetup : MonoBehaviour
{
    public bool enableRagdollByDefault = false;
    public float ragdollForceMultiplier = 1f;
    private Rigidbody[] rigidbodies;
    private Collider[] colliders;
    private Animator animator;
    private Rigidbody mainRigidbody;
    private Collider mainCollider;

    // Initializes on awake
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        mainRigidbody = GetComponent<Rigidbody>();
        mainCollider = GetComponent<Collider>();
        rigidbodies = GetComponentsInChildren<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        SetupRagdoll();
    }

    // Sets up ragdoll components
    private void SetupRagdoll()
    {
        SetRagdollActive(enableRagdollByDefault);
    }

    // Sets ragdoll active state
    public void SetRagdollActive(bool active)
    {
        if (animator != null)
            animator.enabled = !active;
        if (mainRigidbody != null)
        {
            mainRigidbody.isKinematic = active;
            mainRigidbody.useGravity = !active;
        }
        if (mainCollider != null)
            mainCollider.enabled = !active;
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb != mainRigidbody)
            {
                rb.isKinematic = !active;
                rb.useGravity = active;
                rb.detectCollisions = active;
            }
        }
        foreach (Collider col in colliders)
        {
            if (col != mainCollider)
                col.enabled = active;
        }
    }

    // Applies force to ragdoll
    public void ApplyForce(Vector3 force, ForceMode forceMode = ForceMode.Impulse)
    {
        if (mainRigidbody != null)
            mainRigidbody.AddForce(force * ragdollForceMultiplier, forceMode);
    }

    // Applies force at specific position
    public void ApplyForceAtPosition(Vector3 force, Vector3 position, ForceMode forceMode = ForceMode.Impulse)
    {
        if (mainRigidbody != null)
            mainRigidbody.AddForceAtPosition(force * ragdollForceMultiplier, position, forceMode);
    }
}