# Manual Switch Toggle

A mod for **Oxygen Not Included** that restores the old behavior where the **Power Switch** and **Signal Switch** require a duplicant to physically walk to them and toggle them, instead of changing state instantly when clicked.

## What it does

Before patch AP-395113 (February 2020), switches required duplicant interaction. This mod brings that behavior back:

- Clicking a switch creates a **Toggle errand** — a duplicant must walk to it and flip it.
- The errand shows the same icon and priority system as door open/close errands.
- **Clicking a second time** while a toggle is pending cancels the errand.
- Switches support **priorities** (1–9) just like doors and other buildings.
- In **Instant Build Mode** (Ctrl+F4), switches toggle immediately without requiring a duplicant, matching door behavior.

## Installation

1. Download the latest release zip.
2. Extract the contents into:
   ```
   %USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\Local\ManualSwitchToggle\
   ```
   The folder should contain `mod.yaml`, `mod_info.yaml`, and `ManualSwitchToggle.dll`.
3. Launch the game and enable the mod from the Mods menu.

## Building from source

Requirements:
- Visual Studio 2022
- .NET Framework 4.7.1 targeting pack
- Oxygen Not Included installed at `C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded`
  *(adjust the `<HintPath>` entries in `ManualSwitchToggle.csproj` if your game is installed elsewhere)*

Open `ManualSwitchToggle.csproj` in Visual Studio and build (`Ctrl+Shift+B`). The compiled mod is automatically copied to `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\Dev\ManualSwitchToggle\`.

## Compatibility

- Tested on version U59-736649 (June 11, 2026), including the Aquatic Planet Pack DLC.
- Compatible with mods that add custom switches implementing `IPlayerControlledToggle`.
- Does not affect threshold switches (pressure, temperature) — those are automated and unaffected.

## License

MIT — see [LICENSE](LICENSE).
