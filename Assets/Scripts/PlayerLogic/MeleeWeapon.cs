using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class MeleeWeapon : Weapon
{
    [Header("Melee Settings")]
    public float meleeAttackDuration = 0.3f;

    [Header("Melee Animation")]
    public string meleeAttackAnimation = "MeleeAttack";

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

        // Trigger attack animation on player
        if (ownerId == NetworkManager.Singleton.LocalClientId)
        {
            // Find local player and trigger animation
            var playerObjects = FindObjectsOfType<NetworkPlayerController>();
            foreach (var player in playerObjects)
            {
                if (player.IsOwner)
                {
                    player.PlayAttackAnimation();
                    break;
                }
            }
        }

        // Melee-specific visual feedback
        PlayAttackAnimationClientRpc();
        Debug.Log($"Player {ownerId} performed melee attack with {weaponName}");
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        // Play melee-specific attack effects on all clients
        // This is for weapon effects, not character animations
        Debug.Log("Playing melee attack visual effects");
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