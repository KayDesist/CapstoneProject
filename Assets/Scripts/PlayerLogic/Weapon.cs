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
        if (!CanAttack()) return;

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
    private void RequestAttackServerRpc()
    {
        if (CanAttack())
        {
            PerformAttack();
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
        }

        // Visual feedback
        PlayAttackAnimationClientRpc();

        Debug.Log($"Player {ownerId} attacked with {weaponName}");
    }

    private IEnumerator DeactivateHitboxAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (playerHitbox != null)
        {
            playerHitbox.SetActive(false);
        }
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        // Play attack animation or effects on all clients
        if (IsOwner)
        {
            Debug.Log("Playing local attack animation");
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