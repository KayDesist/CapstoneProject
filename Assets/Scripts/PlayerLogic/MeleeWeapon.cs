using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class MeleeWeapon : Weapon
{
    public float meleeAttackDuration = 0.3f;
    public string meleeAttackAnimation = "MeleeAttack";

    // Initializes the melee weapon
    public override void Initialize(ulong ownerClientId, PlayerHealth health, PlayerHitboxDamage hitbox)
    {
        base.Initialize(ownerClientId, health, hitbox);
        baseAttackDuration = meleeAttackDuration;
    }

    // Performs melee attack
    protected override void PerformAttack()
    {
        base.PerformAttack();
        if (ownerId == NetworkManager.Singleton.LocalClientId)
        {
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
        PlayAttackAnimationClientRpc();
    }

    // Client RPC to play attack animation
    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
    }

    // Called when weapon is equipped
    public override void OnEquipped()
    {
        base.OnEquipped();
    }

    // Called when weapon is unequipped
    public override void OnUnequipped()
    {
        base.OnUnequipped();
    }
}