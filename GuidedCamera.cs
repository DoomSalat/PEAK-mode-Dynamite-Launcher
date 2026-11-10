using HarmonyLib;
using UnityEngine;
using Zorro.ControllerSupport;
using Zorro.Settings;

namespace DynamiteLauncher
{
    // Camera and mouse steering of the Guided Dynamite Launcher: while its flare flies, the view rides on the flare body and the mouse turns the flare instead of the player.
    // Any end of the flare drops the override, and the stock camera code puts the view back into the player's eyes in the same frame.
    public static class GuidedCamera
    {
        private const float MaxPitch = 85f;
        private const float FlareSoundFactor = 0.1f;
        private const float CentimetresToMetres = 0.01f;
        private const float PercentToFactor = 0.01f;
        private const float MillisecondsPerSecond = 1000f;

        private static readonly Quaternion MountRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // Static: constant local rotation, looks along the flare nose (local -Y) with local +Z up

        private static FlareProjectile _flare;
        private static Vector3 _startPosition;
        private static Quaternion _startRotation;
        private static float _blend;
        private static AudioSource[] _sounds;
        private static float[] _soundVolumes;
        private static float _yaw;
        private static float _pitch;
        private static Vector2 _smoothedInput;

        public static bool Active
        {
            get
            {
                return _flare != null;
            }
        }

        public static void Begin(FlareProjectile flare)
        {
            Transform camera = MainCamera.instance.transform;
            _flare = flare;
            _startPosition = camera.position;
            _startRotation = camera.rotation;
            _blend = 0f;
            _smoothedInput = Vector2.zero;
            RememberSounds(flare);

            Vector3 direction = flare.Direction.normalized;
            _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        public static void End(FlareProjectile flare)
        {
            if (_flare == flare)
            {
                _flare = null;
            }
        }

        internal static void Tick()
        {
            Character character = Character.localCharacter;
            if (_flare == null || character == null || character.data.dead || character.data.fullyPassedOut)
            {
                _flare = null;
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            Steer(character, deltaTime);
            MuteSounds();
            Mount(deltaTime);
        }

        private static void RememberSounds(FlareProjectile flare)
        {
            _sounds = flare.GetComponentsInChildren<AudioSource>(true);
            _soundVolumes = new float[_sounds.Length];
            for (int i = 0; i < _sounds.Length; i++)
            {
                _soundVolumes[i] = _sounds[i].volume;
            }
        }

        // Set every frame: the game may restore the volume on its own.
        private static void MuteSounds()
        {
            for (int i = 0; i < _sounds.Length; i++)
            {
                if (_sounds[i] != null)
                {
                    _sounds[i].volume = _soundVolumes[i] * FlareSoundFactor;
                }
            }
        }

        // Guided flares never roll: the nose follows the direction while local +Z (where the camera sits) always points to the sky, so steering never tilts the view.
        internal static Quaternion BodyRotation(Vector3 direction)
        {
            return Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Inverse(MountRotation);
        }

        // The camera is rigidly bolted to the flare body: its offset and rotation are fixed in the flare's local space (flare local +Y is the tail, local +Z is always up for guided flares, see BodyRotation), so it turns exactly with the flare.
        // The flare rotation is computed from Direction (as FlareProjectile does each physics step via BodyRotation) so the view does not lag a physics tick behind the steering.
        private static void Mount(float deltaTime)
        {
            Quaternion body = BodyRotation(_flare.Direction);
            Vector3 localOffset = new Vector3(0f, Plugin.GuidedCameraBack.Value, Plugin.GuidedCameraHeight.Value) * CentimetresToMetres;
            Vector3 mount = _flare.transform.position + body * localOffset;
            Quaternion aim = body * MountRotation;
            _blend = Mathf.Min(1f, _blend + deltaTime * MillisecondsPerSecond / Plugin.GuidedCameraBlendTime.Value);
            float eased = Mathf.SmoothStep(0f, 1f, _blend);
            MainCamera.instance.transform.SetPositionAndRotation(Vector3.Lerp(_startPosition, mount, eased), Quaternion.Slerp(_startRotation, aim, eased));
        }

        // Same sensitivity and inversion as the stock camera look, low-pass filtered so the flare banks instead of jittering with the mouse.
        private static void Steer(Character character, float deltaTime)
        {
            SettingsHandler settings = GameHandler.Instance.SettingsHandler;
            float sensitivity = InputHandler.GetCurrentUsedInputScheme() == InputScheme.KeyboardMouse
                ? settings.GetSetting<MouseSensitivitySetting>().Value
                : settings.GetSetting<ControllerSensitivitySetting>().Value;
            Vector2 raw = character.input.lookInput * sensitivity * Plugin.GuidedTurnSpeed.Value * PercentToFactor;
            raw.x *= settings.GetSetting<InvertXSetting>().Value == OffOnMode.OFF ? 1f : -1f;
            raw.y *= settings.GetSetting<InvertYSetting>().Value == OffOnMode.OFF ? 1f : -1f;
            _smoothedInput = Vector2.Lerp(_smoothedInput, raw, 1f - Mathf.Exp(-Plugin.GuidedTurnSmoothing.Value * deltaTime));
            _yaw += _smoothedInput.x;
            _pitch = Mathf.Clamp(_pitch + _smoothedInput.y, -MaxPitch, MaxPitch);
            _flare.Direction = Quaternion.Euler(-_pitch, _yaw, 0f) * Vector3.forward;
        }
    }

    [HarmonyPatch(typeof(MainCameraMovement), "LateUpdate")]
    internal static class GuidedCameraPatch
    {
        private static void Postfix()
        {
            if (GuidedCamera.Active)
            {
                GuidedCamera.Tick();
            }
        }
    }

    // The mouse steers the flare, so the player's own view stays where it was.
    [HarmonyPatch(typeof(CharacterMovement), "CanMoveCamera")]
    internal static class GuidedLookLockPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (GuidedCamera.Active)
            {
                __result = false;
            }
        }
    }
}
