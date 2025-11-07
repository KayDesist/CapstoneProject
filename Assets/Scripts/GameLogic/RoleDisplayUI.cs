using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

public class RoleDisplayUI : MonoBehaviour
{
    public static RoleDisplayUI Instance;

    [Header("Role Display Panel")]
    [SerializeField] private GameObject roleDisplayPanel;
    [SerializeField] private TMP_Text roleTitleText;
    [SerializeField] private TMP_Text roleDescriptionText;
    [SerializeField] private Image roleBackground;
    [SerializeField] private Button continueButton;

    [Header("Role Colors")]
    [SerializeField] private Color survivorColor = Color.blue;
    [SerializeField] private Color cultistColor = Color.red;

    [Header("Role Descriptions")]
    [SerializeField] private string survivorDescription = "Complete tasks and survive! Work with other survivors to escape before the cultist completes their ritual.";
    [SerializeField] private string cultistDescription = "Eliminate all survivors or complete the dark ritual before they escape! Use your abilities to hunt them down.";

    // Event for when the role display is hidden
    public static event Action OnRoleDisplayHidden;

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

        // Hide panel initially
        if (roleDisplayPanel != null)
            roleDisplayPanel.SetActive(false);

        // Setup continue button
        if (continueButton != null)
            continueButton.onClick.AddListener(HideRoleDisplay);
    }

    public void ShowRole(RoleManager.PlayerRole role)
    {
        if (roleDisplayPanel == null) return;

        // Update UI based on role
        switch (role)
        {
            case RoleManager.PlayerRole.Survivor:
                roleTitleText.text = "SURVIVOR";
                roleDescriptionText.text = survivorDescription;
                if (roleBackground != null)
                    roleBackground.color = survivorColor;
                break;
            case RoleManager.PlayerRole.Cultist:
                roleTitleText.text = "CULTIST";
                roleDescriptionText.text = cultistDescription;
                if (roleBackground != null)
                    roleBackground.color = cultistColor;
                break;
        }

        // Show the panel
        roleDisplayPanel.SetActive(true);

        // Auto-hide after 3 seconds if player doesn't click continue
        StartCoroutine(AutoHideRoleDisplay());
    }

    private IEnumerator AutoHideRoleDisplay()
    {
        yield return new WaitForSeconds(3f); // Reduced from 10f to 3f
        HideRoleDisplay();
    }

    private void HideRoleDisplay()
    {
        if (roleDisplayPanel != null)
            roleDisplayPanel.SetActive(false);

        StopAllCoroutines();

        // Notify that the role display has been hidden
        OnRoleDisplayHidden?.Invoke();
        Debug.Log("RoleDisplayUI hidden and event triggered");
    }

    // For testing in editor
    [ContextMenu("Test Survivor Display")]
    private void TestSurvivorDisplay()
    {
        ShowRole(RoleManager.PlayerRole.Survivor);
    }

    [ContextMenu("Test Cultist Display")]
    private void TestCultistDisplay()
    {
        ShowRole(RoleManager.PlayerRole.Cultist);
    }

    [ContextMenu("Hide Display")]
    private void TestHideDisplay()
    {
        HideRoleDisplay();
    }
}