using System.Collections.Generic;
using UnityEngine;

namespace DynamiteLauncher
{
    // Turns the stock orange dynamite explosion green: particle/light/material colours are hue-shifted, baked-in sprite colours get their red and green channels swapped.
    internal static class ExplosionRecolor // Static: stateless helper with a texture cache, no scene object needed
    {
        private const float GreenHueShift = 0.25f;
        private const int MaxTexturePixels = 2048 * 2048;

        private static readonly Dictionary<Texture, Texture> GreenTextures = new Dictionary<Texture, Texture>();

        internal static void Apply(GameObject effect)
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
                if (!material.HasProperty(name))
                {
                    continue;
                }

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
    }
}
