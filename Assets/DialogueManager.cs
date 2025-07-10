using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;
using System;

public class DialogueManager : MonoBehaviour
{
    private static DialogueManager _instance;
    public static DialogueManager Instance => _instance;

    private List<DialogueScript> dialogueScripts;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void SetDialogueScripts()
    {
        dialogueScripts = PermaLists.Instance.DialogueScripts;
        if (dialogueScripts == null || dialogueScripts.Count == 0)
        {
            Debug.LogWarning("Dialogue scripts list is empty or not set.");
        }
        else
        {
            Debug.Log($"Dialogue scripts successfully set with {dialogueScripts.Count} entries.");
        }
    }

    public DialogueLines GetDialogue(NPC npc)
    {
        if (npc == null)
        {
            Debug.LogError("NPC is null.");
            return null;
        }

        string role = npc.Role.ToString();
        string personality = npc.Personality?.PersonalityName;

        Debug.Log($"Dialogue opened with {npc.Name}, who is {role}, with Personality of {personality}");

        if (dialogueScripts == null)
        {
            Debug.LogError("Dialogue scripts list is null.");
            return null;
        }

        DialogueLines dialogue = FindDialogue(role, personality);

        if (dialogue == null)
        {
            dialogue = FindDialogue(role, "Default");
        }

        if (dialogue == null)
        {
            dialogue = FindDialogue("Default", "Default");
        }

        if (dialogue == null)
        {
            Debug.LogError($"No dialogue found for NPC with role: {role}, personality: {personality}, and default fallbacks.");
        }

        return dialogue;
    }

    private DialogueLines FindDialogue(string role, string personality)
    {
        if (string.IsNullOrEmpty(role))
        {
            Debug.LogWarning("Role is null or empty.");
            return null;
        }

        if (string.IsNullOrEmpty(personality))
        {
            Debug.LogWarning("Personality is null or empty.");
            return null;
        }

        var script = dialogueScripts?.FirstOrDefault(d => d.Roles != null && d.Roles.Contains(role));
        if (script == null)
        {
            Debug.LogWarning($"No dialogue script found for role: {role}");
            return null;
        }

        var personalityDialogue = script.Personalities?.FirstOrDefault(p => p.Personality.Equals(personality, StringComparison.OrdinalIgnoreCase));
        if (personalityDialogue == null)
        {
            Debug.LogWarning($"No dialogue found for personality: {personality} in role: {role}");
            return null;
        }

        Debug.Log($"Dialogue found for role: {role} and personality: {personality}");
        return personalityDialogue.Dialogue;
    }

    public string GetIntroductionDialogue(NPC npc)
    {
        DialogueLines dialogueLines = GetDialogue(npc);
        if (dialogueLines == null)
        {
            Debug.LogError("Introduction dialogue not found for NPC.");
            return "I don't understand.";
        }

        List<string> dialogueList = npc.HasMetPlayer ? dialogueLines.Introduction.HasMetPlayer : dialogueLines.Introduction.FirstTime;

        // Pick a random line from the list
        string dialogueText = dialogueList[UnityEngine.Random.Range(0, dialogueList.Count)];

        return ReplacePlaceholders(dialogueText, npc);
    }

