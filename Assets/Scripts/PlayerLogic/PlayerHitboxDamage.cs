using UnityEngine;
using Unity.Netcode;

public class PlayerHitboxDamage : NetworkBehaviour
{
    [Header("Damage Settings")]
    public float defaultDamage = 20f;

    [Header("Positioning")]
    public float attackRange = 2f;
    public float attackWidth = 1f;

    private ulong ownerId;
    private bool isActive = false;
    private float currentDamage = 20f;

    // Removed SetWeapon method - replaced with SetActive
    public void SetActive(bool active, float weaponDamage = 20f, ulong attackerId = 0)
    {
        if (!IsServer) return;

        isActive = active;
        currentDamage = weaponDamage;
        ownerId = attackerId;

        // Update position based on player's current position and forward
        UpdatePosition();

        Debug.Log($"Hitbox {(active ? "activated" : "deactivated")} for player {ownerId} with damage {currentDamage}");
    }

    private void Update()
    {
        // Update position every frame to follow player
        if (isActive)
        {
            UpdatePosition();
        }
    }

    private void UpdatePosition()
    {
        if (transform.parent == null) return;

        // Get player's position and forward direction
        Transform playerTransform = transform.parent;
        Vector3 playerPosition = playerTransform.position;
        Vector3 playerForward = playerTransform.forward;

        // Position hitbox in front of player
        transform.position = playerPosition + playerForward * (attackRange / 2f);
        transform.rotation = Quaternion.LookRotation(playerForward);

        // Scale collider based on weapon range
        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.size = new Vector3(attackWidth, 1f, attackRange);
            collider.center = new Vector3(0, 0, attackRange / 2f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // CRITICAL: Only process damage on the server
        if (!IsServer) return;
        if (!isActive) return;

        // Don't damage yourself
        if (other.TryGetComponent(out NetworkObject netObj))
        {
            if (netObj.OwnerClientId == ownerId)
            {
                Debug.Log("Hitbox: Ignoring self-damage");
                return;
            }
        }

        // Damage another player if they have a health component
        if (other.TryGetComponent(out PlayerHealth health))
        {
            // Apply damage directly on the server
            health.TakeDamage(currentDamage, ownerId);
            Debug.Log($"Hitbox: Hit player {health.OwnerClientId} for {currentDamage} damage");

            // Visual/audio feedback for all clients
            PlayHitEffectClientRpc(other.transform.position);
        }
    }

    [ClientRpc]
    private void PlayHitEffectClientRpc(Vector3 hitPosition)
    {
        // Play hit effects on all clients (blood, sound, etc.)
        Debug.Log("Playing hit effect at: " + hitPosition);


    }
}