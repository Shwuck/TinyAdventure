using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChangelogUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject changelogPanel; // The panel that holds the changelog UI
    public TMP_Text changelogDetailsText; // Text box for displaying full changelog

    [Header("Buttons")]
    public Button openChangelogButton; // Button to open the changelog panel
    public Button closeChangelogButton; // Button to close the changelog panel

    private void Start()
    {
        // Ensure panel is hidden at start
        changelogPanel.SetActive(false);

        // Assign button listeners
        if (openChangelogButton != null)
        {
            openChangelogButton.onClick.AddListener(OpenChangelogPanel);
        }
        if (closeChangelogButton != null)
        {
            closeChangelogButton.onClick.AddListener(CloseChangelogPanel);
        }
    }

    private void DisplayFullChangelog()
    {
        if (ChangeLogDataLoader.changelogData == null || ChangeLogDataLoader.changelogData.Count == 0)
        {
            Debug.LogError("No changelog data found!");
            changelogDetailsText.text = "No changelog available.";
            return;
        }

        string formattedText = "<b>TinyAdventure Changelog</b>\n\n";

        foreach (var changelog in ChangeLogDataLoader.changelogData) 
        {
            formattedText += $"<b>{changelog.title} ({changelog.version})</b>\n<i>{changelog.date}</i>\n\n";

            foreach (var section in changelog.sections)
            {
                formattedText += $"<b>{section.subtitle}</b>\n";

                foreach (var subsection in section.subsections)
                {
                    formattedText += $"<i>{subsection.subtitle}</i>\n";
                    foreach (var change in subsection.changes)
                    {
                        formattedText += $"- {change}\n";
                    }
                }
                formattedText += "\n";
            }
        }

        changelogDetailsText.text = formattedText;
    }

    private void OpenChangelogPanel()
    {
        changelogPanel.SetActive(true);
        // Load and display the changelog text
        DisplayFullChangelog();
    }

    private void CloseChangelogPanel()
    {
        changelogPanel.SetActive(false);
    }
}
