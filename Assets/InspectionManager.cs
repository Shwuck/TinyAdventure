using UnityEngine;
using System.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class InspectionManager : MonoBehaviour
{
    public static InspectionManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Inspect(IInteractable interactable)
    {
        if (interactable == null)
        {
            Debug.LogWarning("Tried to inspect a null object.");
            return;
        }

        Debug.Log($"Inspecting: {interactable.Name}");

        string inspectionText = interactable switch
        {
            NPC npc => GetNPCDescription(npc),
            Monster monster => GetMonsterDescription(monster),  //
            BaseObject obj => GetObjectDescription(obj),
            Item item => GetItemDescription(item),
            _ => $"You are inspecting {interactable.Name}: {interactable.Description ?? "No details available."}"
        };

        UIController.Instance.UpdateInspectionText(inspectionText);
        UIController.Instance.OpenInspectionPanel();
    }

    private string GetNPCDescription(NPC npc)
    {
        StringBuilder description = new StringBuilder();

        description.Append($"{npc.Name} stands before you, a {npc.Race.Name} {npc.Role}");

        if (npc.Age > 0)
        {
            description.Append(npc.Age switch
            {
                < 18 => ", barely an adult, their youthful features full of potential.",
                < 40 => ", moving with the confidence of their prime years.",
                < 60 => ", their presence steady, the weight of experience clear in their gaze.",
                _ => ", their face lined with age, each wrinkle telling a story of a life well-traveled."
            });
        }

        if (!string.IsNullOrEmpty(npc.Home))
        {
            description.Append($" They hail from {npc.Home}, a place that seems to have left its mark on them.");
        }

        string anatomyDescription = GetNotableAnatomyDescription(npc.Anatomy);
        if (!string.IsNullOrEmpty(anatomyDescription))
        {
            description.Append($" {anatomyDescription}");
        }

        description.Append($" Their stance suggests they are {GetMoodAndStance(npc.Stance)}.");

        string equippedItems = GetEquippedItemsDescription(npc);
        if (!string.IsNullOrEmpty(equippedItems))
        {
            description.Append($" {equippedItems}{(equippedItems.Contains("wear") || equippedItems.Contains("wield") ? "" : " adds to their overall appearance.")}.");
        }

        return description.ToString();
    }

    private string GetEquippedItemsDescription(NPC npc)
    {
        string torsoItem = npc.EquippedItems.TryGetValue(EquipmentSlot.Body, out Item torso) ? torso.ItemInGameName : null;
        string mainHandItem = npc.EquippedItems.TryGetValue(EquipmentSlot.MainHand, out Item mainHand) ? mainHand.ItemInGameName : null;

        if (torsoItem == null && mainHandItem == null)
            return "They don’t appear to be carrying anything significant.";

        if (torsoItem != null && mainHandItem != null)
            return $"They wear a {torsoItem}, with a {mainHandItem} at their side.";

        return torsoItem != null
            ? $"They wear a {torsoItem}, their posture suggesting confidence in its protection."
            : $"They hold a {mainHandItem}, fingers resting on its grip with familiarity.";
    }

    private string GetMonsterDescription(Monster monster)
    {
        StringBuilder description = new StringBuilder();

        description.Append($"The {monster.MonsterName}, a {monster.Type.ToString().ToLower()}, towers before you");

        if (monster.IsBoss)
        {
            description.Append(", its very presence crackling with power—this is no ordinary foe. It is a **Boss Monster**, radiating an aura of undeniable danger.");
        }

        description.Append($". Its health flickers at {monster.Health}/{monster.MaxHealth}, each movement betraying { (monster.Health > monster.MaxHealth * 0.7 ? "its unyielding strength" : "its growing fatigue") }.");

        description.Append($" With a strength of {monster.Strength} and speed of {monster.Speed}, it moves with { (monster.Speed > 10 ? "unsettling quickness, its strikes likely hard to avoid." : "a lumbering menace, every step deliberate and crushing.") }");

        string anatomyDescription = GetNotableAnatomyDescription(monster.Anatomy);
        if (!string.IsNullOrEmpty(anatomyDescription))
        {
            description.Append($" {anatomyDescription}");
        }

        string equippedItems = GetMonsterEquippedItemsDescription(monster);
        if (!string.IsNullOrEmpty(equippedItems))
        {
            description.Append($" {equippedItems.CapitalizeFirst()} completes its fearsome appearance.");
        }

        return description.ToString();
    }

    private string GetMonsterEquippedItemsDescription(Monster monster)
    {
        string torsoItem = monster.EquippedItems.TryGetValue(EquipmentSlot.Body, out Item torso) ? torso.ItemInGameName : null;
        string mainHandItem = monster.EquippedItems.TryGetValue(EquipmentSlot.MainHand, out Item mainHand) ? mainHand.ItemInGameName : null;

        List<string> descriptions = new List<string>();

        if (torsoItem != null)
        {
            descriptions.Add($"It wears a {torsoItem}, its form encased in protection.");
        }

        if (mainHandItem != null)
        {
            descriptions.Add($"It wields a {mainHandItem}, gripping it with deadly intent.");
        }

        return descriptions.Count > 0 ? string.Join(" ", descriptions) : "";
    }

    private string GetAnimalDescription(Animal animal)
    {
        StringBuilder description = new StringBuilder();

        description.Append($"You spot a {animal.Name}, a {animal.Size.ToString().ToLower()} creature that ");

        if (animal.IsPredator)
        {
            description.Append("carries itself with confidence, eyes scanning its surroundings for prey.");
        }
        else
        {
            description.Append("moves cautiously, ever watchful for threats.");
        }

        if (animal.IsDomestic)
        {
            description.Append(" It seems accustomed to human presence, standing relaxed.");
        }
        else if (animal.IsTame)
        {
            description.Append(" Though wild by nature, it appears tame, showing no fear.");
        }
        else
        {
            description.Append(" Its movements are tense, ready to flee at a moment’s notice.");
        }

        if (animal.IsPack)
        {
            description.Append(animal.IsLeader
                ? " This one leads the pack, setting the pace for the others."
                : " It moves in perfect harmony with its pack, never wandering too far.");
        }

        if (animal.IsHerd)
        {
            description.Append(" Staying close to its herd, it relies on numbers for safety.");
        }

        if (animal.Hide != HideType.None)
        {
            description.Append($" Its {animal.Hide.ToString().ToLower().Replace("_", " ")} offers a natural layer of protection.");
        }

        string anatomyDescription = GetNotableAnatomyDescription(animal.Anatomy);
        if (!string.IsNullOrEmpty(anatomyDescription))
        {
            description.Append($" {anatomyDescription}");
        }

        return description.ToString();
    }

    private string GetNotableAnatomyDescription(Anatomy anatomy)
    {
        if (anatomy == null || anatomy.BodyParts == null || anatomy.BodyParts.Count == 0)
            return "";

        var notableParts = new List<string>();

        foreach (var bodyPartList in anatomy.BodyParts.Values)
        {
            foreach (var part in bodyPartList)
            {
                if (part.IsLost)
                {
                    notableParts.Add($"missing {part.Name}");
                }
                else if (part.Scars >= ScarSeverity.Moderate)
                {
                    string scarDesc = part.Scars switch
                    {
                        ScarSeverity.Moderate => $"a scarred {part.Name}",
                        ScarSeverity.Many => $"a heavily scarred {part.Name}",
                        ScarSeverity.Disfigured => $"a disfigured {part.Name}",
                        _ => $"a marked {part.Name}"
                    };
                    notableParts.Add(scarDesc);
                }
                else if (part.Health < part.MaxHealth * 0.3f)
                {
                    notableParts.Add($"a wounded {part.Name}");
                }
            }
        }

        if (notableParts.Count == 0)
            return "";

        return $"Notably, {string.Join(", ", notableParts)}.";
    }

    private string GetMoodAndStance(NPCStance stance)
    {
        return stance switch
        {
            NPCStance.Hostile => "hostile towards you",
            NPCStance.Friendly => "pleased to see you",
            NPCStance.Fleeing => "nervous and ready to run",
            _ => "neutral"
        };
    }

    private string GetObjectDescription(BaseObject obj)
    {
        return obj.Description ?? "There doesn’t seem to be anything notable about it.";
    }

    private string GetItemDescription(Item item)
    {
        return $"{item.ItemInGameName}: {item.Description}";
    }
}


public static class StringExtensions
{
    public static string CapitalizeFirst(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpper(input[0]) + input.Substring(1);
    }
}

