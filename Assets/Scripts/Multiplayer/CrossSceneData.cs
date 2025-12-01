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
        Debug.Log($"Assigning character for client {clientId}");

        // Host (clientId 0) always gets Mizuki (index 2)
        if (clientId == 0)
        {
            Debug.Log($"Host assigned to Mizuki (index 2)");
            return 2;
        }

        // Other clients get assigned in order
        int[] characterOrder = { 1, 3, 0, 4 }; // Sam, Elijah, Jaxen, Clint
        int orderIndex = (int)clientId - 1;

        if (orderIndex < characterOrder.Length)
        {
            int charIndex = characterOrder[orderIndex];
            Debug.Log($"Client {clientId} assigned to {GetCharacterName(charIndex)} (index {charIndex})");
            return charIndex;
        }

        // Fallback: cycle through available characters if more than 5 players
        int fallbackIndex = orderIndex % 5;
        Debug.Log($"Client {clientId} assigned to {GetCharacterName(fallbackIndex)} (fallback index {fallbackIndex})");
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

    // Reset all data
    public static void Reset()
    {
        LobbyMode = "";
        JoinCode = "";
        PlayerName = "Player";
        PlayerAvatarIndex = 0;
        Debug.Log("CrossSceneData Reset");
    }

    // NEW: Enhanced validation methods
    public static bool IsValidJoinCode()
    {
        bool isValid = !string.IsNullOrEmpty(JoinCode) && JoinCode.Length >= 4;
        if (!isValid)
        {
            Debug.LogWarning($"Invalid JoinCode: '{JoinCode}'");
        }
        return isValid;
    }

    public static bool IsValidLobbyMode()
    {
        bool isValid = LobbyMode == "Host" || LobbyMode == "Client";
        if (!isValid)
        {
            Debug.LogWarning($"Invalid LobbyMode: '{LobbyMode}'");
        }
        return isValid;
    }

    
    public static bool ValidateClientSetup()
    {
        if (LobbyMode == "Client")
        {
            if (string.IsNullOrEmpty(JoinCode))
            {
                Debug.LogError("Client setup invalid: No JoinCode provided!");
                return false;
            }
            if (JoinCode.Length < 4)
            {
                Debug.LogError($"Client setup invalid: JoinCode too short: '{JoinCode}'");
                return false;
            }
        }
        return true;
    }

    // Debug helper
    public static void LogCurrentData()
    {
        Debug.Log($"=== CROSS SCENE DATA ===");
        Debug.Log($"LobbyMode: {LobbyMode}");
        Debug.Log($"JoinCode: {JoinCode}");
        Debug.Log($"PlayerName: {PlayerName}");
        Debug.Log($"IsValid: {ValidateClientSetup()}");
    }
}