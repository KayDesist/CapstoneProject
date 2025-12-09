using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Unity.Netcode;

public class GameHUDManager : MonoBehaviour
{
    public static GameHUDManager Instance;

    [Header("Persistent Role Display")]
    [SerializeField] private GameObject persistentRoleDisplay;
    [SerializeField] private TMP_Text persistentRoleText;
    [SerializeField] private Image persistentRoleIcon;

    [Header("Health & Stamina Panel")]
    [SerializeField] private GameObject healthStaminaPanel;
    [SerializeField] private Slider healthBar;
    [SerializeField] private Slider staminaBar;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text staminaText;

    [Header("Task Panel")]
    [SerializeField] private GameObject taskPanel;
    [SerializeField] private TMP_Text totalTasksText;
    [SerializeField] private TMP_Text tasksText;

    [Header("Interaction UI")]
    [SerializeField] private GameObject interactionPanel;
    [SerializeField] private TMP_Text interactionText;
    [SerializeField] private Slider interactionProgressBar;

    [Header("Icons")]
    [SerializeField] private Sprite survivorIcon;
    [SerializeField] private Sprite cultistIcon;

    private List<string> survivorTaskList = new List<string>();
    private List<string> cultistTaskList = new List<string>();
    private List<string> currentTasks = new List<string>();
    private RoleManager.PlayerRole currentRole;

    // Reference to local player's health component
    private PlayerHealth localPlayerHealth;
    private NetworkPlayerController localPlayerController;

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

    private void Start()
    {
        Debug.Log("GameHUDManager started");

        // Initialize UI state - hide all panels initially
        if (persistentRoleDisplay != null) persistentRoleDisplay.SetActive(false);
        if (healthStaminaPanel != null) healthStaminaPanel.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(false);

        // CRITICAL FIX: Ensure interaction panel is ALWAYS hidden at start
        HideInteractionPrompt();
        HideInteractionProgress();

        // Initialize with default values
        UpdateHealth(100, 100);
        UpdateStamina(100, 100);

        // Start checking for role assignment
        StartCoroutine(InitializeWithDelay());
    }

    private IEnumerator InitializeWithDelay()
    {
        yield return new WaitForSeconds(1f);

        // Check if we already have a role
        if (RoleManager.Instance != null)
        {
            currentRole = RoleManager.Instance.GetLocalPlayerRole();
            if (currentRole != RoleManager.PlayerRole.Survivor || RoleManager.Instance.IsCultist(NetworkManager.Singleton.LocalClientId))
            {
                Debug.Log($"Found existing role: {currentRole}");
                OnRoleAssigned(currentRole);
            }
        }

        // Find local player health
        StartCoroutine(FindLocalPlayerHealth());
    }

