using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace DynamiteLauncher
{
    public class FlareProjectile : MonoBehaviour
    {
        private const float CastMargin = 0.15f;

        private static Color FlareColor
        {
            get
            {
                return ColorUtility.TryParseHtmlString(Plugin.FlareColorHex.Value, out Color color) ? color : Color.red;
            }
        }

        private static readonly List<FlareProjectile> Active = new List<FlareProjectile>();

        public Vector3 Direction { get; set; } // Set: assigned by ShootPatch.Launch right after AddComponent
        public Item Source { get; set; } // Set: assigned by ShootPatch.Launch right after AddComponent
        public int Variant { get; set; } // Set: read from the launcher at fire time, because the launcher item can be destroyed (stowed) while the flare flies

        private Item _item;
        private float _age;
        private bool _finished;
        private bool _guided;

        private void Start()
        {
            _item = GetComponent<Item>();
            _item.rig.useGravity = false;
            _item.blockInteraction = true;
            Active.Add(this);
            _guided = Variants.IsGuided(Variant);
            if (_guided && Source != null && Source.photonView.IsMine)
            {
                GuidedCamera.Begin(this);
            }

            if (!Plugin.FlareLit.Value)
            {
                return;
            }

            Flare flare = GetComponent<Flare>();
            flare.flareColor = FlareColor;
            _item.GetData<ColorItemData>(DataEntryKey.Color).Value = FlareColor;
            _item.GetData<BoolItemData>(DataEntryKey.FlareActive).Value = true;
        }

        private void OnDestroy()
        {
            Active.Remove(this);
            GuidedCamera.End(this);
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
            rig.rotation = _guided ? GuidedCamera.BodyRotation(Direction) : RotationFor(Direction);

            float distance = Plugin.FlareSpeed.Value * Time.fixedDeltaTime + CastMargin;
            RaycastHit[] hits = Physics.SphereCastAll(rig.position, Plugin.FlareRadius.Value, Direction, distance, HelperFunctions.AllPhysical, QueryTriggerInteraction.Ignore);
            bool hasImpact = false;
            float nearestDistance = float.MaxValue;
            Vector3 impactPoint = Vector3.zero;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || (CharacterRagdoll.TryGetCharacterFromCollider(hit.collider, out Character character) && character == Character.localCharacter))
                {
                    continue;
                }

                // Distance 0 means the cast started inside this collider (hit.point is not valid then).
                bool inside = hit.distance <= 0f;
                if (hit.distance < nearestDistance)
                {
                    hasImpact = true;
                    nearestDistance = hit.distance;
                    impactPoint = inside ? rig.position : hit.point + hit.normal * Plugin.ExplosionOffset.Value;
                }
            }

            if (hasImpact)
            {
                Impact(impactPoint);
            }
            else if (_age >= Plugin.FlareLifetime.Value)
            {
                Finish();
            }
        }

        // Explodes every flare in flight that was fired from this launcher.
        public static void DetonateAll(Item source)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                FlareProjectile flare = Active[i];
                if (flare == null)
                {
                    Active.RemoveAt(i);
                }
                else if (flare.Source == source && !flare._finished)
                {
                    flare.Impact(flare.transform.position);
                }
            }
        }

        // The flare's flame end is its local +Y: point it backwards so the opposite end leads.
        public static Quaternion RotationFor(Vector3 direction)
        {
            return Quaternion.FromToRotation(Vector3.up, -direction);
        }

        internal static void Detonate(int variant, Vector3 point)
        {
            if (Variants.IsJump(variant))
            {
                JumpBlast.Trigger(point);
            }
            else
            {
                Plugin.Instance.StartCoroutine(Plugin.Instance.ExplodeDynamite(point));
            }
        }

        private void Impact(Vector3 point)
        {
            Detonate(Variant, point);
            Finish();
        }

        private void Finish()
        {
            _finished = true;
            GuidedCamera.End(this);
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
