using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;
using System;

public class MessageLogManager : MonoBehaviour
{
    public static MessageLogManager Instance { get; private set; }

    private Queue<Message> messages = new Queue<Message>(15); // Latest 15 messages
    private List<Message> fullLogHistory = new List<Message>(); // Full log history
    public MessageLogUIManager messageLogUIManager;

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

    public void Log(string eventType, params object[] args)
    {
        string newMessageText = FormatMessage(eventType, args);
        MessageType messageType = GetMessageType(eventType);

        bool isStackable = IsStackable(eventType);

        if (messages.Count > 0 && isStackable && messages.Last().Type == messageType && messages.Last().Text.Contains(args[0].ToString()))
        {
            messages.Last().UpdateCount();
        }
        else
        {
            AddMessage(new Message(GetCurrentTimeStamp(), messageType, newMessageText));
            messageLogUIManager.Refresh();
        }
    }

    public void AddMessage(Message message)
    {
        if (messages.Count >= 15)
        {
            messages.Dequeue();
        }
        messages.Enqueue(message);
        fullLogHistory.Add(message);

        messageLogUIManager.UpdateDisplay(messages.ToList());
        UIController.Instance?.UpdateMessageLogUI();
    }

    public void SaveLogToFile()
    {
        string logText = string.Join("\n", fullLogHistory.Select(m => $"{m.TimeStamp}: {m.Text}"));
        File.WriteAllText("MessageLog.txt", logText);
    }

    private string FormatMessage(string eventType, object[] args)
    {
        try
        {
            return eventType switch
            {
                // Combat Messages
                "combat_hit" when args.Length >= 5 =>
                    $"{args[0]} <color=red>hits</color> {args[1]}'s {args[3]} for <color=yellow>{args[2]}</color> {GetDamageTypeColor(args[4].ToString())}{args[4]}</color> damage.",

                "combat_critical" when args.Length >= 5 =>
                    $"<color=orange>CRITICAL!</color> {args[0]} <color=red>devastates</color> {args[1]}'s {args[3]} for <color=yellow>{args[2]}</color> {GetDamageTypeColor(args[4].ToString())}{args[4]}</color> damage!",

                "combat_miss" when args.Length >= 2 =>
                    $"{args[0]} <color=grey>misses</color> {args[1]}.",

                "combat_armor_block" when args.Length >= 3 =>
                    $"{args[1]}'s <color=blue>armor</color> absorbs <color=yellow>{args[2]}</color> damage from {args[0]}!",

                "combat_status" when args.Length >= 3 =>
                    $"{args[0]} is now <color=purple>{args[1]}</color>! ({args[2]} turns)",

                "combat_start" when args.Length >= 1 =>
                    $"<color=red>Combat started in {args[0]}.</color>",

                "combat_player_turn" when args.Length >= 3 =>
                    $"<color=green>Your turn.</color> AP: {args[1]}, MP: {args[2]}.",

                "combat_enemy_turn" when args.Length >= 1 =>
                    $"<color=orange>Enemy turn: {args[0]}.</color>",

                "combat_bystander_turn" when args.Length >= 1 =>
                    $"<color=grey>Bystander turn: {args[0]}.</color>",

                "combat_animal_turn" when args.Length >= 1 =>
                    $"<color=yellow>Animal turn: {args[0]}.</color>",

                "combat_monster_turn" when args.Length >= 1 =>
                    $"<color=orange>Monster turn: {args[0]}.</color>",

                "combat_wait_turn" when args.Length >= 1 =>
                    $"<color=grey>Wait for your turn. {args[0]} is acting.</color>",

                "combat_no_ap" when args.Length >= 1 =>
                    $"<color=grey>Not enough AP for {args[0]}.</color>",

                "combat_no_mp" when args.Length >= 1 =>
                    $"<color=grey>Not enough MP to {args[0]}.</color>",

                "combat_auto_end_no_resources" =>
                    "<color=grey>No AP or MP remaining. Ending turn.</color>",

                "combat_no_resources_manual_end" =>
                    "<color=grey>No AP or MP remaining. Press End Turn.</color>",

                "combat_manual_end_turn" =>
                    "<color=grey>Turn ended.</color>",

                "combat_auto_end_on" =>
                    "<color=grey>Auto-End Turn: On.</color>",

                "combat_auto_end_off" =>
                    "<color=grey>Auto-End Turn: Off.</color>",

                "item" when args.Length >= 1 =>
                    $"<color=yellow>You picked up {args[0]}.</color>",

                "exploration" when args.Length >= 2 =>
                    $"<color=green>{args[0]} {args[1]}.</color>",

                "social" when args.Length >= 2 =>
                    $"<color=blue>{args[0]} said: \"{args[1]}\"</color>",

                "special" when args.Length >= 1 =>
                    $"<color=purple>Special action performed: {args[0]}.</color>",

                _ => $"<color=gray>Unknown event: {eventType} (args={string.Join(", ", args)})</color>"
            };
        }
        catch (Exception e)
        {
            GameDebugger.Instance.LogError($"MessageLogManager: Error formatting message: {eventType}, {e.Message}");
            return $"[Error processing message: {eventType}]";
        }
    }

