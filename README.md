# Dynamite Launcher

A new item for PEAK: a camo **Dynamite Launcher** built on the Anti-Zooka. It fires a slow, lit flare that turns into instantly exploding dynamite on impact.

## Gameplay

![Dynamite Launcher firing a flare that explodes on impact](https://raw.githubusercontent.com/DoomSalat/PEAK-mode-Dynamite-Launcher/c43c7293776bd7deaa97c1229359508942acd8a9/assets/gameplay.gif)

## Variants

| Variant | Look | Ammo |
|---|---|---|
| Silver | Red camo barrel, silver frame | 3 shots (configurable) |
| Gold | Red camo barrel, original gold frame | Infinite (configurable) |
| Dynamite Jumper (silver) | Green camo barrel, silver frame | 3 shots (configurable) |
| Golden Dynamite Jumper | Green camo barrel, original gold frame | Infinite (configurable) |

The **Dynamite Jumper** fires the same flare, but its blast is harmless: no real dynamite, no stun, no injury, and no fall damage right after the jump. It only throws you away from the blast point, so shoot at your feet to jump. Everyone nearby is pushed, but only players with the mod see the green explosion; others just see the flare vanish. Left mouse button fires from the front; the right mouse button fires from the rear end of the barrel, so the flare flies backwards and the blast throws you toward the crosshair (costs one charge, same as a normal shot).

## How to get it

By default, use an item spawner such as ItemSpawnerPremium to find “Dynamite Launcher”, “Golden Dynamite Launcher”, “Dynamite Jumper” and “Golden Dynamite Jumper”. The host can also enable world loot in the configuration.

## Multiplayer

Other players can join your lobby without installing the mod. When you fire the Dynamite Launcher, everyone can see the flare and the explosion because both use networked PEAK objects.

Players without the mod see a standard Anti-Zooka and get its usual shot if they pick it up and fire it. Players with the mod see the launcher’s custom name, camouflage and inventory icon.

## Configuration

Save changes to `BepInEx/config/capitan.peak.dynamitelauncher.cfg` while PEAK is running and the mod reloads them automatically. The silver launcher’s starting ammo count applies to newly spawned launchers. The file is created on first launch.

| Section | Setting | Default | Values | Effect |
| --- | --- | --- | --- | --- |
| `[Loot]` | `SpawnInLoot` | `false` | true / false | Host only. Master switch for world loot: while off, no launcher or jumper spawns in luggage or statues, and the loot settings below are ignored. |
| `[Loot]` | `EnableRareChestLoot` | `false` | true / false | Host only. Adds both launchers to ordinary luggage. |
| `[Loot]` | `ChestLootRarity` | `Rare` | Common, Uncommon, Rare, Epic, Legendary, Mythic, RidiculouslyRare | Host only. Sets the loot weight of both launchers in ordinary luggage when EnableRareChestLoot is enabled. |
| `[Loot]` | `EnableMagicalLoot` | `false` | true / false | Host only. Adds both launchers to statue rewards and ancient luggage with Mythic weight. |
| `[Loot]` | `JumperEnableRareChestLoot` | `false` | true / false | Host only. Adds both Dynamite Jumpers to ordinary luggage. |
| `[Loot]` | `JumperChestLootRarity` | `Rare` | Same values as ChestLootRarity | Host only. Loot weight of both Dynamite Jumpers in ordinary luggage. |
| `[Loot]` | `JumperEnableMagicalLoot` | `false` | true / false | Host only. Adds both Dynamite Jumpers to statue rewards and ancient luggage with Mythic weight. |
| `[Jumper]` | `SilverAmmo` | `3` | 1–999 shots | Starting ammo for newly spawned silver Dynamite Jumpers. |
| `[Jumper]` | `GoldInfiniteAmmo` | `true` | true / false | Gives the golden Dynamite Jumper infinite ammo. |
| `[Jumper]` | `LaunchSpeed` | `120` | 0–300 m/s | Speed the blast gives at point-blank range; fades with distance. |
| `[Jumper]` | `BlastRange` | `6` | 0.5–30 m | Blast radius; the push fades with distance. |
| `[Jumper]` | `UpwardBias` | `0.7` | 0–5 | Extra upward push. 0 pushes straight away from the blast point. |
| `[Launcher]` | `ChargeSpeedMultiplier` | `1` | 0.1–10× | Charge speed for both variants. 2× halves the charge time. |
| `[Launcher]` | `DetonateOnRightClick` | `true` | true / false | Right mouse button detonates all flares in flight fired from the launcher you hold. |
| `[Launcher]` | `ReplaceOthersShots` | `false` | true/false | For launchers you created: when a player without the mod fires one, replace their anti-gravity sphere with the flare. Works from any client, not only the host. |
| `[Ammo]` | `SilverAmmo` | `3` | 1–999 shots | Starting ammo for newly spawned silver launchers. |
| `[Ammo]` | `GoldInfiniteAmmo` | `true` | true / false | Gives the gold launcher infinite ammo. |
| `[Flare]` | `Speed` | `14` | 1–200 m/s | Flare flight speed. |
| `[Flare]` | `LifetimeSeconds` | `25` | 1–600 s | Time before a flare expires in flight. |
| `[Flare]` | `CollisionRadius` | `0.12` | 0.01–2 m | Radius used to detect impacts. |
| `[Flare]` | `SpawnOffset` | `0.6` | Number (m) | Distance in front of the muzzle where the flare spawns. |
| `[Flare]` | `Lit` | `true` | true / false | Fire a lit, glowing flare. If false, the flare flies unlit. |
| `[Flare]` | `ColorHex` | `#FF1A1A` | Hex color, e.g. #FF1A1A | Flare light color. Invalid values fall back to red. |
| `[Explosion]` | `OffsetFromSurface` | `0.3` | Number (m) | Distance from the hit surface where the dynamite explodes. |
| `[Explosion]` | `DynamiteDestroyDelaySeconds` | `0.2` | Number (s) | Time before the exploded dynamite is removed. |
| `[Visual]` | `Camo` | `true` | true / false | Shows red barrel camouflage locally. |
| `[Visual]` | `SilverTrim` | `true` | true / false | Shows the silver launcher’s silver trim locally. |

The loot settings apply to future rolls. Statues still prioritize reviving players when the game allows it. Players without the mod see and use a standard Anti-Zooka.

## Localization

Item names and descriptions are translated into all 15 languages the game supports.

## Known limitations

- Inventory slot icons are replaced only for players with the mod.
- If the lobby host does not have the mod, the variant marker may not be synced to the host, so the launcher can behave like a stock Anti-Zooka for the host.

## Source

https://github.com/DoomSalat/PEAK-mode-Dynamite-Launcher
