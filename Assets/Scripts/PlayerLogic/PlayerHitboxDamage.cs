using UnityEngine;
using Unity.Netcode;

public class PlayerHitboxDamage : NetworkBehaviour
{
    public int defaultDamage = 20;
    public float attackRange = 2f;
    public float attackWidth = 1f;
    public float attackHeight = 1f;
    public AudioClip hitSound;
    private ulong ownerId;
    private bool isActive = false;
    private int currentDamage = 20;
    private BoxCollider hitboxCollider;

    // Initializes on awake
    private void Awake()
    {
        hitboxCollider = GetComponent<BoxCollider>();
        if (hitboxCollider == null)
            hitboxCollider = gameObject.AddComponent<BoxCollider>();
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;
    }

    // Sets hitbox active state
    public void SetActive(bool active, int weaponDamage = 20, ulong attackerId = 0)
    {
        if (!IsServer) return;
        isActive = active;
        currentDamage = weaponDamage;
        ownerId = attackerId;
        if (hitboxCollider != null)
            hitboxCollider.enabled = active;
        UpdatePosition();
    }

    // Updates every frame
    private void Update()
    {
        if (isActive)
            UpdatePosition();
    }

    // Updates hitbox position
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
    }

    // Called when trigger enters collider
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!isActive) return;
        if (other.TryGetComponent(out NetworkObject netObj))
        {
            if (netObj.OwnerClientId == ownerId)
                return;
        }
        if (other.TryGetComponent(out PlayerHealth health))
        {
            health.TakeDamage(currentDamage, ownerId);
            PlayHitEffectClientRpc(other.transform.position);
            SetActive(false);
        }
        else
        {
            health = other.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(currentDamage, ownerId);
                PlayHitEffectClientRpc(other.transform.position);
                SetActive(false);
            }
        }
    }

    // Client RPC to play hit effect
    [ClientRpc]
    private void PlayHitEffectClientRpc(Vector3 hitPosition)
    {
        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, hitPosition, 0.7f);
    }

    // Draws gizmos for visualization
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