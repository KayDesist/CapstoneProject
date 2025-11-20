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
        Debug.Log($"RoleManager spawned - IsServer: {IsServer}, IsClient: {IsClient}");

        cultistPlayerId.OnValueChanged += OnCultistAssigned;

        if (IsServer)
        {
            // Wait a moment for all players to connect, then assign roles
            StartCoroutine(AssignRolesWithDelay());
        }
        else
        {
            // Client checks if roles are already assigned
            StartCoroutine(CheckForExistingRoles());
        }
    }

    private IEnumerator AssignRolesWithDelay()
    {
        yield return new WaitForSeconds(3f); // Wait for all clients to connect
        AssignRolesServerRpc();
    }

    private IEnumerator CheckForExistingRoles()
    {
        yield return new WaitForSeconds(1f);
        // If cultist is already assigned, trigger the update
        if (cultistPlayerId.Value != -1)
        {
            OnCultistAssigned(-1, cultistPlayerId.Value);
        }
    }

    [ServerRpc]
    private void AssignRolesServerRpc()
    {
        if (rolesAssigned)
        {
            Debug.Log("Roles already assigned, skipping");
            return;
        }

        var connectedClients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);

        if (connectedClients.Count < 1)
        {
            Debug.LogWarning("No connected clients to assign roles to");
            return;
        }

        // Clear previous assignments
        playerRoles.Clear();

        // Randomly select one player to be the cultist
        int randomIndex = Random.Range(0, connectedClients.Count);
        ulong cultistId = connectedClients[randomIndex];

        // Assign roles to all connected clients
        foreach (ulong clientId in connectedClients)
        {
            PlayerRole role = (clientId == cultistId) ? PlayerRole.Cultist : PlayerRole.Survivor;
            playerRoles[clientId] = role;
            Debug.Log($"Assigned {role} to client {clientId}");
        }

        // Sync the cultist ID across network
        cultistPlayerId.Value = (int)cultistId;
        rolesAssigned = true;

        Debug.Log($"Roles assigned! Cultist: {cultistId}, Total players: {connectedClients.Count}");

        // Notify EndGameManager about all players
        NotifyEndGameManager();

        // Notify all clients about their roles
        NotifyClientsOfRolesClientRpc();
    }

    [ClientRpc]
    private void NotifyClientsOfRolesClientRpc()
    {
        Debug.Log("Received role notification from server");
        // Force role update on all clients
        if (cultistPlayerId.Value != -1)
        {
            OnCultistAssigned(-1, cultistPlayerId.Value);
        }
    }

    private void OnCultistAssigned(int oldCultistId, int newCultistId)
    {
        Debug.Log($"Cultist assignment changed: {oldCultistId} -> {newCultistId}");

        // Update local player's role knowledge
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        PlayerRole localRole = (localClientId == (ulong)newCultistId) ? PlayerRole.Cultist : PlayerRole.Survivor;

        playerRoles[localClientId] = localRole;

        Debug.Log($"Your role is: {localRole} (Client ID: {localClientId}, Cultist ID: {newCultistId})");

        // Apply role-specific settings to player
        ApplyRoleToPlayer(localRole);

        // Show role UI to player via GameHUDManager
        if (GameHUDManager.Instance != null)
        {
            Debug.Log("Notifying GameHUDManager of role assignment");
            GameHUDManager.Instance.OnRoleAssigned(localRole);
        }
        else
        {
            Debug.LogWarning("GameHUDManager.Instance is null!");

            // Fallback: try to use RoleDisplayUI directly
            if (RoleDisplayUI.Instance != null)
            {
                RoleDisplayUI.Instance.ShowRole(localRole);
            }
        }
    }

    private void ApplyRoleToPlayer(PlayerRole role)
    {
        // Find the local player and apply role settings
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (NetworkPlayerController player in allPlayers)
        {
            if (player.IsOwner)
            {
                player.ApplyRoleSpecificSettings(role);
                Debug.Log($"Applied {role} settings to local player");
                break;
            }
        }
    }

    public PlayerRole GetLocalPlayerRole()
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        if (playerRoles.ContainsKey(localClientId))
        {
            return playerRoles[localClientId];
        }

        Debug.LogWarning($"No role found for local client {localClientId}, defaulting to Survivor");
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

    // Add this method to notify EndGameManager about players
    private void NotifyEndGameManager()
    {
        if (EndGameManager.Instance != null && IsServer)
        {
            foreach (var clientId in playerRoles.Keys)
            {
                EndGameManager.Instance.RegisterPlayer(clientId, playerRoles[clientId]);
            }
            Debug.Log("Notified EndGameManager about all players");
        }
        else
        {
            Debug.LogWarning("EndGameManager instance not found or not server");
        }
    }

    public override void OnNetworkDespawn()
    {
        cultistPlayerId.OnValueChanged -= OnCultistAssigned;
        playerRoles.Clear();
        rolesAssigned = false;
    }
}