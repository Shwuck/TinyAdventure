using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Monster : Character
{
    #region Monster Properties
    public int MonsterID { get; set; }
    public string MonsterName { get; set; }
    public int MonsterLevel { get; set; }
    public MonsterType Type { get; set; }
    public List<MonsterAbility> Abilities { get; set; } = new List<MonsterAbility>();
    public bool IsBoss { get; set; }
    public RarityType Rarity { get; set; }
    public override char Symbol { get; set; } = 'M';
    public override string Color { get; set; } = "#FF0000"; // Default monster color
    #endregion

    #region Constructor
    public Monster(MonsterCreationData data)
    {
        Name = data.MonsterName;
        MonsterID = GameManager.Instance.GetMonsterID(); // Generate unique Monster ID
        Type = data.Type;
        IsBoss = data.IsBoss;
        Rarity = data.Rarity;
        Abilities = data.Abilities ?? new List<MonsterAbility>();

        MaxHealth = data.MaxHealth;
        Health = MaxHealth;
        Strength = data.Strength;
        Dexterity = data.Dexterity;
        Constitution = data.Constitution;
        Intelligence = data.Intelligence;
        Wisdom = data.Wisdom;
        Luck = data.Luck;
        Awareness = data.Awareness;
        Speed = data.Speed;
        IsHostile = true;
        CanLeaveArea = false;

        foreach (var resistance in data.DamageResistances)
        {
            if (Resistances.ContainsKey(resistance.Key))
            {
                Resistances[resistance.Key] = resistance.Value;
            }
        }

        Anatomy = AnatomyGenerator.Instance.GenerateAnatomy(data.BodyType);

        // Register the monster in the turn manager
        stateMachine = new StateMachine(this);
        stateMachine.ChangeState(new MonsterIdleState()); // Start in monster idle state
        TurnManager.Instance.RegisterCharacter(this);
    }
    #endregion

    #region Combat
    public override int GetResistance(DamageType damageType)
    {
        if (Resistances.TryGetValue(damageType, out float resistance))
        {
            return Mathf.RoundToInt(resistance);
        }
        return base.GetResistance(damageType);
    }

    protected override void OnDeath()
    {
        GameDebugger.Instance.LogInfo($"Monster {Name} has been slain!");
        MessageLogManager.Instance.Log("combat_result", Name, "has been slain!");

        // Generate remains with stored loot
        MonsterRemains remains = MonsterRemains.GenerateRemains(this);

        // Remove from Turn Manager
        TurnManager.Instance.DeregisterCharacter(this);
        IsActive = false; // Mark as inactive

        // Ensure remains are properly placed and visible
        if (remains != null)
        {
            GameDebugger.Instance.LogInfo($"Remains of {Name} (Level {MonsterLevel}) have been placed at {remains.Position}");
        }
    }

    public void PerformAbility(Character target)
    {
        if (ActionPoints < 3) // Example: Abilities cost 3 AP
        {
            GameDebugger.Instance.LogWarning($"{Name} does not have enough Action Points to use an ability.");
            return;
        }

        if (Abilities == null || Abilities.Count == 0)
        {
            GameDebugger.Instance.LogWarning($"{Name} has no abilities, cannot perform ability attack!");
            return;
        }

        MonsterAbility chosenAbility = Abilities[Random.Range(0, Abilities.Count)];

        if (!IsTargetInRange(target, chosenAbility.Range))
        {
            GameDebugger.Instance.LogWarning($"{Name} tried to use {chosenAbility.Name}, but {target.Name} is out of range.");
            return;
        }

        Dictionary<DamageType, int> damageByType = new Dictionary<DamageType, int>
    {
        { chosenAbility.Type, chosenAbility.Damage }
    };

        MessageLogManager.Instance.Log("combat", Name, "uses", chosenAbility.Name, "on", target.Name);
        GameDebugger.Instance.LogInfo($"{Name} uses {chosenAbility.Name} on {target.Name}!");

        target.TakeDamage(damageByType, this);

        SpendActionPoints(3); // Spend AP after using ability
    }

    #endregion

    #region AI Behavior
    public void UpdateMonsterAI()
    {
        stateMachine.Update(); // Let the state machine decide what to do
    }

    private void Patrol()
    {
        MoveInRandomDirection();
        GameDebugger.Instance.LogInfo($"{Name} is patrolling.");
    }

    public Character FindClosestEnemy()
    {
        Character player = PlayerStats.Instance.CurrentPlayerCharacter;

        // Prioritize Player if visible
        if (player != null && CanSeeTarget(player)) return player;

        // Otherwise, target the closest NPC
        return TurnManager.Instance.GetAllRegisteredCharacters()
            .Where(c => c is NPC && CanSeeTarget(c)) // Ensure they are visible
            .OrderBy(c => Vector2.Distance(this.Position, c.Position))
            .FirstOrDefault();
    }


    #endregion
}

public class MonsterAbility
{
    public string Name { get; set; }
    public int Damage { get; set; }
    public DamageType Type { get; set; }
    public int Range { get; set; } = 1;
}
