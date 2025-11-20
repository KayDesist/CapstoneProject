using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class MeleeWeapon : Weapon
{
    [Header("Melee Settings")]
    public float meleeAttackDuration = 0.3f;

    public override void Initialize(ulong ownerClientId, PlayerHealth health, PlayerHitboxDamage hitbox)
    {
        base.Initialize(ownerClientId, health, hitbox);
        // Use the melee duration for this weapon type
        baseAttackDuration = meleeAttackDuration;
        Debug.Log($"MeleeWeapon initialized for player {ownerId}");
    }

    protected override void PerformAttack()
    {
        // Let base class handle timing and hitbox activation
        base.PerformAttack();

        // Melee-specific visual feedback
        PlayAttackAnimationClientRpc();
        Debug.Log($"Player {ownerId} performed melee attack with {weaponName}");
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        // Play melee-specific attack effects on all clients
        if (IsOwner)
        {
            Debug.Log("Playing local melee attack animation");
        }
        else
        {
            Debug.Log("Playing remote melee attack animation");
        }
    }

    public override void OnEquipped()
    {
        base.OnEquipped();
        Debug.Log($"Melee weapon {weaponName} equipped");
    }

    public override void OnUnequipped()
    {
        // Base class already handles deactivation
        base.OnUnequipped();
    }
}