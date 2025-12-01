using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerSpectator : NetworkBehaviour
{
    [Header("Spectating Settings")]
    public KeyCode spectatePreviousKey = KeyCode.Q;
    public KeyCode spectateNextKey = KeyCode.E;
    public KeyCode exitSpectateKey = KeyCode.Space;

    [Header("Camera References")]
    public Camera mainCamera;           // Your normal first-person camera
    public Camera spectatorCamera;      // Dedicated spectating camera

    [Header("Spectator Camera Settings")]
    public float cameraHeight = 2f;
    public float cameraDistance = 3f;
    public float cameraSmoothness = 5f;
    public float rotationSpeed = 2f;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 60f;

    [Header("Spectator GUI Settings")]
    public bool showSpectatorGUI = true;
    public GUIStyle spectatorGUIStyle;

    private bool isSpectating = false;
    private int currentSpectateIndex = -1;
    private List<ulong> spectatablePlayers = new List<ulong>();
    private NetworkPlayerController currentSpectatedPlayer;

    // References
    private PlayerHealth playerHealth;
    private NetworkPlayerController playerController;

    // Camera orbit variables
    private float currentYaw = 0f;
    private float currentPitch = 15f;
    private Vector3 cameraOffset;

    // GUI styles
    private GUIStyle whiteTextStyle;
    private GUIStyle boldWhiteTextStyle;
    private GUIStyle yellowTextStyle;

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerController = GetComponent<NetworkPlayerController>();

        // Ensure cameras are in correct initial state
        if (mainCamera != null)
            mainCamera.enabled = true;

        if (spectatorCamera != null)
        {
            spectatorCamera.enabled = false;
            spectatorCamera.gameObject.SetActive(false);
        }

        // Initialize GUI styles
        InitializeGUIStyles();

        // Only enable spectating for local player
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        Debug.Log("PlayerSpectator initialized - Press Q/E to spectate when dead, Space to exit");
    }

    private void InitializeGUIStyles()
    {
        // White text style (normal)
        whiteTextStyle = new GUIStyle();
        whiteTextStyle.normal.textColor = Color.white;
        whiteTextStyle.fontSize = 14;
        whiteTextStyle.richText = true;

        // Bold white text style
        boldWhiteTextStyle = new GUIStyle();
        boldWhiteTextStyle.normal.textColor = Color.white;
        boldWhiteTextStyle.fontSize = 14;
        boldWhiteTextStyle.fontStyle = FontStyle.Bold;
        boldWhiteTextStyle.richText = true;

        // Yellow text style for highlights
        yellowTextStyle = new GUIStyle();
        yellowTextStyle.normal.textColor = Color.yellow;
        yellowTextStyle.fontSize = 14;
        yellowTextStyle.richText = true;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Check if player is dead and can spectate
        if (playerHealth != null && !playerHealth.IsAlive())
        {
            HandleSpectateInput();

            // Update spectator camera position if spectating
            if (isSpectating)
            {
                UpdateSpectatorCamera();
            }
        }
        else if (isSpectating)
        {
            // If player respawned or is alive, stop spectating
            StopSpectating();
        }
    }

    private void HandleSpectateInput()
    {
        // Start spectating if not already
        if (!isSpectating && (Input.GetKeyDown(spectatePreviousKey) || Input.GetKeyDown(spectateNextKey)))
        {
            StartSpectating();
            return;
        }

        if (!isSpectating) return;

        // Handle camera rotation when right mouse button is held
        if (Input.GetMouseButton(1))
        {
            currentYaw += Input.GetAxis("Mouse X") * rotationSpeed;
            currentPitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
            currentPitch = Mathf.Clamp(currentPitch, minVerticalAngle, maxVerticalAngle);
        }

        // Mouse wheel to adjust distance
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            cameraDistance = Mathf.Clamp(cameraDistance - scroll * 5f, 1f, 10f);
        }

        // Cycle through players
        if (Input.GetKeyDown(spectatePreviousKey))
        {
            SpectatePreviousPlayer();
        }
        else if (Input.GetKeyDown(spectateNextKey))
        {
            SpectateNextPlayer();
        }

        // Exit spectating
        if (Input.GetKeyDown(exitSpectateKey))
        {
            StopSpectating();
        }
    }

    private void UpdateSpectatorCamera()
    {
        if (currentSpectatedPlayer == null || spectatorCamera == null) return;

        // Calculate camera position based on orbital angles
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 desiredOffset = rotation * new Vector3(0, 0, -cameraDistance);
        desiredOffset.y += cameraHeight;

        // Smoothly interpolate camera position
        cameraOffset = Vector3.Lerp(cameraOffset, desiredOffset, cameraSmoothness * Time.deltaTime);

        // Set camera position to look at the spectated player
        spectatorCamera.transform.position = currentSpectatedPlayer.transform.position + cameraOffset;
        spectatorCamera.transform.LookAt(currentSpectatedPlayer.transform.position + Vector3.up * cameraHeight * 0.5f);
    }

    private void StartSpectating()
    {
        if (isSpectating) return;

        UpdateSpectatablePlayers();

        if (spectatablePlayers.Count == 0)
        {
            Debug.Log("No players available to spectate");
            // Still allow entering spectator mode even if no players
            EnterSpectatorMode();
            return;
        }

        isSpectating = true;
        currentSpectateIndex = 0;

        EnterSpectatorMode();

        // Spectate the first player
        SpectatePlayer(currentSpectateIndex);

        Debug.Log($"Started spectating. Players available: {spectatablePlayers.Count}");
    }

    private void EnterSpectatorMode()
    {
        // Switch cameras
        if (mainCamera != null)
            mainCamera.enabled = false;

        if (spectatorCamera != null)
        {
            spectatorCamera.enabled = true;
            spectatorCamera.gameObject.SetActive(true);
        }

        // Reset camera angles
        currentYaw = 0f;
        currentPitch = 15f;
        cameraDistance = 3f;

        // Show spectator UI through GameHUDManager (optional)
        if (GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.ShowSpectatorUI();
            GameHUDManager.Instance.HideGameHUDForSpectator();
        }

        // Ensure cursor is unlocked for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Entered spectator mode - GUI should be visible");
    }

    private void UpdateSpectatablePlayers()
    {
        spectatablePlayers.Clear();

        // Only server can access other players' objects directly
        if (!IsServer)
        {
            // For clients, use a scene-based approach
            FindPlayersInScene();
            return;
        }

        // Server can use the direct method
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // Skip ourselves
            if (clientId == OwnerClientId) continue;

            NetworkObject playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObj != null)
            {
                PlayerHealth health = playerObj.GetComponent<PlayerHealth>();
                if (health != null && health.IsAlive())
                {
                    spectatablePlayers.Add(clientId);
                }
            }
        }

        Debug.Log($"Server updated spectatable players: {spectatablePlayers.Count} players available");
    }

    // Client-side method to find players
    private void FindPlayersInScene()
    {
        spectatablePlayers.Clear();

        Debug.Log("Client searching for players in scene...");

        // Find all NetworkPlayerController objects in the scene
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();

        foreach (NetworkPlayerController player in allPlayers)
        {
            // Skip ourselves
            if (player.IsOwner) continue;

            // Check if player is alive
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null && health.IsAlive() && player.NetworkObject != null)
            {
                spectatablePlayers.Add(player.NetworkObject.OwnerClientId);
                Debug.Log($"Found alive player: {player.NetworkObject.OwnerClientId}");
            }
        }

        Debug.Log($"Client found {spectatablePlayers.Count} players in scene");
    }

    public void StopSpectating()
    {
        if (!isSpectating) return;

        isSpectating = false;
        currentSpectateIndex = -1;

        // Switch back to main camera
        if (mainCamera != null)
            mainCamera.enabled = true;

        if (spectatorCamera != null)
        {
            spectatorCamera.enabled = false;
            spectatorCamera.gameObject.SetActive(false);
        }

        // Hide spectator UI through GameHUDManager (optional)
        if (GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.HideSpectatorUI();
            GameHUDManager.Instance.RestoreGameHUDAfterSpectator();
        }

        // Clear current spectated player
        currentSpectatedPlayer = null;

        Debug.Log("Stopped spectating");
    }

    private void SpectatePreviousPlayer()
    {
        if (!isSpectating || spectatablePlayers.Count == 0) return;

        currentSpectateIndex--;
        if (currentSpectateIndex < 0)
            currentSpectateIndex = spectatablePlayers.Count - 1;

        SpectatePlayer(currentSpectateIndex);
    }

    private void SpectateNextPlayer()
    {
        if (!isSpectating || spectatablePlayers.Count == 0) return;

        currentSpectateIndex++;
        if (currentSpectateIndex >= spectatablePlayers.Count)
            currentSpectateIndex = 0;

        SpectatePlayer(currentSpectateIndex);
    }

    private void SpectatePlayer(int index)
    {
        if (index < 0 || index >= spectatablePlayers.Count) return;

        ulong targetPlayerId = spectatablePlayers[index];
        Debug.Log($"Attempting to spectate player: {targetPlayerId}");

        // FIXED: Use different approach for server vs client
        NetworkObject targetPlayerObj = null;

        if (IsServer)
        {
            // Server can use the direct method
            targetPlayerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(targetPlayerId);
        }
        else
        {
            // Client needs to search in scene
            NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
            foreach (NetworkPlayerController player in allPlayers)
            {
                if (player.NetworkObject != null && player.NetworkObject.OwnerClientId == targetPlayerId)
                {
                    targetPlayerObj = player.NetworkObject;
                    break;
                }
            }
        }

        if (targetPlayerObj != null)
        {
            currentSpectatedPlayer = targetPlayerObj.GetComponent<NetworkPlayerController>();

            if (currentSpectatedPlayer != null)
            {
                // Reset camera angles to face the new target
                currentYaw = currentSpectatedPlayer.transform.eulerAngles.y;
                currentPitch = 15f;

                Debug.Log($"Now spectating player {targetPlayerId}");
            }
            else
            {
                Debug.LogError($"Found player object but no NetworkPlayerController component for {targetPlayerId}");
            }
        }
        else
        {
            Debug.LogWarning($"Player {targetPlayerId} not found in scene, updating player list...");

            // Player no longer exists, update list
            UpdateSpectatablePlayers();

            if (spectatablePlayers.Count > 0)
            {
                currentSpectateIndex = Mathf.Clamp(currentSpectateIndex, 0, spectatablePlayers.Count - 1);
                SpectatePlayer(currentSpectateIndex);
            }
            else
            {
                // No players left to spectate, but stay in spectator mode
            }
        }
    }

    // ============ PERMANENT SPECTATOR GUI ============
    private void OnGUI()
    {
        // Don't draw spectator GUI if end game UI is active
        // FIXED: Use public field check
        if (EndGameUI.Instance != null && EndGameUI.Instance.endGamePanel != null &&
            EndGameUI.Instance.endGamePanel.activeInHierarchy)
        {
            return;
        }

        if (!isSpectating || !showSpectatorGUI || !IsOwner) return;

        // Top-left corner position
        int xPos = 20;
        int yPos = 20;
        int width = 320;
        int lineHeight = 22;

        // Calculate total height based on content
        int totalLines = 9;
        int totalHeight = (totalLines * lineHeight) + 20;

        // Create a semi-transparent background
        Texture2D backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
        backgroundTexture.Apply();

        GUIStyle backgroundStyle = new GUIStyle();
        backgroundStyle.normal.background = backgroundTexture;

        GUI.Box(new Rect(xPos - 10, yPos - 10, width + 20, totalHeight), "", backgroundStyle);

        // SPECTATOR GUI CONTENT - ALL WHITE TEXT
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "SPECTATOR MODE", boldWhiteTextStyle);
        yPos += lineHeight;

        // Current player being spectated
        string playerInfo = currentSpectatedPlayer != null ?
            $"Player {spectatablePlayers[currentSpectateIndex]}" : "Free Camera";
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Spectating: ", whiteTextStyle);
        GUI.Label(new Rect(xPos + 85, yPos, width, lineHeight), playerInfo, yellowTextStyle);
        yPos += lineHeight;

        // Player list info
        if (spectatablePlayers.Count > 0)
        {
            GUI.Label(new Rect(xPos, yPos, width, lineHeight), $"Players: {currentSpectateIndex + 1}/{spectatablePlayers.Count}", whiteTextStyle);
            yPos += lineHeight;
        }
        else
        {
            GUI.Label(new Rect(xPos, yPos, width, lineHeight), "No players available", whiteTextStyle);
            yPos += lineHeight;
        }

        yPos += 10; // Spacer

        // Controls
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Controls:", boldWhiteTextStyle);
        yPos += lineHeight;
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Q - Previous Player", whiteTextStyle);
        yPos += lineHeight;
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "E - Next Player", whiteTextStyle);
        yPos += lineHeight;
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Right Mouse - Look Around", whiteTextStyle);
        yPos += lineHeight;
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Mouse Wheel - Zoom", whiteTextStyle);
        yPos += lineHeight;
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Space - Exit Spectator", whiteTextStyle);

        // Clean up the texture to prevent memory leaks
        DestroyImmediate(backgroundTexture);
    }

    // NEW: Method to toggle spectator GUI
    public void ToggleSpectatorGUI(bool show)
    {
        showSpectatorGUI = show;

        // Also hide the GameHUDManager spectator UI if it exists
        if (GameHUDManager.Instance != null)
        {
            if (!show)
            {
                GameHUDManager.Instance.HideSpectatorUI();
            }
        }

        Debug.Log($"Spectator GUI {(show ? "shown" : "hidden")}");
    }

    // Public methods
    public bool IsSpectating()
    {
        return isSpectating;
    }

    public NetworkPlayerController GetSpectatedPlayer()
    {
        return currentSpectatedPlayer;
    }

    // Handle player death automatically
    public void OnPlayerDied()
    {
        // Auto-start spectating when player dies
        if (IsOwner && !isSpectating)
        {
            Debug.Log("Player died - auto-starting spectating in 1 second");
            // Small delay to let death sequence complete
            Invoke(nameof(StartSpectating), 1f);
        }
    }

    // Call this from PlayerHealth when player dies
    public void HandlePlayerDeath()
    {
        OnPlayerDied();
    }

    private void OnDestroy()
    {
        // Clean up
        if (isSpectating)
        {
            StopSpectating();
        }
    }

    // Debug method to manually trigger spectating
    [ContextMenu("Start Spectating")]
    public void DebugStartSpectating()
    {
        if (IsOwner)
        {
            StartSpectating();
        }
    }

    [ContextMenu("Stop Spectating")]
    public void DebugStopSpectating()
    {
        if (IsOwner)
        {
            StopSpectating();
        }
    }

    [ContextMenu("Debug Player List")]
    public void DebugPlayerList()
    {
        Debug.Log($"=== SPECTATABLE PLAYERS ({spectatablePlayers.Count}) ===");
        foreach (var playerId in spectatablePlayers)
        {
            Debug.Log($"Player ID: {playerId}");
        }
    }

    [ContextMenu("Toggle Spectator GUI")]
    public void ToggleSpectatorGUI()
    {
        showSpectatorGUI = !showSpectatorGUI;
        Debug.Log($"Spectator GUI: {(showSpectatorGUI ? "ON" : "OFF")}");
    }
}