    public (string responseText, bool requiresConfirmation, UnityAction action) DetermineDialogueResponse(NPC npc, string interactionType)
    {
        DialogueLines dialogueLines = GetDialogue(npc);
        if (dialogueLines == null)
        {
            Debug.LogError("Dialogue not found for NPC.");
            return ("I don't understand.", false, null);
        }

        string responseText = "I don't understand."; // Default response
        bool requiresConfirmation = false;
        UnityAction action = null;

        switch (interactionType)
        {
            case "Trade":
                bool isPositiveTradeResponse = UnityEngine.Random.value > 0.5f; // 50/50 chance
                responseText = isPositiveTradeResponse
                    ? dialogueLines.Trade.Yes[UnityEngine.Random.Range(0, dialogueLines.Trade.Yes.Count)]
                    : dialogueLines.Trade.No[UnityEngine.Random.Range(0, dialogueLines.Trade.No.Count)];

                requiresConfirmation = isPositiveTradeResponse;

                if (isPositiveTradeResponse)
                {
                    action = () =>
                    {
                        UIController.Instance.CloseDialoguePanel();
                        UIController.Instance.ActivateTradePanel(npc);
                    };
                }
                break;

            case "Need Anything?":
                if (npc.CurrentNeed != null && npc.CurrentNeed.HasNeed)
                {
                    responseText = $"Yes, I need {npc.CurrentNeed.NumberRequired} {npc.CurrentNeed.ItemName}.";
                    responseText = ReplacePlaceholders(responseText, npc, npc.CurrentNeed.ItemName);

                    requiresConfirmation = true;

                    action = () =>
                    {
                        bool success = ((PlayerInventory)PlayerStats.Instance.CurrentPlayerCharacter.Inventory).TryToGiveItemsToNPC(npc);

                        if (success)
                        {
                            Debug.Log($"Thank you for the {npc.CurrentNeed.ItemName}.");
                        }
                        else
                        {
                            Debug.Log("You don't have what I need right now.");
                        }
                    };
                }
                else
                {
                    responseText = dialogueLines.NeedAnything.No[UnityEngine.Random.Range(0, dialogueLines.NeedAnything.No.Count)];
                }
                break;

            case "Any News?":
                responseText = GetNewsDialogue(dialogueLines, npc);
                requiresConfirmation = false;
                break;

            case "Can we craft?":
                if (npc.IsCraftsman)
                {
                    responseText = "Yes.";
                    requiresConfirmation = true;

                    action = () =>
                    {
                        switch (npc.Role)
                        {
                            case NPCRole.Blacksmith:
                                UIController.Instance.OpenSmithingPanel();
                                break;

                            default:
                                Debug.LogError("Crafting role not handled for this NPC.");
                                break;
                        }
                    };
                }
                else
                {
                    responseText = "I'm not a craftsman.";
                    requiresConfirmation = false;
                }
                break;

            default:
                Debug.LogError("Unhandled interaction type.");
                break;
        }

        responseText = ReplacePlaceholders(responseText, npc);
        Debug.Log($"Response: {responseText}");
        return (responseText, requiresConfirmation, action);
    }

        private string ReplacePlaceholders(string dialogueText, NPC npc, string itemName = "")
    {
        if (string.IsNullOrEmpty(dialogueText))
        {
            return string.Empty;
        }

        string playerName = PlayerStats.Instance.PlayerCharacterName;
        string playerFirstName = PlayerStats.Instance.PlayerCharacterFirstName;

        // Replace all the known placeholders with the actual values
        return dialogueText
            .Replace("[NPC Name]", npc.Name)
            .Replace("[NPC First Name]", npc.FirstName)
            .Replace("[Player Character Name]", playerName)
            .Replace("[Player Name]", playerFirstName)
            .Replace("[Player Character First Name]", playerFirstName)
            .Replace("[Item]", itemName) // Replace the item placeholder with the actual item name
            .Replace("[Number Required]", npc.CurrentNeed?.NumberRequired.ToString()); // Replace the number required placeholder
    }

    private string GetNewsDialogue(DialogueLines dialogueLines, NPC npc)
    {
        string newsResponse;

        // Check if the NPC has any dialogue for "Any News?"
        if (dialogueLines.News != null && dialogueLines.News.Yes != null && dialogueLines.News.Yes.Count > 0)
        {
            // Fetch a random "Yes" response
            newsResponse = dialogueLines.News.Yes[UnityEngine.Random.Range(0, dialogueLines.News.Yes.Count)];

            // Determine the NewsType from the NPC's Role
            NewsType newsType = npc.NewsType;
            Village village = npc.HomeVillage;

            // Fetch news based on the determined NewsType
            List<NewsEntry> newsEntries = NewsManager.Instance.GetVillageNews(village)
                .Where(n => n.NewsType == newsType)
                .ToList();

            if (newsEntries != null && newsEntries.Count > 0)
            {
                // Select a random news entry to share
                NewsEntry selectedNews = newsEntries[UnityEngine.Random.Range(0, newsEntries.Count)];
                // Append the news content to the response
                newsResponse += " " + selectedNews.Content;
            }
            else
            {
                // If no news available, use a fallback "No" response
                newsResponse = dialogueLines.News.No[UnityEngine.Random.Range(0, dialogueLines.News.No.Count)];
            }
        }
        else
        {
            // If no dialogue is available for "Yes", use a fallback "No" response
            newsResponse = dialogueLines.News.No[UnityEngine.Random.Range(0, dialogueLines.News.No.Count)];
        }

        return newsResponse;
    }

    public void ModifyRelationshipAfterDialogue(NPC npc, PlayerStats playerStats, bool positiveInteraction)
    {
        float change = positiveInteraction ? 5f : -5f;
        npc.AdjustRelationship(playerStats.CurrentPlayerCharacter, change);

        Debug.Log($"{npc.Name} now has a relationship of {npc.GetRelationshipValue(playerStats.CurrentPlayerCharacter)} with the player.");
    }



}
