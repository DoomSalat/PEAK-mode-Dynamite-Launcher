using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Peak;
using Photon.Pun;
using UnityEngine;

namespace PeakRedFlareLauncher
{
    [BepInPlugin("capitan.peak.redflarelauncher", "Red Flare Launcher", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static Texture2D Camo { get; private set; }

        public const string SilverPrefabName = "DynamiteLauncher";
        public const string GoldPrefabName = "DynamiteLauncherGold";
        private const string SourcePrefabName = "AntiZooka";

        public static ConfigEntry<UnityEngine.InputSystem.Key> GiveSilverKey { get; private set; }
        public static ConfigEntry<UnityEngine.InputSystem.Key> GiveGoldKey { get; private set; }
        public static ConfigEntry<int> SilverAmmo { get; private set; }
        public static ConfigEntry<bool> GoldInfiniteAmmo { get; private set; }
        public static ConfigEntry<float> FlareSpeed { get; private set; }
        public static ConfigEntry<float> FlareLifetime { get; private set; }
        public static ConfigEntry<float> FlareRadius { get; private set; }
        public static ConfigEntry<float> FlareSpawnOffset { get; private set; }
        public static ConfigEntry<string> FlareColorHex { get; private set; }
        public static ConfigEntry<float> ExplosionOffset { get; private set; }
        public static ConfigEntry<float> DynamiteDestroyDelay { get; private set; }
        public static ConfigEntry<bool> CamoEnabled { get; private set; }
        public static ConfigEntry<bool> SilverTrim { get; private set; }

        public static Texture2D SilverAtlas { get; private set; }
        public static Texture2D SilverIcon { get; private set; }
        public static Texture2D GoldIcon { get; private set; }
        public static GameObject SilverPrefab { get; private set; }
        public static GameObject GoldPrefab { get; private set; }

        // Order matches LocalizedText.Language.
        private static readonly string[] SilverNames = { "Dynamite Launcher", "Lance-dynamite", "Lanciadinamite", "Dynamit-Werfer", "Lanzadinamita", "Lanzadinamita", "Lança-dinamite", "Динамитомёт", "Динамітомет", "炸药发射器", "炸藥發射器", "ダイナマイトランチャー", "다이너마이트 발사기", "Wyrzutnia dynamitu", "Dinamit Fırlatıcı" };
        private static readonly string[] GoldNames = { "Golden Dynamite Launcher", "Lance-dynamite doré", "Lanciadinamite dorato", "Goldener Dynamit-Werfer", "Lanzadinamita dorado", "Lanzadinamita dorado", "Lança-dinamite dourado", "Золотой динамитомёт", "Золотий динамітомет", "黄金炸药发射器", "黃金炸藥發射器", "黄金のダイナマイトランチャー", "황금 다이너마이트 발사기", "Złota wyrzutnia dynamitu", "Altın Dinamit Fırlatıcı" };
        private static readonly string[] SilverDescriptions = { "Fires a slow red flare that turns into exploding dynamite on impact.", "Tire une fusée rouge lente qui devient de la dynamite explosive à l'impact.", "Spara un razzo rosso lento che diventa dinamite esplosiva all'impatto.", "Feuert eine langsame rote Leuchtfackel ab, die beim Aufprall zu explodierendem Dynamit wird.", "Dispara una bengala roja lenta que se convierte en dinamita explosiva al impactar.", "Dispara una bengala roja lenta que se convierte en dinamita explosiva al impactar.", "Dispara um sinalizador vermelho lento que vira dinamite explosiva ao impacto.", "Выпускает медленную красную сигнальную ракету, которая при ударе превращается во взрывающийся динамит.", "Випускає повільну червону сигнальну ракету, яка при ударі стає динамітом, що вибухає.", "发射缓慢飞行的红色信号弹，撞击后变成爆炸的炸药。", "發射緩慢飛行的紅色信號彈，撞擊後變成爆炸的炸藥。", "ゆっくり飛ぶ赤い信号弾を発射し、着弾するとダイナマイトが爆発する。", "느리게 날아가는 붉은 신호탄을 발사하며, 충돌하면 폭발하는 다이너마이트가 됩니다.", "Wystrzeliwuje wolną czerwoną racę, która po uderzeniu zamienia się w wybuchający dynamit.", "Yavaş uçan kırmızı bir fişek atar; çarpınca patlayan dinamite dönüşür." };
        private static readonly string[] GoldDescriptions = { "Fires a slow red flare that turns into exploding dynamite on impact. Infinite ammo.", "Tire une fusée rouge lente qui devient de la dynamite explosive à l'impact. Munitions infinies.", "Spara un razzo rosso lento che diventa dinamite esplosiva all'impatto. Munizioni infinite.", "Feuert eine langsame rote Leuchtfackel ab, die beim Aufprall zu explodierendem Dynamit wird. Unendlich Munition.", "Dispara una bengala roja lenta que se convierte en dinamita explosiva al impactar. Munición infinita.", "Dispara una bengala roja lenta que se convierte en dinamita explosiva al impactar. Munición infinita.", "Dispara um sinalizador vermelho lento que vira dinamite explosiva ao impacto. Munição infinita.", "Выпускает медленную красную сигнальную ракету, которая при ударе превращается во взрывающийся динамит. Бесконечные патроны.", "Випускає повільну червону сигнальну ракету, яка при ударі стає динамітом, що вибухає. Нескінченні патрони.", "发射缓慢飞行的红色信号弹，撞击后变成爆炸的炸药。弹药无限。", "發射緩慢飛行的紅色信號彈，撞擊後變成爆炸的炸藥。彈藥無限。", "ゆっくり飛ぶ赤い信号弾を発射し、着弾するとダイナマイトが爆発する。弾数無制限。", "느리게 날아가는 붉은 신호탄을 발사하며, 충돌하면 폭발하는 다이너마이트가 됩니다. 무한 탄약.", "Wystrzeliwuje wolną czerwoną racę, która po uderzeniu zamienia się w wybuchający dynamit. Nieskończona amunicja.", "Yavaş uçan kırmızı bir fişek atar; çarpınca patlayan dinamite dönüşür. Sınırsız mermi." };

        private void Awake()
        {
            Instance = this;
            GiveSilverKey = Config.Bind("Input", "GiveSilverKey", UnityEngine.InputSystem.Key.F10, "Key that gives the silver Dynamite Launcher (needs to be in a room).");
            GiveGoldKey = Config.Bind("Input", "GiveGoldKey", UnityEngine.InputSystem.Key.F11, "Key that gives the gold Dynamite Launcher.");
            SilverAmmo = Config.Bind("Ammo", "SilverAmmo", 3, new ConfigDescription("Shots in the silver launcher. Applies to newly spawned launchers; the host's value matters for synced items.", new AcceptableValueRange<int>(1, 999)));
            GoldInfiniteAmmo = Config.Bind("Ammo", "GoldInfiniteAmmo", true, "If false, the gold launcher spends ammo like the silver one.");
            FlareSpeed = Config.Bind("Flare", "Speed", 14f, new ConfigDescription("Flare flight speed, m/s.", new AcceptableValueRange<float>(1f, 200f)));
            FlareLifetime = Config.Bind("Flare", "LifetimeSeconds", 25f, new ConfigDescription("The flare is deleted after this many seconds without hitting anything.", new AcceptableValueRange<float>(1f, 600f)));
            FlareRadius = Config.Bind("Flare", "CollisionRadius", 0.12f, new ConfigDescription("Sphere-cast radius used to detect obstacles.", new AcceptableValueRange<float>(0.01f, 2f)));
            FlareSpawnOffset = Config.Bind("Flare", "SpawnOffset", 0.6f, "Distance in front of the muzzle where the flare appears.");
            FlareColorHex = Config.Bind("Flare", "ColorHex", "#FF1A1A", "Flare light color as HTML hex, e.g. #FF1A1A.");
            ExplosionOffset = Config.Bind("Explosion", "OffsetFromSurface", 0.3f, "How far from the hit surface (along its normal) the dynamite explodes.");
            DynamiteDestroyDelay = Config.Bind("Explosion", "DynamiteDestroyDelaySeconds", 0.2f, "Time before the exploded dynamite object is removed.");
            SilverTrim = Config.Bind("Visual", "SilverTrim", true, "Silver stars/outline/frame on the silver launcher (local only); gold launcher keeps the original gold.");
            CamoEnabled = Config.Bind("Visual", "Camo", true, "Red camo on the launcher barrel (local only).");
            string path = Path.Combine(Path.GetDirectoryName(Info.Location), "camo_red.png");
            Camo = new Texture2D(2, 2) { wrapMode = TextureWrapMode.Repeat };
            Camo.LoadImage(File.ReadAllBytes(path));
            Camo.Compress(true);
            SilverAtlas = new Texture2D(2, 2);
            SilverAtlas.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Info.Location), "silver_atlas.png")));
            SilverIcon = LoadTexture("icon_silver.png");
            GoldIcon = LoadTexture("icon_gold.png");
            new Harmony("capitan.peak.redflarelauncher").PatchAll();
        }

        private Texture2D LoadTexture(string fileName)
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Info.Location), fileName)));
            return texture;
        }

        private void Update()
        {
            if (SilverPrefab == null)
            {
                TryRegisterItems();
                return;
            }

            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[GiveSilverKey.Value].wasPressedThisFrame)
            {
                GiveLauncher(SilverPrefabName);
            }
            else if (keyboard[GiveGoldKey.Value].wasPressedThisFrame)
            {
                GiveLauncher(GoldPrefabName);
            }
        }

        private void GiveLauncher(string prefabName)
        {
            Character character = Character.localCharacter;
            if (character == null || !PhotonNetwork.InRoom)
            {
                return;
            }

            GameObject spawned = PhotonNetwork.Instantiate("0_Items/" + prefabName, character.Center, Quaternion.identity, 0);
            spawned.GetComponent<Item>().RequestPickup(character.GetComponent<PhotonView>());
        }

        // Clones the stock Anti-Zooka into two separate items: own prefab name, itemID and localized text.
        private void TryRegisterItems()
        {
            ItemDatabase database = Zorro.Core.SingletonAsset<ItemDatabase>.Instance;
            if (database == null || database.itemLookup.Count == 0)
            {
                return;
            }

            Item source = null;
            foreach (Item candidate in database.Objects)
            {
                if (candidate != null && candidate.gameObject.name == SourcePrefabName)
                {
                    source = candidate;
                    break;
                }
            }

            if (source == null)
            {
                Logger.LogError("Source item '" + SourcePrefabName + "' not found in ItemDatabase");
                enabled = false;
                return;
            }

            ushort id = 0;
            foreach (ushort key in database.itemLookup.Keys)
            {
                id = key > id ? key : id;
            }

            GameObject holder = new GameObject("DynamiteLauncherTemplates");
            holder.SetActive(false);
            DontDestroyOnLoad(holder);

            GoldPrefab = RegisterVariant(database, source, holder, GoldPrefabName, ++id, "DYNAMITE LAUNCHER GOLD", GoldNames, GoldDescriptions, true);
            SilverPrefab = RegisterVariant(database, source, holder, SilverPrefabName, ++id, "DYNAMITE LAUNCHER", SilverNames, SilverDescriptions, false);
            Logger.LogInfo("Registered dynamite launchers, last ID " + id);
        }

        private GameObject RegisterVariant(ItemDatabase database, Item source, GameObject holder, string prefabName, ushort id, string displayName, string[] names, string[] descriptions, bool gold)
        {
            GameObject clone = Instantiate(source.gameObject, holder.transform);
            clone.name = prefabName;
            clone.AddComponent<DynamiteLauncherMarker>();
            if (gold && GoldInfiniteAmmo.Value)
            {
                clone.AddComponent<GoldMarker>();
            }

            Item item = clone.GetComponent<Item>();
            item.totalUses = SilverAmmo.Value;
            item.itemID = id;
            item.UIData.itemName = displayName;
            item.UIData.icon = gold ? GoldIcon : SilverIcon;

            LocalizedText.mainTable[LocalizedText.GetNameIndex(displayName)] = new System.Collections.Generic.List<string>(names);
            LocalizedText.mainTable[LocalizedText.GetDescriptionIndex(displayName)] = new System.Collections.Generic.List<string>(descriptions);

            database.AddRuntimeEntry(item);
            database.itemLookup[id] = item;
            return clone;
        }

        public IEnumerator ExplodeDynamite(Vector3 position)
        {
            GameObject dynamite = PhotonNetwork.Instantiate("0_Items/Dynamite", position, Quaternion.identity, 0);
            dynamite.GetComponent<PhotonView>().RPC("RPC_Explode", Photon.Pun.RpcTarget.All);
            yield return new WaitForSeconds(Plugin.DynamiteDestroyDelay.Value);
            if (dynamite != null)
            {
                PhotonNetwork.Destroy(dynamite);
            }
        }
    }

    // Replaces the anti-gravity sphere spawn with a slow lit flare. Ammo is spent by the item's own Action_ReduceUses.
    [HarmonyPatch(typeof(Action_RaycastSpawnSomething), nameof(Action_RaycastSpawnSomething.RunAction))]
    public static class ShootPatch
    {
        private static bool Prefix(Action_RaycastSpawnSomething __instance)
        {
            if (__instance.GetComponent<DynamiteLauncherMarker>() == null)
            {
                return true;
            }

            Vector3 direction = MainCamera.instance.transform.forward;
            Vector3 position = __instance.rayOrigin.position + direction * Plugin.FlareSpawnOffset.Value;
            GameObject flare = PhotonNetwork.Instantiate("0_Items/Flare", position, FlareProjectile.RotationFor(direction), 0);
            flare.AddComponent<FlareProjectile>().Direction = direction;

            return false;
        }
    }

    // Removes the stock range gate: the gun is always shootable.
    [HarmonyPatch(typeof(Action_RaycastSpawnSomething), "FixedUpdate")]
    public static class RangePatch
    {
        private static void Postfix(Action_RaycastSpawnSomething __instance)
        {
            if (__instance.GetComponent<DynamiteLauncherMarker>() != null)
            {
                __instance.item.UIData.isShootable = true;
            }
        }
    }

    // Local-only visual: red camo on the gun body. Other players keep seeing the stock gun.
    [HarmonyPatch(typeof(Action_Antizooka), "OnEnable")]
    public static class CamoPatch
    {
        private const string BodyName = "Anti-Zooka";
        private static readonly Color SilverColor = new Color(0.85f, 0.87f, 0.92f, 1f);

        // The barrel (submesh 1) samples only this window of the atlas UV space; the camo is stretched over exactly it.
        private const float WindowU = 0.043f;
        private const float WindowV = 0.503f;
        private const float WindowWidth = 0.899f;
        private const float WindowHeight = 0.099f;

        private static void ApplyCamo(Material material)
        {
            Vector2 scale = new Vector2(1f / WindowWidth, 1f / WindowHeight);
            Vector2 offset = new Vector2(-WindowU / WindowWidth, -WindowV / WindowHeight);
            material.SetTexture("_BaseMap", Plugin.Camo);
            material.SetTexture("_MainTex", Plugin.Camo);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.1f);
        }

        private static void Postfix(Action_Antizooka __instance)
        {
            if (__instance.GetComponent<DynamiteLauncherMarker>() == null || __instance.GetComponent<CamoMarker>() != null)
            {
                return;
            }

            __instance.gameObject.AddComponent<CamoMarker>();
            foreach (MeshRenderer renderer in __instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != BodyName)
                {
                    continue;
                }

                Material[] materials = renderer.materials;
                if (Plugin.CamoEnabled.Value)
                {
                    Material barrel = new Material(materials[0]);
                    ApplyCamo(barrel);
                    materials[1] = barrel;
                }

                bool silver = !__instance.name.StartsWith(Plugin.GoldPrefabName);
                if (silver && Plugin.SilverTrim.Value)
                {
                    materials[0].SetTexture("_MainTex", Plugin.SilverAtlas);
                    materials[0].SetTexture("_BaseMap", Plugin.SilverAtlas);
                    materials[0].SetColor("_BaseColor", SilverColor);
                }

                renderer.materials = materials;
            }
        }
    }

    public class CamoMarker : MonoBehaviour
    {
    }

    public class DynamiteLauncherMarker : MonoBehaviour
    {
    }

    public class GoldMarker : MonoBehaviour
    {
    }

    // Gold variant never spends ammo.
    [HarmonyPatch(typeof(Action_ReduceUses), nameof(Action_ReduceUses.ReduceUsesRPC))]
    public static class InfiniteAmmoPatch
    {
        private static bool Prefix(Action_ReduceUses __instance)
        {
            return __instance.GetComponent<GoldMarker>() == null;
        }
    }

    // Serves the cloned item prefab: it is not in Resources, so the stock Photon pool cannot find it.
    [HarmonyPatch(typeof(DefaultPool), nameof(DefaultPool.Instantiate))]
    public static class PoolPatch
    {
        private static bool Prefix(string prefabId, Vector3 position, Quaternion rotation, ref GameObject __result)
        {
            GameObject prefab = prefabId == "0_Items/" + Plugin.SilverPrefabName ? Plugin.SilverPrefab : prefabId == "0_Items/" + Plugin.GoldPrefabName ? Plugin.GoldPrefab : null;
            if (prefab == null)
            {
                return true;
            }

            bool wasActive = prefab.activeSelf;
            prefab.SetActive(false);
            __result = Object.Instantiate(prefab, position, rotation);
            prefab.SetActive(wasActive);
            return false;
        }
    }

    // Mods like ItemSpawnerPremium check Resources.Load("0_Items/<name>") before spawning; answer it for our cloned prefabs.
    [HarmonyPatch(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(System.Type) })]
    public static class ResourcesLoadPatch
    {
        private static void Postfix(string path, ref Object __result)
        {
            if (__result != null)
            {
                return;
            }

            if (path == "0_Items/" + Plugin.SilverPrefabName)
            {
                __result = Plugin.SilverPrefab;
            }
            else if (path == "0_Items/" + Plugin.GoldPrefabName)
            {
                __result = Plugin.GoldPrefab;
            }
        }
    }

    public class FlareProjectile : MonoBehaviour
    {
        private static Color FlareColor
        {
            get
            {
                return ColorUtility.TryParseHtmlString(Plugin.FlareColorHex.Value, out Color color) ? color : Color.red;
            }
        }

        public Vector3 Direction;

        // The flare's flame end is its local +Y: point it backwards so the opposite end leads.
        public static Quaternion RotationFor(Vector3 direction)
        {
            return Quaternion.FromToRotation(Vector3.up, -direction);
        }

        private Item _item;
        private float _age;
        private bool _finished;

        private void Start()
        {
            _item = GetComponent<Item>();
            _item.rig.useGravity = false;
            _item.blockInteraction = true;

            Flare flare = GetComponent<Flare>();
            flare.flareColor = FlareColor;
            _item.GetData<ColorItemData>(DataEntryKey.Color).Value = FlareColor;
            _item.GetData<BoolItemData>(DataEntryKey.FlareActive).Value = true;
        }

        private void FixedUpdate()
        {
            if (_finished)
            {
                return;
            }

            _age += Time.fixedDeltaTime;
            Rigidbody rig = _item.rig;
            rig.linearVelocity = Direction * Plugin.FlareSpeed.Value;
            rig.angularVelocity = Vector3.zero;
            rig.rotation = RotationFor(Direction);

            float distance = Plugin.FlareSpeed.Value * Time.fixedDeltaTime + 0.15f;
            RaycastHit[] hits = Physics.SphereCastAll(rig.position, Plugin.FlareRadius.Value, Direction, distance, HelperFunctions.AllPhysical, QueryTriggerInteraction.Ignore);
            RaycastHit? nearest = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.distance <= 0f || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (nearest == null || hit.distance < nearest.Value.distance)
                {
                    nearest = hit;
                }
            }

            if (nearest != null)
            {
                Impact(nearest.Value.point + nearest.Value.normal * Plugin.ExplosionOffset.Value);
            }
            else if (_age >= Plugin.FlareLifetime.Value)
            {
                Finish();
            }
        }

        private void Impact(Vector3 point)
        {
            Plugin.Instance.StartCoroutine(Plugin.Instance.ExplodeDynamite(point));
            Finish();
        }

        private void Finish()
        {
            _finished = true;
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
