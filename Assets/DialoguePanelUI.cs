using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

public class DialoguePanelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private TextMeshProUGUI npcDescriptionText;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject playerDialogueOptionsPanel;
    [SerializeField] private GameObject dialogueOptionButtonPrefab;
    [SerializeField] private GameObject responseWithButtonPrefab;
    [SerializeField] private GameObject responseWithoutButtonPrefab;
    [SerializeField] private Transform responsePanelParent;

    private NPC currentNPC;

    void Awake()
    {
        dialoguePanel.SetActive(false);
    }

    public void SetupDialogue(NPC npc)
    {
        currentNPC = npc;
        dialoguePanel.SetActive(true);

        npcNameText.text = $"{npc.Name}";

        string role = npc.Role.ToString();
        string personality = npc.Personality?.PersonalityName;

        Debug.Log($"DialoguePanelUI: Initiated dialogue with {npc.Name}, who is a {role}, with a personality of {personality}");

        SetNPCDescription(npc);

        // Show initial introduction dialogue
        string introductionText = DialogueManager.Instance.GetIntroductionDialogue(npc);
        ShowResponseUI(responseWithoutButtonPrefab, introductionText);

        PopulateDialogueOptions();

        npc.FirstMetPlayerCharacter();
    }


    private void PopulateDialogueOptions()
    {
        ClearDialogueOptions();

        CreateDialogueOptionButton("Trade", () => RequestDialogueResponse(currentNPC, "Trade"));
        CreateDialogueOptionButton("Need anything?", () => RequestDialogueResponse(currentNPC, "Need Anything?"));

        // Add the "Can we craft?" option if the NPC is a craftsman
        if (currentNPC.IsCraftsman)
        {
            CreateDialogueOptionButton("Can we craft?", () => RequestDialogueResponse(currentNPC, "Can we craft?"));
        }

        // This option will allow players to ask for any news from the NPC
        CreateDialogueOptionButton("Any news?", () => RequestDialogueResponse(currentNPC, "Any News?"));
    }

    private void CreateDialogueOptionButton(string interactionName, UnityAction onClickAction)
    {
        GameObject buttonGO = Instantiate(dialogueOptionButtonPrefab, playerDialogueOptionsPanel.transform);
        Button button = buttonGO.GetComponent<Button>();
        TextMeshProUGUI buttonTextComponent = button.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonTextComponent != null)
        {
            buttonTextComponent.text = interactionName;
        }
        else
        {
            Debug.LogWarning("TextMeshProUGUI component not found on button or its children.");
        }

        button.onClick.AddListener(onClickAction);
    }

    private void ClearDialogueOptions()
    {
        foreach (Transform child in playerDialogueOptionsPanel.transform)
        {
            Destroy(child.gameObject);
        }
    }

    private void RequestDialogueResponse(NPC npc, string interactionType)
    {
        var (responseText, requiresConfirmation, action) = DialogueManager.Instance.DetermineDialogueResponse(npc, interactionType);

        if (responseText == null)
        {
            responseText = "I don't understand.";
        }

        GameObject responsePrefab = requiresConfirmation ? responseWithButtonPrefab : responseWithoutButtonPrefab;

        // Provide the action to the UI
        ShowResponseUI(responsePrefab, responseText, action);
    }

    private void ShowResponseUI(GameObject prefab, string responseText, UnityAction buttonAction = null, string buttonText = "")
    {
        if (prefab == null)
        {
            Debug.LogWarning("Response prefab is null. Cannot display response UI.");
            return;
        }

        ClearResponsePanel();

        GameObject responseGO = Instantiate(prefab, responsePanelParent);
        TextMeshProUGUI responseTextComponent = responseGO.GetComponentInChildren<TextMeshProUGUI>();

        if (responseTextComponent != null)
        {
            responseTextComponent.text = responseText;
        }
        else
        {
            Debug.LogWarning("TextMeshProUGUI component not found on response prefab or its children.");
        }

        // Attach the button action and set button text if provided
        Button responseButton = responseGO.GetComponentInChildren<Button>();
        if (responseButton != null && buttonAction != null)
        {
            if (!string.IsNullOrEmpty(buttonText))
            {
                responseButton.GetComponentInChildren<TextMeshProUGUI>().text = buttonText; // Set custom button text
            }
            responseButton.onClick.AddListener(buttonAction);
        }
    }

    private void ClearResponsePanel()
    {
        foreach (Transform child in responsePanelParent)
        {
            Destroy(child.gameObject);
        }
    }

    private void SetNPCDescription(NPC npc)
    {
        if (npcDescriptionText != null)
        {
            // Start with the basic NPC description
            string description = $"You are talking to {npc.Name}, a {npc.Race.Name} {npc.Role}.";

            // Append what the NPC is wearing
            string equipmentDescription = GetEquippedItemsDescription(npc);
            if (!string.IsNullOrEmpty(equipmentDescription))
            {
                description += $" They're wearing {equipmentDescription}.";
            }

            npcDescriptionText.text = description;
        }
        else
        {
            Debug.LogWarning("NPC Description Text is not assigned.");
        }
    }

    private string GetEquippedItemsDescription(NPC npc)
    {
        List<string> equippedItemsDescriptions = new List<string>();
        string mainHandDescription = null;
        string feetDescription = null;

        if (npc.EquippedItems == null || npc.EquippedItems.Count == 0 || npc.EquippedItems.All(item => item.Value == null))
        {
            return "They are currently naked! Look away!";
        }

        foreach (var item in npc.EquippedItems)
        {
            EquipmentSlot slot = item.Key;
            Item equippedItem = item.Value;

            if (equippedItem != null)
            {
                switch (slot)
                {
                    case EquipmentSlot.Head:
                    case EquipmentSlot.Face:
                    case EquipmentSlot.Body:
                    case EquipmentSlot.Legs:
                    case EquipmentSlot.Neck:
                    case EquipmentSlot.Waist:
                        string slotDescription = slot switch
                        {
                            EquipmentSlot.Head => "on their head",
                            EquipmentSlot.Face => "on their face",
                            EquipmentSlot.Body => "on their body",
                            EquipmentSlot.Legs => "on their legs",
                            EquipmentSlot.Neck => "around their neck",
                            EquipmentSlot.Waist => "around their waist",
                            _ => $"on their {slot.ToString().ToLower()}",
                        };
                        equippedItemsDescriptions.Add($"a {equippedItem.ItemInGameName} {slotDescription}");
                        break;

                    case EquipmentSlot.Feet:
                        feetDescription = $"And they're wearing {equippedItem.ItemInGameName} on their feet.";
                        break;

                    case EquipmentSlot.MainHand:
                        mainHandDescription = $"They are carrying a {equippedItem.ItemInGameName} in their main hand.";
                        break;

                        // We exclude OffHand as per the request.
                }
            }
            else
            {
                Debug.LogWarning($"{npc.Name} has no item equipped in {slot}");
            }
        }

        // Combine the descriptions
        string finalDescription = string.Join(", ", equippedItemsDescriptions);

        if (!string.IsNullOrEmpty(feetDescription))
        {
            finalDescription += (finalDescription.Length > 0 ? ", " : "") + feetDescription;
        }

        if (!string.IsNullOrEmpty(mainHandDescription))
        {
            finalDescription += (finalDescription.Length > 0 ? " " : "") + mainHandDescription;
        }

        return finalDescription;
    }



    public void CloseDialoguePanel()
    {
        dialoguePanel.SetActive(false);
        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
        PlayerController.Instance?.ClearInteractingWithTarget("DialoguePanelUI.CloseDialoguePanel");
        UIController.Instance.DeactivateGreyOutPanel();
        Debug.Log($"DialoguePanelUI: Closed dialogue panel for {currentNPC.Name}");
    }
}
