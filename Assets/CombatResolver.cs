using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AttackCategory
{
    Weapon,
    Unarmed,
    Natural,
    Magic,
    Ability
}

public class AttackContext
{
    public Character Attacker { get; set; }
    public Character Defender { get; set; }
    public string SourceActionName { get; set; }
    public AttackCategory Category { get; set; }
    public DamageType RequestedDamageType { get; set; }
    public Item Weapon { get; set; }
    public bool ApplyOnHitEffects { get; set; } = true;
    public bool IsPlayerControlled { get; set; }
}

public class DamageLine
{
    public DamageType DamageType { get; set; }
    public int RawAmount { get; set; }
    public int ResistancePercent { get; set; }
    public int AmountAfterResistance { get; set; }
    public int ArmourReduction { get; set; }
    public int FinalAmount { get; set; }
}

public class DamagePacket
{
    public bool UsesWeapon { get; set; }
    public bool IsUnarmedOrNatural { get; set; }
    public string ScalingStat { get; set; }
    public float ScalingBonusCalculated { get; set; }
    public bool ScalingBonusApplied { get; set; }
    public bool DamageTypeConverted { get; set; }
    public DamageType ConvertedFromType { get; set; } = DamageType.None;
    public DamageType ConvertedToType { get; set; } = DamageType.None;
    public Dictionary<DamageType, int> OriginalDamageByType { get; set; } = new Dictionary<DamageType, int>();
    public Dictionary<DamageType, int> FinalDamageByType { get; set; } = new Dictionary<DamageType, int>();
}

public class AttackResult
{
    public bool DidStart { get; set; }
    public bool IsValid { get; set; }
    public string InvalidReason { get; set; }
    public string ResolverName { get; set; } = "CombatResolver";
    public Character Attacker { get; set; }
    public Character Defender { get; set; }
    public string ActionName { get; set; }
    public AttackCategory Category { get; set; }
    public DamageType RequestedDamageType { get; set; }
    public Item Weapon { get; set; }
    public float AccuracyValue { get; set; }
    public float DefenderEvasionValue { get; set; }
    public float HitRoll { get; set; }
    public bool Hit { get; set; }
    public float CriticalChance { get; set; }
    public float CriticalRoll { get; set; }
    public bool IsCriticalHit { get; set; }
    public int CriticalMultiplier { get; set; } = 100;
    public DamagePacket DamagePacket { get; set; } = new DamagePacket();
    public List<DamageLine> DamageLines { get; set; } = new List<DamageLine>();
    public string SelectedBodyPartName { get; set; }
    public string BodyPartEquipmentSlots { get; set; }
    public string CoveredArmour { get; set; }
    public int ArmourValuePresent { get; set; }
    public int ArmourValueUsed { get; set; }
    public bool BodyPartCoverageUsed { get; set; }
    public int BodyPartHealthBefore { get; set; }
    public int BodyPartHealthAfter { get; set; }
    public int DefenderHealthBefore { get; set; }
    public int DefenderHealthAfter { get; set; }
    public bool DefenderWasAliveBefore { get; set; }
    public bool DefenderIsAliveAfter { get; set; }
    public bool DefenderWasActiveBefore { get; set; }
    public bool DefenderIsActiveAfter { get; set; }
    public bool DeathOccurred { get; set; }
    public bool OnHitEffectsApplied { get; set; }
    public bool WeaponOnHitEffectsApplied { get; set; }
    public bool OnHitTakenEffectsPresent { get; set; }
    public bool OnHitTakenEffectsApplied { get; set; }
    public bool CombatContextRefreshed { get; set; }
    public List<string> Warnings { get; set; } = new List<string>();

    public DamageLine GetOrCreateDamageLine(DamageType damageType)
    {
        DamageLine line = DamageLines.FirstOrDefault(existing => existing.DamageType == damageType);
        if (line == null)
        {
            line = new DamageLine { DamageType = damageType };
            DamageLines.Add(line);
        }

        return line;
    }
}

public static class CombatResolver
{
    public static AttackContext CreatePhysicalAttackContext(Character attacker, Character defender, DamageType requestedDamageType, string sourceActionName = null)
    {
        Item weapon = attacker != null ? attacker.GetMainHandItem() : null;
        bool isNaturalAttack = weapon == null && (attacker is Monster || attacker is Animal);

        return new AttackContext
        {
            Attacker = attacker,
            Defender = defender,
            RequestedDamageType = requestedDamageType,
            SourceActionName = sourceActionName ?? CombatActionResolutionDiagnosticsLogger.InferActionName(attacker, requestedDamageType),
            Category = weapon != null ? AttackCategory.Weapon : isNaturalAttack ? AttackCategory.Natural : AttackCategory.Unarmed,
            Weapon = weapon,
            ApplyOnHitEffects = true,
            IsPlayerControlled = attacker != null && attacker == PlayerStats.Instance?.CurrentPlayerCharacter
        };
    }

