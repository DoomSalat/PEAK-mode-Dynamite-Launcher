# Dynamite Launcher

A new item for PEAK: a camo **Dynamite Launcher** built on the Anti-Zooka. It fires a slow, lit flare that turns into instantly exploding dynamite on impact.

## Variants

| Variant | Look | Ammo |
|---|---|---|
| Silver | Red camo barrel, silver frame | 3 shots (configurable) |
| Gold | Red camo barrel, original gold frame | Infinite (configurable) |

## How to get it

The mod does not add the launcher to the world loot. **You need any other mod that can spawn items from the game's item list** (for example ItemSpawnerPremium). Both variants appear in that list as "Dynamite Launcher" and "Golden Dynamite Launcher".

## Multiplayer

- Players **without** the mod see and use an ordinary Anti-Zooka, even if they pick up your launcher and shoot it.
- Players **with** the mod see the camo and the launcher name, and your launcher fires the flare.
- The flare and the explosion are vanilla networked objects, so **everyone** sees them, with or without the mod.

## Configuration

`BepInEx/config/capitan.peak.dynamitelauncher.cfg` (created on first launch). Changes to the file are picked up while the game is running, no restart needed (only the ammo count applies to newly spawned launchers):

- **Ammo** - silver ammo count, whether the gold launcher has infinite ammo.
- **Flare** - speed, lifetime, collision radius, spawn offset, light color (hex).
- **Explosion** - offset from the hit surface, dynamite removal delay.
- **Visual** - camo on/off, silver trim on/off (local only).

## Localization

Item names and descriptions are translated into all 15 languages the game supports.

## Known limitations

- Inventory slot icons are replaced only for players with the mod.
- If the lobby host does not have the mod, the variant marker may not be synced to the host, so the launcher can behave like a stock Anti-Zooka for the host.

## Source

https://github.com/DoomSalat/PEAK-mode-Dynamite-Launcher
