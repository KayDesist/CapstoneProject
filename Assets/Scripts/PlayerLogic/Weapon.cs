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
    public float baseAttackDuration = 0.5f; // Increased for better testing

    protected float lastAttackTime;
    protected PlayerHealth ownerHealth;
    public ulong ownerId;
    public PlayerHitboxDamage playerHitbox;

    // Timer-based attack system
    protected float weaponAttackEndTime = 0f;
    protected bool isAttackActive = false;

    // Updated Initialize method with hitbox parameter
    public virtual void Initialize(ulong ownerClientId, PlayerHealth health, PlayerHitboxDamage hitbox)
    {
        ownerId = ownerClientId;
        ownerHealth = health;
        playerHitbox = hitbox;

        Debug.Log($"Weapon {weaponName} initialized for player {ownerId}. Hitbox: {playerHitbox != null}");
    }

    protected virtual void Update()
    {
        // Handle attack duration without coroutines
        if (isAttackActive && Time.time >= weaponAttackEndTime)
        {
            Debug.Log("Weapon: Attack duration ended, deactivating");
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
        Debug.Log($"Weapon: Local attack initiated by client {ownerId}");

        // Only do minimal local checks for immediate feedback
        if (Time.time < lastAttackTime + attackCooldown)
        {
            Debug.Log("Weapon on cooldown - rejected locally");
            return;
        }

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

        Debug.Log($"Weapon: Server received attack request from {senderClientId}");

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

        Debug.Log($"Weapon: PerformAttack called. Active until: {weaponAttackEndTime}");

        ConsumeStamina();

        // Activate player's hitbox
        if (playerHitbox != null)
        {
            playerHitbox.SetActive(true, damage, ownerId);
            Debug.Log($"Hitbox activated for player {ownerId} with damage {damage}");
        }
        else
        {
            Debug.LogError("playerHitbox is null in PerformAttack! Weapon may not be properly initialized.");
        }

        // Visual feedback on all clients
        PlayAttackEffectsClientRpc();

        Debug.Log($"Player {ownerId} performed attack with {weaponName}");
    }

    protected virtual void DeactivateAttack()
    {
        if (isAttackActive)
        {
            Debug.Log("Weapon: Deactivating attack");
            isAttackActive = false;

            if (playerHitbox != null)
            {
                playerHitbox.SetActive(false);
                Debug.Log("Hitbox deactivated");
            }
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
            Debug.Log($"Consuming {staminaCost} stamina");
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
            Debug.Log("Weapon unequipped during active attack - deactivating");
            DeactivateAttack();
        }
        Debug.Log($"{weaponName} unequipped");
    }

    public override void OnNetworkDespawn()
    {
        // Clean up any active attacks
        if (isAttackActive)
        {
            Debug.Log("Weapon network despawn - deactivating attack");
            DeactivateAttack();
        }
        base.OnNetworkDespawn();
    }

    // Debug method to test weapon directly
    [ContextMenu("Test Attack")]
    private void TestAttack()
    {
        if (IsOwner)
        {
            Attack();
        }
        else
        {
            Debug.Log("Cannot test attack - not owner");
        }
    }
}