using Peak;
using System.Collections;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace DynamiteLauncher
{
    // Optional: the client that created a launcher swaps the stock sphere a player without the mod fires from it for our flare.
    [HarmonyPatch(typeof(AntiSphere), "FixedUpdate")]
    public static class RemoteShotPatch
    {
        private const byte DestroyEventCode = 204;
        private const int DestroyResends = 4;
        private const float DestroyResendDelay = 0.4f;

        private class Checked : MonoBehaviour
        {
        }

        private static void Prefix(AntiSphere __instance)
        {
            if (!Plugin.ReplaceOthersShots.Value || __instance.GetComponent<Checked>() != null)
            {
                return;
            }

            __instance.gameObject.AddComponent<Checked>();
            PhotonView view = __instance.GetComponent<PhotonView>();
            if (view == null || view.IsMine)
            {
                return;
            }

            foreach (Character character in Character.AllCharacters)
            {
                Item item = character.data.currentItem;
                if (character.photonView.Owner != view.Owner || item == null || Plugin.VariantOf(item) == 0)
                {
                    continue;
                }

                if (!IsCreator(item))
                {
                    return;
                }

                Action_RaycastSpawnSomething action = item.GetComponent<Action_RaycastSpawnSomething>();
                Vector3 muzzle = action.rayOrigin.position;
                Vector3 direction = __instance.transform.position - muzzle;
                ShootPatch.Launch(item, muzzle, direction.sqrMagnitude > 0.01f ? direction.normalized : character.transform.forward);
                DestroyRemote(view);
                return;
            }
        }

        private static bool IsCreator(Item item)
        {
            return item.data.TryGetDataEntry<IntItemData>(Plugin.CreatorKey, out IntItemData value) && value.Value == PhotonNetwork.LocalPlayer.ActorNumber;
        }

        // Events from different senders are unordered: a third client may get our destroy before the shooter's instantiate, so resend.
        private static IEnumerator SendDestroyRepeatedly(int viewId)
        {
            Hashtable data = new Hashtable { [(byte)0] = viewId };
            for (int i = 0; i < DestroyResends; i++)
            {
                PhotonNetwork.NetworkingClient.OpRaiseEvent(DestroyEventCode, data, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
                yield return new WaitForSeconds(DestroyResendDelay);
            }
        }

        // Photon's own Destroy refuses foreign objects, so send the raw destroy event (code 204) like PhotonNetwork.Destroy does.
        private static void DestroyRemote(PhotonView view)
        {
            if (view.IsMine || PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(view.gameObject);
                return;
            }

            Plugin.Instance.StartCoroutine(SendDestroyRepeatedly(view.ViewID));
            Object.Destroy(view.gameObject);
        }
    }
}
