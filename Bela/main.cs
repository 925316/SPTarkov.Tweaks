using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Bela;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.sp-tarkov.Bela.Tweaks";
    public override string Name { get; init; } = "Tweaks";
    public override string Author { get; init; } = "Bela";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.2.1");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; } = "https://github.com/925316/SPTarkov.Tweaks";
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "AGPL-3.0";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class Main(
    ISptLogger<Main> logger,
    ModHelper modHelper,
    ConfigServer configServer,
    DatabaseService databaseService
    )
    : IOnLoad
{
    //private readonly BotConfig m_botConfig = configServer.GetConfig<BotConfig>();
    //private readonly HideoutConfig m_hideoutConfig = configServer.GetConfig<HideoutConfig>();
    //private readonly WeatherConfig m_weatherConfig = configServer.GetConfig<WeatherConfig>();
    //private readonly AirdropConfig m_airdropConfig = configServer.GetConfig<AirdropConfig>();
    //private readonly PmcChatResponse m_pmcChatResponseConfig = configServer.GetConfig<PmcChatResponse>();
    //private readonly QuestConfig m_questConfig = configServer.GetConfig<QuestConfig>();
    //private readonly PmcConfig m_pmcConfig = configServer.GetConfig<PmcConfig>();
    private readonly CoreConfig m_coreConfig = configServer.GetConfig<CoreConfig>();

    private const string m_ammoParentId = "5485a8684bdc2da71d8b4567";

    public Task OnLoad()
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
                logger.Warning("[Bela Tweaks]: config.json not found, created default one.");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[Bela Tweaks]: Failed to load config.json, using defaults. Error: {ex.Message}");
            config = new ModConfig();
            WriteDefaultConfigWithComments(configPath);
        }

        var globals = databaseService.GetGlobals(); // globals.json
        var locations = databaseService.GetLocations();
        var items = databaseService.GetItems();
        var ragfairSettings = globals.Configuration.RagFair;

        m_coreConfig.Fixes.RemoveModItemsFromProfile = config.RemoveModItemsFromProfile;
        m_coreConfig.Fixes.RemoveInvalidTradersFromProfile = config.RemoveInvalidTradersFromProfile;
        m_coreConfig.Fixes.FixProfileBreakingInventoryItemIssues = config.FixProfileBreakingInventoryItemIssues;

        logger.Warning($"[Bela Tweaks]: RemoveModItemsFromProfile = {m_coreConfig.Fixes.RemoveModItemsFromProfile}, " +
                       $"RemoveInvalidTradersFromProfile = {m_coreConfig.Fixes.RemoveInvalidTradersFromProfile}, " +
                       $"FixProfileBreakingInventoryItemIssues = {m_coreConfig.Fixes.FixProfileBreakingInventoryItemIssues}");

        if (config.EnableAmmoLoadTweaks)
        {
            globals.Configuration.BaseLoadTime = config.BaseLoadTime;
            globals.Configuration.BaseUnloadTime = config.BaseUnLoadTime;
            logger.Info($"[Bela Tweaks]: BaseLoadTime = {config.BaseLoadTime} seconds, BaseUnloadTime = {config.BaseUnLoadTime} seconds");
        }

        foreach (var kvp in locations.GetAllPropertiesAsDictionary())
        {
            if (kvp.Value is Location location && location.Base != null)
            {
                location.Base.ExitAccessTime = config.RaidTimeMinutes;
                location.Base.EscapeTimeLimit = config.RaidTimeMinutes;
                location.Base.EscapeTimeLimitCoop = config.RaidTimeMinutes;
                location.Base.EscapeTimeLimitPVE = config.RaidTimeMinutes;
                logger.Info($"[Bela Tweaks]: {location.Base.Id} EscapeTimeLimit set to {config.RaidTimeMinutes}");
            }
        }

        foreach (var kvp in items)
        {
            var item = kvp.Value;
            if (config.AmmoStackMultiplier > 1 && item.Parent.ToString() == m_ammoParentId)
            {
                item.Properties.StackMaxSize *= config.AmmoStackMultiplier;
            }
        }

        if (config.AmmoStackMultiplier > 1)
        {
            logger.Info($"[Bela Tweaks]: The bullet stack has been adjusted by {config.AmmoStackMultiplier} times");
        }

        logger.Success("[Bela Tweaks]: Done!");

        return Task.CompletedTask;
    }

    public static void WriteDefaultConfigWithComments(string configPath)
    {
        var jsonc = @"{
  // Whether to remove items added by Mods from the player profile
  ""RemoveModItemsFromProfile"": false,

  // Whether to remove invalid trader data to prevent save corruption
  ""RemoveInvalidTradersFromProfile"": false,

  // Fix inventory item issues that may cause save corruption
  ""FixProfileBreakingInventoryItemIssues"": false,

  // Enable tweaks for ammo loading/unloading speed
  ""EnableAmmoLoadTweaks"": true,

  // Default 0.85, base loading time (seconds). Smaller values = faster loading
  ""BaseLoadTime"": 0.05,

  // Default 0.3, base unloading time (seconds). Smaller values = faster unloading
  ""BaseUnLoadTime"": 0.05,

  // Time limit per raid (minutes)
  ""RaidTimeMinutes"": 120,

  // Ammo stack multiplier, e.g. 6 means originally 30 rounds per slot → 180 rounds
  ""AmmoStackMultiplier"": 6
}";

        File.WriteAllText(configPath, jsonc, Encoding.UTF8);
    }
}

public class ModConfig
{
    public bool RemoveModItemsFromProfile { get; set; } = false;
    public bool RemoveInvalidTradersFromProfile { get; set; } = false;
    public bool FixProfileBreakingInventoryItemIssues { get; set; } = false;
    public bool EnableAmmoLoadTweaks { get; set; } = true;
    public double BaseLoadTime { get; set; } = 0.05;
    public double BaseUnLoadTime { get; set; } = 0.05;
    public int RaidTimeMinutes { get; set; } = 120;
    public int AmmoStackMultiplier { get; set; } = 6;
}