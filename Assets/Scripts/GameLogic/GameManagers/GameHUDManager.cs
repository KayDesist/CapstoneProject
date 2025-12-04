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

    [Header("Spectator UI")]
    [SerializeField] private GameObject spectatorPanel;
    [SerializeField] private TMP_Text spectatorPlayerText;
    [SerializeField] private TMP_Text spectatorControlsText;

    [Header("Icons")]
    [SerializeField] private Sprite survivorIcon;
    [SerializeField] private Sprite cultistIcon;

    private List<string> survivorTaskList = new List<string>();
    private List<string> cultistTaskList = new List<string>();
    private List<string> currentTasks = new List<string>();
    private RoleManager.PlayerRole currentRole;

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
        if (spectatorPanel != null) spectatorPanel.SetActive(false);

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

    // ============ SPECTATOR UI METHODS ============
    public void ShowSpectatorUI(string currentPlayerName = "")
    {
        if (spectatorPanel != null)
        {
            spectatorPanel.SetActive(true);
            UpdateSpectatorInfo(currentPlayerName);
            Debug.Log($"Spectator UI shown - Panel active: {spectatorPanel.activeInHierarchy}");

            // Force update the canvas to ensure it renders
            Canvas.ForceUpdateCanvases();
        }
        else
        {
            Debug.LogError("Spectator panel reference is null in GameHUDManager!");
        }
    }

    public void HideSpectatorUI()
    {
        if (spectatorPanel != null)
        {
            spectatorPanel.SetActive(false);
            Debug.Log("Spectator UI hidden");
        }
    }

    public void HideGameHUDForSpectator()
    {
        // Hide normal game HUD when spectating
        if (persistentRoleDisplay != null)
        {
            persistentRoleDisplay.SetActive(false);
            Debug.Log("Hidden persistent role display for spectator");
        }
        if (healthStaminaPanel != null)
        {
            healthStaminaPanel.SetActive(false);
            Debug.Log("Hidden health/stamina panel for spectator");
        }
        if (taskPanel != null)
        {
            taskPanel.SetActive(false);
            Debug.Log("Hidden task panel for spectator");
        }
        if (interactionPanel != null)
        {
            interactionPanel.SetActive(false);
            Debug.Log("Hidden interaction panel for spectator");
        }
    }

    public void RestoreGameHUDAfterSpectator()
    {
        // Restore normal game HUD after spectating
        if (persistentRoleDisplay != null)
        {
            persistentRoleDisplay.SetActive(true);
            Debug.Log("Restored persistent role display after spectator");
        }
        if (healthStaminaPanel != null)
        {
            healthStaminaPanel.SetActive(true);
            Debug.Log("Restored health/stamina panel after spectator");
        }
        if (taskPanel != null)
        {
            taskPanel.SetActive(true);
            Debug.Log("Restored task panel after spectator");
        }
    }

    public void UpdateSpectatorInfo(string currentPlayerName = "")
    {
        if (spectatorPanel == null)
        {
            Debug.LogError("Spectator panel is null in UpdateSpectatorInfo!");
            return;
        }

        if (!spectatorPanel.activeInHierarchy)
        {
            Debug.LogWarning("Spectator panel is not active when trying to update info!");
            return;
        }

        if (spectatorPlayerText != null)
        {
            if (!string.IsNullOrEmpty(currentPlayerName))
                spectatorPlayerText.text = $"Spectating: {currentPlayerName}";
            else
                spectatorPlayerText.text = "Spectating: Free Camera";

            Debug.Log($"Updated spectator text to: {spectatorPlayerText.text}");
        }
        else
        {
            Debug.LogError("Spectator player text is null!");
        }

        if (spectatorControlsText != null)
        {
            spectatorControlsText.text = "Q - Previous Player\n" +
                                        "E - Next Player\n" +
                                        "Right Mouse - Look Around\n" +
                                        "Mouse Wheel - Zoom\n" +
                                        "Space - Exit Spectator";
        }
        else
        {
            Debug.LogError("Spectator controls text is null!");
        }
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
            healthText.text = $"{currentHealth}/{maxHealth}";
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
            staminaText.text = $"{currentStamina}/{maxStamina}";
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
        if (spectatorPanel != null) spectatorPanel.SetActive(false);

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
            currentTasks[0] = "- Repair Generator ✓";
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

    [ContextMenu("Show Spectator UI")]
    private void TestShowSpectatorUI()
    {
        ShowSpectatorUI("Test Player");
    }

    [ContextMenu("Hide Spectator UI")]
    private void TestHideSpectatorUI()
    {
        HideSpectatorUI();
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