    private IEnumerator FindLocalPlayerHealth()
    {
        int attempts = 0;
        while (localPlayerHealth == null && attempts < 10)
        {
            attempts++;

            // Find the local player
            var playerControllers = FindObjectsOfType<NetworkPlayerController>();
            foreach (var controller in playerControllers)
            {
                if (controller.IsOwner)
                {
                    localPlayerController = controller;
                    localPlayerHealth = controller.GetComponent<PlayerHealth>();

                    if (localPlayerHealth != null)
                    {
                        Debug.Log($"Found local player health component for client {NetworkManager.Singleton.LocalClientId}");
                        // Subscribe to health changes
                        SubscribeToHealthChanges();
                        break;
                    }
                }
            }

            if (localPlayerHealth == null)
            {
                Debug.Log($"Attempt {attempts}: Could not find local player health, trying again...");
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (localPlayerHealth == null)
        {
            Debug.LogWarning("Could not find local player health component after 10 attempts");
        }
    }

    private void SubscribeToHealthChanges()
    {
        // We'll need to modify PlayerHealth to expose events or NetworkVariable callbacks
        // For now, we'll update UI in Update() by polling
        Debug.Log("Subscribed to local player health changes");
    }

    public void OnRoleAssigned(RoleManager.PlayerRole role)
    {
        Debug.Log($"GameHUDManager: Role assigned - {role}");

        currentRole = role;
        SetupRoleSpecificHUD();
        ShowTemporaryRoleDisplay();
    }

    private void SetupRoleSpecificHUD()
    {
        Debug.Log($"Setting up HUD for role: {currentRole}");

        Sprite roleIcon = null;
        string roleName = "";

        switch (currentRole)
        {
            case RoleManager.PlayerRole.Survivor:
                roleName = "SURVIVOR";
                roleIcon = survivorIcon;
                SetupSurvivorTasks();
                break;

            case RoleManager.PlayerRole.Cultist:
                roleName = "CULTIST";
                roleIcon = cultistIcon;
                SetupCultistTasks();
                break;
        }

        // Update persistent display
        if (persistentRoleText != null) persistentRoleText.text = roleName;
        if (persistentRoleIcon != null && roleIcon != null) persistentRoleIcon.sprite = roleIcon;
    }

    private void ShowTemporaryRoleDisplay()
    {
        // Show the temporary role display using RoleDisplayUI
        if (RoleDisplayUI.Instance != null)
        {
            RoleDisplayUI.Instance.ShowRole(currentRole);
            Debug.Log("Temporary role display shown via RoleDisplayUI");

            // Start coroutine to show persistent HUD after temporary display is hidden
            StartCoroutine(ShowPersistentHUDAfterDelay(3f));
        }
        else
        {
            Debug.LogWarning("RoleDisplayUI.Instance is null! Showing persistent HUD immediately");
            ShowPersistentHUD();
        }
    }

    private IEnumerator ShowPersistentHUDAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowPersistentHUD();
    }

    private void ShowPersistentHUD()
    {
        // Show all persistent HUD elements
        if (persistentRoleDisplay != null)
        {
            persistentRoleDisplay.SetActive(true);
            Debug.Log("Persistent role display shown");
        }

        if (healthStaminaPanel != null)
        {
            healthStaminaPanel.SetActive(true);
            Debug.Log("Health/stamina panel shown");
        }

        if (taskPanel != null)
        {
            taskPanel.SetActive(true);
            Debug.Log("Task panel shown");
        }

        // FIX: CRITICAL - Ensure interaction panel remains hidden when showing persistent HUD
        HideInteractionPrompt();
        HideInteractionProgress();
    }

    // NEW: Update method to poll health and stamina values
    private void Update()
    {
        if (localPlayerHealth != null)
        {
            // Update health UI
            UpdateHealth(localPlayerHealth.GetCurrentHealth(), localPlayerHealth.maxHealth);

            // Update stamina UI
            UpdateStamina(localPlayerHealth.GetStamina(), localPlayerHealth.maxStamina);
        }
    }

    // NEW: Special method to show HUD for end game (for dead players)
    public void ShowHUDForEndGame()
    {
        Debug.Log("Showing HUD for end game");

        // Make sure the gameObject is active
        gameObject.SetActive(true);

        // Show all HUD elements
        ShowPersistentHUD();
    }


    // ============ TASK MANAGEMENT ============
    private void SetupSurvivorTasks()
    {
        survivorTaskList.Clear();

        if (TaskManager.Instance != null)
        {
            var tasks = TaskManager.Instance.GetSurvivorTasksForUI();
            survivorTaskList.AddRange(tasks);
            currentTasks = new List<string>(survivorTaskList);
            UpdateTotalTasksText(0, currentTasks.Count);
        }
        else
        {
            // Fallback tasks
            survivorTaskList.AddRange(new string[] {
                "- Repair Generator (0/3)",
                "- Collect Firewood (0/5)",
                "- Find Car Keys (0/1)"
            });
            currentTasks = new List<string>(survivorTaskList);
            UpdateTotalTasksText(0, currentTasks.Count);
        }

        UpdateTasksText();
        Debug.Log($"Setup {survivorTaskList.Count} survivor tasks");
    }

    private void SetupCultistTasks()
    {
        cultistTaskList.Clear();

        if (TaskManager.Instance != null)
        {
            var tasks = TaskManager.Instance.GetCultistTasksForUI();
            cultistTaskList.AddRange(tasks);
            currentTasks = new List<string>(cultistTaskList);
            UpdateTotalTasksText(0, currentTasks.Count);
        }
        else
        {
            // Fallback tasks
            cultistTaskList.AddRange(new string[] {
                "- Place Ritual Candles (0/3)",
                "- Collect Sacrificial Items (0/2)",
                "- Activate Altars (0/2)"
            });
            currentTasks = new List<string>(cultistTaskList);
            UpdateTotalTasksText(0, currentTasks.Count);
        }

        UpdateTasksText();
        Debug.Log($"Setup {cultistTaskList.Count} cultist tasks");
    }

    private void UpdateTasksText()
    {
        if (tasksText != null)
        {
            tasksText.text = string.Join("\n", currentTasks);
        }
    }

    // ============ HEALTH & STAMINA ============
    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.Round(currentHealth)}/{maxHealth}";
        }
    }

    public void UpdateStamina(float currentStamina, float maxStamina)
    {
        if (staminaBar != null)
        {
            staminaBar.maxValue = maxStamina;
            staminaBar.value = currentStamina;
        }

        if (staminaText != null)
        {
            staminaText.text = $"{Mathf.Round(currentStamina)}/{maxStamina}";
        }
    }

    // ============ TASK PROGRESS ============
    public void UpdateTaskProgress(int taskIndex, string newStatus)
    {
        Debug.Log($"Updating task {taskIndex}, status: {newStatus}");

        // Update the appropriate task list based on current role
        if (currentRole == RoleManager.PlayerRole.Survivor)
        {
            if (taskIndex >= 0 && taskIndex < survivorTaskList.Count)
            {
                survivorTaskList[taskIndex] = newStatus;
                currentTasks = new List<string>(survivorTaskList);
                Debug.Log($"Updated survivor task {taskIndex}");
            }
            else
            {
                Debug.LogWarning($"Invalid survivor task index: {taskIndex}");
            }
        }
        else if (currentRole == RoleManager.PlayerRole.Cultist)
        {
            if (taskIndex >= 0 && taskIndex < cultistTaskList.Count)
            {
                cultistTaskList[taskIndex] = newStatus;
                currentTasks = new List<string>(cultistTaskList);
                Debug.Log($"Updated cultist task {taskIndex}");
            }
            else
            {
                Debug.LogWarning($"Invalid cultist task index: {taskIndex}");
            }
        }
        else
        {
            Debug.Log($"Skipping task update - Role: {currentRole}");
            return;
        }

        UpdateTasksText();
        UpdateTotalTasksCompleted();
        Debug.Log($"Task {taskIndex} updated successfully");
    }

    private void UpdateTotalTasksCompleted()
    {
        int completed = 0;
        foreach (var task in currentTasks)
        {
            if (task.Contains("✓"))
            {
                completed++;
            }
        }

        UpdateTotalTasksText(completed, currentTasks.Count);
    }

    private void UpdateTotalTasksText(int completed, int total)
    {
        if (totalTasksText != null)
        {
            if (currentRole == RoleManager.PlayerRole.Survivor)
            {
                totalTasksText.text = $"{completed}/{total} Tasks Complete";
            }
            else
            {
                totalTasksText.text = $"{completed}/{total} Objectives Complete";
            }
        }
    }

    // ============ INTERACTION UI METHODS ============
    public void ShowInteractionPrompt(string promptText)
    {
        if (interactionPanel != null && interactionText != null)
        {
            interactionText.text = promptText;
            interactionPanel.SetActive(true);
            Debug.Log($"Showing interaction prompt: {promptText}");
        }
    }

    public void HideInteractionPrompt()
    {
        if (interactionPanel != null)
        {
            interactionPanel.SetActive(false);
            Debug.Log("Hiding interaction prompt");
        }

        if (interactionProgressBar != null)
        {
            interactionProgressBar.gameObject.SetActive(false);
            interactionProgressBar.value = 0f; // Reset progress
        }
    }

    public void ShowInteractionProgress(float progress, float maxProgress)
    {
        if (interactionProgressBar != null)
        {
            if (!interactionProgressBar.gameObject.activeSelf)
            {
                interactionProgressBar.gameObject.SetActive(true);
            }
            interactionProgressBar.maxValue = maxProgress;
            interactionProgressBar.value = progress;
        }
    }

    public void HideInteractionProgress()
    {
        if (interactionProgressBar != null)
        {
            interactionProgressBar.gameObject.SetActive(false);
            interactionProgressBar.value = 0f;
        }
    }

    // FIX: Reset method to clean up UI state
    public void ResetHUD()
    {
        HideInteractionPrompt();
        HideInteractionProgress();

        if (persistentRoleDisplay != null) persistentRoleDisplay.SetActive(false);
        if (healthStaminaPanel != null) healthStaminaPanel.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(false);

        Debug.Log("HUD fully reset");
    }

    public static void ResetInstance()
    {
        if (Instance != null)
        {
            // Reset HUD state before destroying
            Instance.ResetHUD();
            Destroy(Instance.gameObject);
            Instance = null;
            Debug.Log("GameHUDManager instance reset");
        }
    }

    // ============ TESTING METHODS ============
    [ContextMenu("Test Role Assignment - Survivor")]
    private void TestSurvivorRole()
    {
        OnRoleAssigned(RoleManager.PlayerRole.Survivor);
    }

    [ContextMenu("Test Role Assignment - Cultist")]
    private void TestCultistRole()
    {
        OnRoleAssigned(RoleManager.PlayerRole.Cultist);
    }

    [ContextMenu("Test Health Update")]
    private void TestHealthUpdate()
    {
        UpdateHealth(75, 100);
    }

    [ContextMenu("Test Task Complete")]
    private void TestTaskComplete()
    {
        if (currentTasks.Count > 0)
        {
            currentTasks[0] = "- Repair Generator: DONE";
            UpdateTasksText();
            UpdateTotalTasksText(1, currentTasks.Count);
        }
    }

    [ContextMenu("Test Interaction Prompt")]
    private void TestInteractionPrompt()
    {
        ShowInteractionPrompt("Press E to interact");
    }

    [ContextMenu("Test Interaction Progress")]
    private void TestInteractionProgress()
    {
        ShowInteractionProgress(1.5f, 3f);
    }

    [ContextMenu("Show Persistent HUD")]
    private void TestShowPersistentHUD()
    {
        ShowPersistentHUD();
    }

    [ContextMenu("Debug Current Tasks")]
    private void DebugCurrentTasks()
    {
        Debug.Log("=== CURRENT TASKS ===");
        for (int i = 0; i < currentTasks.Count; i++)
        {
            Debug.Log($"Task {i}: {currentTasks[i]}");
        }
    }
}