using Unity.Netcode;
using UnityEngine;

public static class CrossSceneData
{
    // Lobby connection data
    public static string LobbyMode { get; set; } = ""; // "Host" or "Client"
    public static string JoinCode { get; set; } = "";

    // Player preferences
    public static string PlayerName { get; set; } = "Player";
    public static int PlayerAvatarIndex { get; set; } = 0;

    // Character assignments
    public static int GetCharacterIndexForClient(ulong clientId)
    {
        Debug.Log($"[CrossSceneData] Assigning character for client {clientId}");

        // Host (clientId 0) always gets Mizuki (index 2)
        if (clientId == 0)
        {
            Debug.Log($"[CrossSceneData] Host assigned to Mizuki (index 2)");
            return 2;
        }

        // Other clients get assigned in order
        int[] characterOrder = { 1, 3, 0, 4 }; // Sam, Elijah, Jaxen, Clint
        int orderIndex = (int)clientId - 1;

        if (orderIndex < characterOrder.Length)
        {
            int charIndex = characterOrder[orderIndex];
            Debug.Log($"[CrossSceneData] Client {clientId} assigned to {GetCharacterName(charIndex)} (index {charIndex})");
            return charIndex;
        }

        // Fallback: cycle through available characters if more than 5 players
        int fallbackIndex = orderIndex % 5;
        Debug.Log($"[CrossSceneData] Client {clientId} assigned to {GetCharacterName(fallbackIndex)} (fallback index {fallbackIndex})");
        return fallbackIndex;
    }

    public static string GetCharacterName(int characterIndex)
    {
        switch (characterIndex)
        {
            case 0: return "Jaxen";
            case 1: return "Sam";
            case 2: return "Mizuki";
            case 3: return "Elijah";
            case 4: return "Clint";
            default: return "Unknown";
        }
    }

    // Game settings
    public static int MaxPlayers { get; set; } = 10;
    public static float GameDuration { get; set; } = 600f;

    // Connection validation
    public static bool IsConnectionValid()
    {
        if (string.IsNullOrEmpty(LobbyMode))
        {
            Debug.LogError("[CrossSceneData] LobbyMode is not set!");
            return false;
        }

        if (LobbyMode == "Host")
        {
            // Host should have a join code if they successfully created a lobby
            if (string.IsNullOrEmpty(JoinCode))
            {
                Debug.LogWarning("[CrossSceneData] Host doesn't have a join code - might be first setup");
                return true; // Still valid for initial host creation
            }
            return true;
        }
        else if (LobbyMode == "Client")
        {
            // Client must have a join code
            if (string.IsNullOrEmpty(JoinCode))
            {
                Debug.LogError("[CrossSceneData] Client doesn't have a join code!");
                return false;
            }

            if (JoinCode.Length < 4)
            {
                Debug.LogError($"[CrossSceneData] Client join code too short: {JoinCode}");
                return false;
            }

            return true;
        }
        else
        {
            Debug.LogError($"[CrossSceneData] Invalid LobbyMode: {LobbyMode}");
            return false;
        }
    }

    // Connection established check
    public static bool IsConnectionEstablished()
    {
        if (!IsConnectionValid())
            return false;

        // Additional check if we have NetworkManager running
        if (NetworkManager.Singleton != null)
        {
            if (LobbyMode == "Host" && !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[CrossSceneData] Host mode but not server - connection may be broken");
                return false;
            }

            if (LobbyMode == "Client" && !NetworkManager.Singleton.IsClient)
            {
                Debug.LogWarning("[CrossSceneData] Client mode but not client - connection may be broken");
                return false;
            }
        }

        return true;
    }

    // Reset all data
    public static void Reset()
    {
        LobbyMode = "";
        JoinCode = "";
        PlayerName = "Player";
        PlayerAvatarIndex = 0;
        Debug.Log("[CrossSceneData] All data reset");
    }

    // Enhanced validation methods
    public static bool IsValidJoinCode()
    {
        bool isValid = !string.IsNullOrEmpty(JoinCode) && JoinCode.Length >= 4;
        if (!isValid)
        {
            Debug.LogWarning($"[CrossSceneData] Invalid JoinCode: '{JoinCode}'");
        }
        return isValid;
    }

    public static bool IsValidLobbyMode()
    {
        bool isValid = LobbyMode == "Host" || LobbyMode == "Client";
        if (!isValid)
        {
            Debug.LogWarning($"[CrossSceneData] Invalid LobbyMode: '{LobbyMode}'");
        }
        return isValid;
    }

    // Debug helper
    public static void LogCurrentData()
    {
        Debug.Log($"=== CROSS SCENE DATA ===");
        Debug.Log($"LobbyMode: {LobbyMode}");
        Debug.Log($"JoinCode: {JoinCode}");
        Debug.Log($"PlayerName: {PlayerName}");
        Debug.Log($"IsValid: {IsConnectionValid()}");
        Debug.Log($"IsEstablished: {IsConnectionEstablished()}");

        if (NetworkManager.Singleton != null)
        {
            Debug.Log($"NetworkManager: Listening={NetworkManager.Singleton.IsListening}");
            Debug.Log($"IsServer: {NetworkManager.Singleton.IsServer}");
            Debug.Log($"IsClient: {NetworkManager.Singleton.IsClient}");
            Debug.Log($"IsHost: {NetworkManager.Singleton.IsHost}");
        }
    }
}