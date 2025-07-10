using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerCharacter : Character
{
    public int PlayerCharacterID { get; set; }
    public string CharacterName { get; set; }
    public int PlayerCharacterTurnStarted { get; set; }

    public string FirstName;
    public string Surname { get; set; }
    public string FullName => $"{FirstName} {Surname}";

    public Race CharacterRace { get; set; }
    public Background CharacterBackground { get; set; }

    // Player Base Stats taken from Race.
    public int BaseStrengthFromRace { get; set; }
    public int BaseDexterityFromRace { get; set; }
    public int BaseConstitutionFromRace { get; set; }
    public int BaseIntelligenceFromRace { get; set; }
    public int BaseWisdomFromRace { get; set; }
    public int BaseCharismaFromRace { get; set; }
    public int BaseLuckFromRace { get; set; }

    // Derived Stats
    public int MaxSatiety { get; set; }
    public int MaxStamina { get; set; }

    public PlayerCharacter() : base()
    {
        Inventory = PlayerInventory.Instance;
        Inventory.Owner = this;
        PlayerInventory.Instance.SwitchCharacterInventory(this);
        Inventory.Owner = this;
    }

    protected override void OnDeath()
    {
        Debug.Log($"Player Character {Name} has died!");

        PlayerStats.Instance.Die();

        IsActive = false;  // Mark the NPC as inactive
    }

}

public class PlayerCharacterFactory
{
    public static PlayerCharacter CreatePlayerCharacter(
        int id, string firstName, string surname, Race race, SubRace subRace, Background background,
        int birthdayDay, Season birthdaySeason, int birthdayYear)
    {
        PlayerInventory.Initialize();
        // Use subrace stats if available, otherwise use race stats
        int baseStrength = subRace != null ? subRace.BaseStrength : race.BaseStrength;
        int baseDexterity = subRace != null ? subRace.BaseDexterity : race.BaseDexterity;
        int baseConstitution = subRace != null ? subRace.BaseConstitution : race.BaseConstitution;
        int baseIntelligence = subRace != null ? subRace.BaseIntelligence : race.BaseIntelligence;
        int baseWisdom = subRace != null ? subRace.BaseWisdom : race.BaseWisdom;
        int baseCharisma = subRace != null ? subRace.BaseCharisma : race.BaseCharisma;
        int baseLuck = subRace != null ? subRace.BaseLuck : race.BaseLuck;

        // Apply background stat modifiers
        int strength = baseStrength + GetStatModifier(background, "Strength");
        int dexterity = baseDexterity + GetStatModifier(background, "Dexterity");
        int constitution = baseConstitution + GetStatModifier(background, "Constitution");
        int intelligence = baseIntelligence + GetStatModifier(background, "Intelligence");
        int wisdom = baseWisdom + GetStatModifier(background, "Wisdom");
        int charisma = baseCharisma + GetStatModifier(background, "Charisma");
        int luck = baseLuck + GetStatModifier(background, "Luck");

        // Example values for derived stats
        int maxSatiety = 100 + constitution * 2;
        int maxStamina = 100 + dexterity * 2;
        int maxHealth = constitution * 2;
        int maxActionPoints = dexterity * 2;
        int maxMovePoints = 2;

        string bodyType = string.IsNullOrEmpty(race.BodyType) ? "Humanoid" : race.BodyType;
        Anatomy anatomy = AnatomyGenerator.Instance.GenerateAnatomy(bodyType);

        if (anatomy == null)
        {
            Debug.LogError($"[PlayerCharacterFactory] Failed to generate anatomy for body type: {bodyType}");
            return null;
        }


        // Create Player Character
        PlayerCharacter newCharacter = new PlayerCharacter
        {
            IInteractableID = id,
            FirstName = firstName,
            Surname = surname,
            CharacterRace = race,
            CharacterBackground = background,
            BaseStrengthFromRace = baseStrength,
            BaseDexterityFromRace = baseDexterity,
            BaseConstitutionFromRace = baseConstitution,
            BaseIntelligenceFromRace = baseIntelligence,
            BaseWisdomFromRace = baseWisdom,
            BaseCharismaFromRace = baseCharisma,
            BaseLuckFromRace = baseLuck,
            Strength = strength,
            Dexterity = dexterity,
            Constitution = constitution,
            Intelligence = intelligence,
            Wisdom = wisdom,
            Charisma = charisma,
            Luck = luck,
            MaxHealth = maxHealth,
            Health = maxHealth,
            MaxActionPoints = maxActionPoints,
            ActionPoints = maxActionPoints,
            MaxMovePoints = maxMovePoints,
            MovePoints = maxMovePoints,
            MaxSatiety = maxSatiety,
            MaxStamina = maxStamina,
            BirthdayDay = birthdayDay,
            BirthdaySeason = birthdaySeason,
            BirthdayYear = birthdayYear,
            IsAlive = true,
            IsActive = true,
            Name = $"{firstName} {surname}",
            Spouse = null,
            Ancestors = new List<Character>(),
            Relationships = new Dictionary<int, float>(),
            Anatomy = anatomy
        };


        AssignBackgroundSpecificLoadout(newCharacter);
        Debug.Log($"Checking {newCharacter.FullName}'s inventory after loadout:");
        foreach (var container in newCharacter.Inventory.GetInventoryContainers())
        {
            Debug.Log($"- {container.Name}: {container.Amount}");
        }
        PermaLists.Instance.PlayerCharacters.Add(newCharacter);

        PlayerInventory.Instance.SwitchCharacterInventory(newCharacter);

        // Call Debug Logging
        CreatePlayerCharacterDebug(newCharacter);

        return newCharacter;
    }

