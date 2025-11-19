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
    public float attackDuration = 0.3f;

    protected float lastAttackTime;
    protected PlayerHealth ownerHealth;
    protected ulong ownerId;
    protected PlayerHitboxDamage playerHitbox;

    // Updated Initialize method with hitbox parameter
    public virtual void Initialize(ulong ownerClientId, PlayerHealth health, PlayerHitboxDamage hitbox)
    {
        ownerId = ownerClientId;
        ownerHealth = health;
        playerHitbox = hitbox;
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
        if (!CanAttack())
        {
            Debug.Log("Attack rejected locally - CanAttack returned false");
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

    [ServerRpc]
    private void RequestAttackServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        // Verify the sender owns this weapon
        if (senderClientId != ownerId)
        {
            Debug.LogWarning($"Client {senderClientId} attempted to attack with weapon owned by {ownerId}");
            return;
        }

        if (CanAttack())
        {
            PerformAttack();
            Debug.Log($"Server approved attack from client {senderClientId}");
        }
        else
        {
            Debug.Log($"Server rejected attack from client {senderClientId} - CanAttack returned false");
        }
    }

    protected virtual void PerformAttack()
    {
        lastAttackTime = Time.time;
        ConsumeStamina();

        // Activate player's hitbox
        if (playerHitbox != null)
        {
            playerHitbox.SetActive(true, damage, ownerId);
            StartCoroutine(DeactivateHitboxAfterDelay(attackDuration));
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

    private IEnumerator DeactivateHitboxAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (playerHitbox != null)
        {
            playerHitbox.SetActive(false);
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

    [ServerRpc]
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
        Debug.Log($"{weaponName} unequipped");
    }
}