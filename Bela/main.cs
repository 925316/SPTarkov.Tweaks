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

namespace Bela;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.sp-tarkov.Bela.Tweaks";
    public override string Name { get; init; } = "Tweaks";
    public override string Author { get; init; } = "Bela";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.2.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; } = "not_now";
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "AGPL-3.0";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class Main(
    ISptLogger<Main> logger,
    ModHelper modHelper,
    // ConfigServer configServer,
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
    private const string m_ammoParentId = "5485a8684bdc2da71d8b4567";

    public Task OnLoad()
    {
        logger.Warning("[Bela Tweaks]: loading...");

        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var config = modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config.json");

        var globals = databaseService.GetGlobals(); // globals.json
        var locations = databaseService.GetLocations();
        var items = databaseService.GetItems();
        var ragfairSettings = globals.Configuration.RagFair;

        if (config.EnableAmmoLoadTweaks)
        {
            globals.Configuration.BaseLoadTime = 0.05;
            globals.Configuration.BaseUnloadTime = 0.05;
            logger.Info("[Bela Tweaks]: BaseLoadTime = BaseUnloadTime = 0.05 seconds");
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
            if (item.Parent.ToString().Contains(m_ammoParentId))
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
}

public class ModConfig
{
    public bool EnableAmmoLoadTweaks { get; set; } = true;
    public int RaidTimeMinutes { get; set; } = 120;
    public int AmmoStackMultiplier { get; set; } = 6;
}