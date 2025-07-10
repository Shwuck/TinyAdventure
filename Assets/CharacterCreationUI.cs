using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class CharacterCreationUI : MonoBehaviour
{
    public TMP_InputField firstNameInput;
    public TMP_InputField surnameInput;
    public TMP_Dropdown raceDropdown;
    public TMP_Dropdown subraceDropdown;
    public TMP_Dropdown backgroundDropdown;
    public TMP_Text raceDescriptionText;
    public TMP_Text backgroundDescriptionText;
    public Button createCharacterButton;
    public Button randomiseButton;
    public TMP_Text previewText;

    private void Start()
    {
        PopulateRaceDropdown();
        PopulateBackgroundDropdown();

        // Setup listeners
        SetupListeners();

        // Initial UI Updates
        UpdateSubraceDropdown();
        UpdateBackgroundDescriptionAndModifiers();
        UpdatePreviewText();
        UpdateCreateButtonInteractability();
    }

    private void SetupListeners()
    {
        // Input fields and dropdowns to update the preview text
        firstNameInput.onValueChanged.AddListener(_ => UpdatePreviewText());
        surnameInput.onValueChanged.AddListener(_ => UpdatePreviewText());

        // Randomise button listener
        randomiseButton.onClick.AddListener(RandomiseCharacter); // Add listener for randomiseButton

        // When the background changes, update both the preview text and the background description and modifiers
        backgroundDropdown.onValueChanged.AddListener(_ =>
        {
            UpdatePreviewText();
            UpdateBackgroundDescriptionAndModifiers();
            UpdateCreateButtonInteractability();
        });

        // Race changes affect subraces, descriptions, and modifiers
        raceDropdown.onValueChanged.AddListener(_ =>
        {
            UpdateSubraceDropdown();
            UpdateRaceOrSubraceDescriptionAndModifiers();
            UpdatePreviewText();
            UpdateCreateButtonInteractability();
        });

        // Subrace selection affects the race or subrace description and modifiers
        subraceDropdown.onValueChanged.AddListener(_ =>
        {
            UpdateRaceOrSubraceDescriptionAndModifiers();
            UpdatePreviewText();
            UpdateCreateButtonInteractability();
        });

        // Create character button
        createCharacterButton.onClick.AddListener(OnCreateCharacterClicked);
    }

    private void UpdatePreviewText()
    {
        string firstName = firstNameInput.text;
        string surname = surnameInput.text;
        string race = raceDropdown.options[raceDropdown.value].text;
        string subrace = subraceDropdown.options.Count > 0 ? subraceDropdown.options[subraceDropdown.value].text : "";
        string background = backgroundDropdown.options[backgroundDropdown.value].text;

        // Determine whether to show race or subrace
        string raceOrSubrace = string.IsNullOrEmpty(subrace) ? race : subrace;

        // Adjust the size for the name and make the race or subrace and background bold
        previewText.text = $"You will be known as:\n" +
                           $"<size=150%>{firstName} {surname}</size>\n" +
                           $"The <b>{raceOrSubrace} {background}</b>";
    }

    private void UpdateCreateButtonInteractability()
    {
        string raceName = raceDropdown.options[raceDropdown.value].text;
        string backgroundName = backgroundDropdown.options[backgroundDropdown.value].text;
        string subraceName = subraceDropdown.options.Count > 0 ? subraceDropdown.options[subraceDropdown.value].text : "";

        Race race = PermaLists.Instance.Races.Find(r => r.Name == raceName);
        Background background = PermaLists.Instance.Backgrounds.Find(b => b.Name == backgroundName);
        SubRace subRace = race.HasSubRace ? race.SubRaces.Find(sr => sr.Name == subraceName) : null;

        bool isRaceUnlocked = race != null && race.IsUnlocked;
        bool isBackgroundUnlocked = background != null && background.IsUnlocked;
        bool isSubRaceUnlocked = subRace == null || subRace.IsUnlocked;

        createCharacterButton.interactable = isRaceUnlocked && isBackgroundUnlocked && isSubRaceUnlocked;
    }

    private void OnCreateCharacterClicked()
    {
        // Ensure dropdowns exist
        if (raceDropdown == null || backgroundDropdown == null)
        {
            Debug.LogError("Dropdowns are not assigned in the CharacterCreationUI.");
            return;
        }

        if (raceDropdown.options.Count == 0 || backgroundDropdown.options.Count == 0)
        {
            Debug.LogError("Race or Background dropdowns have no options.");
            return;
        }

        // Validate input fields
        if (string.IsNullOrWhiteSpace(firstNameInput.text) || string.IsNullOrWhiteSpace(surnameInput.text))
        {
            Debug.LogWarning("First name and surname cannot be empty.");
            return;
        }

        // Ensure GameManager exists before using it
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager instance is null. Cannot generate player ID.");
            return;
        }
        int playerID = GameManager.Instance.GetPlayerCharacterID();

        // Ensure PermaLists is loaded
        if (PermaLists.Instance == null || PermaLists.Instance.Races == null || PermaLists.Instance.Backgrounds == null)
        {
            Debug.LogError("PermaLists is not initialized properly. Cannot create a character.");
            return;
        }

        // Fetch selections from UI
        string firstName = firstNameInput.text;
        string surname = surnameInput.text;
        string raceName = raceDropdown.options[raceDropdown.value].text;
        string backgroundName = backgroundDropdown.options[backgroundDropdown.value].text;
        string subraceName = subraceDropdown.options.Count > 0 ? subraceDropdown.options[subraceDropdown.value].text : "";

        // Retrieve the Race and Background
        Race race = PermaLists.Instance.Races.Find(r => r.Name == raceName);
        Background background = PermaLists.Instance.Backgrounds.Find(b => b.Name == backgroundName);

        if (race == null || background == null)
        {
            Debug.LogError($"Race or Background not found. Race: {raceName}, Background: {backgroundName}");
            return;
        }

        // Retrieve SubRace if applicable
        SubRace subRace = null;
        if (race.HasSubRace && race.SubRaces != null)
        {
            subRace = race.SubRaces.Find(sr => sr.Name == subraceName);
        }

        if (race.HasSubRace && subRace == null)
        {
            Debug.LogWarning($"Race {raceName} has subraces, but no valid subrace was found.");
        }

        // Remove any previously created character to prevent duplicates
        if (PlayerStats.Instance.CurrentPlayerCharacter != null)
        {
            Debug.LogWarning($"Overwriting previous character: {PlayerStats.Instance.CurrentPlayerCharacter.FullName}");
            PermaLists.Instance.PlayerCharacters.Remove(PlayerStats.Instance.CurrentPlayerCharacter);
        }

        // Create the player character
        PlayerCharacter newCharacter = PlayerCharacterFactory.CreatePlayerCharacter(
            id: playerID,
            firstName: firstName,
            surname: surname,
            race: race,
            subRace: subRace,
            background: background,
            birthdayDay: 1,
            birthdaySeason: Season.Spring,
            birthdayYear: 2000
        );

        if (newCharacter == null)
        {
            Debug.LogError("Character creation failed. Factory returned null.");
            return;
        }

        // Ensure PlayerStats exists before adding the character
        if (PlayerStats.Instance == null)
        {
            Debug.LogError("PlayerStats is not initialized.");
            return;
        }

        // Set new character as active
        PlayerStats.Instance.CurrentPlayerCharacter = newCharacter;
        PlayerStats.Instance.AddPlayerCharacter(newCharacter);  // This already adds it to PermaLists
        PlayerInventory.Instance.SwitchCharacterInventory(newCharacter);

        // Update game state
        GameManager.Instance.PlayerSet = true;

        Debug.Log($"Created new character: {newCharacter.FullName}");
    }



    private void PopulateRaceDropdown()
    {
        raceDropdown.ClearOptions();
        List<string> raceNames = new List<string>();

        foreach (var race in PermaLists.Instance.Races)
        {
            if (race.IsUnlocked)
            {
                raceNames.Add(race.Name);
            }
            else
            {
                raceNames.Add("???????");
            }
        }

        raceDropdown.AddOptions(raceNames);
        UpdateRaceOrSubraceDescriptionAndModifiers();
    }

    private void UpdateSubraceDropdown()
    {
        Race selectedRace = PermaLists.Instance.Races[raceDropdown.value];
        subraceDropdown.ClearOptions();
        List<string> subraceNames = new List<string>();

        if (selectedRace.HasSubRace && selectedRace.SubRaces.Count > 0)
        {
            foreach (var subrace in selectedRace.SubRaces)
            {
                if (subrace.IsUnlocked)
                {
                    subraceNames.Add(subrace.Name);
                }
                else
                {
                    subraceNames.Add("???????");
                }
            }
            subraceDropdown.AddOptions(subraceNames);
            subraceDropdown.gameObject.SetActive(true);
        }
        else
        {
            subraceDropdown.gameObject.SetActive(false);
        }

        UpdateRaceOrSubraceDescriptionAndModifiers();
    }

    private void PopulateBackgroundDropdown()
    {
        backgroundDropdown.ClearOptions();
        List<string> backgroundNames = new List<string>();

        foreach (var background in PermaLists.Instance.Backgrounds)
        {
            if (background.IsUnlocked)
            {
                backgroundNames.Add(background.Name);
            }
            else
            {
                backgroundNames.Add("???????");
            }
        }

        backgroundDropdown.AddOptions(backgroundNames);
        UpdateBackgroundDescriptionAndModifiers();
    }

    private void UpdateRaceOrSubraceDescriptionAndModifiers()
    {
        Race selectedRace = PermaLists.Instance.Races[raceDropdown.value];
        string description;
        string modifiers;

        if (selectedRace.IsUnlocked)
        {
            description = selectedRace.Description;
            modifiers = selectedRace.HasSubRace && subraceDropdown.options.Count > 0 && subraceDropdown.value < selectedRace.SubRaces.Count
                ? FormatModifierText(selectedRace.SubRaces[subraceDropdown.value])
                : FormatModifierText(selectedRace);
        }
        else
        {
            description = $"This race is locked. Hint: {selectedRace.UnlockHint}";
            modifiers = string.Empty;
        }

        raceDescriptionText.text = $"{description}\n\n{modifiers}";
    }

    private void UpdateBackgroundDescriptionAndModifiers()
    {
        if (PermaLists.Instance.Backgrounds.Count > backgroundDropdown.value)
        {
            Background selectedBackground = PermaLists.Instance.Backgrounds[backgroundDropdown.value];
            string description;
            string modifiers = FormatBackgroundModifiers(selectedBackground.StatModifiers);

            if (selectedBackground.IsUnlocked)
            {
                description = selectedBackground.Description;
            }
            else
            {
                description = $"This background is locked. Hint: {selectedBackground.UnlockHint}";
            }

            backgroundDescriptionText.text = $"{description}\n\n{modifiers}";
        }
    }

    private string FormatModifierText(Race race)
    {
        return FormatModifierText(
            race.BaseStrength,
            race.BaseDexterity,
            race.BaseConstitution,
            race.BaseIntelligence,
            race.BaseWisdom,
            race.BaseCharisma,
            race.BaseLuck
        );
    }

    private string FormatModifierText(SubRace subRace)
    {
        return FormatModifierText(
            subRace.BaseStrength,
            subRace.BaseDexterity,
            subRace.BaseConstitution,
            subRace.BaseIntelligence,
            subRace.BaseWisdom,
            subRace.BaseCharisma,
            subRace.BaseLuck
        );
    }

    private string FormatModifierText(int baseStrength, int baseDexterity, int baseConstitution, int baseIntelligence, int baseWisdom, int baseCharisma, int baseLuck)
    {
        List<string> modifiers = new List<string>();

        if (baseStrength != 0) modifiers.Add($"{FormatModifier(baseStrength)} Strength");
        if (baseDexterity != 0) modifiers.Add($"{FormatModifier(baseDexterity)} Dexterity");
        if (baseConstitution != 0) modifiers.Add($"{FormatModifier(baseConstitution)} Constitution");
        if (baseIntelligence != 0) modifiers.Add($"{FormatModifier(baseIntelligence)} Intelligence");
        if (baseWisdom != 0) modifiers.Add($"{FormatModifier(baseWisdom)} Wisdom");
        if (baseCharisma != 0) modifiers.Add($"{FormatModifier(baseCharisma)} Charisma");
        if (baseLuck != 0) modifiers.Add($"{FormatModifier(baseLuck)} Luck");

        return string.Join("\n", modifiers);
    }

    private string FormatBackgroundModifiers(Dictionary<string, int> statModifiers)
    {
        List<string> modifiers = new List<string>();

        foreach (var modifier in statModifiers)
        {
            if (modifier.Value != 0)
            {
                string color = modifier.Value > 0 ? "green" : "red";
                modifiers.Add($"<color={color}>{FormatModifier(modifier.Value)} {modifier.Key}</color>");
            }
        }

        return string.Join("\n", modifiers);
    }

    private string FormatModifier(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }

    private void RandomiseCharacter()
    {
        // Create a System.Random instance seeded with the current system time
        System.Random random = new System.Random((int)System.DateTime.Now.Ticks);

        // Randomise race selection
        raceDropdown.value = random.Next(0, raceDropdown.options.Count);
        UpdateSubraceDropdown(); // Update the subrace based on the selected race

        // Retrieve the selected race instance
        string selectedRaceName = raceDropdown.options[raceDropdown.value].text;
        Race selectedRace = PermaLists.Instance.Races.Find(r => r.Name == selectedRaceName);

        // Get random names based on the selected race
        (string firstName, string surname) = GetRandomName(selectedRace);
        firstNameInput.text = firstName;
        surnameInput.text = surname;

        // Randomise subrace selection if subraces are available
        if (subraceDropdown.options.Count > 0)
        {
            subraceDropdown.value = random.Next(0, subraceDropdown.options.Count);
        }

        // Randomise background selection
        backgroundDropdown.value = random.Next(0, backgroundDropdown.options.Count);

        // Update the UI
        UpdatePreviewText();
        UpdateCreateButtonInteractability();
    }

    private (string, string) GetRandomName(Race race)
    {
        List<string> firstNames;
        List<string> surnames;

        switch (race.Name)
        {
            case "Human":
                firstNames = PermaLists.Instance.HumanFirstNames;
                surnames = PermaLists.Instance.HumanSurnames;
                break;
            case "Dwarf":
                firstNames = PermaLists.Instance.DwarfFirstNames;
                surnames = PermaLists.Instance.DwarfSurnames;
                break;
            case "Elf":
                firstNames = PermaLists.Instance.ElfFirstNames;
                surnames = PermaLists.Instance.ElfSurnames;
                break;
            case "Sabren":
                firstNames = PermaLists.Instance.SabrenFirstNames;
                surnames = PermaLists.Instance.SabrenSurnames;
                break;
            case "Saurosin":
                firstNames = PermaLists.Instance.SaurosinFirstNames;
                surnames = PermaLists.Instance.SaurosinSurnames;
                break;
            case "Caraphrax":
                firstNames = PermaLists.Instance.CaraphraxFirstNames;
                surnames = PermaLists.Instance.CaraphraxSurnames;
                break;
            default:
                firstNames = new List<string> { "John" };
                surnames = new List<string> { "Doe" };
                break;
        }

        if (firstNames.Count == 0 || surnames.Count == 0)
        {
            Debug.LogWarning("Name lists are empty or not initialized properly. Using fallback names.");
            return ("John", "Doe");
        }

        string firstName = firstNames[Random.Range(0, firstNames.Count)];
        string surname = surnames[Random.Range(0, surnames.Count)];

        return (firstName, surname);
    }



}
