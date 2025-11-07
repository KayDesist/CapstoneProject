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

    [Header("Icons")]
    [SerializeField] private Sprite survivorIcon;
    [SerializeField] private Sprite cultistIcon;

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

        // Initialize UI state - hide persistent HUD initially
        if (persistentRoleDisplay != null) persistentRoleDisplay.SetActive(false);
        if (healthStaminaPanel != null) healthStaminaPanel.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(false);

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
            StartCoroutine(ShowPersistentHUDAfterDelay(3f)); // Match the reduced time
        }
        else
        {
            Debug.LogWarning("RoleDisplayUI.Instance is null! Showing persistent HUD immediately");
            ShowPersistentHUD();
        }
    }

    private IEnumerator ShowPersistentHUDAfterDelay(float delay)
    {
        yield return new WaitForSeconds(3f);
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
    }

    private void SetupSurvivorTasks()
    {
        currentTasks.Clear();

        if (TaskManager.Instance != null)
        {
            var tasks = TaskManager.Instance.GetSurvivorTasksForUI();
            currentTasks.AddRange(tasks);
            UpdateTotalTasksText(0, tasks.Count);
        }
        else
        {
            // Fallback tasks
            currentTasks.AddRange(new string[] {
                "Repair Generator (0/3)",
                "Collect Firewood (0/5)",
                "Find Car Keys (0/1)",
                "Fix Radio Tower (0/2)",
                "Gather Supplies (0/4)"
            });
            UpdateTotalTasksText(0, currentTasks.Count);
        }

        UpdateTasksText();
    }

    private void SetupCultistTasks()
    {
        currentTasks.Clear();

        if (TaskManager.Instance != null)
        {
            var tasks = TaskManager.Instance.GetCultistTasksForUI();
            currentTasks.AddRange(tasks);
            UpdateTotalTasksText(0, tasks.Count);
        }
        else
        {
            // Fallback tasks
            currentTasks.AddRange(new string[] {
                "Place Ritual Candles (0/3)",
                "Collect Sacrificial Items (0/2)",
                "Activate Altars (0/2)",
                "Eliminate Survivors (0/0)"
            });
            UpdateTotalTasksText(0, currentTasks.Count);
        }

        UpdateTasksText();
    }

    private void UpdateTasksText()
    {
        if (tasksText != null)
        {
            tasksText.text = string.Join("\n", currentTasks);
        }
    }

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

    public void UpdateTaskProgress(int taskIndex, string newStatus)
    {
        if (taskIndex >= 0 && taskIndex < currentTasks.Count)
        {
            currentTasks[taskIndex] = newStatus;
            UpdateTasksText();
            UpdateTotalTasksCompleted();
        }
    }

    private void UpdateTotalTasksCompleted()
    {
        int completed = 0;
        foreach (var task in currentTasks)
        {
            if (task.Contains("?"))
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

    // For testing in editor
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
            currentTasks[0] = "Repair Generator ?";
            UpdateTasksText();
            UpdateTotalTasksText(1, currentTasks.Count);
        }
    }

    [ContextMenu("Show Persistent HUD")]
    private void TestShowPersistentHUD()
    {
        ShowPersistentHUD();
    }
}