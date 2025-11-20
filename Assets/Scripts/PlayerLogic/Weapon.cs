using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Weapon : NetworkBehaviour
{
    [Header("Weapon Settings")]
    public string weaponName = "Weapon";
    public float damage = 25f;
    public float attackCooldown = 1f;
    public float staminaCost = 10f;
    public float baseAttackDuration = 0.3f;

    protected float lastAttackTime;
    protected PlayerHealth ownerHealth;
    protected ulong ownerId;
    protected PlayerHitboxDamage playerHitbox;

    // Timer-based attack system
    protected float weaponAttackEndTime = 0f;
    protected bool isAttackActive = false;

    // Updated Initialize method with hitbox parameter
    public virtual void Initialize(ulong ownerClientId, PlayerHealth health, PlayerHitboxDamage hitbox)
    {
        ownerId = ownerClientId;
        ownerHealth = health;
        playerHitbox = hitbox;
    }

    protected virtual void Update()
    {
        // Handle attack duration without coroutines
        if (isAttackActive && Time.time >= weaponAttackEndTime)
        {
            DeactivateAttack();
        }
    }

    public virtual bool CanAttack()
    {
        if (Time.time < lastAttackTime + attackCooldown)
        {
            Debug.Log("Weapon on cooldown");
            return false;
        }
        if (ownerHealth != null && ownerHealth.GetStamina() < staminaCost)
        {
            Debug.Log("Not enough stamina");
            return false;
        }
        return true;
    }

    public virtual void Attack()
    {
        // Only do minimal local checks for immediate feedback
        if (Time.time < lastAttackTime + attackCooldown)
        {
            Debug.Log("Weapon on cooldown - rejected locally");
            return;
        }

        Debug.Log($"Local attack initiated by client {ownerId}");

        if (IsServer)
        {
            PerformAttack();
        }
        else
        {
            RequestAttackServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestAttackServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        // Verify the sender is the owner of this weapon
        if (senderClientId != ownerId)
        {
            Debug.LogWarning($"Client {senderClientId} attempted to attack with weapon owned by {ownerId}");
            return;
        }

        // Server-side validation
        if (Time.time < lastAttackTime + attackCooldown)
        {
            Debug.Log($"Server rejected attack - on cooldown");
            return;
        }

        if (ownerHealth != null && ownerHealth.GetStamina() < staminaCost)
        {
            Debug.Log($"Server rejected attack - not enough stamina");
            return;
        }

        PerformAttack();
        Debug.Log($"Server approved attack from client {senderClientId}");
    }

    protected virtual void PerformAttack()
    {
        lastAttackTime = Time.time;
        weaponAttackEndTime = Time.time + baseAttackDuration;
        isAttackActive = true;

        ConsumeStamina();

        // Activate player's hitbox
        if (playerHitbox != null)
        {
            playerHitbox.SetActive(true, damage, ownerId);
            Debug.Log($"Hitbox activated for player {ownerId} with damage {damage}");
        }
        else
        {
            Debug.LogError("playerHitbox is null in PerformAttack!");
        }

        // Visual feedback on all clients
        PlayAttackEffectsClientRpc();

        Debug.Log($"Player {ownerId} performed attack with {weaponName}");
    }

    protected virtual void DeactivateAttack()
    {
        isAttackActive = false;
        if (playerHitbox != null)
        {
            playerHitbox.SetActive(false);
            Debug.Log("Hitbox deactivated");
        }
    }

    [ClientRpc]
    private void PlayAttackEffectsClientRpc()
    {
        // Play attack effects on all clients
        if (IsOwner)
        {
            Debug.Log("Playing local attack effects");
        }
        else
        {
            Debug.Log("Playing remote attack effects");
            // Add particle effects, sounds, etc. for other players' attacks
        }
    }

    protected void ConsumeStamina()
    {
        if (ownerHealth != null && staminaCost > 0)
        {
            if (IsServer)
            {
                ownerHealth.ConsumeStamina(staminaCost);
            }
            else
            {
                RequestStaminaConsumptionServerRpc(staminaCost);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStaminaConsumptionServerRpc(float cost)
    {
        if (ownerHealth != null)
        {
            ownerHealth.ConsumeStamina(cost);
        }
    }

    public virtual void Use(ulong userClientId)
    {
        Attack();
    }

    public virtual void OnEquipped()
    {
        Debug.Log($"{weaponName} equipped by player {ownerId}");
    }

    public virtual void OnUnequipped()
    {
        // Ensure attack is deactivated when unequipped
        if (isAttackActive)
        {
            DeactivateAttack();
        }
        Debug.Log($"{weaponName} unequipped");
    }

    public override void OnNetworkDespawn()
    {
        // Clean up any active attacks
        if (isAttackActive)
        {
            DeactivateAttack();
        }
        base.OnNetworkDespawn();
    }
}