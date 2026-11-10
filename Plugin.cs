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
    [BepInPlugin("capitan.peak.dynamitelauncher", "Dynamite Launcher", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        public static Texture2D Camo { get; private set; }

        public const string SilverPrefabName = "DynamiteLauncher";
        public const string GoldPrefabName = "DynamiteLauncherGold";
        internal const string SourcePrefabName = "AntiZooka";
        private const float ReloadDelay = 0.3f;

        // Variant marker stored in item instance data: the item itself stays a stock Anti-Zooka on the network.
        public const DataEntryKey VariantKey = (DataEntryKey)200;
        public const DataEntryKey CreatorKey = (DataEntryKey)201;
        public const int SilverVariant = 1;
        public const int GoldVariant = 2;

        internal static ConfigEntry<bool> EnableRareChestLoot { get; private set; }
        internal static ConfigEntry<Rarity> ChestLootRarity { get; private set; }
        internal static ConfigEntry<bool> EnableMagicalLoot { get; private set; }
        public static ConfigEntry<int> SilverAmmo { get; private set; }
        public static ConfigEntry<bool> GoldInfiniteAmmo { get; private set; }
        public static ConfigEntry<float> ChargeSpeedMultiplier { get; private set; }
        public static ConfigEntry<bool> ReplaceOthersShots { get; private set; }
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
            EnableRareChestLoot = Config.Bind("Loot", "EnableRareChestLoot", false, "Host only. Add both launchers to ordinary luggage using ChestLootRarity.");
            ChestLootRarity = Config.Bind("Loot", "ChestLootRarity", Rarity.Rare, "Host only. Rarity of both launchers in ordinary luggage when EnableRareChestLoot is enabled.");
            EnableMagicalLoot = Config.Bind("Loot", "EnableMagicalLoot", false, "Host only. Add both launchers to statue rewards and ancient luggage as Mythic loot.");
            SilverAmmo = Config.Bind("Ammo", "SilverAmmo", 3, new ConfigDescription("Shots in the silver launcher. Applies to newly spawned launchers; the host's value matters for synced items.", new AcceptableValueRange<int>(1, 999)));
            GoldInfiniteAmmo = Config.Bind("Ammo", "GoldInfiniteAmmo", true, "If false, the gold launcher spends ammo like the silver one.");
            ChargeSpeedMultiplier = Config.Bind("Launcher", "ChargeSpeedMultiplier", 1f, new ConfigDescription("Charge speed for both launcher variants. 1 keeps the stock speed; 2 charges twice as fast.", new AcceptableValueRange<float>(0.1f, 10f)));
            ReplaceOthersShots = Config.Bind("Launcher", "ReplaceOthersShots", false, "Launchers you created: when a player without the mod fires one, replace their anti-gravity sphere with the flare (you must be in the room).");
            FlareSpeed = Config.Bind("Flare", "Speed", 14f, new ConfigDescription("Flare flight speed, m/s.", new AcceptableValueRange<float>(1f, 200f)));
            FlareLifetime = Config.Bind("Flare", "LifetimeSeconds", 25f, new ConfigDescription("The flare is deleted after this many seconds without hitting anything.", new AcceptableValueRange<float>(1f, 600f)));
            FlareRadius = Config.Bind("Flare", "CollisionRadius", 0.12f, new ConfigDescription("Sphere-cast radius used to detect obstacles.", new AcceptableValueRange<float>(0.01f, 2f)));
            FlareSpawnOffset = Config.Bind("Flare", "SpawnOffset", 0.6f, "Distance in front of the muzzle where the flare appears.");
            FlareColorHex = Config.Bind("Flare", "ColorHex", "#FF1A1A", "Flare light color as HTML hex, e.g. #FF1A1A.");
            ExplosionOffset = Config.Bind("Explosion", "OffsetFromSurface", 0.3f, "How far from the hit surface (along its normal) the dynamite explodes.");
            DynamiteDestroyDelay = Config.Bind("Explosion", "DynamiteDestroyDelaySeconds", 0.2f, "Time before the exploded dynamite object is removed.");
            SilverTrim = Config.Bind("Visual", "SilverTrim", true, "Silver stars/outline/frame on the silver launcher (local only); gold launcher keeps the original gold.");
            CamoEnabled = Config.Bind("Visual", "Camo", true, "Red camo on the launcher barrel (local only).");
            Camo = LoadTexture("camo_red.png");
            Camo.wrapMode = TextureWrapMode.Repeat;
            Camo.Compress(true);
            SilverAtlas = LoadTexture("silver_atlas.png");
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

        private static Texture2D LoadTexture(string fileName)
        {
            using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("DynamiteLauncher." + fileName)
                ?? throw new FileNotFoundException("Embedded texture not found.", fileName);
            using var image = new MemoryStream();
            stream.CopyTo(image);

            var texture = new Texture2D(2, 2);
            texture.LoadImage(image.ToArray());
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
                return;
            }

            GameObject spawned = PhotonNetwork.Instantiate("0_Items/" + SourcePrefabName, character.Center, Quaternion.identity, 0);
            Item item = spawned.GetComponent<Item>();
            InitializeLauncher(item, variant);
            item.RequestPickup(character.GetComponent<PhotonView>());
        }

        internal static void InitializeLauncher(Item item, int variant)
        {
            item.GetData<IntItemData>(VariantKey).Value = variant;
            item.GetData<IntItemData>(CreatorKey).Value = PhotonNetwork.LocalPlayer.ActorNumber;
            OptionableIntItemData uses = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
            uses.HasData = true;
            uses.Value = SilverAmmo.Value;
            item.GetData<FloatItemData>(DataEntryKey.UseRemainingPercentage).Value = 1f;
            item.GetComponent<PhotonView>().RPC("SetItemInstanceDataRPC", RpcTarget.All, item.data);

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
            _registered = true;
        }

        private GameObject RegisterVariant(ItemDatabase database, Item source, GameObject holder, string prefabName, ushort id, string displayName, string[] names, string[] descriptions, bool gold)
        {
            GameObject clone = Instantiate(source.gameObject, holder.transform);
            clone.name = prefabName;
            Item item = clone.GetComponent<Item>();
            if (clone.TryGetComponent<LootData>(out var lootData))
            {
                lootData.spawnLocations = SpawnPool.None;
                lootData.useOtherItemForSpawningValidity = source.gameObject;
            }
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
}