    public static AttackResult ResolveAttack(AttackContext context)
    {
        AttackResult result = new AttackResult
        {
            Attacker = context?.Attacker,
            Defender = context?.Defender,
            ActionName = context?.SourceActionName ?? "Attack",
            Category = context != null ? context.Category : AttackCategory.Unarmed,
            RequestedDamageType = context != null ? context.RequestedDamageType : DamageType.None,
            Weapon = context?.Weapon,
        };

        if (!ValidateAttackContext(context, result, logFailure: true))
        {
            return result;
        }

        Character attacker = context.Attacker;
        Character defender = context.Defender;
        ActionCostProfile attackCostProfile = ActionCostProfileResolver.BuildForCombatAttackContext(context);
        ActionCostProfileResolver.LogPredictedCost("CombatResolver.ResolveAttack", result.ActionName, attackCostProfile, attacker);

        result.AccuracyValue = attacker.CalculateAccuracyAgainst(defender);
        result.DefenderEvasionValue = defender.IsPlayerVisible ? defender.GetStatValue("Dexterity") : 0f;
        result.HitRoll = Random.Range(0f, 100f);
        result.Hit = result.HitRoll < result.AccuracyValue;

        if (result.Hit)
        {
            result.IsCriticalHit = attacker.DetermineCriticalHit(out float criticalChance, out float criticalRoll);
            result.CriticalChance = criticalChance;
            result.CriticalRoll = criticalRoll;
            result.CriticalMultiplier = result.IsCriticalHit ? attacker.GetCriticalHitMultiplier() : 100;

            result.DamagePacket = attacker.BuildDamagePacket(context);
            Dictionary<DamageType, int> resolvedDamage = new Dictionary<DamageType, int>(result.DamagePacket.FinalDamageByType);

            if (result.IsCriticalHit)
            {
                float criticalMultiplier = result.CriticalMultiplier / 100f;
                foreach (DamageType damageType in resolvedDamage.Keys.ToList())
                {
                    resolvedDamage[damageType] = Mathf.RoundToInt(resolvedDamage[damageType] * criticalMultiplier);
                }
            }

            foreach (KeyValuePair<DamageType, int> damageEntry in resolvedDamage)
            {
                result.GetOrCreateDamageLine(damageEntry.Key).RawAmount = damageEntry.Value;
            }

            defender.TakeDamage(resolvedDamage, attacker, result.IsCriticalHit, result);

            if (context.ApplyOnHitEffects)
            {
                attacker.ApplyOnHitEffects(defender, result);
            }
        }

        CombatActionResolutionDiagnosticsLogger.LogEvent("[ATTACK RESOLVED]", "CombatResolver.ResolveAttack",
            $"ActionName={result.ActionName}\n" +
            $"RequestedDamageType={result.RequestedDamageType}\n" +
            $"AttackCategory={result.Category}\n" +
            $"Weapon={CombatActionResolutionDiagnosticsLogger.FormatItemSummary(result.Weapon)}\n" +
            $"OriginalOutgoingDamage={CombatActionResolutionDiagnosticsLogger.FormatDamageDictionary(result.DamagePacket.OriginalDamageByType)}\n" +
            $"FinalOutgoingDamage={CombatActionResolutionDiagnosticsLogger.FormatDamageDictionary(result.DamagePacket.FinalDamageByType)}\n" +
            $"WeaponDamageFlattened={false}\n" +
            $"DamageTypeConverted={result.DamagePacket.DamageTypeConverted}\n" +
            $"ConvertedFromType={result.DamagePacket.ConvertedFromType}\n" +
            $"ConvertedToType={result.DamagePacket.ConvertedToType}\n" +
            $"WeaponStatBonusCalculated={result.DamagePacket.ScalingBonusCalculated}\n" +
            $"WeaponStatBonusApplied={result.DamagePacket.ScalingBonusApplied}\n" +
            $"AccuracyValue={result.AccuracyValue}\n" +
            $"DefenderEvasionValue={result.DefenderEvasionValue}\n" +
            $"HitRoll={result.HitRoll}\n" +
            $"HitResult={(result.Hit ? "Hit" : "Miss")}\n" +
            $"CritChance={result.CriticalChance}\n" +
            $"CritRoll={result.CriticalRoll}\n" +
            $"CritResult={result.IsCriticalHit}\n" +
            $"CritMultiplier={result.CriticalMultiplier}\n" +
            $"SelectedBodyPart={result.SelectedBodyPartName ?? "None"}\n" +
            $"CoveredArmour={result.CoveredArmour ?? "None"}\n" +
            $"ArmourValuePresent={result.ArmourValuePresent}\n" +
            $"ArmourValueUsed={result.ArmourValueUsed}\n" +
            $"BodyPartCoverageUsed={result.BodyPartCoverageUsed}\n" +
            $"DamageBreakdown={FormatDamageLines(result)}\n" +
            $"HealthBefore={result.DefenderHealthBefore}\n" +
            $"HealthAfter={result.DefenderHealthAfter}\n" +
            $"DeathOccurred={result.DeathOccurred}\n" +
            $"Resolver={result.ResolverName}",
            attacker, defender);

        return result;
    }

