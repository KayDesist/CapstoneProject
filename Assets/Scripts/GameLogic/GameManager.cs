using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Game Settings")]
    [SerializeField] private int minPlayersToStart = 5;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Game State")]
    public NetworkVariable<GameState> currentGameState = new NetworkVariable<GameState>();
    public NetworkVariable<int> survivorsAlive = new NetworkVariable<int>();
    public NetworkVariable<int> tasksCompleted = new NetworkVariable<int>();

    private List<ulong> playerIds = new List<ulong>();
    private Dictionary<ulong, PlayerRole> playerRoles = new Dictionary<ulong, PlayerRole>();
    private ulong cultistPlayerId;

    public enum GameState
    {
        Lobby,
        Starting,
        InProgress,
        SurvivorsWin,
        CultistWin
    }

    public enum PlayerRole
    {
        Survivor,
        Cultist
    }

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
            InitializeGame();
        }

        currentGameState.OnValueChanged += OnGameStateChanged;
    }

    private void InitializeGame()
    {
        // Get all connected players
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            playerIds.Add(clientId);
        }

        // Assign roles randomly
        AssignRoles();

        // Spawn players
        SpawnPlayers();

        // Update game state
        currentGameState.Value = GameState.InProgress;
        survivorsAlive.Value = playerIds.Count - 1; // All except cultist
    }

    private void AssignRoles()
    {
        // Randomly select one player to be cultist
        int cultistIndex = Random.Range(0, playerIds.Count);
        cultistPlayerId = playerIds[cultistIndex];

        // Assign roles to all players
        foreach (var playerId in playerIds)
        {
            PlayerRole role = (playerId == cultistPlayerId) ? PlayerRole.Cultist : PlayerRole.Survivor;
            playerRoles[playerId] = role;

            // Notify each player of their role (only they will know)
            NotifyPlayerRoleClientRpc(role, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { playerId } }
            });
        }

        Debug.Log($"Cultist is player {cultistPlayerId}");
    }

    [ClientRpc]
    private void NotifyPlayerRoleClientRpc(PlayerRole role, ClientRpcParams rpcParams = default)
    {
        // This only executes on the target client
        Debug.Log($"You are: {role}");

        // Update local player's role
        PlayerController localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            localPlayer.SetRole(role);
        }
    }

    private void SpawnPlayers()
    {
        if (!IsServer) return;

        List<Transform> availableSpawnPoints = new List<Transform>(spawnPoints);

        foreach (var playerId in playerIds)
        {
            if (availableSpawnPoints.Count == 0) break;

            int spawnIndex = Random.Range(0, availableSpawnPoints.Count);
            Transform spawnPoint = availableSpawnPoints[spawnIndex];
            availableSpawnPoints.RemoveAt(spawnIndex);

            // Player is already spawned by NetworkManager, just move them
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var client))
            {
                if (client.PlayerObject != null)
                {
                    client.PlayerObject.transform.position = spawnPoint.position;
                    client.PlayerObject.transform.rotation = spawnPoint.rotation;
                }
            }
        }
    }

    private PlayerController GetLocalPlayer()
    {
        // Find the local player in the scene
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            if (player.IsOwner)
                return player;
        }
        return null;
    }

    private void OnGameStateChanged(GameState previous, GameState current)
    {
        // Handle game state changes (win/lose conditions)
        if (current == GameState.SurvivorsWin || current == GameState.CultistWin)
        {
            ShowEndGameScreen(current);
        }
    }

    [ServerRpc]
    public void CompleteTaskServerRpc()
    {
        tasksCompleted.Value++;

        // Check if survivors completed all tasks
        if (tasksCompleted.Value >= 5) // Adjust based on your task count
        {
            currentGameState.Value = GameState.SurvivorsWin;
        }
    }

    [ServerRpc]
    public void PlayerDiedServerRpc(ulong playerId)
    {
        if (playerRoles.TryGetValue(playerId, out PlayerRole role))
        {
            if (role == PlayerRole.Survivor)
            {
                survivorsAlive.Value--;

                // Check if cultist wins by eliminating all survivors
                if (survivorsAlive.Value <= 0)
                {
                    currentGameState.Value = GameState.CultistWin;
                }
            }
        }
    }

    private void ShowEndGameScreen(GameState endState)
    {
        // This will be called on all clients when game ends
        if (EndGameUI.Instance != null)
        {
            bool survivorsWin = (endState == GameState.SurvivorsWin);
            EndGameUI.Instance.ShowEndGameScreen(survivorsWin);
        }
    }
}