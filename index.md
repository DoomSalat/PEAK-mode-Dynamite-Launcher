# Dynamite Launcher — Feature Index

> Два варианта клона AntiZooka: Dynamite Launcher (серебро, конечные заряды) и Golden Dynamite Launcher (бесконечные заряды). Выстрел — медленный горящий флаер, при столкновении — мгновенный динамит. Версия 1.1.0.

## Layer Map
```
Plugin (BepInEx entry, конфиг, каталог, VariantOf/InitializeLauncher/GiveLauncher, ExplodeDynamite)
├── ShootPatches.cs  — ShootPatch, RemoteShotPatch, ChargeSpeedPatch, RangePatch, InfiniteAmmoPatch
├── VisualPatches.cs — CamoPatch, VariantDataPatch, ItemNamePatch, InventoryIconPatch, BackpackIconPatch, CamoMarker
├── SpawnPatches.cs  — SpawnInHandPatch, ResourcesLoadPatch
├── LootPatches.cs   — LauncherLootPoolPatch, LauncherLootSpawnPatch
├── LauncherText.cs  — названия/описания, 15 языков
└── FlareProjectile.cs — движение флаера, SphereCast, динамит
```

## Network model
По сети лаунчер — ванильный `0_Items/AntiZooka`. Данные в `ItemInstanceData`:
- `(DataEntryKey)200` `IntItemData` — вариант (1 серебро, 2 золото), читается через `Plugin.VariantOf`.
- `(DataEntryKey)201` `IntItemData` — ActorNumber создателя лаунчера (`InitializeLauncher`).

Игроки без мода видят/берут/стреляют обычной антизукой. Клиенты с модом по метке включают камуфляж/серебро, имя, флаер, бесконечные патроны золота. Флаер и динамит — ванильные префабы, видны всем.

## Shot flow
Ванильно: `Action_Antizooka.RunAction` (только у владельца) шлёт `RPCA_Shoot` (эффекты, без снаряда); снаряд `AntiSphere` создаёт `Action_RaycastSpawnSomething.RunAction` на машине стреляющего.

- **Стреляющий с модом:** `ShootPatch` (prefix `RunAction`) -> `ShootPatch.Launch` -> `PhotonNetwork.Instantiate("0_Items/Flare")` + `FlareProjectile` -> удар -> `ExplodeDynamite` (Instantiate `0_Items/Dynamite` + RPC взрыва, затем Destroy).
- **Стреляющий без мода** (только при `Launcher.ReplaceOthersShots = true`, по умолчанию выключено): его клиент создаёт `AntiSphere`. `RemoteShotPatch` (prefix `AntiSphere.FixedUpdate`, один раз на сферу) проверяет: сфера чужая, у её владельца в руке лаунчер с меткой, создатель лаунчера (ключ 201) — текущий клиент. Тогда запускает флаер от `rayOrigin` в точку сферы и удаляет сферу: `PhotonNetwork.Destroy`, если владелец/мастер, иначе сырое событие Photon 204 (`OpRaiseEvent`, ключ `0` = ViewID) 4 раза с интервалом 0.4 с плюс локальный `Object.Destroy`. Хостом быть не нужно.

## Entry points
| Trigger | Handler |
|---|---|
| Выстрел владельца с модом | ShootPatch |
| Выстрел игрока без мода | RemoteShotPatch (подменяющий клиент) |
| Удержание кнопки | ChargeSpeedPatch (делит `usingTimePrimary` на `ChargeSpeedMultiplier`) |
| Spawner Premium | SpawnInHandPatch -> `GiveLauncher(variant)`, `ResourcesLoadPatch` отвечает на `Resources.Load` |
| Сундуки (хост) | LauncherLootPoolPatch подмешивает лаунчеры в пул, LauncherLootSpawnPatch подменяет имя на AntiZooka и вызывает `InitializeLauncher` |

## Classes
| Class | Role |
|---|---|
| ShootPatch | prefix `RunAction`; `Launch(muzzle, direction)` общий запуск флаера |
| RemoteShotPatch | подмена выстрела игрока без мода (см. выше) |
| ChargeSpeedPatch | множитель скорости зарядки, finalizer возвращает значение |
| RangePatch | лаунчер всегда `isShootable` |
| InfiniteAmmoPatch | золото не тратит патроны (только у клиентов с модом) |
| CamoPatch | камуфляж ствола (submesh 1, копия материала рамы, UV-окно), серебряная рама (`silver_atlas.png`), локально |
| VariantDataPatch | postfix `SetItemInstanceDataRPC`: визуал, если метка пришла позже `OnEnable` |
| ItemNamePatch | локализованное имя варианта |
| InventoryIconPatch / BackpackIconPatch | иконки инвентаря/рюкзака |
| SpawnInHandPatch / ResourcesLoadPatch | поддержка ItemSpawnerPremium |
| LauncherLootPoolPatch / LauncherLootSpawnPatch | лут в сундуках (хост) |
| FlareProjectile | полёт флаера, SphereCast, динамит |

## Config
`BepInEx/config/capitan.peak.dynamitelauncher.cfg`: Ammo (SilverAmmo, GoldInfiniteAmmo), Flare (Speed, LifetimeSeconds, CollisionRadius, SpawnOffset, ColorHex), Explosion (OffsetFromSurface, DynamiteDestroyDelaySeconds), Visual (Camo, SilverTrim), Loot (EnableRareChestLoot, ChestLootRarity, EnableMagicalLoot), Launcher (ChargeSpeedMultiplier, ReplaceOthersShots). Живая перезагрузка: `FileSystemWatcher` ставит флаг, `Update` через 0.3 с вызывает `Config.Reload()`; `SettingChanged` для Camo/SilverTrim вызывает `CamoPatch.RefreshAll()`. Ammo — только для новых лаунчеров.

## Assets
Текстуры (`camo_red.png`, `silver_atlas.png`, `icon_silver.png`, `icon_gold.png`) вшиты в dll как `EmbeddedResource` (`DynamiteLauncher.<file>.png`). `assets/gen_icons.py` перекрашивает иконку AntiZooka. Клон-записи каталога (свои itemID) существуют локально только для списка Premium и не инстанцируются; `LootData.spawnLocations = None`.

## Build / Files
`dotnet build -c Release`; в профиль копируется только dll, если задан `ProfilePlugins`. Папка `thunderstore/` — manifest.json и icon.png; корневой README.md входит в пакет.

## Cross-Feature / Save
Сохранения нет (состояние в instance data предмета). Зависит от Photon (ссылка на `Photon3Unity3D.dll` для `Hashtable`/`OpRaiseEvent`).

## Shortcuts / unverified
- Принятие события 204 от не-владельца по коду Photon (декомпиляция): обработчик владельца не проверяет; онлайн не проверено.
- События от разных отправителей не упорядочены: поэтому 4 повторные отправки; третий клиент может на мгновение увидеть сферу.
- Патроны игрока без мода тратятся ванильно, бесконечность золота для него не действует.
- Если создатель вышел из комнаты, подмена для его лаунчеров не работает. Лаунчеры без ключа 201 не подменяются.
