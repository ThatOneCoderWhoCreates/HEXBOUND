using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// Orbit camera behind the local fighter, rotated with the mouse or right stick (Genshin Impact style).
    /// With lock-on, it swings behind the player to keep the opponent in view.
    /// The camera is local only: it never affects the simulation except through the movement direction.
    /// </summary>
    public sealed class ThirdPersonCamera
    {
        public readonly Camera Camera;

        public float Yaw;
        public float Pitch = 16f;
        public float Distance = 6.2f;

        private Vector3 _focus;
        private Vector3 _focusVelocity;
        private float _shake;
        private bool _hasFocus;

        public ThirdPersonCamera(Transform parent)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            Camera = go.AddComponent<Camera>();
            Camera.fieldOfView = 58f;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 450f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.12f, 0.04f, 0.03f);
            go.AddComponent<AudioListener>();
            UniversalAdditionalCameraData data = go.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        public void AddShake(float amount)
        {
            _shake = Mathf.Max(_shake, amount);
        }

        /// <summary>Menu view: slow orbit around the arena.</summary>
        public void UpdateMenu(float deltaTime)
        {
            Yaw += deltaTime * 5f;
            Pitch = 17f;
            Vector3 focus = new Vector3(0f, -1.5f, 0f);
            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Camera.transform.position = focus - rot * Vector3.forward * 27f;
            Camera.transform.rotation = rot;
            _hasFocus = false;
        }

        public void UpdateFollow(Vector3 player, Vector3 opponent, bool lockOn, Vector2 look, bool invertY, float deltaTime)
        {
            Yaw += look.x;
            Pitch -= look.y * (invertY ? -1f : 1f);
            Pitch = Mathf.Clamp(Pitch, -15f, 65f);

            Vector3 focusTarget = player + Vector3.up * 1.45f;
            if (lockOn)
            {
                Vector3 toOpp = opponent - player;
                toOpp.y = 0f;
                if (toOpp.sqrMagnitude > 0.25f)
                {
                    float targetYaw = Mathf.Atan2(toOpp.x, toOpp.z) * Mathf.Rad2Deg;
                    Yaw = Mathf.LerpAngle(Yaw, targetYaw, 1f - Mathf.Exp(-6f * deltaTime));
                    Pitch = Mathf.Lerp(Pitch, 14f, 1f - Mathf.Exp(-3f * deltaTime));
                }
                focusTarget = Vector3.Lerp(focusTarget, opponent + Vector3.up * 1.2f, 0.3f);
            }

            if (!_hasFocus)
            {
                _focus = focusTarget;
                _hasFocus = true;
            }
            _focus = Vector3.SmoothDamp(_focus, focusTarget, ref _focusVelocity, 0.06f, Mathf.Infinity, deltaTime);

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 pos = _focus - rot * Vector3.forward * Distance;
            if (pos.y < 0.35f && Mathf.Abs(player.x) < 13.5f && Mathf.Abs(player.z) < 13.5f) pos.y = 0.35f;

            _shake = Mathf.MoveTowards(_shake, 0f, deltaTime * 2.5f);
            if (_shake > 0f)
            {
                float t = Time.unscaledTime * 55f;
                pos += new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * _shake * 0.35f;
            }

            Camera.transform.position = pos;
            Camera.transform.rotation = Quaternion.LookRotation(_focus - pos, Vector3.up);
        }

        /// <summary>Snaps the camera behind a fighter facing the given yaw (used at round start).</summary>
        public void SnapBehind(float yaw)
        {
            Yaw = yaw;
            Pitch = 16f;
            _hasFocus = false;
        }
    }
}
