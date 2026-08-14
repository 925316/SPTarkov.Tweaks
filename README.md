# Speedloader

A server-side mod for SPT 4.1.x: faster ammo loading, adjustable raid time limits, bigger ammo stacks, skill progression tuning, and save-corruption fixes.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build

Double-click `build.bat`, or run:

```powershell
dotnet build SPTarkov.Tweaks.sln -c Release
```

Output goes to `Build\Release\SPT_Runtime\user\mods\Speedloader`.

## Install

- Copy the `SPT_Runtime` folder from `Build\Release` into your server root, or
- Copy only the `Speedloader` folder into `<server root>\SPT_Runtime\user\mods\`

Start the server. A default `config.jsonc` is generated in the mod folder on first run.

## License

[AGPL-3.0](LICENSE)

## Post

[sp-mod.com](https://sp-mod.com/mod/2458/belastweaks)