    private static int GetStatModifier(Background background, string statName)
    {
        return background.StatModifiers.ContainsKey(statName) ? background.StatModifiers[statName] : 0;
    }

    private static void AssignBackgroundSpecificLoadout(PlayerCharacter character)
    {
        if (PermaLists.Instance.Loadouts == null)
        {
            GameDebugger.Instance.LogWarning("Loadouts list is null. Skipping loadout assignment.");
            return;
        }

        if (character.CharacterBackground == null)
        {
            GameDebugger.Instance.LogWarning($"Character {character.FullName} has no background assigned. Skipping loadout assignment.");
            return;
        }

        // Find applicable loadouts for the player's background
        var applicableLoadouts = PermaLists.Instance.Loadouts
            .Where(loadout => loadout.ApplicablePlayerBackgrounds != null &&
                              loadout.ApplicablePlayerBackgrounds.Contains(character.CharacterBackground.Name))
            .ToList();

        if (!applicableLoadouts.Any())
        {
            GameDebugger.Instance.LogWarning($"No applicable loadouts found for background {character.CharacterBackground.Name}. Skipping loadout assignment.");
            return;
        }

        // Select a random loadout from the available options
        var selectedLoadout = applicableLoadouts[UnityEngine.Random.Range(0, applicableLoadouts.Count)];
        character.Loadout = selectedLoadout;
        character.Money = selectedLoadout.Money;

        GameDebugger.Instance.LogInfo($"Selected loadout '{selectedLoadout.LoadoutName}' for {character.FullName}.");

        // EQUIPPING ITEMS
        if (selectedLoadout.Equipment != null && selectedLoadout.Equipment.Any())
        {
            GameDebugger.Instance.LogInfo($"Assigning equipment from loadout '{selectedLoadout.LoadoutName}'...");

            foreach (var equipmentSlot in selectedLoadout.Equipment.Keys)
            {
                if (selectedLoadout.Equipment[equipmentSlot] == null)
                {
                    GameDebugger.Instance.LogWarning($"Equipment slot {equipmentSlot} in loadout {selectedLoadout.LoadoutName} has a null item. Skipping.");
                    continue;
                }

                Item item = ItemGenerator.Instance.GenerateItem(selectedLoadout.Equipment[equipmentSlot]);
                if (item != null)
                {
                    GameDebugger.Instance.LogInfo($"Generated item '{item.ItemInGameName}' for slot {equipmentSlot}. Equipping to {character.FullName}.");

                    PlayerInventory.Instance.AddItem(item);

                    PlayerInventory.Instance.EquipItem(item, equipmentSlot);
                    item.IsEquipped = true;
                }
                else
                {
                    GameDebugger.Instance.LogWarning($"Failed to generate item '{selectedLoadout.Equipment[equipmentSlot]}' for slot {equipmentSlot}. Skipping.");
                }
            }
        }
        else
        {
            GameDebugger.Instance.LogWarning($"Selected loadout '{selectedLoadout.LoadoutName}' has no equipment defined.");
        }

        // ADDING INVENTORY ITEMS
        if (selectedLoadout.Inventory != null && selectedLoadout.Inventory.Any())
        {
            GameDebugger.Instance.LogInfo($"Adding inventory items from loadout '{selectedLoadout.LoadoutName}'...");

            foreach (var inventoryItem in selectedLoadout.Inventory)
            {
                if (inventoryItem.Key == null)
                {
                    GameDebugger.Instance.LogWarning($"Null item key found in inventory of loadout '{selectedLoadout.LoadoutName}'. Skipping.");
                    continue;
                }

                for (int i = 0; i < inventoryItem.Value; i++)
                {
                    Item item = ItemGenerator.Instance.GenerateItem(inventoryItem.Key);
                    if (item != null)
                    {
                        GameDebugger.Instance.LogInfo($"Adding '{item.ItemInGameName}' to {character.FullName}'s inventory.");
                        PlayerInventory.Instance.AddItem(item);
                    }
                    else
                    {
                        GameDebugger.Instance.LogWarning($"Failed to generate inventory item '{inventoryItem.Key}'. Skipping.");
                    }
                }
            }
        }
        else
        {
            GameDebugger.Instance.LogWarning($"Selected loadout '{selectedLoadout.LoadoutName}' has no inventory items defined.");
        }

        // FINAL INVENTORY CHECK
        GameDebugger.Instance.LogInfo($"Final inventory check for {character.FullName}:");
        var inventoryContents = PlayerInventory.Instance.GetInventoryContainers();

        if (inventoryContents.Count == 0)
        {
            GameDebugger.Instance.LogWarning($"{character.FullName}'s inventory is empty after loadout assignment!");
        }
        else
        {
            System.Text.StringBuilder inventoryLog = new System.Text.StringBuilder();
            inventoryLog.AppendLine($"Final Inventory for {character.FullName}:");

            foreach (var container in inventoryContents)
            {
                inventoryLog.AppendLine($"- {container.Name}: {container.Amount}");
            }

            GameDebugger.Instance.LogInfo(inventoryLog.ToString());
        }
    }

