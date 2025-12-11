using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerSpectator : NetworkBehaviour
{
    public KeyCode spectatePreviousKey = KeyCode.Q;
    public KeyCode spectateNextKey = KeyCode.E;
    public KeyCode exitSpectateKey = KeyCode.Space;
    public Camera mainCamera;
    public Camera spectatorCamera;
    public float cameraHeight = 2f;
    public float cameraDistance = 3f;
    public float cameraSmoothness = 5f;
    public float rotationSpeed = 2f;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 60f;
    public bool showSpectatorGUI = true;
    private bool isSpectating = false;
    private int currentSpectateIndex = -1;
    private List<ulong> spectatablePlayers = new List<ulong>();
    private NetworkPlayerController currentSpectatedPlayer;
    private PlayerHealth playerHealth;
    private NetworkPlayerController playerController;
    private float currentYaw = 0f;
    private float currentPitch = 15f;
    private Vector3 cameraOffset;

    // Initializes on start
    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerController = GetComponent<NetworkPlayerController>();
        if (mainCamera != null)
            mainCamera.enabled = true;
        if (spectatorCamera != null)
        {
            spectatorCamera.enabled = false;
            spectatorCamera.gameObject.SetActive(false);
        }
        if (!IsOwner)
        {
            enabled = false;
            return;
        }
    }

    // Updates every frame
    private void Update()
    {
        if (!IsOwner) return;
        if (playerHealth != null && !playerHealth.IsAlive())
        {
            if (!isSpectating && NetworkManager.Singleton.ConnectedClientsIds.Count > 2)
                StartCoroutine(DelayedStartSpectating());
            HandleSpectateInput();
            if (isSpectating)
                UpdateSpectatorCamera();
        }
        else if (isSpectating)
            StopSpectating();
    }

    // Delays start of spectating
    private System.Collections.IEnumerator DelayedStartSpectating()
    {
        yield return new WaitForSeconds(1f);
        if (!isSpectating && playerHealth != null && !playerHealth.IsAlive())
            StartSpectating();
    }

    // Handles spectate input
    private void HandleSpectateInput()
    {
        if (!isSpectating) return;
        if (Input.GetMouseButton(1))
        {
            currentYaw += Input.GetAxis("Mouse X") * rotationSpeed;
            currentPitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
            currentPitch = Mathf.Clamp(currentPitch, minVerticalAngle, maxVerticalAngle);
        }
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
            cameraDistance = Mathf.Clamp(cameraDistance - scroll * 5f, 1f, 10f);
        if (Input.GetKeyDown(spectatePreviousKey))
            SpectatePreviousPlayer();
        else if (Input.GetKeyDown(spectateNextKey))
            SpectateNextPlayer();
        if (Input.GetKeyDown(exitSpectateKey))
            StopSpectating();
    }

    // Updates spectator camera
    private void UpdateSpectatorCamera()
    {
        if (currentSpectatedPlayer == null || spectatorCamera == null) return;
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 desiredOffset = rotation * new Vector3(0, 0, -cameraDistance);
        desiredOffset.y += cameraHeight;
        cameraOffset = Vector3.Lerp(cameraOffset, desiredOffset, cameraSmoothness * Time.deltaTime);
        spectatorCamera.transform.position = currentSpectatedPlayer.transform.position + cameraOffset;
        spectatorCamera.transform.LookAt(currentSpectatedPlayer.transform.position + Vector3.up * cameraHeight * 0.5f);
    }

    // Starts spectating
    public void StartSpectating()
    {
        if (isSpectating) return;
        UpdateSpectatablePlayers();
        if (spectatablePlayers.Count == 0)
            return;
        isSpectating = true;
        currentSpectateIndex = 0;
        EnterSpectatorMode();
        SpectatePlayer(currentSpectateIndex);
    }

    // Enters spectator mode
    private void EnterSpectatorMode()
    {
        if (mainCamera != null)
            mainCamera.enabled = false;
        if (spectatorCamera != null)
        {
            spectatorCamera.enabled = true;
            spectatorCamera.gameObject.SetActive(true);
        }
        currentYaw = 0f;
        currentPitch = 15f;
        cameraDistance = 3f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Updates list of spectatable players
    private void UpdateSpectatablePlayers()
    {
        spectatablePlayers.Clear();
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (NetworkPlayerController player in allPlayers)
        {
            if (player.IsOwner) continue;
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null && health.IsAlive() && player.NetworkObject != null)
                spectatablePlayers.Add(player.NetworkObject.OwnerClientId);
        }
    }

    // Stops spectating
    public void StopSpectating()
    {
        if (!isSpectating) return;
        isSpectating = false;
        currentSpectateIndex = -1;
        if (mainCamera != null)
            mainCamera.enabled = true;
        if (spectatorCamera != null)
        {
            spectatorCamera.enabled = false;
            spectatorCamera.gameObject.SetActive(false);
        }
        currentSpectatedPlayer = null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Spectates previous player
    private void SpectatePreviousPlayer()
    {
        if (!isSpectating || spectatablePlayers.Count == 0) return;
        currentSpectateIndex--;
        if (currentSpectateIndex < 0)
            currentSpectateIndex = spectatablePlayers.Count - 1;
        SpectatePlayer(currentSpectateIndex);
    }

    // Spectates next player
    private void SpectateNextPlayer()
    {
        if (!isSpectating || spectatablePlayers.Count == 0) return;
        currentSpectateIndex++;
        if (currentSpectateIndex >= spectatablePlayers.Count)
            currentSpectateIndex = 0;
        SpectatePlayer(currentSpectateIndex);
    }

    // Spectates specific player
    private void SpectatePlayer(int index)
    {
        if (index < 0 || index >= spectatablePlayers.Count) return;
        ulong targetPlayerId = spectatablePlayers[index];
        NetworkPlayerController[] allPlayers = FindObjectsOfType<NetworkPlayerController>();
        foreach (NetworkPlayerController player in allPlayers)
        {
            if (player.NetworkObject != null && player.NetworkObject.OwnerClientId == targetPlayerId)
            {
                currentSpectatedPlayer = player;
                currentYaw = currentSpectatedPlayer.transform.eulerAngles.y;
                currentPitch = 15f;
                return;
            }
        }
        UpdateSpectatablePlayers();
        if (spectatablePlayers.Count > 0)
        {
            currentSpectateIndex = Mathf.Clamp(currentSpectateIndex, 0, spectatablePlayers.Count - 1);
            SpectatePlayer(currentSpectateIndex);
        }
    }

    // Draws spectator GUI
    private void OnGUI()
    {
        if (EndGameUI.Instance != null && EndGameUI.Instance.endGamePanel != null &&
            EndGameUI.Instance.endGamePanel.activeInHierarchy)
            return;
        if (!isSpectating || !showSpectatorGUI || !IsOwner) return;
        int xPos = 20;
        int yPos = 20;
        int width = 320;
        int lineHeight = 22;
        int totalLines = 9;
        int totalHeight = (totalLines * lineHeight) + 20;
        Texture2D backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
        backgroundTexture.Apply();
        GUIStyle backgroundStyle = new GUIStyle();
        backgroundStyle.normal.background = backgroundTexture;
        GUI.Box(new Rect(xPos - 10, yPos - 10, width + 20, totalHeight), "", backgroundStyle);
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "SPECTATOR MODE", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14, fontStyle = FontStyle.Bold });
        yPos += lineHeight;
        string playerInfo = currentSpectatedPlayer != null ? $"Player {spectatablePlayers[currentSpectateIndex]}" : "Free Camera";
        GUI.Label(new Rect(xPos, yPos, width, lineHeight), "Spectating: ", new GUIStyle { normal = { textColor = Color.white }, fontSize = 14 });
        GUI.Label(new Rect(xPos + 85, yPos, width, lineHeight), playerInfo, new GUIStyle { normal = { textColor = Color.yellow }, fontSize = 14 });
        yPos += lineHeight;
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
        DestroyImmediate(backgroundTexture);
    }

    // Handles player death
    public void HandlePlayerDeath()
    {
        OnPlayerDied();
    }

    // Called when player dies
    private void OnPlayerDied()
    {
        if (IsOwner && !isSpectating && NetworkManager.Singleton.ConnectedClientsIds.Count > 2)
            Invoke(nameof(StartSpectating), 1f);
    }

    // Called when object is destroyed
    private void OnDestroy()
    {
        if (isSpectating)
            StopSpectating();
    }
}