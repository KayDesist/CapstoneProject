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

        Debug.Log("PlayerSpectator initialized - Press Q/E to spectate when dead, Space to exit");
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
            return;
        }

        isSpectating = true;
        currentSpectateIndex = 0;

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

        // Spectate the first player
        SpectatePlayer(currentSpectateIndex);

        Debug.Log($"Started spectating. Players available: {spectatablePlayers.Count}");
        ShowSpectateHint();
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
        HideSpectateHint();
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

        // Find the player object
        if (NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(targetPlayerId) != null)
        {
            NetworkObject targetPlayerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(targetPlayerId);
            currentSpectatedPlayer = targetPlayerObj.GetComponent<NetworkPlayerController>();

            if (currentSpectatedPlayer != null)
            {
                // Reset camera angles to face the new target
                currentYaw = currentSpectatedPlayer.transform.eulerAngles.y;
                currentPitch = 15f;

                Debug.Log($"Now spectating player {targetPlayerId}");
                UpdateSpectateHint();
            }
        }
        else
        {
            // Player no longer exists, update list
            UpdateSpectatablePlayers();
            if (spectatablePlayers.Count > 0)
            {
                currentSpectateIndex = Mathf.Clamp(currentSpectateIndex, 0, spectatablePlayers.Count - 1);
                SpectatePlayer(currentSpectateIndex);
            }
            else
            {
                StopSpectating();
            }
        }
    }

    private void UpdateSpectatablePlayers()
    {
        spectatablePlayers.Clear();

        // Find all alive players except ourselves
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

        // If we lost our current target, reset index
        if (currentSpectateIndex >= spectatablePlayers.Count)
        {
            currentSpectateIndex = spectatablePlayers.Count - 1;
        }
    }

    // UI Methods
    private void ShowSpectateHint()
    {
        if (GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.ShowInteractionPrompt("Spectating - Q/Previous | E/Next | Space/Exit | Right Mouse: Look Around | Scroll: Zoom");
        }
    }

    private void UpdateSpectateHint()
    {
        if (isSpectating && GameHUDManager.Instance != null)
        {
            string playerInfo = currentSpectatedPlayer != null ?
                $"Player {spectatablePlayers[currentSpectateIndex]}" : "No Player";
            GameHUDManager.Instance.ShowInteractionPrompt($"Spectating: {playerInfo} - Q/Previous | E/Next | Space/Exit");
        }
    }

    private void HideSpectateHint()
    {
        if (GameHUDManager.Instance != null)
        {
            GameHUDManager.Instance.HideInteractionPrompt();
        }
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

    // Debug info in game view
    private void OnGUI()
    {
        if (isSpectating && currentSpectatedPlayer != null)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label("SPECTATOR MODE");
            GUILayout.Label($"Spectating: Player {spectatablePlayers[currentSpectateIndex]}");
            GUILayout.Label("Controls:");
            GUILayout.Label("Q - Previous Player");
            GUILayout.Label("E - Next Player");
            GUILayout.Label("Right Mouse - Look Around");
            GUILayout.Label("Mouse Wheel - Zoom");
            GUILayout.Label("Space - Exit Spectator");
            GUILayout.EndArea();
        }
    }
}