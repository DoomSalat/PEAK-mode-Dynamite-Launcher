# Dynamite Launcher — Feature Index

> Два отдельных предмета (клоны AntiZooka): Dynamite Launcher (серебро, 3 заряда) и Dynamite Launcher Gold (золото, бесконечные заряды): красный камуфляж (локально), выстрел — медленный горящий флаер, при столкновении — мгновенный динамит.

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
По сети лаунчер — ванильный `0_Items/AntiZooka` (ванильный itemID). Вариант помечен в instance data ключом `(DataEntryKey)200` (`IntItemData`: 1 серебро, 2 золото; `Plugin.VariantOf`). Игроки без мода видят/берут/стреляют обычной антизукой. Клиенты с модом по метке включают камуфляж/серебро, выстрел флаером, имя, бесконечные патроны золота. Флаер и динамит — ванильные префабы, видны всем.

## Build
`dotnet build -c Release` -> копирует dll + camo_red.png в профиль Default (Thunderstore Mod Manager).

| VariantDataPatch | Postfix `Item.SetItemInstanceDataRPC`: применяет визуал, когда метка пришла после OnEnable |
| ItemNamePatch | Postfix `Item.GetItemName`: локализованное имя варианта при наличии метки |
| SpawnInHandPatch | Prefix `CharacterItems.SpawnItemInHand`: имена клонов -> `Plugin.GiveLauncher(variant)` (ванильная антизука + метка) |
| InfiniteAmmoPatch | Золотой вариант: prefix на `Action_ReduceUses.ReduceUsesRPC` не списывает заряды |
| Локализация | `NAME_`/`DESC_` ключи в `LocalizedText.mainTable` на все 15 языков (заполнено в Plugin.cs) |

## Shortcuts / unverified
- Выдача только через мод-спаунер (Premium). Не проверено в игре. Иконка инвентаря модифицированная только в каталоге Premium; в слотах инвентаря у предмета ванильная иконка антизуки.

## Config
`BepInEx/config/capitan.peak.dynamitelauncher.cfg` (создаётся при первом запуске): Ammo (SilverAmmo, GoldInfiniteAmmo), Flare (Speed, LifetimeSeconds, CollisionRadius, SpawnOffset, ColorHex), Explosion (OffsetFromSurface, DynamiteDestroyDelaySeconds), Visual (Camo, SilverTrim).

## Inventory icons
`assets/gen_icons.py` перекрашивает оригинальную иконку AntiZooka (камуфляжный ствол; рама серебро/золото) -> `icon_silver.png`, `icon_gold.png`; назначаются в `item.UIData.icon` при регистрации. Генератор читает исходную иконку из scratchpad (путь в скрипте).

## ItemSpawnerPremium
`ResourcesLoadPatch` отвечает на `Resources.Load("0_Items/<имя>")` клон-префабом, иначе Premium отказывается спавнить ("prefabMissing"). Спавн перехватывает `SpawnInHandPatch`. Клон-записи каталога (свои itemID) существуют только локально для списка Premium и никогда не инстанцируются.

## Files
| File | Content |
|---|---|
| Plugin.cs | Config, asset loading, catalog registration, `VariantOf`, `GiveLauncher` (вызывается из SpawnInHandPatch), `ExplodeDynamite` |
| LauncherText.cs | Names/descriptions in 15 languages |
| ShootPatches.cs | ShootPatch, RangePatch, InfiniteAmmoPatch |
| VisualPatches.cs | CamoPatch, VariantDataPatch, ItemNamePatch, InventoryIconPatch, BackpackIconPatch |
| SpawnPatches.cs | SpawnInHandPatch, ResourcesLoadPatch (Premium support) |
| FlareProjectile.cs | Flare movement and impact |
| thunderstore/ | manifest.json + icon.png; README.md in the root goes into the package |

## Live config
`FileSystemWatcher` на .cfg ставит флаг, `Update` через 0.3 с вызывает `Config.Reload()`. Все значения читаются через `.Value` в момент использования; `SettingChanged` для Camo/SilverTrim вызывает `CamoPatch.RefreshAll()` (материалы пересобираются из оригиналов, хранятся в `CamoMarker`). Ammo — только для новых лаунчеров.
