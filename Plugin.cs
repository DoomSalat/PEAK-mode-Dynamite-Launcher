using System.Collections;
using System.IO;
using BepInEx;
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

        public const string PrefabName = "DynamiteLauncher";
        public const string DisplayName = "DYNAMITE LAUNCHER";
        private const string SourcePrefabName = "AntiZooka";

        public static GameObject Prefab { get; private set; }

        private void Awake()
        {
            Instance = this;
            string path = Path.Combine(Path.GetDirectoryName(Info.Location), "camo_red.png");
            Camo = new Texture2D(2, 2) { wrapMode = TextureWrapMode.Repeat };
            Camo.LoadImage(File.ReadAllBytes(path));
            new Harmony("capitan.peak.redflarelauncher").PatchAll();
        }

        private void Update()
        {
            if (Prefab == null)
            {
                TryRegisterItem();
            }
        }

        // Clones the stock Anti-Zooka into a separate item: own prefab name, itemID and display name.
        private void TryRegisterItem()
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

            id++;

            GameObject holder = new GameObject("DynamiteLauncherTemplate");
            holder.SetActive(false);
            DontDestroyOnLoad(holder);
            GameObject clone = Instantiate(source.gameObject, holder.transform);
            clone.name = PrefabName;
            clone.AddComponent<DynamiteLauncherMarker>();
            Item item = clone.GetComponent<Item>();
            item.itemID = id;
            item.UIData.itemName = DisplayName;

            int languages = System.Enum.GetNames(typeof(LocalizedText.Language)).Length;
            System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < languages; i++)
            {
                names.Add(i == 0 ? "Dynamite Launcher" : "");
            }

            LocalizedText.mainTable[LocalizedText.GetNameIndex(DisplayName)] = names;

            database.AddRuntimeEntry(item);
            database.itemLookup[id] = item;
            Prefab = clone;
            Logger.LogInfo("Registered item '" + PrefabName + "' with ID " + id);
        }

        public IEnumerator ExplodeDynamite(Vector3 position)
        {
            GameObject dynamite = PhotonNetwork.Instantiate("0_Items/Dynamite", position, Quaternion.identity, 0);
            dynamite.GetComponent<PhotonView>().RPC("RPC_Explode", Photon.Pun.RpcTarget.All);
            yield return new WaitForSeconds(0.2f);
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
            Vector3 position = __instance.rayOrigin.position + direction * 0.6f;
            GameObject flare = PhotonNetwork.Instantiate("0_Items/Flare", position, Quaternion.LookRotation(direction), 0);
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

                foreach (Material material in renderer.materials)
                {
                    foreach (string property in material.GetTexturePropertyNames())
                    {
                        if (material.GetTexture(property) != null)
                        {
                            material.SetTexture(property, Plugin.Camo);
                        }
                    }
                }
            }
        }
    }

    public class CamoMarker : MonoBehaviour
    {
    }

    public class DynamiteLauncherMarker : MonoBehaviour
    {
    }

    // Serves the cloned item prefab: it is not in Resources, so the stock Photon pool cannot find it.
    [HarmonyPatch(typeof(DefaultPool), nameof(DefaultPool.Instantiate))]
    public static class PoolPatch
    {
        private static bool Prefix(string prefabId, Vector3 position, Quaternion rotation, ref GameObject __result)
        {
            if (prefabId != "0_Items/" + Plugin.PrefabName || Plugin.Prefab == null)
            {
                return true;
            }

            GameObject prefab = Plugin.Prefab;
            bool wasActive = prefab.activeSelf;
            prefab.SetActive(false);
            __result = Object.Instantiate(prefab, position, rotation);
            prefab.SetActive(wasActive);
            return false;
        }
    }

    public class FlareProjectile : MonoBehaviour
    {
        private const float Speed = 14f;
        private const float Radius = 0.12f;
        private const float Lifetime = 25f;
        private static readonly Color FlareColor = new Color(1f, 0.1f, 0.1f, 1f);

        public Vector3 Direction;

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
            rig.linearVelocity = Direction * Speed;
            rig.angularVelocity = Vector3.zero;

            float distance = Speed * Time.fixedDeltaTime + 0.15f;
            RaycastHit[] hits = Physics.SphereCastAll(rig.position, Radius, Direction, distance, HelperFunctions.AllPhysical, QueryTriggerInteraction.Ignore);
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
                Impact(nearest.Value.point + nearest.Value.normal * 0.3f);
            }
            else if (_age >= Lifetime)
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
