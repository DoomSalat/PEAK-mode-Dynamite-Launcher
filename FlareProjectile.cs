using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace DynamiteLauncher
{
    public class FlareProjectile : MonoBehaviour
    {
        private static Color FlareColor
        {
            get
            {
                return ColorUtility.TryParseHtmlString(Plugin.FlareColorHex.Value, out Color color) ? color : Color.red;
            }
        }

        private static readonly List<FlareProjectile> Active = new List<FlareProjectile>();

        public Vector3 Direction;
        public Item Source;

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

        private Item _item;
        private float _age;
        private bool _finished;

        private void Start()
        {
            _item = GetComponent<Item>();
            _item.rig.useGravity = false;
            _item.blockInteraction = true;
            Active.Add(this);

            if (!Plugin.FlareLit.Value)
            {
                return;
            }

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

        internal static void Detonate(Item source, Vector3 point)
        {
            if (Variants.IsJump(Plugin.VariantOf(source)))
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
            Detonate(Source, point);
            Finish();
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }

        private void Finish()
        {
            _finished = true;
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
