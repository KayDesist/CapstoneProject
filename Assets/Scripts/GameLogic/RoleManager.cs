using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Collections;

public class RoleManager : NetworkBehaviour
{
    public static RoleManager Instance;

    public enum PlayerRole
    {
        Survivor,
        Cultist
    }

    private NetworkVariable<int> cultistPlayerId = new NetworkVariable<int>(-1);
    private Dictionary<ulong, PlayerRole> playerRoles = new Dictionary<ulong, PlayerRole>();
    private bool rolesAssigned = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Wait a moment for all players to connect, then assign roles
            StartCoroutine(AssignRolesWithDelay());
        }

        cultistPlayerId.OnValueChanged += OnCultistAssigned;
    }

    private IEnumerator AssignRolesWithDelay()
    {
        yield return new WaitForSeconds(1f);
        AssignRoles();
    }

    [ServerRpc]
    private void AssignRolesServerRpc()
    {
        if (rolesAssigned) return;

        var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        if (connectedClients.Count < 2) return;

        // Clear previous assignments
        playerRoles.Clear();

        // Randomly select one player to be the cultist
        int randomIndex = Random.Range(0, connectedClients.Count);
        ulong cultistId = connectedClients[randomIndex];

        // Assign roles
        foreach (ulong clientId in connectedClients)
        {
            PlayerRole role = (clientId == cultistId) ? PlayerRole.Cultist : PlayerRole.Survivor;
            playerRoles[clientId] = role;
        }

        // Sync the cultist ID across network
        cultistPlayerId.Value = (int)cultistId;
        rolesAssigned = true;

        Debug.Log($"Roles assigned! Cultist: {cultistId}, Total players: {connectedClients.Count}");
    }

    private void AssignRoles()
    {
        AssignRolesServerRpc();
    }

    private void OnCultistAssigned(int oldCultistId, int newCultistId)
    {
        // Update local player's role knowledge
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        PlayerRole localRole = (localClientId == (ulong)newCultistId) ? PlayerRole.Cultist : PlayerRole.Survivor;

        playerRoles[localClientId] = localRole;

        // Show role UI to player
        if (RoleDisplayUI.Instance != null)
        {
            RoleDisplayUI.Instance.ShowRole(localRole);
        }

        Debug.Log($"Your role: {localRole}");
    }

    public PlayerRole GetLocalPlayerRole()
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        if (playerRoles.ContainsKey(localClientId))
        {
            return playerRoles[localClientId];
        }
        return PlayerRole.Survivor; // Default to survivor
    }

    public PlayerRole GetPlayerRole(ulong clientId)
    {
        if (playerRoles.ContainsKey(clientId))
        {
            return playerRoles[clientId];
        }
        return PlayerRole.Survivor;
    }

    public bool IsCultist(ulong clientId)
    {
        return GetPlayerRole(clientId) == PlayerRole.Cultist;
    }

    public override void OnNetworkDespawn()
    {
        cultistPlayerId.OnValueChanged -= OnCultistAssigned;
        playerRoles.Clear();
        rolesAssigned = false;
    }
}