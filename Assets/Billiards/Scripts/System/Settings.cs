using UnityEngine;
using UnityEngine.UI;

namespace Billiards.System
{
    /// <summary>
    /// Attach this to an empty GameObject. Assign the "Settings" UI Button
    /// and the settings panel (GameObject) in the Inspector. When the button
    /// is clicked this toggles the panel the same way pressing Escape does.
    /// </summary>
    public class Settings : MonoBehaviour
    {
        [Header("Assign in Inspector")]
        [Tooltip("Button that will toggle the settings panel")]
        public Button settingsButton;

        [Tooltip("The settings panel GameObject to open/close")]
        public GameObject settingsPanel;

        private void OnEnable()
        {
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
            }
        }

        private void Start()
        {
            if (settingsPanel == null)
            {
                Debug.LogWarning("Settings: settingsPanel is not assigned in the Inspector.", this);
            }

            if (settingsButton == null)
            {
                Debug.LogWarning("Settings: settingsButton is not assigned in the Inspector.", this);
            }
        }

        private void OnSettingsButtonClicked()
        {
            ToggleSettingsPanel();
        }

        /// <summary>
        /// Toggles the assigned settings panel (same behaviour as Escape key handler).
        /// </summary>
        public void ToggleSettingsPanel()
        {
            if (settingsPanel == null) return;
            bool newState = !settingsPanel.activeSelf;
            settingsPanel.SetActive(newState);
        }
    }
}
