using UnityEngine;

public static class CrossSceneData
{
    public static string LobbyMode { get; set; } = "";
    public static string JoinCode { get; set; } = "";
    public static string PlayerName { get; set; } = "Player";
    public static int PlayerAvatarIndex { get; set; } = 0;
    public static int MaxPlayers { get; set; } = 10;
    public static float GameDuration { get; set; } = 600f;

    // Resets all data to default values
    public static void Reset()
    {
        LobbyMode = "";
        JoinCode = "";
        PlayerName = "Player";
        PlayerAvatarIndex = 0;
    }

    // Validates if the join code is not empty and has minimum length
    public static bool IsValidJoinCode()
    {
        return !string.IsNullOrEmpty(JoinCode) && JoinCode.Length >= 4;
    }

    // Validates if lobby mode is either Host or Client
    public static bool IsValidLobbyMode()
    {
        return LobbyMode == "Host" || LobbyMode == "Client";
    }

    // Logs current data for debugging purposes
    public static void LogCurrentData()
    {
        Debug.Log($"CrossSceneData: Mode={LobbyMode}, JoinCode={JoinCode}, PlayerName={PlayerName}");
    }
}