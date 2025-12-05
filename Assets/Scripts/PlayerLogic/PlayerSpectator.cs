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
    public Camera mainCamera;
    public Camera spectatorCamera;

    [Header("Spectator Camera Settings")]
    public float cameraHeight = 2f;
    public float cameraDistance = 3f;
    public float cameraSmoothness = 5f;
    public float rotationSpeed = 2f;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 60f;

    [Header("Spectator GUI")]
    public bool showSpectatorGUI = true;

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

        // Only enable spectating for local player
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        Debug.Log("PlayerSpectator initialized");
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
            // Check if there are enough players to spectate (more than 2 total players)
            if (NetworkManager.Singleton.ConnectedClientsIds.Count <= 2)
            {
                Debug.Log("Not enough players to spectate. Minimum 3 players required.");
                return;
            }

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

        // Calculate camera position
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 desiredOffset = rotation * new Vector3(0, 0, -cameraDistance);
        desiredOffset.y += cameraHeight;

        // Smoothly interpolate camera position
        cameraOffset = Vector3.Lerp(cameraOffset, desiredOffset, cameraSmoothness * Time.deltaTime);

        // Set camera position
        spectatorCamera.transform.position = currentSpectatedPlayer.transform.position + cameraOffset;
        spectatorCamera.transform.LookAt(currentSpectatedPlayer.transform.position + Vector3.up * cameraHeight * 0.5f);
    }

    private void StartSpectating()
    {
        if (isSpectating) return;

        UpdateSpectatablePlayers();

        // Check again for 2-player game
        if (spectatablePlayers.Count == 0)
        {
            Debug.Log("No other players alive to spectate.");
            return;
        }

        isSpectating = true;
        currentSpectateIndex = 0;

        EnterSpectatorMode();
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

        // Ensure cursor is unlocked
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Entered spectator mode");
    }

    private void UpdateSpectatablePlayers()
    {
        spectatablePlayers.Clear();

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
            }
        }

        Debug.Log($"Found {spectatablePlayers.Count} spectatable players");
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

        // Search for the player in the scene
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (NetworkPlayerController player in allPlayers)
        {
            if (player.NetworkObject != null && player.NetworkObject.OwnerClientId == targetPlayerId)
            {
                currentSpectatedPlayer = player;
                // Reset camera angles to face the new target
                currentYaw = currentSpectatedPlayer.transform.eulerAngles.y;
                currentPitch = 15f;
                Debug.Log($"Now spectating player {targetPlayerId}");
                return;
            }
        }

        Debug.LogWarning($"Player {targetPlayerId} not found, updating player list...");
        UpdateSpectatablePlayers();

        if (spectatablePlayers.Count > 0)
        {
            currentSpectateIndex = Mathf.Clamp(currentSpectateIndex, 0, spectatablePlayers.Count - 1);
            SpectatePlayer(currentSpectateIndex);
        }
    }

    // ============ SPECTATOR GUI ============
    private void OnGUI()
    {
        // Don't draw if end game UI is active
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

        // Calculate total height
        int totalLines = 9;
        int totalHeight = (totalLines * lineHeight) + 20;

        // Create background
        Texture2D backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
        backgroundTexture.Apply();

        GUIStyle backgroundStyle = new GUIStyle();
        backgroundStyle.normal.background = backgroundTexture;

        GUI.Box(new Rect(xPos - 10, yPos - 10, width + 20, totalHeight), "", backgroundStyle);

        // GUI CONTENT
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "SPECTATOR MODE", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14, fontStyle = FontStyle.Bold });
        yPos += lineHeight;

        // Current player being spectated
        string playerInfo = currentSpectatedPlayer != null ?
            $"Player {spectatablePlayers[currentSpectateIndex]}" : "Free Camera";
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Spectating: ", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14 });
        GUI.Label(new Rect(xPos + 85, yPos, width, lineHeight), playerInfo, new GUIStyle { normal = { textColor = Color.yellow }, fontSize = 14 });
        yPos += lineHeight;

        // Player list info
        if (spectatablePlayers.Count > 0)
        {
            GUI.Label(new Rect(xPos, yPos, width, lineHeight), $"Players: {currentSpectateIndex + 1}/{spectatablePlayers.Count}", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14 });
            yPos += lineHeight;
        }
        else
        {
            GUI.Label(new Rect(xPos, yPos, width, lineHeight), "No players available", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14 });
            yPos += lineHeight;
        }

        yPos += 10;

        // Controls
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Controls:", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14, fontStyle = FontStyle.Bold });
        yPos += lineHeight;
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Q - Previous Player", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14 });
        yPos += lineHeight;
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "E - Next Player", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14 });
        yPos += lineHeight;
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Right Mouse - Look Around", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14 });
        yPos += lineHeight;
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Mouse Wheel - Zoom", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14 });
        yPos += lineHeight;
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Space - Exit Spectator", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14 });

        // Clean up
        DestroyImmediate(backgroundTexture);
    }

    // ADDED BACK: This method is called by PlayerHealth when player dies
    public void HandlePlayerDeath()
    {
        OnPlayerDied();
    }

    // Auto-start spectating when player dies
    private void OnPlayerDied()
    {
        // Check if there are enough players to spectate (more than 2 total players)
        if (IsOwner && !isSpectating && NetworkManager.Singleton.ConnectedClientsIds.Count > 2)
        {
            Debug.Log("Player died - auto-starting spectating in 1 second");
            Invoke(nameof(StartSpectating), 1f);
        }
    }

    private void OnDestroy()
    {
        if (isSpectating)
        {
            StopSpectating();
        }
    }
}