// CrossSceneData.cs
using UnityEngine;

public static class CrossSceneData
{
    // Lobby connection data
    public static string LobbyMode { get; set; } = ""; // "Host" or "Client"
    public static string JoinCode { get; set; } = "";

    // Player preferences (optional - for future use)
    public static string PlayerName { get; set; } = "Player";
    public static int PlayerAvatarIndex { get; set; } = 0;

    // Game settings (optional - for future use)
    public static int MaxPlayers { get; set; } = 10;
    public static float GameDuration { get; set; } = 600f; // 10 minutes in seconds

    // Reset all data (useful when returning to main menu)
    public static void Reset()
    {
        LobbyMode = "";
        JoinCode = "";
        PlayerName = "Player";
        PlayerAvatarIndex = 0;
        // Don't reset game settings as they might be user preferences
    }

    // Validation methods
    public static bool IsValidJoinCode()
    {
        return !string.IsNullOrEmpty(JoinCode) && JoinCode.Length >= 4;
    }

    public static bool IsValidLobbyMode()
    {
        return LobbyMode == "Host" || LobbyMode == "Client";
    }

    // Debug helper
    public static void LogCurrentData()
    {
        Debug.Log($"CrossSceneData: Mode={LobbyMode}, JoinCode={JoinCode}, PlayerName={PlayerName}");
    }
}