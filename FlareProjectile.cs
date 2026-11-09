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
            Plugin.Log("Flare impact at " + point + " after " + _age.ToString("F2") + "s");
            Plugin.Instance.StartCoroutine(Plugin.Instance.ExplodeDynamite(point));
            Finish();
        }

        private void Finish()
        {
            _finished = true;
            if (_age >= Plugin.FlareLifetime.Value)
            {
                Plugin.Log("Flare expired without impact");
            }

            PhotonNetwork.Destroy(gameObject);
        }
    }
}
