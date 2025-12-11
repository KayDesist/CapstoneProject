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

    // Called when object spawns on network
    public override void OnNetworkSpawn()
    {
        cultistPlayerId.OnValueChanged += OnCultistAssigned;

        if (IsServer)
        {
            StartCoroutine(AssignRolesWithDelay());
        }
        else
        {
            StartCoroutine(CheckForExistingRoles());
        }
    }

    // Assigns roles with delay
    private IEnumerator AssignRolesWithDelay()
    {
        yield return new WaitForSeconds(3f);
        AssignRolesServerRpc();
    }

    // Checks for existing roles
    private IEnumerator CheckForExistingRoles()
    {
        yield return new WaitForSeconds(1f);
        if (cultistPlayerId.Value != -1)
        {
            OnCultistAssigned(-1, cultistPlayerId.Value);
        }
    }

    // Server RPC to assign roles
    [ServerRpc]
    private void AssignRolesServerRpc()
    {
        if (rolesAssigned)
        {
            return;
        }

        var connectedClients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);

        if (connectedClients.Count < 1)
        {
            return;
        }

        playerRoles.Clear();

        int randomIndex = Random.Range(0, connectedClients.Count);
        ulong cultistId = connectedClients[randomIndex];

        foreach (ulong clientId in connectedClients)
        {
            PlayerRole role = (clientId == cultistId) ? PlayerRole.Cultist : PlayerRole.Survivor;
            playerRoles[clientId] = role;
        }

        cultistPlayerId.Value = (int)cultistId;
        rolesAssigned = true;

        NotifyEndGameManager();
        NotifyClientsOfRolesClientRpc();
    }

    // Client RPC to notify clients of roles
    [ClientRpc]
    private void NotifyClientsOfRolesClientRpc()
    {
        if (cultistPlayerId.Value != -1)
        {
            OnCultistAssigned(-1, cultistPlayerId.Value);
        }
    }

    // Called when cultist assignment changes
    private void OnCultistAssigned(int oldCultistId, int newCultistId)
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        PlayerRole localRole = (localClientId == (ulong)newCultistId) ? PlayerRole.Cultist : PlayerRole.Survivor;

        playerRoles[localClientId] = localRole;

        ApplyRoleToPlayer(localRole);

        if (GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.OnRoleAssigned(localRole);
        }
        else
        {
            if (RoleDisplayUI.Instance != null)
            {
                RoleDisplayUI.Instance.ShowRole(localRole);
            }
        }
    }

    // Applies role to player
    private void ApplyRoleToPlayer(PlayerRole role)
    {
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (NetworkPlayerController player in allPlayers)
        {
            if (player.IsOwner)
            {
                player.ApplyRoleSpecificSettings(role);
                break;
            }
        }
    }

    // Gets local player role
    public PlayerRole GetLocalPlayerRole()
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        if (playerRoles.ContainsKey(localClientId))
        {
            return playerRoles[localClientId];
        }

        return PlayerRole.Survivor;
    }

    // Gets player role by client ID
    public PlayerRole GetPlayerRole(ulong clientId)
    {
        if (playerRoles.ContainsKey(clientId))
        {
            return playerRoles[clientId];
        }

        if (cultistPlayerId.Value != -1 && clientId == (ulong)cultistPlayerId.Value)
        {
            return PlayerRole.Cultist;
        }

        return PlayerRole.Survivor;
    }

    // Checks if player is cultist
    public bool IsCultist(ulong clientId)
    {
        return GetPlayerRole(clientId) == PlayerRole.Cultist;
    }

    // Notifies EndGameManager about players
    private void NotifyEndGameManager()
    {
        if (EndGameManager.Instance != null && IsServer)
        {
            foreach (var clientId in playerRoles.Keys)
            {
                EndGameManager.Instance.RegisterPlayer(clientId, playerRoles[clientId]);
            }
        }
    }

    // Called when object despawns from network
    public override void OnNetworkDespawn()
    {
        cultistPlayerId.OnValueChanged -= OnCultistAssigned;
        playerRoles.Clear();
        rolesAssigned = false;
    }

    // Resets static instance
    public static void ResetInstance()
    {
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
        }
    }

    // Debugs all roles
    [ContextMenu("Debug All Roles")]
    public void DebugAllRoles()
    {
        foreach (var kvp in playerRoles)
        {
        }
    }
}