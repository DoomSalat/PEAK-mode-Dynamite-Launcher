# PeakRedFlareLauncher — Feature Index

> Anti-Zooka (антигравитационная базука): красный камуфляж (локально), выстрел — медленный горящий флаер, при столкновении — мгновенный динамит.

## Classes (Plugin.cs)
| Class | Role |
|---|---|
| Plugin | BepInEx entry, загрузка `camo_red.png`, корутина `ExplodeDynamite` |
| ShootPatch | Harmony prefix `Action_RaycastSpawnSomething.RunAction` для предмета с `Action_Antizooka`: спавнит флаер вместо AntiSphere |
| CamoPatch | Harmony postfix `Action_Antizooka.OnEnable`: подменяет текстуры меша `Anti-Zooka` (только локально) |
| FlareProjectile | Движение флаера (14 м/с, без гравитации), SphereCast, при ударе — динамит |

## Data Flow
RunAction -> PhotonNetwork.Instantiate("0_Items/Flare") -> FlareProjectile -> hit -> Instantiate("0_Items/Dynamite") + RPC_Explode(All) -> Destroy

## Network visibility
Только ванильные префабы (Flare, Dynamite) и ванильные RPC — игроки без мода видят флаер и взрыв. Текстура подменена только у клиентов с модом. Расход 3 зарядов — штатный `Action_ReduceUses` предмета.

## Build
`dotnet build -c Release` -> копирует dll + camo_red.png в профиль Default (Thunderstore Mod Manager).
