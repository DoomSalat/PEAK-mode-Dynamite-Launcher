using Peak;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace DynamiteLauncher
{
    // Replaces the anti-gravity sphere spawn with a slow lit flare. Ammo is spent by the item's own Action_ReduceUses.
    [HarmonyPatch(typeof(Action_RaycastSpawnSomething), nameof(Action_RaycastSpawnSomething.RunAction))]
    public static class ShootPatch
    {
        private const float RearMuzzleDistance = 2f;

        // Every shot goes through here: if something solid sits where the flare would appear (a wall right in front or behind the shooter), blast there instead of spawning a flare inside the mesh.
        internal static void Launch(Item source, Vector3 muzzle, Vector3 direction, bool rear = false)
        {
            int variant = Plugin.VariantOf(source);
            float reach = Plugin.FlareSpawnOffset.Value + Plugin.FlareRadius.Value;
            Vector3 shot = direction;
            Vector3 spawn = muzzle;
            if (rear)
            {
                shot = -direction;
                reach += RearMuzzleDistance;
                spawn = muzzle + shot * RearMuzzleDistance;
            }

            if (SpawnBlocked(muzzle, shot, reach, out Vector3 blastPoint))
            {
                FlareProjectile.Detonate(variant, blastPoint);
                return;
            }

            GameObject flare = PhotonNetwork.Instantiate(Plugin.ItemsFolder + Plugin.FlarePrefabName, spawn + shot * Plugin.FlareSpawnOffset.Value, FlareProjectile.RotationFor(shot), 0);
            FlareProjectile projectile = flare.AddComponent<FlareProjectile>();
            projectile.Direction = shot;
            projectile.Source = source;
            projectile.Variant = variant;
        }

        private static bool IsOwnCollider(Collider collider)
        {
            return CharacterRagdoll.TryGetCharacterFromCollider(collider, out Character character) && character == Character.localCharacter;
        }

        private static bool SpawnBlocked(Vector3 start, Vector3 direction, float reach, out Vector3 point)
        {
            float nearest = float.MaxValue;
            point = Vector3.zero;
            foreach (RaycastHit hit in Physics.RaycastAll(start, direction, reach, HelperFunctions.AllPhysical, QueryTriggerInteraction.Ignore))
            {
                if (IsOwnCollider(hit.collider) || hit.distance <= 0f || hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
                point = hit.point + hit.normal * Plugin.ExplosionOffset.Value;
            }

            if (nearest < float.MaxValue)
            {
                return true;
            }

            Vector3 end = start + direction * reach;
            foreach (Collider collider in Physics.OverlapSphere(end, Plugin.FlareRadius.Value, HelperFunctions.AllPhysical, QueryTriggerInteraction.Ignore))
            {
                if (!IsOwnCollider(collider))
                {
                    point = end;
                    return true;
                }
            }

            return false;
        }

        private static bool Prefix(Item ___item, Transform ___rayOrigin)
        {
            if (Plugin.VariantOf(___item) == 0)
            {
                return true;
            }

            Launch(___item, ___rayOrigin.position, MainCamera.instance.transform.forward);

            return false;
        }
    }
}
