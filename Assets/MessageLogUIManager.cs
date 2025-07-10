using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class MessageLogUIManager : MonoBehaviour
{
    public TextMeshProUGUI messageText;
    public TMP_Dropdown filterDropdown;

    private List<Message> allMessages = new List<Message>();
    private List<string> displayMessages = new List<string>();

    private void Start()
    {
        if (filterDropdown == null)
        {
            Debug.LogError("Filter dropdown is not assigned in MessageLogUIManager.");
            return;
        }

        // Populate the dropdown with message types
        filterDropdown.ClearOptions();
        List<string> options = new List<string> { "All" };
        options.AddRange(System.Enum.GetNames(typeof(MessageType)));
        filterDropdown.AddOptions(options);

        // Subscribe to dropdown value change event
        filterDropdown.onValueChanged.AddListener(delegate { FilterMessages(); });

        // Initially display all messages
        DisplayMessages();
    }

    public void Refresh()
    {
        FilterMessages();
    }

    public void UpdateDisplay(List<Message> messages)
    {
        allMessages = new List<Message>(messages);
        FilterMessages();
    }

    private void FilterMessages()
    {
        if (filterDropdown == null || filterDropdown.options.Count == 0)
        {
            Debug.LogWarning("Filter dropdown not properly initialized.");
            return;
        }

        string selectedFilter = filterDropdown.options[filterDropdown.value].text;
        displayMessages.Clear();

        if (selectedFilter == "All")
        {
            displayMessages = allMessages.Select(m => m.Text).ToList();
        }
        else
        {
            if (System.Enum.TryParse(selectedFilter, out MessageType selectedType))
            {
                displayMessages = allMessages.Where(m => m.Type == selectedType).Select(m => m.Text).ToList();
            }
            else
            {
                Debug.LogWarning($"Unknown message type: {selectedFilter}");
            }
        }

        DisplayMessages();
    }

    public void DisplayMessages()
    {
        if (messageText == null)
        {
            Debug.LogError("Message Text component not assigned in MessageLogUIManager.");
            return;
        }

        // Join only the last 15 messages to maintain UI performance
        messageText.text = string.Join("\n", displayMessages.TakeLast(15));
    }
}
