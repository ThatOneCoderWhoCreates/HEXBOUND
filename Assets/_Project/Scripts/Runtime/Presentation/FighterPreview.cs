using HEXBOUND.Simulation;
using UnityEngine;

namespace HEXBOUND.Presentation
{
    /// <summary>
    /// A small stage far away from the arena with its own camera, rendering one fighter (idle, slowly turning)
    /// into a texture for the character select screen.
    /// </summary>
    public sealed class FighterPreview
    {
        private static readonly Vector3 StagePosition = new Vector3(0f, 2000f, 0f);

        public readonly RenderTexture Texture;

        private readonly Transform _stage;
        private readonly Camera _camera;
        private FighterVisual _visual;
        private FighterAnimator _animator;
        private int _character = -1;
        private float _yaw;
        private FighterState _state;

        public FighterPreview(Transform parent)
        {
            _stage = new GameObject("Fighter Preview Stage").transform;
            _stage.SetParent(parent, false);
            _stage.position = StagePosition;

            Texture = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32) { name = "Fighter Preview", antiAliasing = 4 };
            Texture.Create();

            var camGo = new GameObject("Preview Camera");
            camGo.transform.SetParent(_stage, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.targetTexture = Texture;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.05f, 0.03f, 0.09f, 0f);
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 40f;
            _camera.enabled = false;

            var key = new GameObject("Preview Key Light").AddComponent<Light>();
            key.transform.SetParent(_stage, false);
            key.type = LightType.Point;
            key.transform.localPosition = new Vector3(2.2f, 3.2f, 3.2f);
            key.range = 14f;
            key.intensity = 6f;
            key.color = new Color(1f, 0.9f, 0.82f);
            var rim = new GameObject("Preview Rim Light").AddComponent<Light>();
            rim.transform.SetParent(_stage, false);
            rim.type = LightType.Point;
            rim.transform.localPosition = new Vector3(-2.5f, 2.5f, -2.5f);
            rim.range = 12f;
            rim.intensity = 5f;
            rim.color = new Color(0.65f, 0.45f, 1f);

            GameObject disc = MaterialFactory.Primitive(PrimitiveType.Cylinder, _stage, MaterialFactory.Lit(new Color(0.12f, 0.08f, 0.16f), 0.6f, 0.2f), "Preview Floor");
            disc.transform.localScale = new Vector3(2.6f, 0.04f, 2.6f);
            disc.transform.localPosition = new Vector3(0f, -0.04f, 0f);
        }

        /// <summary>Shows the fighter and starts rendering. Call Hide when the screen closes.</summary>
        public void Show(int characterId)
        {
            _camera.enabled = true;
            if (characterId == _character && _visual != null) return;
            Clear();
            _character = characterId;
            CharacterDef c = GameContent.GetCharacter(characterId);
            _visual = FighterRigBuilder.Build(c, _stage, 0);
            _visual.Root.localPosition = Vector3.zero;
            _animator = new FighterAnimator(_visual, c.Stance == FighterStance.Beast);
            _state = default(FighterState);
            _state.CharacterId = (byte)characterId;
            _state.Action = FighterAction.Idle;
            _state.Grounded = true;
            _state.MoveId = -1;

            // Frame the whole body.
            float h = c.Height.ToFloat();
            float distance = h * 1.9f + 1.2f;
            _camera.transform.localPosition = new Vector3(0f, h * 0.55f, distance);
            _camera.transform.LookAt(_stage.position + Vector3.up * (h * 0.48f));
            _yaw = 200f;
        }

        public void Hide()
        {
            _camera.enabled = false;
            Clear();
        }

        public void Update(float deltaTime)
        {
            if (_visual == null) return;
            _yaw += deltaTime * 28f;
            _visual.Root.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            CharacterDef c = GameContent.GetCharacter(_character);
            _state.ActionTicks++;
            _animator.Animate(ref _state, c, deltaTime, 0f);
        }

        private void Clear()
        {
            if (_animator != null) _animator.Dispose();
            if (_visual != null && _visual.Root != null) Object.Destroy(_visual.Root.gameObject);
            _visual = null;
            _animator = null;
            _character = -1;
        }
    }
}
