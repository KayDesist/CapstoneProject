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

    [Header("Audio")]
    public AudioClip roleRevealSound;
    private AudioSource audioSource;

    [Header("Role Colors")]
    [SerializeField] private Color survivorColor = Color.blue;
    [SerializeField] private Color cultistColor = Color.red;

    [Header("Role Descriptions")]
    [SerializeField] private string survivorDescription = "Complete tasks and survive! Work with other survivors to escape before the cultist completes their ritual.";
    [SerializeField] private string cultistDescription = "Eliminate all survivors or complete the dark ritual before they escape! Use your abilities to hunt them down.";

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

        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (roleDisplayPanel != null)
            roleDisplayPanel.SetActive(false);

        if (continueButton != null)
            continueButton.onClick.AddListener(HideRoleDisplay);
    }

    public void ShowRole(RoleManager.PlayerRole role)
    {
        if (roleDisplayPanel == null) return;

        // Play role reveal sound
        if (audioSource != null && roleRevealSound != null)
        {
            audioSource.PlayOneShot(roleRevealSound);
        }

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

        roleDisplayPanel.SetActive(true);
        StartCoroutine(AutoHideRoleDisplay());
    }

    private IEnumerator AutoHideRoleDisplay()
    {
        yield return new WaitForSeconds(3f);
        HideRoleDisplay();
    }

    private void HideRoleDisplay()
    {
        if (roleDisplayPanel != null)
            roleDisplayPanel.SetActive(false);

        StopAllCoroutines();
        OnRoleDisplayHidden?.Invoke();
        Debug.Log("RoleDisplayUI hidden and event triggered");
    }
}