using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

/// <summary>
/// Manages a toggleable help modal that displays controls and simulation information.
/// Press '?' or 'H' to toggle visibility.
/// </summary>
[AddComponentMenu("Simulation/UI/Help Modal Controller")]
public sealed class HelpModalController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The UIDocument component containing the help modal")]
    public UIDocument helpDocument;

    [Header("Settings")]
    [Tooltip("Key to toggle the help modal")]
    public Key toggleKey = Key.Slash; // '?' key (Shift + /)

    [Tooltip("Alternative toggle key")]
    public Key alternativeKey = Key.H;

    private VisualElement modalRoot;
    private VisualElement modalOverlay;
    private bool isVisible = false;

    void Awake()
    {
        if (helpDocument == null)
            helpDocument = GetComponent<UIDocument>();

        if (helpDocument != null)
        {
            modalRoot = helpDocument.rootVisualElement;
            modalOverlay = modalRoot?.Q<VisualElement>("help-modal");

            // Initially hide the modal
            HideModal();
        }
        else
        {
            Debug.LogError("HelpModalController: No UIDocument found!");
        }
    }

    void Update()
    {
        // Check for toggle key press
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool togglePressed = keyboard[toggleKey].wasPressedThisFrame ||
                             keyboard[alternativeKey].wasPressedThisFrame;

        if (togglePressed)
        {
            ToggleModal();
        }

        // Also allow ESC to close if open
        if (isVisible && keyboard.escapeKey.wasPressedThisFrame)
        {
            HideModal();
        }
    }

    /// <summary>
    /// Toggle the visibility of the help modal
    /// </summary>
    public void ToggleModal()
    {
        if (isVisible)
            HideModal();
        else
            ShowModal();
    }

    /// <summary>
    /// Show the help modal
    /// </summary>
    public void ShowModal()
    {
        if (modalOverlay != null)
        {
            modalOverlay.style.display = DisplayStyle.Flex;
            isVisible = true;
            Debug.Log("Help modal shown");
        }
    }

    /// <summary>
    /// Hide the help modal
    /// </summary>
    public void HideModal()
    {
        if (modalOverlay != null)
        {
            modalOverlay.style.display = DisplayStyle.None;
            isVisible = false;
        }
    }

    /// <summary>
    /// Check if the modal is currently visible
    /// </summary>
    public bool IsVisible => isVisible;
}
