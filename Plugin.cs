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

        private void Awake()
        {
            Instance = this;
            string path = Path.Combine(Path.GetDirectoryName(Info.Location), "camo_red.png");
            Camo = new Texture2D(2, 2) { wrapMode = TextureWrapMode.Repeat };
            Camo.LoadImage(File.ReadAllBytes(path));
            new Harmony("capitan.peak.redflarelauncher").PatchAll();
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
            if (__instance.GetComponent<Action_Antizooka>() == null)
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
            if (__instance.GetComponent<Action_Antizooka>() != null)
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
            if (__instance.GetComponent<CamoMarker>() != null)
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
