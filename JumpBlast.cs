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
        private const float GreenHueShift = 0.25f;
        private const int MaxTexturePixels = 2048 * 2048;


        private static readonly System.Collections.Generic.Dictionary<Texture, Texture> GreenTextures = new System.Collections.Generic.Dictionary<Texture, Texture>();

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
                _effectPrefab = Resources.Load<GameObject>("0_Items/Dynamite").GetComponent<Dynamite>().explosionPrefab;
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

            Recolor(effect);
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
            Recolor(_greenOrb);
            return _greenOrb;
        }

        private static void Recolor(GameObject effect)
        {
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.startColor = Shift(main.startColor);
                ParticleSystem.ColorOverLifetimeModule overLifetime = system.colorOverLifetime;
                if (overLifetime.enabled)
                {
                    overLifetime.color = Shift(overLifetime.color);
                }

                ParticleSystem.ColorBySpeedModule bySpeed = system.colorBySpeed;
                if (bySpeed.enabled)
                {
                    bySpeed.color = Shift(bySpeed.color);
                }

                ParticleSystem.TrailModule trails = system.trails;
                if (trails.enabled)
                {
                    trails.colorOverLifetime = Shift(trails.colorOverLifetime);
                    trails.colorOverTrail = Shift(trails.colorOverTrail);
                }
            }

            foreach (Light light in effect.GetComponentsInChildren<Light>(true))
            {
                light.color = Shift(light.color);
            }

            foreach (Renderer renderer in effect.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.materials)
                {
                    RecolorMaterial(material);
                }
            }
        }

        // Walks every colour and texture property of the shader, whatever its name.
        private static void RecolorMaterial(Material material)
        {
            Shader shader = material.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string name = shader.GetPropertyName(i);
                switch (shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        material.SetColor(name, Shift(material.GetColor(name)));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        if (!name.Contains("Bump") && !name.Contains("Normal") && material.GetTexture(name) is Texture2D texture)
                        {
                            material.SetTexture(name, Greened(texture));
                        }
                        break;
                }
            }
        }

        // Explosion sprites often have orange baked in: swap the red and green channels once per texture.
        private static Texture Greened(Texture2D source)
        {
            if (GreenTextures.TryGetValue(source, out Texture cached))
            {
                return cached;
            }

            if (source.width * source.height > MaxTexturePixels)
            {
                return source;
            }

            RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, target);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            Color32[] pixels = copy.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                byte red = pixels[i].r;
                pixels[i].r = pixels[i].g;
                pixels[i].g = red;
            }

            copy.SetPixels32(pixels);
            copy.Apply();
            GreenTextures[source] = copy;
            return copy;
        }

        private static Color Shift(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            Color shifted = Color.HSVToRGB((hue + GreenHueShift) % 1f, saturation, value, true);
            shifted.a = color.a;
            return shifted;
        }

        private static Gradient Shift(Gradient gradient)
        {
            GradientColorKey[] keys = gradient.colorKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].color = Shift(keys[i].color);
            }

            Gradient result = new Gradient();
            result.SetKeys(keys, gradient.alphaKeys);
            return result;
        }

        private static ParticleSystem.MinMaxGradient Shift(ParticleSystem.MinMaxGradient source)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Shift(source.color));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Shift(source.colorMin), Shift(source.colorMax));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Shift(source.gradient));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Shift(source.gradientMin), Shift(source.gradientMax));
                case ParticleSystemGradientMode.RandomColor:
                    return new ParticleSystem.MinMaxGradient(Shift(source.gradient)) { mode = ParticleSystemGradientMode.RandomColor };
                default:
                    return source;
            }
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