    private string GetDamageTypeColor(string damageType)
    {
        return damageType switch
        {
            "Fire" => "<color=orange>",
            "Ice" => "<color=cyan>",
            "Piercing" => "<color=lightblue>",
            "Slashing" => "<color=red>",
            "Bludgeoning" => "<color=yellow>",
            _ => "<color=white>"
        };
    }

    private MessageType GetMessageType(string eventType)
    {
        return eventType switch
        {
            "combat_hit" => MessageType.Combat,
            "combat_critical" => MessageType.Combat,
            "combat_miss" => MessageType.Combat,
            "combat_start" => MessageType.Combat,
            "combat_player_turn" => MessageType.Combat,
            "combat_enemy_turn" => MessageType.Combat,
            "combat_bystander_turn" => MessageType.Combat,
            "combat_animal_turn" => MessageType.Combat,
            "combat_monster_turn" => MessageType.Combat,
            "combat_wait_turn" => MessageType.Combat,
            "combat_no_ap" => MessageType.Combat,
            "combat_no_mp" => MessageType.Combat,
            "combat_auto_end_no_resources" => MessageType.Combat,
            "combat_no_resources_manual_end" => MessageType.Combat,
            "combat_manual_end_turn" => MessageType.Combat,
            "combat_auto_end_on" => MessageType.Combat,
            "combat_auto_end_off" => MessageType.Combat,
            "combat_status" => MessageType.StatusEffect,
            "combat_armor_block" => MessageType.Defensive,
            "item" => MessageType.Item,
            "exploration" => MessageType.Exploration,
            "social" => MessageType.Social,
            "special" => MessageType.Special,
            _ => MessageType.Other
        };
    }

    private bool IsStackable(string eventType)
    {
        return eventType switch
        {
            "combat_hit" => false,
            "combat_miss" => false,
            "combat_critical" => false,
            "combat_start" => false,
            "combat_player_turn" => false,
            "combat_enemy_turn" => false,
            "combat_bystander_turn" => false,
            "combat_animal_turn" => false,
            "combat_monster_turn" => false,
            "combat_wait_turn" => false,
            "combat_no_ap" => false,
            "combat_no_mp" => false,
            "combat_auto_end_no_resources" => false,
            "combat_no_resources_manual_end" => false,
            "combat_manual_end_turn" => false,
            "combat_auto_end_on" => false,
            "combat_auto_end_off" => false,
            "combat_status" => false,
            "combat_armor_block" => false,
            _ => true  // Only stack non-combat, repeatable messages
        };
    }

    private string GetCurrentTimeStamp()
    {
        return $"[{TimeManager.Instance.GetCurrentTimeFormatted()}]";
    }

    public void ClearMessages()
    {
        messages.Clear();
        messageLogUIManager.UpdateDisplay(new List<Message>());
        UIController.Instance?.UpdateMessageLogUI();
    }
}

public class Message
{
    public string TimeStamp { get; private set; }
    public MessageType Type { get; private set; }
    public string Text { get; private set; }
    private int stackCount = 1;

    public Message(string timeStamp, MessageType type, string text)
    {
        TimeStamp = timeStamp;
        Type = type;
        Text = text;
    }

    public void UpdateCount()
    {
        stackCount++;
        Text = Text.Contains("(x") ? Text.Substring(0, Text.IndexOf("(x")) : Text;
        Text += $" (x{stackCount})";
    }
}

public enum MessageType
{
    Combat,
    StatusEffect,
    Defensive,
    Item,
    Exploration,
    Social,
    Special,
    Other
}
