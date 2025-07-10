using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ModifierType
{
    Flat,       // Direct addition/subtraction (e.g., +5 Strength)
    Multiplier  // Percentage-based modification (e.g., +20% Strength)
}

public class BuffDebuff
{
    public string Name { get; private set; }
    public string Source { get; private set; }
    public string AffectedStat { get; private set; } // For stats like "Strength", "Dexterity"
    public DamageType? AffectedResistance { get; private set; } // Separate for resistances
    public DamageType? AffectedDamageType { get; private set; } // Separate for damage modifications
    public ModifierType Type { get; private set; }
    public float EffectAmount { get; private set; }
    public int Duration { get; private set; }

    public void ModifyEffectAmount(float amount)
    {
        EffectAmount += amount;
    }

    // Constructor for Stat Buff/Debuff
    public BuffDebuff(string name, string source, string affectedStat, ModifierType type, float effectAmount, int duration)
    {
        Name = name;
        Source = source;
        AffectedStat = affectedStat;
        AffectedResistance = null;
        AffectedDamageType = null;
        Type = type;
        EffectAmount = effectAmount;
        Duration = duration;
    }

    // Unified Constructor for Resistance & Damage Type Buff/Debuff
    public BuffDebuff(string name, string source, DamageType? affectedResistance, DamageType? affectedDamageType, ModifierType type, float effectAmount, int duration)
    {
        Name = name;
        Source = source;
        AffectedStat = null;
        AffectedResistance = affectedResistance;
        AffectedDamageType = affectedDamageType;
        Type = type;
        EffectAmount = effectAmount;
        Duration = duration;
    }

    public void ReduceDuration()
    {
        if (Duration > 0) Duration--;
    }

    public bool IsExpired()
    {
        return Duration == 0;
    }

    public override string ToString()
    {
        string target = AffectedStat ?? AffectedResistance?.ToString() ?? AffectedDamageType?.ToString();
        string typeText = Type == ModifierType.Flat ? $"{EffectAmount}" : $"{EffectAmount}%";
        return $"{Name} ({Source}): {target} {Type} {typeText} ({(Duration > 0 ? Duration + " turns" : "Permanent")})";
    }
}

public abstract class OnHitEffect
{
    public string EffectName { get; private set; }
    public int Duration { get; private set; } // Turns the effect lasts

    protected OnHitEffect(string effectName, int duration)
    {
        EffectName = effectName;
        Duration = duration;
    }

    public abstract void ApplyEffect(Character attacker, Character target);
}

public class BleedEffect : OnHitEffect
{
    private float damagePerTurn;

    public BleedEffect(int duration, float damage) : base("Bleeding", duration)
    {
        damagePerTurn = damage;
    }

    public override void ApplyEffect(Character attacker, Character target)
    {
        target.AffectedBy.Add(new BuffDebuff(
            "Bleeding",
            $"{attacker.Name}'s Attack",
            "Health",
            ModifierType.Flat,
            -damagePerTurn,
            Duration
        ));

        Debug.Log($"{attacker.Name} inflicted Bleeding on {target.Name} for {Duration} turns.");
    }
}

public class SlownessEffect : OnHitEffect
{
    private float speedReduction;

    public SlownessEffect(int duration, float reduction) : base("Slowness", duration)
    {
        speedReduction = reduction;
    }

    public override void ApplyEffect(Character attacker, Character target)
    {
        target.AffectedBy.Add(new BuffDebuff(
            "Slowness",
            $"{attacker.Name}'s Attack",
            "Dexterity",
            ModifierType.Flat,
            -speedReduction,
            Duration
        ));

        Debug.Log($"{attacker.Name} inflicted Slowness on {target.Name} for {Duration} turns.");
    }
}

public class BurnEffect : OnHitEffect
{
    private float burnDamage;

    public BurnEffect(int duration, float damage) : base("Burning", duration)
    {
        burnDamage = damage;
    }

    public override void ApplyEffect(Character attacker, Character target)
    {
        target.AffectedBy.Add(new BuffDebuff(
            "Burning",
            $"{attacker.Name}'s Attack",
            "Health",
            ModifierType.Flat,
            -burnDamage,
            Duration
        ));

        Debug.Log($"{attacker.Name} inflicted Burning on {target.Name} for {Duration} turns.");
    }
}

public class ShockEffect : OnHitEffect
{
    private float stunChance;

    public ShockEffect(int duration, float chance) : base("Shocked", duration)
    {
        stunChance = chance;
    }

    public override void ApplyEffect(Character attacker, Character target)
    {
        if (UnityEngine.Random.Range(0f, 100f) < stunChance)
        {
            target.AffectedBy.Add(new BuffDebuff(
                "Shocked",
                $"{attacker.Name}'s Attack",
                "Dexterity", // Changed from "Speed"
                ModifierType.Flat,
                -5,
                Duration
            ));

            Debug.Log($"{attacker.Name} inflicted Shock on {target.Name} for {Duration} turns.");
        }
    }

}

public class StunEffect : OnHitEffect
{
    public StunEffect(int duration) : base("Stunned", duration) { }

    public override void ApplyEffect(Character attacker, Character target)
    {
        target.AffectedBy.Add(new BuffDebuff(
            "Stunned",
            $"{attacker.Name}'s Attack",
            "Speed",
            ModifierType.Flat,
            -10,
            Duration
        ));
        Debug.Log($"{attacker.Name} inflicted Stun on {target.Name} for {Duration} turns.");
    }
}

public class WeaknessEffect : OnHitEffect
{
    private float strengthReduction;

    public WeaknessEffect(int duration, float reduction) : base("Weakened", duration)
    {
        strengthReduction = reduction;
    }

    public override void ApplyEffect(Character attacker, Character target)
    {
        target.AffectedBy.Add(new BuffDebuff(
            "Weakened",
            $"{attacker.Name}'s Attack",
            "Strength",
            ModifierType.Flat,
            -strengthReduction,
            Duration
        ));
        Debug.Log($"{attacker.Name} inflicted Weakness on {target.Name} for {Duration} turns.");
    }
}