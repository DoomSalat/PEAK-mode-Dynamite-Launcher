using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Peak;
using Photon.Pun;
using UnityEngine;

namespace DynamiteLauncher
{
    [BepInPlugin("capitan.peak.dynamitelauncher", "Dynamite Launcher", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        private static string _logPath;

        // Written next to BepInEx's own log, flushed on every line, truncated on each game start.
        public static void Log(string message)
        {
            string role = PhotonNetwork.InRoom ? (PhotonNetwork.IsMasterClient ? "host" : "client") + " #" + PhotonNetwork.LocalPlayer.ActorNumber : "offline";
            string line = "[" + role + "] " + message;
            Instance.Logger.LogInfo(line);
            File.AppendAllText(_logPath, System.DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + System.Environment.NewLine);
        }

        public static Texture2D Camo { get; private set; }

        public const string SilverPrefabName = "DynamiteLauncher";
        public const string GoldPrefabName = "DynamiteLauncherGold";
        private const string SourcePrefabName = "AntiZooka";
        private const float ReloadDelay = 0.3f;

        // Variant marker stored in item instance data: the item itself stays a stock Anti-Zooka on the network.
        public const DataEntryKey VariantKey = (DataEntryKey)200;
        public const int SilverVariant = 1;
        public const int GoldVariant = 2;

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

        private FileSystemWatcher _configWatcher;
        private volatile bool _changed; // Set from the watcher thread; Unity APIs are only touched in Update.
        private float _reloadAt;
        private bool _registered;

        public static Texture2D SilverAtlas { get; private set; }
        public static Texture2D SilverIcon { get; private set; }
        public static Texture2D GoldIcon { get; private set; }
        public static GameObject SilverPrefab { get; private set; }
        public static GameObject GoldPrefab { get; private set; }


        private void Awake()
        {
            Instance = this;
            _logPath = Path.Combine(Paths.BepInExRootPath, "DynamiteLauncher.log");
            File.WriteAllText(_logPath, "");
            Log("Dynamite Launcher v" + Info.Metadata.Version + " loading");
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
            CamoEnabled.SettingChanged += (sender, args) => CamoPatch.RefreshAll();
            SilverTrim.SettingChanged += (sender, args) => CamoPatch.RefreshAll();
            StartConfigWatcher();
            new Harmony("capitan.peak.dynamitelauncher").PatchAll();
        }

        // Edits to the .cfg file are picked up while the game runs; all values are read live, so no restart is needed.
        private void StartConfigWatcher()
        {
            _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(Config.ConfigFilePath), Path.GetFileName(Config.ConfigFilePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += (sender, args) => _changed = true;
            _configWatcher.Created += (sender, args) => _changed = true;
        }

        private void OnDestroy()
        {
            _configWatcher?.Dispose();
        }

        private Texture2D LoadTexture(string fileName)
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Info.Location), fileName)));
            return texture;
        }

        private void Update()
        {
            if (_changed)
            {
                _changed = false;
                _reloadAt = Time.realtimeSinceStartup + ReloadDelay;
            }

            if (_reloadAt > 0f && Time.realtimeSinceStartup >= _reloadAt)
            {
                _reloadAt = 0f;
                Config.Reload();
                Log("Config reloaded from file");
            }

            if (!_registered)
            {
                TryRegisterItems();
            }
        }

        public static int VariantOf(Item item)
        {
            return item != null && item.data != null && item.data.TryGetDataEntry<IntItemData>(VariantKey, out IntItemData value) ? value.Value : 0;
        }

        public static void GiveLauncher(int variant)
        {
            Character character = Character.localCharacter;
            if (character == null || !PhotonNetwork.InRoom)
            {
                Log("GiveLauncher(" + variant + ") skipped: character=" + (character != null) + ", inRoom=" + PhotonNetwork.InRoom);
                return;
            }

            GameObject spawned = PhotonNetwork.Instantiate("0_Items/" + SourcePrefabName, character.Center, Quaternion.identity, 0);
            Item item = spawned.GetComponent<Item>();
            item.GetData<IntItemData>(VariantKey).Value = variant;
            OptionableIntItemData uses = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
            uses.HasData = true;
            uses.Value = SilverAmmo.Value;
            item.GetData<FloatItemData>(DataEntryKey.UseRemainingPercentage).Value = 1f;
            item.GetComponent<PhotonView>().RPC("SetItemInstanceDataRPC", RpcTarget.All, item.data);
            item.RequestPickup(character.GetComponent<PhotonView>());
            Log("Spawned launcher variant " + variant + ", viewID " + item.photonView.ViewID + ", ammo " + uses.Value);
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
                Log("ERROR: source item '" + SourcePrefabName + "' not found in ItemDatabase");
                _registered = true;
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

            GoldPrefab = RegisterVariant(database, source, holder, GoldPrefabName, ++id, "DYNAMITE LAUNCHER GOLD", LauncherText.GoldNames, LauncherText.GoldDescriptions, true);
            SilverPrefab = RegisterVariant(database, source, holder, SilverPrefabName, ++id, "DYNAMITE LAUNCHER", LauncherText.SilverNames, LauncherText.SilverDescriptions, false);
            Log("Registered dynamite launchers, last ID " + id);
            _registered = true;
        }

        private GameObject RegisterVariant(ItemDatabase database, Item source, GameObject holder, string prefabName, ushort id, string displayName, string[] names, string[] descriptions, bool gold)
        {
            GameObject clone = Instantiate(source.gameObject, holder.transform);
            clone.name = prefabName;
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
            Log("Dynamite explosion at " + position);
            GameObject dynamite = PhotonNetwork.Instantiate("0_Items/Dynamite", position, Quaternion.identity, 0);
            dynamite.GetComponent<PhotonView>().RPC("RPC_Explode", Photon.Pun.RpcTarget.All);
            yield return new WaitForSeconds(Plugin.DynamiteDestroyDelay.Value);
            if (dynamite != null)
            {
                PhotonNetwork.Destroy(dynamite);
            }
        }
    }
}
