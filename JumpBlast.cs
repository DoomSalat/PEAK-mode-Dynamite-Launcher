using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DynamiteLauncher
{
    // Harmless blast of the Dynamite Jumper: no dynamite object, no stun, no status.
    // Players with the mod get an event (green explosion + no fall damage); every other character is pushed with the stock RPCA_AddForceAtPosition, so players without the mod just fly.
    public static class JumpBlast
    {
        private const byte EventCode = 77;
        private const float FallDamageCapTime = 8f;
        private const float PushDuration = 0.15f;
        private const float MinimumFactor = 0.35f;

        private static GameObject _effectPrefab;
        private static GameObject _greenOrb;

        public static void Subscribe()
        {
            PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
        }

        public static void Unsubscribe()
        {
            PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
        }

        public static void Trigger(Vector3 point)
        {
            SpawnEffect(point);
            PushSelf(point);
            PushOthers(point);
            PhotonNetwork.RaiseEvent(EventCode, new float[] { point.x, point.y, point.z }, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        }

        private static void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != EventCode || !(photonEvent.CustomData is float[] data) || data.Length != 3)
            {
                return;
            }

            Vector3 point = new Vector3(data[0], data[1], data[2]);
            SpawnEffect(point);
            Character character = Character.localCharacter;
            if (character != null && (character.Center - point).sqrMagnitude <= Plugin.JumpBlastRange.Value * Plugin.JumpBlastRange.Value)
            {
                character.refs.movement.CapFallDamage(0f, FallDamageCapTime);
            }
        }

        // The stock dynamite explosion with its AOE (stun, statuses) removed and every colour turned green.
        private static void SpawnEffect(Vector3 point)
        {
            if (_effectPrefab == null)
            {
                _effectPrefab = Resources.Load<GameObject>(Plugin.ItemsFolder + Plugin.DynamitePrefabName).GetComponent<Dynamite>().explosionPrefab;
            }

            GameObject holder = new GameObject("JumpBlastHolder");
            holder.SetActive(false);
            GameObject effect = Object.Instantiate(_effectPrefab, point, Quaternion.identity, holder.transform);
            foreach (AOE aoe in effect.GetComponentsInChildren<AOE>(true))
            {
                Object.DestroyImmediate(aoe);
            }

            // The fireballs are not part of this prefab: ExplosionEffect spawns them later from its own explosionOrb prefab, so hand it a green copy.
            ExplosionEffect explosion = effect.GetComponentInChildren<ExplosionEffect>(true);
            if (explosion != null && explosion.explosionOrb != null)
            {
                explosion.explosionOrb = GreenOrb(explosion.explosionOrb);
            }

            ExplosionRecolor.Apply(effect);
            effect.transform.SetParent(null, true);
            Object.Destroy(holder);
        }

        // Kept alive for the whole session: the orbs are instantiated from it a fraction of a second after the blast.
        private static GameObject GreenOrb(GameObject original)
        {
            if (_greenOrb != null)
            {
                return _greenOrb;
            }

            GameObject root = new GameObject("JumpBlastOrbTemplates");
            root.SetActive(false);
            Object.DontDestroyOnLoad(root);
            _greenOrb = Object.Instantiate(original, root.transform);
            ExplosionRecolor.Apply(_greenOrb);
            return _greenOrb;
        }

        private static void PushSelf(Vector3 point)
        {
            Character character = Character.localCharacter;
            if (character == null || character.data.dead)
            {
                return;
            }

            Vector3 away = character.Center - point;
            float range = Plugin.JumpBlastRange.Value;
            float distance = away.magnitude;
            if (distance > range)
            {
                return;
            }

            float factor = Mathf.Max(1f - distance / range, MinimumFactor);
            Vector3 direction = (distance > 0.001f ? away / distance : Vector3.zero) + Vector3.up * Plugin.JumpUpwardBias.Value;
            Vector3 acceleration = direction.normalized * Plugin.JumpForce.Value * factor / PushDuration;
            character.refs.movement.CapFallDamage(0f, FallDamageCapTime);
            Plugin.Instance.StartCoroutine(PushOverTime(character, acceleration));
        }

        // A single-frame shove is eaten by the movement controller: spread the speed over several physics steps.
        private static IEnumerator PushOverTime(Character character, Vector3 acceleration)
        {
            for (float elapsed = 0f; elapsed < PushDuration && character != null; elapsed += Time.fixedDeltaTime)
            {
                foreach (Bodypart part in character.refs.ragdoll.partList)
                {
                    part.AddForce(acceleration, ForceMode.Acceleration);
                }

                yield return new WaitForFixedUpdate();
            }
        }

        // Stock RPC every client understands; an impulse of speed * mass changes the speed of a body part by about LaunchSpeed.
        private static void PushOthers(Vector3 point)
        {
            float range = Plugin.JumpBlastRange.Value;
            foreach (Character character in Character.AllCharacters)
            {
                if (character == Character.localCharacter || character.data.dead)
                {
                    continue;
                }

                Vector3 away = character.Center - point;
                if (away.sqrMagnitude > range * range)
                {
                    continue;
                }

                float mass = 0f;
                foreach (Bodypart part in character.refs.ragdoll.partList)
                {
                    mass += part.Rig.mass;
                }

                mass /= character.refs.ragdoll.partList.Count;
                Vector3 direction = (away.normalized + Vector3.up * Plugin.JumpUpwardBias.Value).normalized;
                Vector3 impulse = direction * Plugin.JumpForce.Value * mass;
                character.photonView.RPC("RPCA_AddForceAtPosition", RpcTarget.All, impulse, point, range);
            }
        }
    }
}
