using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class MeleeWeapon : Weapon
{
    [Header("Melee Settings")]
    public float attackDuration = 0.3f;

    private bool isAttacking = false;
    private float attackEndTime = 0f;

    // Override Initialize to match base class signature
    public override void Initialize(ulong ownerClientId, PlayerHealth health, PlayerHitboxDamage hitbox)
    {
        base.Initialize(ownerClientId, health, hitbox);
        Debug.Log($"MeleeWeapon initialized for player {ownerId}");
    }

    private void Update()
    {
        // Handle hitbox deactivation without coroutine
        if (isAttacking && Time.time >= attackEndTime)
        {
            if (playerHitbox != null)
            {
                playerHitbox.SetActive(false);
            }
            isAttacking = false;
        }
    }

    // Override PerformAttack for melee-specific logic
    protected override void PerformAttack()
    {
        lastAttackTime = Time.time;
        ConsumeStamina();
        isAttacking = true;
        attackEndTime = Time.time + attackDuration;

        // Activate player's hitbox
        if (playerHitbox != null)
        {
            playerHitbox.SetActive(true, damage, ownerId);
        }

        // Visual feedback
        PlayAttackAnimationClientRpc();

        Debug.Log($"Player {ownerId} attacked with {weaponName}");
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

    public override void Use(ulong userClientId)
    {
        Attack();
    }

    public override void OnEquipped()
    {
        base.OnEquipped();
        Debug.Log($"Melee weapon {weaponName} equipped");
    }

    public bool IsAttacking()
    {
        return isAttacking;
    }

    public override void OnUnequipped()
    {
        // Ensure hitbox is deactivated when weapon is unequipped
        if (isAttacking && playerHitbox != null)
        {
            playerHitbox.SetActive(false);
        }
        isAttacking = false;
        base.OnUnequipped();
    }
}