using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class MonsterGenerator : MonoBehaviour
{
    public static MonsterGenerator Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Monster GenerateMonster(string monsterName)
    {
        MonsterCreationData monsterData = PermaLists.Instance.MonsterCreationData.FirstOrDefault(m => m.MonsterName == monsterName);
        if (monsterData == null)
        {
            GameDebugger.Instance.LogError($"Monster '{monsterName}' not found in PermaLists.");
            return null;
        }

        return new Monster(monsterData);
    }

    public List<Monster> GenerateMonstersForArea(TerrainType terrain, int count)
    {
        List<Monster> monsters = new List<Monster>();
        List<MonsterCreationData> availableMonsters = PermaLists.Instance.MonsterCreationData
            .Where(m => m.AllowedTerrains.Contains(terrain))
            .ToList();

        for (int i = 0; i < count; i++)
        {
            if (availableMonsters.Count == 0) break;
            MonsterCreationData selectedData = availableMonsters[UnityEngine.Random.Range(0, availableMonsters.Count)];
            monsters.Add(new Monster(selectedData));
        }

        return monsters;
    }

    private Monster GenerateUndead(
        string baseName,
        string originalRace,
        MonsterType type,
        int health,
        int strength,
        int dexterity,
        int constitution,
        int intelligence,
        int wisdom,
        int luck,
        int awareness,
        int speed,
        List<MonsterAbility> abilities,
        List<TerrainType> terrains,
        char symbol,
        string color,
        Dictionary<DamageType, float> resistances = null)
    {
        string bodyType = PermaLists.Instance.Races.FirstOrDefault(r => r.Name == originalRace)?.BodyType ?? "Humanoid";

        int monsterID = GameManager.Instance.GetMonsterID();
        int iinteractableID = GameManager.Instance.GetInteractableID();

        MonsterCreationData undeadData = new MonsterCreationData
        {
            MonsterName = $"{originalRace} {baseName} {monsterID}",
            Type = type,
            IsBoss = false,
            Rarity = RarityType.Common,
            MaxHealth = health + UnityEngine.Random.Range(-10, 10),
            Strength = Mathf.Max(1, strength + UnityEngine.Random.Range(-2, 2)),
            Dexterity = Mathf.Max(1, dexterity + UnityEngine.Random.Range(-2, 2)),
            Constitution = Mathf.Max(1, constitution + UnityEngine.Random.Range(-2, 2)),
            Intelligence = intelligence,
            Wisdom = wisdom,
            Luck = Mathf.Max(1, luck + UnityEngine.Random.Range(-1, 1)),
            Awareness = Mathf.Max(1, awareness + UnityEngine.Random.Range(-1, 1)),
            Speed = Mathf.Max(1, speed + UnityEngine.Random.Range(-1, 1)),
            BodyType = bodyType,
            Abilities = abilities,
            AllowedTerrains = terrains,
            DamageResistances = resistances ?? new Dictionary<DamageType, float>() // Store resistances here
        };

        return new Monster(undeadData)
        {
            IInteractableID = iinteractableID,
            MonsterID = monsterID,
            Symbol = symbol,
            Color = color
        };
    }

    public Monster CreateSkeleton(string originalRace = "Human")
    {
        return GenerateUndead(
            "Skeleton",
            originalRace,
            MonsterType.Undead,
            75, 8, 12, 6, 3, 3, 1, 8, 6,
            new List<MonsterAbility>
            {
            new MonsterAbility { Name = "Bone Slash", Damage = 10, Type = DamageType.Slashing },
            new MonsterAbility { Name = "Rattle Strike", Damage = 8, Type = DamageType.Bludgeoning }
            },
            new List<TerrainType> { TerrainType.Cave, TerrainType.Ruins, TerrainType.Graveyard },
            'S',
            "#FFFFFF", // White
            new Dictionary<DamageType, float> // Skeleton resistances
            {
            { DamageType.Slashing, 50f },  // 50% resistance to Slashing
            { DamageType.Piercing, 50f }   // 50% resistance to Piercing
            }
        );
    }

    public Monster CreateZombie(string originalRace = "Human")
    {
        return GenerateUndead(
            "Zombie",
            originalRace,
            MonsterType.Undead,
            120, 12, 4, 15, 2, 2, 3, 6, 4,
            new List<MonsterAbility>
            {
            new MonsterAbility { Name = "Rotting Bite", Damage = 15, Type = DamageType.Piercing },
            new MonsterAbility { Name = "Clumsy Slam", Damage = 12, Type = DamageType.Bludgeoning }
            },
            new List<TerrainType> { TerrainType.Graveyard, TerrainType.Cave, TerrainType.Swamp },
            'Z',
            "#008000", // Green
            new Dictionary<DamageType, float> // Zombie resistances
            {
            { DamageType.Bludgeoning, 50f }  // 50% resistance to Bludgeoning
            }
        );
    }

    public Monster CreateSkeletonBoss(string originalRace = "Human")
    {
        return GenerateUndead(
            "Skeleton Lord",
            originalRace,
            MonsterType.Undead,
            150, 10, 14, 8, 3, 3, 1, 8, 6,
            new List<MonsterAbility>
            {
            new MonsterAbility { Name = "Bone Slash", Damage = 12, Type = DamageType.Slashing },
            new MonsterAbility { Name = "Rattle Strike", Damage = 10, Type = DamageType.Bludgeoning }
            },
            new List<TerrainType> { TerrainType.Cave, TerrainType.Ruins, TerrainType.Graveyard },
            'S',
            "#FFFFCC", // Yellowish White
            new Dictionary<DamageType, float> // Boss-level resistances
            {
            { DamageType.Slashing, 65f },  // Bosses have even HIGHER resistances!
            { DamageType.Piercing, 65f }
            }
        );
    }

    public Monster CreateZombieBoss(string originalRace = "Human")
    {
        return GenerateUndead(
            "Dread Zombie",
            originalRace,
            MonsterType.Undead,
            200, 14, 5, 18, 2, 2, 3, 6, 4,
            new List<MonsterAbility>
            {
            new MonsterAbility { Name = "Rotting Bite", Damage = 18, Type = DamageType.Piercing },
            new MonsterAbility { Name = "Crushing Slam", Damage = 15, Type = DamageType.Bludgeoning }
            },
            new List<TerrainType> { TerrainType.Graveyard, TerrainType.Cave, TerrainType.Swamp },
            'Z',
            "#006400", // Darker Green
            new Dictionary<DamageType, float> // Boss-level resistances
            {
            { DamageType.Bludgeoning, 65f }  // Bosses have even HIGHER resistances!
            }
        );
    }


    public Monster ReanimateAsUndead(object deadEntity, bool asZombie)
    {
        if (deadEntity == null)
        {
            GameDebugger.Instance.LogError("Attempted to reanimate a null entity!");
            return null;
        }

        string entityName = "";
        string bodyType = "Humanoid";
        int originalMaxHealth = 100, originalStrength = 10, originalDexterity = 10, originalConstitution = 10, originalSpeed = 5;
        Anatomy originalAnatomy = null;
        Dictionary<EquipmentSlot, Item> originalEquipment = new Dictionary<EquipmentSlot, Item>();

        if (deadEntity is Corpse corpse)
        {
            entityName = corpse.OriginalName;
            bodyType = corpse.BodyType;
            originalMaxHealth = corpse.OriginalMaxHealth;
            originalStrength = corpse.OriginalStrength;
            originalDexterity = corpse.OriginalDexterity;
            originalConstitution = corpse.OriginalConstitution;
            originalSpeed = corpse.OriginalSpeed;
            originalAnatomy = corpse.Anatomy;
            originalEquipment = new Dictionary<EquipmentSlot, Item>(corpse.EquippedItems);
        }
        else if (deadEntity is Carcass carcass)
        {
            entityName = carcass.OriginalName;
            bodyType = carcass.BodyType;
            originalMaxHealth = carcass.OriginalMaxHealth;
            originalStrength = carcass.OriginalStrength;
            originalDexterity = carcass.OriginalDexterity;
            originalConstitution = carcass.OriginalConstitution;
            originalSpeed = carcass.OriginalSpeed;
            originalAnatomy = carcass.Anatomy;
        }
        else
        {
            GameDebugger.Instance.LogError("Invalid entity type for undead reanimation.");
            return null;
        }

        string undeadType = asZombie ? "Zombie" : "Skeleton";
        MonsterCreationData undeadData = new MonsterCreationData
        {
            MonsterName = $"{undeadType} {entityName}",
            Type = MonsterType.Undead,
            IsBoss = false,
            Rarity = RarityType.Common,
            MaxHealth = asZombie ? Mathf.Max(50, originalMaxHealth / 2 + UnityEngine.Random.Range(-10, 10))
                                 : Mathf.Max(40, originalMaxHealth / 3 + UnityEngine.Random.Range(-5, 5)),
            Strength = asZombie ? Mathf.Max(5, originalStrength - 2 + UnityEngine.Random.Range(-1, 2))
                                : Mathf.Max(6, originalStrength - 4 + UnityEngine.Random.Range(-2, 3)),
            Dexterity = asZombie ? Mathf.Max(3, originalDexterity - 3 + UnityEngine.Random.Range(-2, 2))
                                 : Mathf.Max(8, originalDexterity + 2 + UnityEngine.Random.Range(-1, 1)),
            Constitution = asZombie ? Mathf.Max(8, originalConstitution + 3 + UnityEngine.Random.Range(-2, 2))
                                    : Mathf.Max(5, originalConstitution - 3 + UnityEngine.Random.Range(-1, 2)),
            Intelligence = asZombie ? 2 : 3,
            Wisdom = asZombie ? 2 : 3,
            Luck = 1,
            Awareness = asZombie ? 5 : 6,
            Speed = asZombie ? Mathf.Max(2, originalSpeed - 2 + UnityEngine.Random.Range(-1, 1))
                             : Mathf.Max(4, originalSpeed + 1 + UnityEngine.Random.Range(-1, 2)),
            BodyType = asZombie ? bodyType : "Skeletal " + bodyType,
        };

        Monster undead = new Monster(undeadData)
        {
            Anatomy = new Anatomy(asZombie ? bodyType : "Skeletal " + bodyType),
            EquippedItems = asZombie ? new Dictionary<EquipmentSlot, Item>(originalEquipment)
                                     : new Dictionary<EquipmentSlot, Item>(), // Skeletons lose equipment
        };

        return undead;
    }

    // Helper to determine if a body part is flesh-based (should be removed for skeletons)
    private bool IsFleshBased(BodyPart part)
    {
        HashSet<string> fleshParts = new HashSet<string> { "Skin", "Muscle", "Eye", "Heart", "Liver", "Intestines", "Lungs" };
        return fleshParts.Any(fleshPart => part.Name.Contains(fleshPart, StringComparison.OrdinalIgnoreCase));
    }



}
