using SPTarkov.Common.Extensions;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using System.Reflection;
using System.Text;
using System.Text.Json;

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

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public class Main(
    ISptLogger<Main> logger,
    ModHelper modHelper,
    CoreConfig coreConfig,
    GlobalTable globalTable,
    LocationTable locationTable,
    TemplateTable templateTable
    )
    : IOnLoad
{
    private const string m_ammoParentId = "5485a8684bdc2da71d8b4567";

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = System.IO.Path.Combine(pathToMod, "config.jsonc");
        ModConfig config;

        try
        {
            if (File.Exists(configPath))
            {
                config = modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config.jsonc")
                         ?? new ModConfig();
            }
            else
            {
                config = new ModConfig();
                WriteDefaultConfigWithComments(configPath);
                logger.Warning("[Speedloader]: config.json not found, created default one.");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[Speedloader]: Failed to load config.json, using defaults. Error: {ex.Message}");
            config = new ModConfig();
            WriteDefaultConfigWithComments(configPath);
        }

        var globals = globalTable.Configuration; // globals.json
        var locations = locationTable;
        var items = templateTable.Items;
        var ragfairSettings = globals.RagFair;

        if (config.SkillTweaks.Enabled)
        {
            globals.SkillFatiguePerPoint = config.SkillTweaks.SkillFatiguePerPoint;
            globals.SkillFatigueReset = config.SkillTweaks.SkillFatigueReset;
            globals.SkillFreshEffectiveness = config.SkillTweaks.SkillFreshEffectiveness;
            globals.SkillFreshPoints = config.SkillTweaks.SkillFreshPoints;
            globals.SkillPointsBeforeFatigue = config.SkillTweaks.SkillPointsBeforeFatigue;
            globals.SkillMinEffectiveness = config.SkillTweaks.SkillMinEffectiveness;
            globals.SkillsSettings.SkillProgressRate = config.SkillTweaks.SkillProgressRate;
            globals.WeaponSkillProgressRate = config.SkillTweaks.WeaponSkillProgressRate;
            globals.SkillExpPerLevel = config.SkillTweaks.SkillExpPerLevel;
            globals.Exp.MatchEnd.SurvivedMultiplier = config.SkillTweaks.SurvivedMultiplier;

            logger.Warning($"[Speedloader]: FatiguePerPoint={config.SkillTweaks.SkillFatiguePerPoint}, " +
                        $"ProgressRate={config.SkillTweaks.SkillProgressRate}, ExpPerLevel={config.SkillTweaks.SkillExpPerLevel}");
        }

        if (config.CoreFixes.Enabled)
        {
            coreConfig.Fixes.RemoveModItemsFromProfile = config.CoreFixes.RemoveModItemsFromProfile;
            coreConfig.Fixes.RemoveInvalidTradersFromProfile = config.CoreFixes.RemoveInvalidTradersFromProfile;
            coreConfig.Fixes.FixProfileBreakingInventoryItemIssues = config.CoreFixes.FixProfileBreakingInventoryItemIssues;

            logger.Warning($"[Speedloader]: RemoveModItemsFromProfile = {coreConfig.Fixes.RemoveModItemsFromProfile}, " +
                           $"RemoveInvalidTradersFromProfile = {coreConfig.Fixes.RemoveInvalidTradersFromProfile}, " +
                           $"FixProfileBreakingInventoryItemIssues = {coreConfig.Fixes.FixProfileBreakingInventoryItemIssues}");
        }

        if (config.AmmoTweaks.Enabled)
        {
            globals.BaseLoadTime = config.AmmoTweaks.BaseLoadTime;
            globals.BaseUnloadTime = config.AmmoTweaks.BaseUnLoadTime;
            logger.Info($"[Speedloader]: BaseLoadTime = {config.AmmoTweaks.BaseLoadTime} seconds, BaseUnloadTime = {config.AmmoTweaks.BaseUnLoadTime} seconds");
        }

        if (config.RaidTweaks.Enabled)
        {
            foreach (var kvp in locations.GetAllPropertiesAsDictionary())
            {
                if (kvp.Value is Location location && location.Base != null)
                {
                    location.Base.ExitAccessTime = config.RaidTweaks.RaidTimeMinutes;
                    location.Base.EscapeTimeLimit = config.RaidTweaks.RaidTimeMinutes;
                    location.Base.EscapeTimeLimitCoop = config.RaidTweaks.RaidTimeMinutes;
                    location.Base.EscapeTimeLimitPVE = config.RaidTweaks.RaidTimeMinutes;
                }
            }
            logger.Info($"[Speedloader]: All locations EscapeTimeLimit set to {config.RaidTweaks.RaidTimeMinutes} minutes");
        }

        if (config.AmmoTweaks.Enabled)
        {
            foreach (var kvp in items)
            {
                var item = kvp.Value;
                if (config.AmmoTweaks.AmmoStackMultiplier > 1 && item.Parent.ToString() == m_ammoParentId)
                {
                    item.Properties.StackMaxSize *= config.AmmoTweaks.AmmoStackMultiplier;
                }
            }
            logger.Info($"[Speedloader]: The bullet stack has been adjusted by {config.AmmoTweaks.AmmoStackMultiplier} times");
        }

        return Task.CompletedTask;
    }

    public static void WriteDefaultConfigWithComments(string configPath)
    {
        var jsonc = @"{
  ""CoreFixes"": {
    // Whether to enable Core fixes category
    ""Enabled"": true,

    // Default false, Whether to remove items added by Mods from the player profile
    ""RemoveModItemsFromProfile"": false,

    // Default false, Whether to remove invalid trader data to prevent save corruption
    ""RemoveInvalidTradersFromProfile"": false,

    // Default false, Fix inventory item issues that may cause save corruption
    ""FixProfileBreakingInventoryItemIssues"": false
  },

  ""RaidTweaks"": {
    // Whether to enable Raid tweaks category
    ""Enabled"": true,

    // Default 35, Time limit per raid (minutes)
    ""RaidTimeMinutes"": 120
  },

  ""AmmoTweaks"": {
    // Whether to enable Ammo tweaks category
    ""Enabled"": true,

    // Default 0.85, base loading time (seconds). Smaller values = faster loading
    ""BaseLoadTime"": 0.05,

    // Default 0.3, base unloading time (seconds). Smaller values = faster unloading
    ""BaseUnLoadTime"": 0.05,

    // Default 1, Ammo stack multiplier, e.g. 6 means originally 30 rounds per slot -> 180 rounds
    ""AmmoStackMultiplier"": 6
  },

""SkillTweaks"": {
  // Whether to enable Skill tweaks category
  ""Enabled"": true,

  // Fatigue multiplier per skill point (default 1 = no fatigue penalty)
  ""SkillFatiguePerPoint"": 1,

  // Fatigue reset time in seconds (default 0 = never fatigued)
  ""SkillFatigueReset"": 0,

  // Initial skill effectiveness multiplier
  ""SkillFreshEffectiveness"": 1.5,

  // Points before fatigue starts
  ""SkillPointsBeforeFatigue"": 1,

  // Minimum effectiveness multiplier
  ""SkillMinEffectiveness"": 1,

  // Global skill progress rate multiplier
  ""SkillProgressRate"": 1.8,

  // Weapon skill progress rate multiplier
  ""WeaponSkillProgressRate"": 1.8,

  // Experience required per level
  ""SkillExpPerLevel"": 150
}
}";

        File.WriteAllText(configPath, jsonc, Encoding.UTF8);
    }
}