    public static AttackResult ValidateAttack(AttackContext context)
    {
        AttackResult result = new AttackResult
        {
            Attacker = context?.Attacker,
            Defender = context?.Defender,
            ActionName = context?.SourceActionName ?? "Attack",
            Category = context != null ? context.Category : AttackCategory.Unarmed,
            RequestedDamageType = context != null ? context.RequestedDamageType : DamageType.None,
            Weapon = context?.Weapon
        };

        ValidateAttackContext(context, result, logFailure: true);
        return result;
    }

    private static string FormatDamageLines(AttackResult result)
    {
        if (result == null || result.DamageLines == null || result.DamageLines.Count == 0)
        {
            return "None";
        }

        return string.Join("; ",
            result.DamageLines.Select(line =>
                $"{line.DamageType}:Raw={line.RawAmount},Resistance={line.ResistancePercent}%,AfterResistance={line.AmountAfterResistance},ArmourReduction={line.ArmourReduction},Final={line.FinalAmount}"));
    }

    private static void LogInvalidAttack(AttackResult result, string eventName)
    {
        CombatActionResolutionDiagnosticsLogger.LogWarning(eventName,
            $"ActionName={result?.ActionName ?? "Attack"}\n" +
            $"RequestedDamageType={result?.RequestedDamageType.ToString() ?? "None"}\n" +
            $"InvalidReason={result?.InvalidReason ?? "Unknown"}\n" +
            $"AttackerState={DescribeCharacterState(result?.Attacker)}\n" +
            $"DefenderState={DescribeCharacterState(result?.Defender)}",
            result?.Attacker, result?.Defender);
    }

    private static bool ValidateAttackContext(AttackContext context, AttackResult result, bool logFailure)
    {
        if (context == null)
        {
            result.InvalidReason = "AttackContext was null.";
            if (logFailure)
            {
                LogInvalidAttack(result, "CombatResolver.ResolveAttack received a null AttackContext");
            }

            return false;
        }

        if (context.Attacker == null)
        {
            result.InvalidReason = "Attacker was null.";
            if (logFailure)
            {
                LogInvalidAttack(result, "CombatResolver.ResolveAttack received a null attacker");
            }

            return false;
        }

        if (context.Defender == null)
        {
            result.InvalidReason = "Defender was null.";
            if (logFailure)
            {
                LogInvalidAttack(result, "CombatResolver.ResolveAttack received a null defender");
            }

            return false;
        }

        Character attacker = context.Attacker;
        Character defender = context.Defender;

        result.DidStart = true;
        result.IsValid = true;

        if (!attacker.IsAlive)
        {
            result.IsValid = false;
            result.InvalidReason = "Attacker is dead.";
            if (logFailure)
            {
                LogInvalidAttack(result, "CombatResolver.ResolveAttack rejected dead attacker");
            }

            return false;
        }

        if (!attacker.IsActive)
        {
            result.IsValid = false;
            result.InvalidReason = "Attacker is inactive.";
            if (logFailure)
            {
                LogInvalidAttack(result, "CombatResolver.ResolveAttack rejected inactive attacker");
            }

            return false;
        }

        if (!defender.IsAlive)
        {
            result.IsValid = false;
            result.InvalidReason = "Defender is dead.";
            if (logFailure)
            {
                LogInvalidAttack(result, "CombatResolver.ResolveAttack rejected dead defender");
            }

            return false;
        }

        if (!defender.IsActive)
        {
            result.IsValid = false;
            result.InvalidReason = "Defender is inactive.";
            if (logFailure)
            {
                LogInvalidAttack(result, "CombatResolver.ResolveAttack rejected inactive defender");
            }

            return false;
        }

        if (attacker == defender)
        {
            result.IsValid = false;
            result.InvalidReason = "Attacker and defender are the same character.";
            if (logFailure)
            {
                LogInvalidAttack(result, "CombatResolver.ResolveAttack rejected self-targeted attack");
            }

            return false;
        }

        if (!attacker.IsValidCombatTarget(defender))
        {
            result.IsValid = false;
            result.InvalidReason = "Defender is not targetable by the attacker.";
            if (logFailure)
            {
                LogInvalidAttack(result, "CombatResolver.ResolveAttack rejected untargetable defender");
            }

            return false;
        }

        return true;
    }

    private static string DescribeCharacterState(Character character)
    {
        if (character == null)
        {
            return "NULL";
        }

        return $"{character.Name} [{character.IInteractableID}] Type={character.GetType().Name} IsAlive={character.IsAlive} IsActive={character.IsActive} InCombat={character.InCombat} InTurn={character.InTurn} Target={character.Target?.Name ?? "NULL"}";
    }
}
