using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Anatomy
{
    public string BodyType { get; private set; }
    public Dictionary<string, List<BodyPart>> BodyParts { get; private set; }

    public Anatomy(string bodyType)
    {
        BodyType = bodyType;
        BodyParts = new Dictionary<string, List<BodyPart>>();
    }

    public void AddBodyPart(BodyPart part)
    {
        // Only add top-level parts (parts without a parent) to BodyParts
        if (part.ParentPart == null)
        {
            if (!BodyParts.ContainsKey(part.BodyPartType))
                BodyParts[part.BodyPartType] = new List<BodyPart>();

            BodyParts[part.BodyPartType].Add(part);
        }
    }

    public bool HasBodyPart(string partName)
    {
        return BodyParts.ContainsKey(partName) && BodyParts[partName].Any(part => !part.IsLost);
    }

    public bool HasBodyPartType(string bodyPartType)
    {
        return BodyParts.Values.Any(parts => parts.Any(part => part.BodyPartType == bodyPartType && !part.IsLost));
    }

    private List<EquipmentSlot> activeEquipmentSlots = new List<EquipmentSlot>();

    /// **Updated to handle multiple Equipment Slots per Body Part**
    public List<EquipmentSlot> GetActiveEquipmentSlots()
    {
        List<EquipmentSlot> activeSlots = new List<EquipmentSlot>();

        void CheckBodyPartForEquipment(BodyPart part)
        {
            if (part.EquipmentSlots != null && part.EquipmentSlots.Count > 0 && !part.IsLost)
            {
                activeSlots.AddRange(part.EquipmentSlots);
            }
            foreach (var subPart in part.SubParts)
            {
                CheckBodyPartForEquipment(subPart);
            }
        }

        foreach (var bodyPartList in BodyParts.Values)
        {
            foreach (var part in bodyPartList)
            {
                CheckBodyPartForEquipment(part);
            }
        }

        return activeSlots.Distinct().ToList();
    }

    /// **Updated to track multiple equipment slots per body part**
    private void UpdateActiveEquipmentSlots()
    {
        activeEquipmentSlots.Clear();
        foreach (var bodyPartList in BodyParts.Values)
        {
            foreach (var part in bodyPartList)
            {
                if (part.EquipmentSlots != null && part.EquipmentSlots.Count > 0 && !part.IsLost)
                {
                    activeEquipmentSlots.AddRange(part.EquipmentSlots);
                }
            }
        }
    }

    /// **Updated to handle multiple Equipment Slots when removing a Body Part**
    public void RemoveBodyPart(string partName)
    {
        if (!BodyParts.ContainsKey(partName)) return;

        foreach (var part in BodyParts[partName])
        {
            foreach (var subPart in part.SubParts)
            {
                RemoveBodyPart(subPart.Name);
            }
        }

        BodyParts[partName].Clear();
        BodyParts.Remove(partName);
        UpdateActiveEquipmentSlots();
    }

    public bool CanEquipSlot(EquipmentSlot slot)
    {
        return GetActiveEquipmentSlots().Contains(slot);
    }

    public override string ToString()
    {
        return $"Anatomy ({BodyType}):\n" + string.Join("\n",
            BodyParts.Select(kvp =>
                $"- {kvp.Key} ({kvp.Value.Count}): " + string.Join(", ", kvp.Value.Select(part => part.ToString()))));
    }
}


public enum FunctionalityLevel
{
    Full,
    Partial,
    None
}

public enum BodyPosition
{
    High,
    Low,
    Both,
    Any,
    Complete
}

public class BodyPart
{
    public string Name { get; private set; }
    public string BodyPartType { get; private set; }
    public BodyPart ParentPart { get; private set; }
    public BodyPosition Position { get; private set; }
    public bool IsVital { get; private set; }
    public int MaxHealth { get; set; }
    public int Health { get; set; }
    public bool IsLost { get; private set; }
    public FunctionalityLevel Functionality { get; private set; }
    public List<BodyPart> SubParts { get; private set; }
    public List<EquipmentSlot> EquipmentSlots { get; private set; }
    public ScarSeverity Scars { get; set; } = ScarSeverity.None;

    // NEW FIELDS FROM BODYPARTDATA
    public bool BasePart { get; set; }
    public bool SubPart { get; set; }
    public bool HasSubs { get; set; }

    public BodyPart(BodyPartData partData, BodyPart parentPart = null, List<EquipmentSlot> equipmentSlots = null, Anatomy anatomy = null)
    {
        Name = partData.Name;
        BodyPartType = partData.BodyPartType;
        Position = Enum.TryParse(partData.Position, out BodyPosition pos) ? pos : BodyPosition.Both;
        MaxHealth = partData.MaxHealth;
        Health = partData.MaxHealth;
        IsVital = partData.IsVital;
        IsLost = false;
        Functionality = FunctionalityLevel.Full;
        ParentPart = parentPart;
        SubParts = new List<BodyPart>();

        EquipmentSlots = equipmentSlots ?? new List<EquipmentSlot>();

        // Assign boolean properties from BodyPartData
        BasePart = partData.BasePart;
        SubPart = partData.SubPart;
        HasSubs = partData.HasSubs;
    }


    public void AddSubPart(BodyPart part)
    {
        if (!SubParts.Contains(part))
        {
            SubParts.Add(part);
            part.ParentPart = this;
        }
    }

