using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using System.Text.Json.Nodes;
using DamageEffectType = SPTarkov.Server.Core.Models.Enums.DamageEffectType;

namespace Speedloader;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.sp.bela.speedloader";
    public string Name { get; init; } = "Speedloader";
    public string Author { get; init; } = "Bela";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.2.4");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.2");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/925316/SPTarkov.Tweaks";
    public string License { get; init; } = "AGPL-3.0";
    public bool HasPrepatcher { get; init; } = false;
}

[Injectable(TypePriority = int.MaxValue - 1000)]
public class Main(
    ISptLogger<Main> logger,
    ModHelper modHelper,
    GlobalTable globalTable,
    LocationTable locationTable,
    TemplateTable templateTable,
    HideoutTable hideoutTable,
    HideoutConfig hideoutConfig
    )
    : IOnLoad
{
    private static readonly MongoId m_ammoParentId = "5485a8684bdc2da71d8b4567";
    private static readonly MongoId m_rublesItemId = "5449016a4bdc2d6f028b456f";
    private static readonly MongoId m_purseItemId = "5732ee6a24597719ae0c0281";
    private static readonly MongoId m_propitalItemId = "5c0e530286f7747fa1419862";

    private const string m_propitalBuffName = "BuffsPropital";
    private const string m_defaultExplosiveBulletId = "5fc382b6d6fa9c00c571bbc3"; // .338 TAC-X

    private JsonObject? config;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        config = null;

        try
        {
            config = modHelper.GetJsonDataFromModFile<JsonObject>("db", "config.json");
        }
        catch (Exception ex)
        {
            logger.Warning($"[Speedloader]: db/config.json not loaded, using defaults. Error: {ex.Message}");
        }

        var globals = globalTable.Configuration; // globals.json
        var items = templateTable.Items;

        var applied = new List<string>(8);

        if (GetConfig("GrowthTweaks", "Enabled", false))
        {
            var mult = GetConfig("GrowthTweaks", "Multiplier", 2.5);

            // Skill progress rates
            globals.WeaponSkillProgressRate *= mult;
            globals.SkillsSettings.WeaponSkillProgressRate *= mult;
            globals.SkillsSettings.SkillProgressRate *= mult;

            // Experience gains
            var exp = globals.Exp;
            exp.Heal.ExpForHeal = Math.Round(exp.Heal.ExpForHeal * mult);
            exp.Heal.ExpForEnergy = Math.Round(exp.Heal.ExpForEnergy * mult);
            exp.Heal.ExpForHydration = Math.Round(exp.Heal.ExpForHydration * mult);

            exp.Kill.VictimLevelExperience = Math.Round(exp.Kill.VictimLevelExperience * mult);
            exp.Kill.VictimBotLevelExperience = Math.Round(exp.Kill.VictimBotLevelExperience * mult);
            exp.Kill.ExperienceOnDamageAllHealth = Math.Round(exp.Kill.ExperienceOnDamageAllHealth * mult);
            exp.Kill.BotExperienceOnDamageAllHealth = Math.Round(exp.Kill.BotExperienceOnDamageAllHealth * mult);
            exp.Kill.PmcExperienceOnDamageAllHealth = Math.Round(exp.Kill.PmcExperienceOnDamageAllHealth * mult);
            exp.Kill.HeadShotMultiplier *= mult;
            exp.Kill.BotHeadShotMultiplier *= mult;
            exp.Kill.PmcHeadShotMultiplier *= mult;

            exp.MatchEnd.SurvivedMultiplier *= mult;
            exp.MatchEnd.MiaMultiplier *= mult;
            exp.MatchEnd.RunnerMultiplier *= mult;
            exp.MatchEnd.LeftMultiplier *= mult;
            exp.MatchEnd.KilledMultiplier *= mult;
            exp.MatchEnd.SurvivedExperienceReward = (int)Math.Round(exp.MatchEnd.SurvivedExperienceReward * mult);
            exp.MatchEnd.MiaExperienceReward = (int)Math.Round(exp.MatchEnd.MiaExperienceReward * mult);
            exp.MatchEnd.RunnerExperienceReward = (int)Math.Round(exp.MatchEnd.RunnerExperienceReward * mult);
            exp.MatchEnd.TransitExperienceReward = Math.Round(exp.MatchEnd.TransitExperienceReward * mult);

            exp.TriggerMult = Math.Round(exp.TriggerMult * mult);
            exp.ExpForLevelOneDogtag *= mult;
            exp.ExpForLockedDoorOpen = (int)Math.Round(exp.ExpForLockedDoorOpen * mult);
            exp.ExpForLockedDoorBreach = (int)Math.Round(exp.ExpForLockedDoorBreach * mult);

            if (exp.LootAttempts is { } lootAttempts)
            {
                foreach (var lootAttempt in lootAttempts)
                {
                    lootAttempt.ExperiencePoints *= mult;
                }
            }

            applied.Add($"Growth x{mult}");
        }

        if (GetConfig("AmmoTweaks", "Enabled", true))
        {
            var loadTime = GetConfig("AmmoTweaks", "BaseLoadTime", 0.05);
            var unloadTime = GetConfig("AmmoTweaks", "BaseUnLoadTime", 0.05);
            var stackMult = GetConfig("AmmoTweaks", "AmmoStackMultiplier", 6);

            globals.BaseLoadTime = loadTime;
            globals.BaseUnloadTime = unloadTime;

            if (stackMult > 1)
            {
                foreach (var item in items.Values)
                {
                    // Compare MongoId structs directly; ToString() here would allocate once per item
                    if (item.Parent == m_ammoParentId && item.Properties is { } properties)
                    {
                        properties.StackMaxSize *= stackMult;
                    }
                }
            }

            applied.Add($"Ammo(load {loadTime}s, stack x{stackMult})");
        }

        if (GetConfig("RaidTweaks", "Enabled", true))
        {
            var raidMinutes = GetConfig("RaidTweaks", "RaidTimeMinutes", 120);

            foreach (var location in locationTable.GetDictionary().Values)
            {
                if (location?.Base is not { } raidBase)
                {
                    continue;
                }

                raidBase.ExitAccessTime = raidMinutes;
                raidBase.EscapeTimeLimit = raidMinutes;
                raidBase.EscapeTimeLimitCoop = raidMinutes;
                raidBase.EscapeTimeLimitPVE = raidMinutes;
            }

            applied.Add($"Raid {raidMinutes}min");
        }

        if (GetConfig("SecuredContainerTweaks", "Enabled", false))
        {
            var containerWeightKg = GetConfig("SecuredContainerTweaks", "WeightKg", -100.0);
            var containerCount = 0;

            foreach (var (id, container) in items)
            {
                if (container.Properties is { IsSecured: true } properties && id != m_purseItemId)
                {
                    properties.Weight = containerWeightKg;
                    containerCount++;
                }
            }

            applied.Add($"Secure containers {containerCount}x {containerWeightKg}kg");
        }

        if (GetConfig("MoneyTweaks", "Enabled", false))
        {
            var maxLobby = GetConfig("MoneyTweaks", "MaxInLobby", 1000000);

            foreach (var restriction in globals.RestrictionsInRaid)
            {
                if (restriction.TemplateId != m_rublesItemId)
                {
                    continue;
                }

                restriction.MaxInLobby = maxLobby;

                if (restriction.MaxInRaid < maxLobby)
                {
                    restriction.MaxInRaid = maxLobby;
                }

                applied.Add($"Money carry {maxLobby}");
                break;
            }
        }

        if (GetConfig("HideoutTweaks", "Enabled", false))
        {
            var operationSeconds = GetConfig("HideoutTweaks", "OperationTimeSeconds", 3);

            if (operationSeconds > 0)
            {
                var capped = CapHideoutTimes(hideoutTable, hideoutConfig, operationSeconds);
                applied.Add($"Hideout ops {operationSeconds}s (capped {capped})");
            }
        }

        if (GetConfig("PropitalTweaks", "Enabled", false)
            && items.TryGetValue(m_propitalItemId, out var propital)
            && propital.Properties is { } propitalProperties)
        {
            var uses = GetConfig("PropitalTweaks", "Uses", 9);
            var effectSeconds = GetConfig("PropitalTweaks", "EffectSeconds", 300d);

            // Charges: a zero hpResourceRate makes each use cost exactly one charge instead of hp
            propitalProperties.MaxHpResource = uses;
            propitalProperties.HpResourceRate = 0;

            propitalProperties.EffectsDamage ??= new Dictionary<DamageEffectType, EffectsDamageProperties>();
            propitalProperties.EffectsDamage[DamageEffectType.LightBleeding] = new EffectsDamageProperties { Cost = 0, Delay = 0, Duration = 0, FadeOut = 0 };
            propitalProperties.EffectsDamage[DamageEffectType.HeavyBleeding] = new EffectsDamageProperties { Cost = 0, Delay = 0, Duration = 0, FadeOut = 0 };
            propitalProperties.EffectsDamage[DamageEffectType.Fracture] = new EffectsDamageProperties { Cost = 0, Delay = 0, Duration = 0, FadeOut = 0 };
            propitalProperties.EffectsDamage[DamageEffectType.Contusion] = new EffectsDamageProperties { Cost = 0, Delay = 0, Duration = effectSeconds, FadeOut = 0 };
            propitalProperties.EffectsDamage[DamageEffectType.Pain] = new EffectsDamageProperties { Cost = 0, Delay = 0, Duration = effectSeconds, FadeOut = 5 };

            // Names are client side EStimulatorBuffType members; the tail side effects keep their 30s length
            globals.Health.Effects.Stimulator.Buffs[m_propitalBuffName] =
            [
                CreateBuff("HealthRate", effectSeconds, 1),
                CreateBuff("SkillRate", effectSeconds, 20, "Metabolism"),
                CreateBuff("SkillRate", effectSeconds, 20, "Health"),
                CreateBuff("SkillRate", effectSeconds, 20, "Vitality"),
                CreateBuff("HandsTremor", 30, delay: effectSeconds - 30),
                CreateBuff("QuantumTunnelling", 30, delay: effectSeconds - 30),
                CreateBuff("RemoveAllBloodLosses", effectSeconds),
                CreateBuff("RemoveNegativeEffects", effectSeconds),
            ];

            applied.Add($"Propital {uses} uses + all-cure {effectSeconds}s");
        }

        if (GetConfig("ExplosiveBulletTweaks", "Enabled", false))
        {
            var bulletId = GetConfig("ExplosiveBulletTweaks", "BulletId", m_defaultExplosiveBulletId);

            if (!bulletId.IsValidMongoId())
            {
                logger.Warning($"[Speedloader]: ExplosiveBulletTweaks.BulletId '{bulletId}' is not a valid 24-char item id, skipped.");
            }
            else if (!items.TryGetValue(new MongoId(bulletId), out var explosiveBullet)
                || explosiveBullet.Properties is not { } bulletProperties)
            {
                logger.Warning($"[Speedloader]: ExplosiveBulletTweaks.BulletId '{bulletId}' has no item template, skipped.");
            }
            else
            {
                bulletProperties.HasGrenaderComponent = true;
                bulletProperties.ExplosionStrength = 120;
                bulletProperties.ExplosionType = "smallgrenade_expl";
                bulletProperties.FuzeArmTimeSec = 0.01f;
                bulletProperties.MinExplosionDistance = 2;
                bulletProperties.MaxExplosionDistance = 5;
                bulletProperties.Contusion = new Vector3(1.5f, 6f, 9f);
                bulletProperties.FragmentsCount = 5;
                bulletProperties.ShowHitEffectOnExplode = false;
                applied.Add($"Explosive {explosiveBullet.Name ?? bulletId}");
            }
        }

        if (applied.Count > 0)
        {
            logger.Info($"[Speedloader]: {string.Join(", ", applied)}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Caps every hideout construction, improvement and craft time at <paramref name="cap"/> seconds.
    /// </summary>
    /// <returns>Number of times that were actually lowered.</returns>
    private static int CapHideoutTimes(HideoutTable hideoutTable, HideoutConfig hideoutConfig, int cap)
    {
        var capped = 0;

        foreach (var area in hideoutTable.Areas)
        {
            capped += CapArea(area, cap);
        }

        if (hideoutTable.CustomAreas is { } customAreas)
        {
            foreach (var area in customAreas)
            {
                capped += CapArea(area, cap);
            }
        }

        var production = hideoutTable.Production;

        if (production.Recipes is { } recipes)
        {
            foreach (var recipe in recipes)
            {
                if (recipe.ProductionTime is double craftTime && craftTime > cap)
                {
                    recipe.ProductionTime = cap;
                    capped++;
                }
            }
        }

        if (production.ScavRecipes is { } scavRecipes)
        {
            foreach (var scavRecipe in scavRecipes)
            {
                if (scavRecipe.ProductionTime is double craftTime && craftTime > cap)
                {
                    scavRecipe.ProductionTime = cap;
                    capped++;
                }
            }
        }

        var cultistCircle = hideoutConfig.CultistCircle;
        cultistCircle.CraftTimeOverride = cap;

        if (cultistCircle.DirectRewards is { } directRewards)
        {
            foreach (var directReward in directRewards)
            {
                if (directReward.CraftTimeSeconds > cap)
                {
                    directReward.CraftTimeSeconds = cap;
                    capped++;
                }
            }
        }

        return capped;
    }

    private static int CapArea(HideoutArea area, double cap)
    {
        if (area.Stages is null)
        {
            return 0;
        }

        var capped = 0;

        foreach (var stage in area.Stages.Values)
        {
            if (stage.ConstructionTime is double buildTime && buildTime > cap)
            {
                stage.ConstructionTime = cap;
                capped++;
            }

            if (stage.Improvements is null)
            {
                continue;
            }

            foreach (var improvement in stage.Improvements)
            {
                if (improvement.ImprovementTime is double improveTime && improveTime > cap)
                {
                    improvement.ImprovementTime = cap;
                    capped++;
                }
            }
        }

        return capped;
    }

    private T GetConfig<T>(string section, string key, T fallback) where T : struct
    {
        if (config is null || config[section] is not JsonObject sectionObject || sectionObject[key] is not JsonValue value
            || !value.TryGetValue(out T result))
        {
            return fallback;
        }

        return result;
    }

    private string GetConfig(string section, string key, string fallback)
    {
        if (config is null || config[section] is not JsonObject sectionObject || sectionObject[key] is not JsonValue value
            || !value.TryGetValue(out string? result) || string.IsNullOrWhiteSpace(result))
        {
            return fallback;
        }

        return result;
    }

    private static Buff CreateBuff(string buffType, double duration, double value = 0, string skillName = "", double delay = 0)
    {
        return new Buff
        {
            BuffType = buffType,
            Chance = 1,
            Delay = delay,
            Duration = duration,
            Value = value,
            AbsoluteValue = true,
            SkillName = skillName,
        };
    }
}
