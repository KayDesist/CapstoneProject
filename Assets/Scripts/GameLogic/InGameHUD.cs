/*using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InGameHUD : MonoBehaviour
{
    public static InGameHUD Instance;

    [Header("HUD Elements")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private Slider healthBar;
    [SerializeField] private Slider staminaBar;
    [SerializeField] private TMP_Text taskListText;
    [SerializeField] private GameObject[] inventorySlots;

    [Header("Task System")]
    [SerializeField]
    private string[] survivorTasks = {
        "Fix generator in cabin",
        "Collect firewood",
        "Repair radio antenna",
        "Find car keys",
        "Restore power supply"
    };

    private PlayerController playerController;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetPlayerController(PlayerController controller)
    {
        playerController = controller;
        hudPanel.SetActive(true);
        InitializeHUD();
    }

    private void InitializeHUD()
    {
        // Initialize health and stamina bars
        healthBar.maxValue = 100f;
        healthBar.value = 100f;
        staminaBar.maxValue = 100f;
        staminaBar.value = 100f;

        // Initialize task list for survivors
        UpdateTaskList();
    }

    public void UpdateRoleIndicator(GameManager.PlayerRole role)
    {
        if (roleText != null)
        {
            string roleString = (role == GameManager.PlayerRole.Cultist) ? "CULTIST" : "SURVIVOR";
            Color roleColor = (role == GameManager.PlayerRole.Cultist) ? Color.red : Color.green;

            roleText.text = $"Role: {roleString}";
            roleText.color = roleColor;

            // For testing - in final game, hide role from survivors
            if (role == GameManager.PlayerRole.Cultist)
            {
                // Cultist knows they're cultist
                roleText.gameObject.SetActive(true);
            }
            else
            {
                // Survivors might not know their role until later
                // For now, show it for testing
                roleText.gameObject.SetActive(true);
            }
        }
    }

    public void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.value = currentHealth;
            healthBar.maxValue = maxHealth;
        }
    }

    public void UpdateStaminaBar(float currentStamina, float maxStamina)
    {
        if (staminaBar != null)
        {
            staminaBar.value = currentStamina;
            staminaBar.maxValue = maxStamina;
        }
    }

    public void UpdateTaskList()
    {
        if (taskListText != null && playerController != null)
        {
            if (playerController.GetRole() == GameManager.PlayerRole.Survivor)
            {
                string taskDisplay = "SURVIVOR TASKS:\n";
                foreach (string task in survivorTasks)
                {
                    taskDisplay += $"- {task}\n";
                }
                taskListText.text = taskDisplay;
            }
            else
            {
                taskListText.text = "CULTIST OBJECTIVE:\nEliminate all survivors\nor complete the ritual";
            }
        }
    }

    public void UpdateInventorySlot(int slotIndex, Sprite itemIcon, string itemName)
    {
        if (slotIndex >= 0 && slotIndex < inventorySlots.Length)
        {
            GameObject slot = inventorySlots[slotIndex];
            Image icon = slot.GetComponent<Image>();
            TMP_Text text = slot.GetComponentInChildren<TMP_Text>();

            if (icon != null) icon.sprite = itemIcon;
            if (text != null) text.text = itemName;
        }
    }

    public void ShowNotification(string message, float duration = 3f)
    {
        // Simple notification system
        Debug.Log($"HUD Notification: {message}");
        // You can implement a proper notification UI here
    }
}*/