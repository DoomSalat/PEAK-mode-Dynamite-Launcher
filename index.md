# PeakRedFlareLauncher — Feature Index

> Два отдельных предмета (клоны AntiZooka): Dynamite Launcher (серебро, 3 заряда, F10) и Dynamite Launcher Gold (золото, бесконечные заряды, F11): красный камуфляж (локально), выстрел — медленный горящий флаер, при столкновении — мгновенный динамит.

## Classes (Plugin.cs)
| Class | Role |
|---|---|
| Plugin | BepInEx entry, регистрация клона в ItemDatabase, загрузка `camo_red.png`, корутина `ExplodeDynamite` |
| ShootPatch | Harmony prefix `Action_RaycastSpawnSomething.RunAction` для предмета с `Action_Antizooka`: спавнит флаер вместо AntiSphere |
| CamoPatch | Harmony postfix `Action_Antizooka.OnEnable`: камуфляж на ствол (submesh 1, копия материала рамы, UV-окно u 0.043-0.942 / v 0.503-0.602 растянуто tiling/offset), у серебряного варианта рама (submesh 0) получает `silver_atlas.png` (только локально) |
| FlareProjectile | Движение флаера (14 м/с, без гравитации), SphereCast, при ударе — динамит |

## Data Flow
RunAction -> PhotonNetwork.Instantiate("0_Items/Flare") -> FlareProjectile -> hit -> Instantiate("0_Items/Dynamite") + RPC_Explode(All) -> Destroy

## Network visibility
Только ванильные префабы (Flare, Dynamite) и ванильные RPC — игроки без мода видят флаер и взрыв. Текстура подменена только у клиентов с модом. Расход 3 зарядов — штатный `Action_ReduceUses` предмета.

## Build
`dotnet build -c Release` -> копирует dll + camo_red.png в профиль Default (Thunderstore Mod Manager).

| PoolPatch | Отдаёт клон-префаб через Photon DefaultPool |
| DynamiteLauncherMarker | Метка клона; патчи работают только с ней |
| GoldMarker + InfiniteAmmoPatch | Золотой вариант: prefix на `Action_ReduceUses.ReduceUsesRPC` не списывает заряды |
| Локализация | `NAME_`/`DESC_` ключи в `LocalizedText.mainTable` на все 15 языков (заполнено в Plugin.cs) |

## Shortcuts / unverified
- Выдача предметов — F10 (серебро) и F11 (золото); ItemSpawnerPremium не работает.

## Config
`BepInEx/config/capitan.peak.redflarelauncher.cfg` (создаётся при первом запуске): Input (клавиши выдачи), Ammo (SilverAmmo, GoldInfiniteAmmo), Flare (Speed, LifetimeSeconds, CollisionRadius, SpawnOffset, ColorHex), Explosion (OffsetFromSurface, DynamiteDestroyDelaySeconds), Visual (Camo, SilverTrim).

## Inventory icons
`assets/gen_icons.py` перекрашивает оригинальную иконку AntiZooka (камуфляжный ствол; рама серебро/золото) -> `icon_silver.png`, `icon_gold.png`; назначаются в `item.UIData.icon` при регистрации. Генератор читает исходную иконку из scratchpad (путь в скрипте).

## ItemSpawnerPremium
`ResourcesLoadPatch` отвечает на `Resources.Load("0_Items/<имя>")` клон-префабом, иначе Premium отказывается спавнить ("prefabMissing"). Сам спавн идёт через `CharacterItems.SpawnItemInHand` -> `PhotonNetwork.Instantiate` (обслуживается `PoolPatch`).
