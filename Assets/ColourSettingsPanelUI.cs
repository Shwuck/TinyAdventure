using System.Collections.Generic;
using UnityEngine;
using TMPro; // Import TextMeshPro namespace
using UnityEngine.UI;

public class ColourSettingsPanelUI : MonoBehaviour
{
    public TMP_Dropdown panelColourDropdown;
    public TMP_Dropdown backgroundColourDropdown;
    public TMP_Dropdown mapColourDropdown;

    public Image panelColourPreview;
    public Image backgroundColourPreview;
    public Image mapColourPreview;

    public Button closeButton; // Reference to the Close button

    private void Start()
    {
        PopulateDropdowns();

        panelColourDropdown.onValueChanged.AddListener(OnPanelColourSelected);
        backgroundColourDropdown.onValueChanged.AddListener(OnBackgroundColourSelected);
        mapColourDropdown.onValueChanged.AddListener(OnMapColourSelected);

        closeButton.onClick.AddListener(ClosePanel); // Add listener for Close button
    }

    private void PopulateDropdowns()
    {
        List<string> options = new List<string>(ColourPool.AllColours.Keys);

        panelColourDropdown.ClearOptions();
        panelColourDropdown.AddOptions(options);

        backgroundColourDropdown.ClearOptions();
        backgroundColourDropdown.AddOptions(options);

        mapColourDropdown.ClearOptions();
        mapColourDropdown.AddOptions(options);
    }

    private void OnPanelColourSelected(int index)
    {
        string colorName = panelColourDropdown.options[index].text;
        if (ColourPool.AllColours.TryGetValue(colorName, out string hexColor))
        {
            if (ColourPool.IsValidHexColour(hexColor) && ColorUtility.TryParseHtmlString(hexColor, out Color color))
            {
                panelColourPreview.color = color; // Update the preview object
                UIController.Instance.UpdatePanelColors(color);
            }
        }
    }

    private void OnBackgroundColourSelected(int index)
    {
        string colorName = backgroundColourDropdown.options[index].text;
        if (ColourPool.AllColours.TryGetValue(colorName, out string hexColor))
        {
            if (ColourPool.IsValidHexColour(hexColor) && ColorUtility.TryParseHtmlString(hexColor, out Color color))
            {
                backgroundColourPreview.color = color; // Update the preview object
                UIController.Instance.UpdateBackgroundColor(color);
            }
        }
    }

    private void OnMapColourSelected(int index)
    {
        string colorName = mapColourDropdown.options[index].text;
        if (ColourPool.AllColours.TryGetValue(colorName, out string hexColor))
        {
            if (ColourPool.IsValidHexColour(hexColor) && ColorUtility.TryParseHtmlString(hexColor, out Color color))
            {
                mapColourPreview.color = color; // Update the preview object
                UIController.Instance.UpdateMapColor(color);
            }
        }
    }

    // Method to close the panel
    private void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}
