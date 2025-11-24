using UnityEngine;
using Unity.Netcode;

public class PlayerHitboxDamage : NetworkBehaviour
{
    [Header("Damage Settings")]
    public int defaultDamage = 20; // Changed to int

    [Header("Positioning")]
    public float attackRange = 2f;
    public float attackWidth = 1f;
    public float attackHeight = 1f;

    private ulong ownerId;
    private bool isActive = false;
    private int currentDamage = 20; // Changed to int
    private BoxCollider hitboxCollider;

    private void Awake()
    {
        hitboxCollider = GetComponent<BoxCollider>();
        if (hitboxCollider == null)
        {
            hitboxCollider = gameObject.AddComponent<BoxCollider>();
        }

        // Ensure collider is set up properly
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false; // Start disabled

        Debug.Log($"Hitbox collider initialized: {hitboxCollider != null}, isTrigger: {hitboxCollider.isTrigger}");
    }

    public void SetActive(bool active, int weaponDamage = 20, ulong attackerId = 0) // Changed to int
    {
        if (!IsServer)
        {
            Debug.Log($"Hitbox SetActive called on client - ignoring. Active: {active}");
            return;
        }

        isActive = active;
        currentDamage = weaponDamage;
        ownerId = attackerId;

        // Enable/disable the collider
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = active;
            Debug.Log($"Hitbox collider enabled: {active}");
        }

        // Update position based on player's current position and forward
        UpdatePosition();

        Debug.Log($"Hitbox {(active ? "activated" : "deactivated")} for player {ownerId} with damage {currentDamage}");
    }

    private void Update()
    {
        // Update position every frame to follow player when active
        if (isActive)
        {
            UpdatePosition();
        }
    }

    private void UpdatePosition()
    {
        if (transform.parent == null)
        {
            Debug.LogWarning("Hitbox has no parent transform!");
            return;
        }

        // Get player's position and forward direction
        Transform playerTransform = transform.parent;
        Vector3 playerPosition = playerTransform.position;
        Vector3 playerForward = playerTransform.forward;

        // Position hitbox in front of player
        transform.position = playerPosition + playerForward * (attackRange / 2f);
        transform.rotation = Quaternion.LookRotation(playerForward);

        // Scale collider based on weapon range
        if (hitboxCollider != null)
        {
            hitboxCollider.size = new Vector3(attackWidth, attackHeight, attackRange);
            hitboxCollider.center = new Vector3(0, 0, attackRange / 2f);
        }

        // Debug visualization
        Debug.DrawRay(playerPosition, playerForward * attackRange, isActive ? Color.red : Color.gray, 0.1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        // CRITICAL: Only process damage on the server
        if (!IsServer)
        {
            Debug.Log("Hitbox OnTriggerEnter called on client - ignoring");
            return;
        }

        if (!isActive)
        {
            Debug.Log("Hitbox triggered but not active - ignoring");
            return;
        }

        Debug.Log($"Hitbox triggered by: {other.gameObject.name}");

        // Don't damage yourself
        if (other.TryGetComponent(out NetworkObject netObj))
        {
            if (netObj.OwnerClientId == ownerId)
            {
                Debug.Log("Hitbox: Ignoring self-damage");
                return;
            }
            else
            {
                Debug.Log($"Hitbox: Other player ID: {netObj.OwnerClientId}, Owner ID: {ownerId}");
            }
        }
        else
        {
            Debug.Log("Hitbox: No NetworkObject found on triggered object");
        }

        // Damage another player if they have a health component
        if (other.TryGetComponent(out PlayerHealth health))
        {
            // Apply damage directly on the server
            health.TakeDamage(currentDamage, ownerId); // Now passing int
            Debug.Log($"Hitbox: Hit player {health.OwnerClientId} for {currentDamage} damage");

            // Visual/audio feedback for all clients
            PlayHitEffectClientRpc(other.transform.position);

            // Deactivate hitbox after hitting someone to prevent multiple hits
            SetActive(false);
        }
        else
        {
            Debug.Log($"Hitbox: No PlayerHealth component found on {other.gameObject.name}");

            // Check if it's on a child object
            health = other.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(currentDamage, ownerId); // Now passing int
                Debug.Log($"Hitbox: Hit player (via parent) {health.OwnerClientId} for {currentDamage} damage");
                PlayHitEffectClientRpc(other.transform.position);
                SetActive(false);
            }
        }
    }

    [ClientRpc]
    private void PlayHitEffectClientRpc(Vector3 hitPosition)
    {
        // Play hit effects on all clients (blood, sound, etc.)
        Debug.Log($"Client: Playing hit effect at: {hitPosition}");
    }

    private void OnDrawGizmos()
    {
        if (!isActive) return;

        Gizmos.color = Color.red;
        Vector3 center = transform.position + transform.forward * (attackRange / 2f);
        Vector3 size = new Vector3(attackWidth, attackHeight, attackRange);
        Gizmos.DrawWireCube(center, size);

        // Draw the forward direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * attackRange);
    }
}