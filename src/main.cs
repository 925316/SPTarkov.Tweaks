using SPTarkov.Common.Extensions;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using System.Reflection;
using System.Text.Json.Nodes;

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
    TemplateTable templateTable
    )
    : IOnLoad
{
    private const string m_ammoParentId = "5485a8684bdc2da71d8b4567";
    private const string m_armBandParentId = "5b3f15d486f77432d0509248";
    private const string m_rublesItemId = "5449016a4bdc2d6f028b456f";

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

        var applied = new List<string>();

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

            foreach (var lootAttempt in exp.LootAttempts ?? [])
            {
                lootAttempt.ExperiencePoints *= mult;
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
            applied.Add($"Ammo(load {loadTime}s, stack x{stackMult})");

            if (stackMult > 1)
            {
                foreach (var kvp in items)
                {
                    var item = kvp.Value;
                    if (item.Parent.ToString() == m_ammoParentId && item.Properties != null)
                    {
                        item.Properties.StackMaxSize *= stackMult;
                    }
                }
            }
        }

        if (GetConfig("RaidTweaks", "Enabled", true))
        {
            var raidMinutes = GetConfig("RaidTweaks", "RaidTimeMinutes", 120);

            foreach (var (_, location) in locationTable.GetDictionary())
            {
                if (location?.Base == null)
                {
                    continue;
                }

                location.Base.ExitAccessTime = raidMinutes;
                location.Base.EscapeTimeLimit = raidMinutes;
                location.Base.EscapeTimeLimitCoop = raidMinutes;
                location.Base.EscapeTimeLimitPVE = raidMinutes;
            }

            applied.Add($"Raid {raidMinutes}min");
        }

        if (GetConfig("ArmBandTweaks", "Enabled", false))
        {
            var weightKg = GetConfig("ArmBandTweaks", "WeightKg", -100);
            var armbandCount = 0;

            foreach (var kvp in items)
            {
                var item = kvp.Value;
                if (item.Parent.ToString() == m_armBandParentId && item.Properties != null)
                {
                    item.Properties.Weight = weightKg;
                    armbandCount++;
                }
            }

            applied.Add($"Armband {armbandCount}x {weightKg}kg");
        }

        if (GetConfig("MoneyTweaks", "Enabled", true))
        {
            var maxLobby = GetConfig("MoneyTweaks", "MaxInLobby", 1000000);
            var rublesId = new MongoId(m_rublesItemId);

            foreach (var restriction in globals.RestrictionsInRaid)
            {
                if (restriction.TemplateId == rublesId)
                {
                    restriction.MaxInLobby = maxLobby;

                    if (restriction.MaxInRaid < maxLobby)
                    {
                        restriction.MaxInRaid = maxLobby;
                    }

                    applied.Add($"Money carry {maxLobby}");
                    break;
                }
            }
        }


        if (applied.Count > 0)
        {
            logger.Info($"[Speedloader]: {string.Join(", ", applied)}");
        }

        return Task.CompletedTask;
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
}