    public void SetParent(BodyPart parent)
    {
        ParentPart = parent;
    }

    public void TakeDamage(int damage, Character owner)
    {
        if (IsLost) return;

        if (SubParts.Count > 0)
        {
            int subPartDamage = Mathf.FloorToInt(damage * 0.5f);
            int mainPartDamage = damage - subPartDamage;

            foreach (var subPart in SubParts)
            {
                if (!subPart.IsLost)
                {
                    subPart.TakeDamage(subPartDamage, owner);
                }
            }

            ApplyDamage(mainPartDamage, owner);
        }
        else
        {
            ApplyDamage(damage, owner);
        }
    }


    private void ApplyDamage(int damage, Character owner)
    {
        Health -= damage;

        if (Health <= 0)
        {
            int overflowDamage = -Health;
            if (TryToSavePart(owner))
            {
                Health = 1;
                GameDebugger.Instance.LogInfo($"{owner.Name}'s {Name} barely survived at 1 HP.");
            }
            else
            {
                LosePart();
                if (overflowDamage > 0 && IsVital && ParentPart != null)
                {
                    ParentPart.ApplyDamage(overflowDamage, owner);
                }
            }
        }
        else
        {
            UpdateFunctionality();
        }
    }


    public void IncreaseScar()
    {
        if (Scars < ScarSeverity.Disfigured) // Prevent exceeding max level
        {
            Scars += 1;
            GameDebugger.Instance.LogInfo($"{Name} has increased scarring. New Scar Level: {Scars}");
            UpdateFunctionality();
        }
    }

    public void ReduceScar()
    {
        if (Scars > ScarSeverity.None) // Prevent going below 0
        {
            Scars -= 1;
            GameDebugger.Instance.LogInfo($"{Name}'s scars have faded. New Scar Level: {Scars}");
            UpdateFunctionality();
        }
    }

    private bool TryToSavePart(Character owner)
    {
        if (owner == null)
        {
            GameDebugger.Instance.LogError("TryToSavePart called without a valid character reference.");
            return false;
        }

        int constitution = owner.Constitution;
        int baseChance = 20 + (constitution * 5);
        int finalChance = Mathf.Clamp(baseChance, 10, 90);

        int roll = UnityEngine.Random.Range(0, 100);
        GameDebugger.Instance.LogInfo($"{owner.Name} attempts to save {Name}. Constitution: {constitution}, Save Chance: {finalChance}%, Roll: {roll}");

        bool saved = roll < finalChance;

        if (saved && Health == 0) // Ensure scars only increase on true survival events
        {
            Health = 1;
            if (Scars < ScarSeverity.Disfigured)
            {
                IncreaseScar();
            }
            GameDebugger.Instance.LogInfo($"{owner.Name}'s {Name} barely survived but is now scarred (Scar Level: {Scars}).");
        }

        return saved;
    }

    public void LosePart()
    {
        IsLost = true;
        Health = 0;
        Functionality = FunctionalityLevel.None;
        GameDebugger.Instance.LogInfo($"{Name} is now lost!");

        foreach (var subPart in SubParts)
        {
            subPart.LosePart();
        }

        if (IsVital)
        {
            GameDebugger.Instance.LogInfo($"{Name} was vital! Character may die!");
        }

        if (ParentPart != null && !ParentPart.IsLost)
        {
            ParentPart.UpdateFunctionality();
        }
    }

    private void UpdateFunctionality()
    {
        if (Health <= MaxHealth * 0.1f)
        {
            Functionality = FunctionalityLevel.None;
        }
        else if (Health <= MaxHealth * 0.3f)
        {
            Functionality = FunctionalityLevel.Partial;
        }
        else
        {
            Functionality = FunctionalityLevel.Full;
        }

        FunctionalityLevel lowestSubPartFunctionality = FunctionalityLevel.Full;
        int functioningFingers = 0;

        foreach (var subPart in SubParts)
        {
            if (subPart.Functionality < lowestSubPartFunctionality)
            {
                lowestSubPartFunctionality = subPart.Functionality;
            }

            if (subPart.Name == "Finger" && subPart.Functionality != FunctionalityLevel.None)
            {
                functioningFingers++;
            }
        }

        if (Name.Contains("Hand"))
        {
            if (functioningFingers < 3) Functionality = FunctionalityLevel.None;
            else if (functioningFingers < 5) Functionality = FunctionalityLevel.Partial;
        }

        Functionality = lowestSubPartFunctionality < Functionality ? lowestSubPartFunctionality : Functionality;
    }

    public bool HasFunctioningSubPart(string partName)
    {
        return SubParts.Exists(subPart => subPart.Name == partName && subPart.Functionality != FunctionalityLevel.None);
    }

    public override string ToString()
    {
        string equipmentSlotsInfo = EquipmentSlots.Count > 0
            ? $" (Equip Slots: {string.Join(", ", EquipmentSlots)})"
            : "";
        return $"{Name}{equipmentSlotsInfo} {(IsLost ? "[LOST]" : $"({Health}/{MaxHealth}) - {Functionality}")}";
    }

}

public enum ScarSeverity
{
    None = 0,       // No scars
    Light = 1,      // Minor scars, cosmetic only
    Moderate = 2,   // Noticeable scars
    Many = 3,       // Significant scarring
    Disfigured = 4  // Major scars, possible functionality loss
}