public class ModConfig
{
    public CoreFixesConfig CoreFixes { get; set; } = new CoreFixesConfig();
    public RaidTweaksConfig RaidTweaks { get; set; } = new RaidTweaksConfig();
    public AmmoTweaksConfig AmmoTweaks { get; set; } = new AmmoTweaksConfig();
    public SkillTweaksConfig SkillTweaks { get; set; } = new SkillTweaksConfig();
}

public class CoreFixesConfig
{
    public bool Enabled { get; set; } = true;
    public bool RemoveModItemsFromProfile { get; set; } = false;
    public bool RemoveInvalidTradersFromProfile { get; set; } = false;
    public bool FixProfileBreakingInventoryItemIssues { get; set; } = false;
}

public class RaidTweaksConfig
{
    public bool Enabled { get; set; } = true;
    public int RaidTimeMinutes { get; set; } = 120;
}

public class AmmoTweaksConfig
{
    public bool Enabled { get; set; } = true;
    public double BaseLoadTime { get; set; } = 0.05;
    public double BaseUnLoadTime { get; set; } = 0.05;
    public int AmmoStackMultiplier { get; set; } = 6;
}

public class SkillTweaksConfig
{
    public bool Enabled { get; set; } = true;
    public double SkillFatiguePerPoint { get; set; } = 1;
    public int SkillFatigueReset { get; set; } = 0;
    public double SkillFreshEffectiveness { get; set; } = 1.5;
    public int SkillFreshPoints { get; set; } = 1;
    public int SkillPointsBeforeFatigue { get; set; } = 1;
    public double SkillMinEffectiveness { get; set; } = 1;
    public double SkillProgressRate { get; set; } = 1.8;
    public double WeaponSkillProgressRate { get; set; } = 1.8;
    public int SkillExpPerLevel { get; set; } = 150;
    public double SurvivedMultiplier { get; set; } = 2.5;
}
