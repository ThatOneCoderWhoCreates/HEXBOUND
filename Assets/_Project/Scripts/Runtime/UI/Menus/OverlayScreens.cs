using System;
using UnityEngine;
using UnityEngine.UI;

namespace HEXBOUND.UI
{
    /// <summary>Pause menu, controls list and match results.</summary>
    public sealed class OverlayScreens
    {
        public readonly GameObject Pause;
        public readonly GameObject Controls;
        public readonly GameObject Results;

        public event Action Resume;
        public event Action ShowControls;
        public event Action ToggleHitboxes;
        public event Action LeaveMatch;
        public event Action CloseControls;
        public event Action Rematch;
        public event Action ToMenu;

        private readonly Text _resultTitle;
        private readonly Text _resultDetail;
        private readonly Button _hitboxButton;
        private readonly Button _rematchButton;
        private readonly Text _sensitivity;

        public event Action<float> SensitivityChanged;

        public OverlayScreens(Transform canvas)
        {
            // Pause.
            RectTransform pause = UIFactory.Stretch(canvas, "Pause");
            Pause = pause.gameObject;
            UIFactory.Image(UIFactory.Stretch(pause, "Shade"), new Color(0f, 0f, 0f, 0.6f));
            UIFactory.Label(pause, "PAUSED", 72, UIFactory.TextColor, TextAnchor.MiddleCenter, new Vector2(0, 300), new Vector2(800, 100), FontStyle.Bold);
            UIFactory.Button(pause, "RESUME", new Vector2(0, 170), new Vector2(460, 76), () => Resume?.Invoke());
            UIFactory.Button(pause, "CONTROLS", new Vector2(0, 75), new Vector2(460, 76), () => ShowControls?.Invoke());
            _hitboxButton = UIFactory.Button(pause, "SHOW HITBOXES: OFF", new Vector2(0, -20), new Vector2(460, 76), () => ToggleHitboxes?.Invoke(), 26);
            UIFactory.Button(pause, "-", new Vector2(-180, -115), new Vector2(90, 76), () => SensitivityChanged?.Invoke(-0.02f));
            _sensitivity = UIFactory.Label(pause, "MOUSE 0.12", 28, UIFactory.TextColor, TextAnchor.MiddleCenter, new Vector2(0, -115), new Vector2(260, 60), FontStyle.Bold);
            UIFactory.Button(pause, "+", new Vector2(180, -115), new Vector2(90, 76), () => SensitivityChanged?.Invoke(0.02f));
            UIFactory.Button(pause, "LEAVE MATCH", new Vector2(0, -230), new Vector2(460, 76), () => LeaveMatch?.Invoke());
            Pause.SetActive(false);

            // Controls.
            RectTransform controls = UIFactory.Stretch(canvas, "Controls");
            Controls = controls.gameObject;
            UIFactory.Image(UIFactory.Stretch(controls, "Shade"), new Color(0f, 0f, 0f, 0.75f));
            UIFactory.Label(controls, "CONTROLS", 64, UIFactory.TextColor, TextAnchor.MiddleCenter, new Vector2(0, 430), new Vector2(800, 90), FontStyle.Bold);
            string keyboard =
                "<b>KEYBOARD + MOUSE</b>\n\n" +
                "WASD  Move (relative to camera)\n" +
                "Mouse  Camera\n" +
                "Left click  Light attack (3-hit combo)\n" +
                "Right click  Heavy attack (breaks block)\n" +
                "Q / E  Skills\n" +
                "R  Ultimate\n" +
                "Shift  Dodge (air dash in the air)\n" +
                "Space  Jump (rise while flying)\n" +
                "Left Ctrl  Block (hold)  |  Parry: tap just before a hit\n" +
                "F  Grab (beats block)\n" +
                "M  Recharge magic (+30, every 5 s)\n" +
                "Tab / Middle mouse  Lock-on\n" +
                "Esc  Pause";
            string pad =
                "<b>GAMEPAD</b>\n\n" +
                "Left stick  Move\n" +
                "Right stick  Camera\n" +
                "X  Light    Y  Heavy\n" +
                "RB  Skill 1    RT  Skill 2\n" +
                "LT  Ultimate\n" +
                "B  Dodge    A  Jump\n" +
                "LB  Block / Parry\n" +
                "D-pad down  Grab\n" +
                "D-pad up  Recharge\n" +
                "Right stick click  Lock-on\n" +
                "Start  Pause\n\n" +
                "<b>TRAINING</b>\nF1 hitboxes  F2 bot mode  F4 reset";
            Text k = UIFactory.Label(controls, keyboard, 28, UIFactory.TextColor, TextAnchor.UpperLeft, new Vector2(-380, -30), new Vector2(760, 760));
            k.supportRichText = true;
            Text g = UIFactory.Label(controls, pad, 28, UIFactory.TextColor, TextAnchor.UpperLeft, new Vector2(430, -30), new Vector2(620, 760));
            g.supportRichText = true;
            UIFactory.Button(controls, "CLOSE", new Vector2(0, -470), new Vector2(320, 70), () => CloseControls?.Invoke());
            Controls.SetActive(false);

            // Results.
            RectTransform results = UIFactory.Stretch(canvas, "Results");
            Results = results.gameObject;
            UIFactory.Image(UIFactory.Stretch(results, "Shade"), new Color(0f, 0f, 0f, 0.55f));
            _resultTitle = UIFactory.Label(results, "VICTORY", 110, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0, 200), new Vector2(1600, 150), FontStyle.Bold);
            _resultDetail = UIFactory.Label(results, string.Empty, 32, UIFactory.TextColor, TextAnchor.MiddleCenter, new Vector2(0, 50), new Vector2(1400, 160));
            _rematchButton = UIFactory.Button(results, "REMATCH", new Vector2(-220, -150), new Vector2(380, 80), () => Rematch?.Invoke());
            UIFactory.Button(results, "MAIN MENU", new Vector2(220, -150), new Vector2(380, 80), () => ToMenu?.Invoke());
            Results.SetActive(false);
        }

        public void SetHitboxLabel(bool on)
        {
            UIFactory.SetButtonLabel(_hitboxButton, "SHOW HITBOXES: " + (on ? "ON" : "OFF"));
        }

        public void SetSensitivityLabel(float value)
        {
            _sensitivity.text = "MOUSE " + value.ToString("0.00");
        }

        public void ShowResults(string title, Color color, string detail, string rematchLabel)
        {
            _resultTitle.text = title;
            _resultTitle.color = color;
            _resultDetail.text = detail;
            UIFactory.SetButtonLabel(_rematchButton, rematchLabel);
            Results.SetActive(true);
        }
    }
}
