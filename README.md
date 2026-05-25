# Nebulaetrix Mutators

White Knuckle mod that adds 13 mutators (custom gamemode modifiers) to the Campaign settings panel. Toggle one or more before starting a run to change how the game plays.

Built on top of [CiCi's Trinket & Binding Framework](https://thunderstore.io/c/white-knuckle/p/CiCisMods/CiCisTrinketAndBindingFramework/), which provides the `CustomModeRegistry` API this mod registers against.

## Mutators

| Mode | Difficulty | Description |
| --- | --- | --- |
| Devil Daggers | Easy | Shoot rebar by opening your hands |
| Zen Mode | Easy | No Mass, Bloodbugs, Teeth, etc |
| Zero-G | Easy | Zero gravity |
| elkcunK etihW | Medium | Start at the top and descend (WIP) |
| Volatile | Medium | Randomly explode |
| Markiplier% | Medium | No inventory |
| Amputated | Hard | Missing an arm |
| Baby Knuckle | Hard | 66% smaller |
| Disoriented | Hard | Inverted camera |
| Glass Knuckle | Hard | One shot to everything |
| Paraplegic | Hard | No jumping |
| Marathon Mode | Extreme | Every level in existence |
| Wind Tunnel | Extreme | Constant downward blizzard |

Combine mutators for named combos (Detonating Daggers, Backwards Buddha, Masochism, etc.). All 12 currently shippable modes active at once unlocks "Why".

## Install

1. Install [BepInEx 5](https://thunderstore.io/c/white-knuckle/p/BepInEx/BepInExPack/) (5.4.2305 or newer).
2. Install [CiCi's Trinket & Binding Framework](https://thunderstore.io/c/white-knuckle/p/CiCisMods/CiCisTrinketAndBindingFramework/).
3. Drop `NebulaetrixMutators.dll` into `BepInEx/plugins/` and `more_mutators_assets` into `BepInEx/plugins/Assets/`.

## Build

```
dotnet build -c Release
```

Output lands in `bin/Release/netstandard2.1/`. The build target also copies the DLL and asset bundle directly into your BepInEx plugins folder.

Override the BepInEx and game install paths via:
```
dotnet build -c Release -p:BepInExPath=... -p:GameManagedPath=...
```

## Credits

Original [MoreMutators](https://thunderstore.io/c/white-knuckle/p/Nebulaetrix/MoreMutators/) mod by **Nebulaetrix**. This is a port that uses the Trinket & Binding Framework's gamemode-settings UI instead of MoreMutators' custom menu, with Nebulaetrix's permission.
