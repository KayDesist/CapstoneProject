using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Weapon : NetworkBehaviour
{
    public string weaponName = "Weapon";
    public int damage = 25;
    public float attackCooldown = 1f;
    public int staminaCost = 10;
    public float baseAttackDuration = 0.5f;
    public string attackAnimationName = "Attack";
    public AudioClip swingSound;
    protected float lastAttackTime;
    protected PlayerHealth ownerHealth;
    public ulong ownerId;
    public PlayerHitboxDamage playerHitbox;
    protected NetworkPlayerController playerController;
    protected float weaponAttackEndTime = 0f;
    protected bool isAttackActive = false;

    // Initializes the weapon
    public virtual void Initialize(ulong ownerClientId, PlayerHealth health, PlayerHitboxDamage hitbox)
    {
        ownerId = ownerClientId;
        ownerHealth = health;
        playerHitbox = hitbox;
        if (health != null)
            playerController = health.GetComponent<NetworkPlayerController>();
    }

    // Updates every frame
    protected virtual void Update()
    {
        if (isAttackActive && Time.time >= weaponAttackEndTime)
            DeactivateAttack();
    }

    // Checks if weapon can attack
    public virtual bool CanAttack()
    {
        if (Time.time < lastAttackTime + attackCooldown)
            return false;
        if (ownerHealth != null && ownerHealth.GetStamina() < staminaCost)
            return false;
        return true;
    }

    // Performs attack
    public virtual void Attack()
    {
        if (Time.time < lastAttackTime + attackCooldown)
            return;
        if (IsServer)
            PerformAttack();
        else
            RequestAttackServerRpc();
    }

    // Server RPC to request attack
    [ServerRpc(RequireOwnership = false)]
    private void RequestAttackServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (senderClientId != ownerId)
            return;
        if (Time.time < lastAttackTime + attackCooldown)
            return;
        if (ownerHealth != null && ownerHealth.GetStamina() < staminaCost)
            return;
        PerformAttack();
    }

    // Performs the actual attack
    protected virtual void PerformAttack()
    {
        lastAttackTime = Time.time;
        weaponAttackEndTime = Time.time + baseAttackDuration;
        isAttackActive = true;
        ConsumeStamina();
        if (playerController != null)
            playerController.PlayAttackAnimation();
        if (playerHitbox != null)
            playerHitbox.SetActive(true, damage, ownerId);
        PlayAttackEffectsClientRpc();
    }

    // Deactivates attack
    protected virtual void DeactivateAttack()
    {
        if (isAttackActive)
        {
            isAttackActive = false;
            if (playerHitbox != null)
                playerHitbox.SetActive(false);
        }
    }

    // Client RPC to play attack effects
    [ClientRpc]
    private void PlayAttackEffectsClientRpc()
    {
        if (swingSound != null)
            AudioSource.PlayClipAtPoint(swingSound, transform.position, 0.5f);
    }

    // Consumes stamina for attack
    protected void ConsumeStamina()
    {
        if (ownerHealth != null && staminaCost > 0)
        {
            if (IsServer)
                ownerHealth.ConsumeStamina(staminaCost);
            else
                RequestStaminaConsumptionServerRpc(staminaCost);
        }
    }

    // Server RPC to request stamina consumption
    [ServerRpc(RequireOwnership = false)]
    private void RequestStaminaConsumptionServerRpc(int cost)
    {
        if (ownerHealth != null)
            ownerHealth.ConsumeStamina(cost);
    }

    // Uses the weapon
    public virtual void Use(ulong userClientId)
    {
        Attack();
    }

    // Called when weapon is equipped
    public virtual void OnEquipped()
    {
    }

    // Called when weapon is unequipped
    public virtual void OnUnequipped()
    {
        if (isAttackActive)
            DeactivateAttack();
    }

    // Called when weapon despawns from network
    public override void OnNetworkDespawn()
    {
        if (isAttackActive)
            DeactivateAttack();
        base.OnNetworkDespawn();
    }
}