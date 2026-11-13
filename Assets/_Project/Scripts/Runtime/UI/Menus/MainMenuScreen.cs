using System;
using UnityEngine;
using UnityEngine.UI;

namespace HEXBOUND.UI
{
    public sealed class MainMenuScreen
    {
        public readonly GameObject Root;

        public event Action Training;
        public event Action VersusBot;
        public event Action Online;
        public event Action Controls;
        public event Action Quit;

        public MainMenuScreen(Transform canvas)
        {
            RectTransform root = UIFactory.Stretch(canvas, "Main Menu");
            Root = root.gameObject;

            Image shade = UIFactory.Image(UIFactory.Stretch(root, "Shade"), new Color(0.02f, 0.01f, 0.05f, 0.35f));
            shade.raycastTarget = false;

            UIFactory.Label(root, "HEXBOUND", 150, new Color(0.85f, 0.7f, 1f), TextAnchor.MiddleCenter, new Vector2(0, 300), new Vector2(1400, 200), FontStyle.Bold);
            UIFactory.Label(root, "1v1 ARENA FIGHTER", 34, UIFactory.MutedText, TextAnchor.MiddleCenter, new Vector2(0, 190), new Vector2(1000, 60));

            float y = 70;
            UIFactory.Button(root, "TRAINING", new Vector2(0, y), new Vector2(460, 78), () => Training?.Invoke());
            UIFactory.Button(root, "VERSUS BOT", new Vector2(0, y -= 96), new Vector2(460, 78), () => VersusBot?.Invoke());
            UIFactory.Button(root, "ONLINE", new Vector2(0, y -= 96), new Vector2(460, 78), () => Online?.Invoke());
            UIFactory.Button(root, "CONTROLS", new Vector2(0, y -= 96), new Vector2(460, 78), () => Controls?.Invoke());
            UIFactory.Button(root, "QUIT", new Vector2(0, y -= 96), new Vector2(460, 78), () => Quit?.Invoke());

            UIFactory.Label(root, "Prototype build. Placeholder animation and sound.  Models: \"PBR Shadowkin Mage (Rigged)\" by Ferocious Industries, \"Arcane Inspired Character\" by schs05 (both CC-BY-4.0, Sketchfab).", 20, UIFactory.MutedText, TextAnchor.MiddleCenter, new Vector2(0, -500), new Vector2(1800, 40));
        }

        public void Show(bool visible)
        {
            Root.SetActive(visible);
        }
    }
}
