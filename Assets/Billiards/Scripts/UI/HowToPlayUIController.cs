using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Nasıl Oynanır" panelini açıp kapatan UI kontrol scripti.
/// </summary>
public class HowToPlayUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private Button howToPlayButton;
    [SerializeField] private Text infoText;

    private void Start()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(false); // Start hidden
        if (howToPlayButton != null)
            howToPlayButton.onClick.AddListener(TogglePanel);

        if (infoText != null)
        {
            infoText.text =
                "<b>How To Play</b>\n\n" +
                "<b>Move cue</b>: WASD or Arrows\n" +
                "<b>Power</b>: Q / E\n" +
                "<b>Shoot</b>: Space\n" +
                "Score by hitting at least 3 cushions and 2 balls in one shot!";
        }
    }

    private void TogglePanel()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(!howToPlayPanel.activeSelf);
    }
}