    public static PlayerCharacter GenerateDefaultCharacter()
    {
        int playerID = GameManager.Instance != null ? GameManager.Instance.GetPlayerCharacterID() : 0;
        PlayerInventory.Initialize();

        string defaultFirstName = "John";
        string defaultSurname = "Doe";
        Race defaultRace = PermaLists.Instance.Races.Find(r => r.Name == "Human");
        SubRace defaultSubRace = null;
        Background defaultBackground = PermaLists.Instance.Backgrounds.Find(b => b.Name == "Commoner");

        int defaultBirthdayDay = 1;
        Season defaultBirthdaySeason = Season.Spring;
        int defaultBirthdayYear = 2000;

        PlayerCharacter newCharacter = CreatePlayerCharacter(
            playerID, defaultFirstName, defaultSurname, defaultRace, defaultSubRace, defaultBackground,
            defaultBirthdayDay, defaultBirthdaySeason, defaultBirthdayYear
        );

        PlayerInventory.Instance.SwitchCharacterInventory(newCharacter);
        AssignBackgroundSpecificLoadout(newCharacter);

        Debug.Log($"Generated Default Character: {newCharacter.FullName}");

        return newCharacter;
    }

    public static void CreatePlayerCharacterDebug(PlayerCharacter character)
    {
        System.Text.StringBuilder logBuilder = new System.Text.StringBuilder();

        logBuilder.AppendLine("===== Created Player Character =====");
        logBuilder.AppendLine($"Name: {character.FullName}");

        if (character.Anatomy == null)
        {
            logBuilder.AppendLine("Anatomy is NULL!");
            Debug.Log(logBuilder.ToString());
            return;
        }

        logBuilder.AppendLine($"Anatomy Type: {character.Anatomy.BodyType}\n");
        logBuilder.AppendLine("Assigned Body Parts:");

        // Recursive function to log the hierarchy of body parts with sub-part count and equipment slots
        void LogBodyPartHierarchy(BodyPart part, int depth)
        {
            string indent = new string(' ', depth * 2);
            string equipInfo = (part.EquipmentSlots != null && part.EquipmentSlots.Count > 0)
                ? $" (Equip Slots: {string.Join(", ", part.EquipmentSlots)})"
                : "";
            string subPartInfo = part.SubParts.Count > 0 ? $" (Sub-Parts: {part.SubParts.Count})" : "";

            logBuilder.AppendLine($"{indent}- {part.Name}{equipInfo} (Health: {part.Health}/{part.MaxHealth}, Functionality: {part.Functionality}, Lost: {part.IsLost}){subPartInfo}");

            foreach (var subPart in part.SubParts)
            {
                LogBodyPartHierarchy(subPart, depth + 1);
            }
        }

        // Log all body parts
        if (character.Anatomy.BodyParts.Count == 0)
        {
            logBuilder.AppendLine("No body parts assigned!");
        }
        else
        {
            foreach (var bodyPartList in character.Anatomy.BodyParts.Values)
            {
                foreach (var part in bodyPartList)
                {
                    if (part.ParentPart == null) // Only log top-level parts
                    {
                        LogBodyPartHierarchy(part, 1);
                    }
                }
            }
        }

        // Equipment Slots Debugging
        logBuilder.AppendLine("\n=== Equipment Slots Debugging ===");

        List<EquipmentSlot> activeSlots = character.Anatomy.GetActiveEquipmentSlots();

        foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
        {
            bool isAvailable = activeSlots.Contains(slot);
            string status = isAvailable ? "<color=green>Available</color>" : "<color=red>Unavailable</color>";
            string equippedItem = character.EquippedItems.TryGetValue(slot, out Item item) ? item.ItemInGameName : "Empty";

            logBuilder.AppendLine($"  - {slot}: {status} | Equipped: {equippedItem}");
        }

        logBuilder.AppendLine("===================================");
        Debug.Log(logBuilder.ToString());
    }

    }
