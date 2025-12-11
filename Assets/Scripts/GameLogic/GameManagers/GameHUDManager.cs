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
        if (persistentRoleDisplay != null) persistentRoleDisplay.SetActive(false);
        if (healthStaminaPanel != null) healthStaminaPanel.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(false);

        HideInteractionPrompt();
        HideInteractionProgress();

        UpdateHealth(100, 100);
        UpdateStamina(100, 100);

        StartCoroutine(InitializeWithDelay());
    }

    private IEnumerator InitializeWithDelay()
    {
        yield return new WaitForSeconds(1f);

        if (RoleManager.Instance != null)
        {
            currentRole = RoleManager.Instance.GetLocalPlayerRole();
            if (currentRole != RoleManager.PlayerRole.Survivor || RoleManager.Instance.IsCultist(NetworkManager.Singleton.LocalClientId))
            {
                OnRoleAssigned(currentRole);
            }
        }

        StartCoroutine(FindLocalPlayerHealth());
    }

    private IEnumerator FindLocalPlayerHealth()
    {
        int attempts = 0;
        while (localPlayerHealth == null && attempts < 10)
        {
            attempts++;

            var playerControllers = FindObjectsOfType<NetworkPlayerController>();
            foreach (var controller in playerControllers)
            {
                if (controller.IsOwner)
                {
                    localPlayerController = controller;
                    localPlayerHealth = controller.GetComponent<PlayerHealth>();

                    if (localPlayerHealth != null)
                    {
                        SubscribeToHealthChanges();
                        break;
                    }
                }
            }

            if (localPlayerHealth == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private void SubscribeToHealthChanges()
    {
    }

    public void OnRoleAssigned(RoleManager.PlayerRole role)
    {
        currentRole = role;
        SetupRoleSpecificHUD();
        ShowTemporaryRoleDisplay();
    }

    private void SetupRoleSpecificHUD()
    {
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

        if (persistentRoleText != null) persistentRoleText.text = roleName;
        if (persistentRoleIcon != null && roleIcon != null) persistentRoleIcon.sprite = roleIcon;
    }

    private void ShowTemporaryRoleDisplay()
    {
        if (RoleDisplayUI.Instance != null)
        {
            RoleDisplayUI.Instance.ShowRole(currentRole);
            StartCoroutine(ShowPersistentHUDAfterDelay(3f));
        }
        else
        {
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
        if (persistentRoleDisplay != null) persistentRoleDisplay.SetActive(true);
        if (healthStaminaPanel != null) healthStaminaPanel.SetActive(true);
        if (taskPanel != null) taskPanel.SetActive(true);

        HideInteractionPrompt();
        HideInteractionProgress();
    }

    private void Update()
    {
        if (localPlayerHealth != null)
        {
            UpdateHealth(localPlayerHealth.GetCurrentHealth(), localPlayerHealth.maxHealth);
            UpdateStamina(localPlayerHealth.GetStamina(), localPlayerHealth.maxStamina);
        }
    }

    public void ShowHUDForEndGame()
    {
        gameObject.SetActive(true);
        ShowPersistentHUD();
    }

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
            survivorTaskList.AddRange(new string[] {
                "- Repair Generator (0/3)",
                "- Collect Firewood (0/5)",
                "- Find Car Keys (0/1)"
            });
            currentTasks = new List<string>(survivorTaskList);
            UpdateTotalTasksText(0, currentTasks.Count);
        }

        UpdateTasksText();
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
            cultistTaskList.AddRange(new string[] {
                "- Place Ritual Candles (0/3)",
                "- Collect Sacrificial Items (0/2)",
                "- Activate Altars (0/2)"
            });
            currentTasks = new List<string>(cultistTaskList);
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

    // Update health display
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

    // Update stamina display
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

    // Update specific task progress
    public void UpdateTaskProgress(int taskIndex, string newStatus)
    {
        if (currentRole == RoleManager.PlayerRole.Survivor)
        {
            if (taskIndex >= 0 && taskIndex < survivorTaskList.Count)
            {
                survivorTaskList[taskIndex] = newStatus;
                currentTasks = new List<string>(survivorTaskList);
            }
        }
        else if (currentRole == RoleManager.PlayerRole.Cultist)
        {
            if (taskIndex >= 0 && taskIndex < cultistTaskList.Count)
            {
                cultistTaskList[taskIndex] = newStatus;
                currentTasks = new List<string>(cultistTaskList);
            }
        }
        else
        {
            return;
        }

        UpdateTasksText();
        UpdateTotalTasksCompleted();
    }

    // Count completed tasks
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

    // Update task completion counter
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

    // Show interaction prompt
    public void ShowInteractionPrompt(string promptText)
    {
        if (interactionPanel != null && interactionText != null)
        {
            interactionText.text = promptText;
            interactionPanel.SetActive(true);
        }
    }

    // Hide interaction prompt
    public void HideInteractionPrompt()
    {
        if (interactionPanel != null)
        {
            interactionPanel.SetActive(false);
        }

        if (interactionProgressBar != null)
        {
            interactionProgressBar.gameObject.SetActive(false);
            interactionProgressBar.value = 0f;
        }
    }

    // Show interaction progress bar
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

    // Hide interaction progress bar
    public void HideInteractionProgress()
    {
        if (interactionProgressBar != null)
        {
            interactionProgressBar.gameObject.SetActive(false);
            interactionProgressBar.value = 0f;
        }
    }

    // Reset all HUD elements
    public void ResetHUD()
    {
        HideInteractionPrompt();
        HideInteractionProgress();

        if (persistentRoleDisplay != null) persistentRoleDisplay.SetActive(false);
        if (healthStaminaPanel != null) healthStaminaPanel.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(false);
    }

    // Static reset method
    public static void ResetInstance()
    {
        if (Instance != null)
        {
            Instance.ResetHUD();
            Destroy(Instance.gameObject);
            Instance = null;
        }
    }

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
        for (int i = 0; i < currentTasks.Count; i++)
        {
            Debug.Log($"Task {i}: {currentTasks[i]}");
        }
    }
}