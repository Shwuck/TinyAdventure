using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using System.Text;
using System.Collections.Generic;

public class VillageInfoPanel : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI villageNameText;
    public TextMeshProUGUI villageStatsText;
    public TextMeshProUGUI notableNPCsText;
    public TextMeshProUGUI playerReputationText;
    public Button closeButton;

    public TextMeshProUGUI newsText;



    private Village currentVillage;

    private void Awake()
    {
        // Assign the button from the scene (assuming it's a child of the same GameObject)
        if (closeButton == null)
        {
            closeButton = GetComponentInChildren<Button>();
        }

        // Add a listener to the button to call CloseVillageInfoPanel
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseVillageInfoPanel);
        }
        else
        {
            Debug.LogWarning("Close button is not assigned in the VillageInfoPanel.");
        }
    }

    public void SetVillage(Village village)
    {
        Debug.Log("SetVillage called");
        currentVillage = village;
        DisplayVillageName();
        DisplayVillageStats();
        DisplayNotableNPCs();
        DisplayPlayerReputation();
        DisplayNews();
    }


    private bool IsVillageSet()
    {
        return currentVillage != null;
    }

    private bool IsTextComponentValid(TextMeshProUGUI textComponent)
    {
        return textComponent != null;
    }

    private void DisplayVillageName()
    {
        if (IsVillageSet() && IsTextComponentValid(villageNameText))
        {
            villageNameText.text = currentVillage.VillageName;
        }
    }

    private void DisplayVillageStats()
    {
        if (IsVillageSet() && IsTextComponentValid(villageStatsText))
        {
            StringBuilder statsBuilder = new StringBuilder();

            // Add a title for the Village Stats
            statsBuilder.AppendLine("<b>Village Stats:</b>");
            statsBuilder.AppendLine(); // Add a blank line below the title

            var stats = currentVillage.Stats;

            statsBuilder.AppendLine($"Population: {CalculatePercentage(stats.Population, stats.MaxPopulation):F1}%");
            statsBuilder.AppendLine($"Food: {CalculatePercentage(stats.StoredFood, stats.MaxStoredFood):F1}%");
            statsBuilder.AppendLine($"Water: {CalculatePercentage(stats.StoredWater, stats.MaxStoredWater):F1}%");
            statsBuilder.AppendLine($"Building Materials: {CalculatePercentage(stats.StoredWood + stats.StoredStone, stats.MaxStoredWood + stats.MaxStoredStone):F1}%");
            statsBuilder.AppendLine($"Prestige: {CalculatePercentage(stats.Prestige, stats.MaxPrestige):F1}%");

            statsBuilder.AppendLine(); // Add a break between stats and needed resource
            statsBuilder.AppendLine($"{currentVillage.VillageName} is currently in need of {stats.NeededResource}.");

            villageStatsText.text = statsBuilder.ToString();
        }
    }

    private void DisplayNotableNPCs()
    {
        Debug.Log("DisplayNotableNPCs called");
        if (!IsVillageSet() || !IsTextComponentValid(notableNPCsText))
        {
            return;
        }

        List<NPC> notableNPCs = GetUniqueNotableNPCs();

        // Add a title for the Notable NPCs
        StringBuilder npcBuilder = new StringBuilder();
        npcBuilder.AppendLine("<b>Notable NPCs:</b>");
        npcBuilder.AppendLine(); // Add a blank line below the title

        if (notableNPCs.Count == 0)
        {
            npcBuilder.Append("No notable NPCs.");
        }
        else
        {
            npcBuilder.Append(string.Join("\n", notableNPCs.Select(npc => $"{npc.NPCID} {npc.FirstName} {npc.Surname}, {npc.Role}")));
        }

        notableNPCsText.text = npcBuilder.ToString();
    }


    private List<NPC> GetUniqueNotableNPCs()
    {
        var uniqueNPCs = new HashSet<string>(); // To store unique NPCs by combining name and role
        var uniqueNotableNPCs = new List<NPC>();

        foreach (var npc in currentVillage.VillageNPCs.Where(npc => npc.Role != NPCRole.Villager))
        {
            // Create a unique identifier using name and role
            string npcIdentifier = $"{npc.FirstName} {npc.Surname} - {npc.Role}";

            // If the NPC (name and role) is not already in the set, add it
            if (uniqueNPCs.Add(npcIdentifier))
            {
                uniqueNotableNPCs.Add(npc); // Add the NPC to the final list
            }
        }

        return uniqueNotableNPCs;
    }


    private void DisplayPlayerReputation()
    {
        if (IsVillageSet() && IsTextComponentValid(playerReputationText))
        {
            var stats = currentVillage.Stats;

            float recognitionPercentage = CalculatePercentage(stats.PlayerRecognition, stats.MaxPlayerRecognition);
            float renownPercentage = CalculatePercentage(stats.PlayerRenown, stats.MaxPlayerRenown);

            // Apply colour coding based on thresholds
            string recognitionColor = recognitionPercentage >= 75 ? "green" : recognitionPercentage < 25 ? "red" : "yellow";
            string renownColor = renownPercentage >= 75 ? "green" : renownPercentage < 25 ? "red" : "yellow";

            playerReputationText.text = $"<color={recognitionColor}>Recognition: {recognitionPercentage:F1}%</color>\n" +
                                        $"<color={renownColor}>Renown: {renownPercentage:F1}%</color>";
        }
    }

    private float CalculatePercentage(float current, float max)
    {
        return (max > 0) ? (current / max) * 100 : 0; // Prevent division by zero
    }

    private void DisplayNews()
    {
        if (!IsVillageSet() || !IsTextComponentValid(newsText))
        {
            return;
        }

        StringBuilder newsBuilder = new StringBuilder();

        // Retrieve the news for the current village
        List<NewsEntry> villageNews = NewsManager.Instance.GetVillageNews(currentVillage);

        // Get one item of regional news
        NewsEntry regionalNews = villageNews.FirstOrDefault(news => news.NewsType == NewsType.Regional);
        if (regionalNews != null)
        {
            newsBuilder.AppendLine("<b>Regional News:</b>");
            newsBuilder.AppendLine($"{regionalNews.Title}: {regionalNews.Content}");
        }
        else
        {
            newsBuilder.AppendLine("<b>Regional News:</b>");
            newsBuilder.AppendLine("No regional news at this time.");
        }

        // Get one item of worldwide news
        NewsEntry worldwideNews = villageNews.FirstOrDefault(news => news.NewsType == NewsType.Worldwide);
        if (worldwideNews != null)
        {
            newsBuilder.AppendLine("<b>Worldwide News:</b>");
            newsBuilder.AppendLine($"{worldwideNews.Title}: {worldwideNews.Content}");
        }
        else
        {
            newsBuilder.AppendLine("<b>Worldwide News:</b>");
            newsBuilder.AppendLine("No worldwide news at this time.");
        }

        // Set the news text to the combined result
        newsText.text = newsBuilder.ToString();
    }

    public void OpenVillageInfoPanel(Village village)
    {
        if (panel != null)
        {
            panel.SetActive(true);
            SetVillage(village);
        }
    }

    public void CloseVillageInfoPanel()
    {
        if (panel != null)
        {
            panel.SetActive(false);
            UIController.Instance.DeactivateGreyOutPanel();
        }
    }
}
