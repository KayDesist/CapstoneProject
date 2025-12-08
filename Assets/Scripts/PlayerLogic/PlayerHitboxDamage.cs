using UnityEngine;
using Unity.Netcode;

public class PlayerHitboxDamage : NetworkBehaviour
{
    [Header("Damage Settings")]
    public int defaultDamage = 20;

    [Header("Positioning")]
    public float attackRange = 2f;
    public float attackWidth = 1f;
    public float attackHeight = 1f;

    [Header("Audio")]
    public AudioClip hitSound;

    private ulong ownerId;
    private bool isActive = false;
    private int currentDamage = 20;
    private BoxCollider hitboxCollider;

    private void Awake()
    {
        hitboxCollider = GetComponent<BoxCollider>();
        if (hitboxCollider == null)
        {
            hitboxCollider = gameObject.AddComponent<BoxCollider>();
        }

        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;

        Debug.Log($"Hitbox collider initialized: {hitboxCollider != null}, isTrigger: {hitboxCollider.isTrigger}");
    }

    public void SetActive(bool active, int weaponDamage = 20, ulong attackerId = 0)
    {
        if (!IsServer) return;

        isActive = active;
        currentDamage = weaponDamage;
        ownerId = attackerId;

        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = active;
            Debug.Log($"Hitbox collider enabled: {active}");
        }

        UpdatePosition();
    }

    private void Update()
    {
        if (isActive)
        {
            UpdatePosition();
        }
    }

    private void UpdatePosition()
    {
        if (transform.parent == null) return;

        Transform playerTransform = transform.parent;
        Vector3 playerPosition = playerTransform.position;
        Vector3 playerForward = playerTransform.forward;

        transform.position = playerPosition + playerForward * (attackRange / 2f);
        transform.rotation = Quaternion.LookRotation(playerForward);

        if (hitboxCollider != null)
        {
            hitboxCollider.size = new Vector3(attackWidth, attackHeight, attackRange);
            hitboxCollider.center = new Vector3(0, 0, attackRange / 2f);
        }

        Debug.DrawRay(playerPosition, playerForward * attackRange, isActive ? Color.red : Color.gray, 0.1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!isActive) return;

        Debug.Log($"Hitbox triggered by: {other.gameObject.name}");

        if (other.TryGetComponent(out NetworkObject netObj))
        {
            if (netObj.OwnerClientId == ownerId)
            {
                Debug.Log("Hitbox: Ignoring self-damage");
                return;
            }
        }

        if (other.TryGetComponent(out PlayerHealth health))
        {
            health.TakeDamage(currentDamage, ownerId);
            Debug.Log($"Hitbox: Hit player {health.OwnerClientId} for {currentDamage} damage");

            // Play hit sound on all clients
            PlayHitEffectClientRpc(other.transform.position);

            SetActive(false);
        }
        else
        {
            health = other.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(currentDamage, ownerId);
                Debug.Log($"Hitbox: Hit player (via parent) {health.OwnerClientId} for {currentDamage} damage");
                PlayHitEffectClientRpc(other.transform.position);
                SetActive(false);
            }
        }
    }

    [ClientRpc]
    private void PlayHitEffectClientRpc(Vector3 hitPosition)
    {
        // Play hit sound at position
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, hitPosition, 0.7f);
        }
    }

    private void OnDrawGizmos()
    {
        if (!isActive) return;

        Gizmos.color = Color.red;
        Vector3 center = transform.position + transform.forward * (attackRange / 2f);
        Vector3 size = new Vector3(attackWidth, attackHeight, attackRange);
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * attackRange);
    }
}