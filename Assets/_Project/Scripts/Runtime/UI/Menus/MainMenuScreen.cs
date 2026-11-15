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
        public event Action Beast;
        public event Action Arcade;
        public event Action Online;
        public event Action Controls;
        public event Action Quit;

        public MainMenuScreen(Transform canvas)
        {
            RectTransform root = UIFactory.Stretch(canvas, "Main Menu");
            Root = root.gameObject;

            Image shade = UIFactory.Image(UIFactory.Stretch(root, "Shade"), new Color(0.02f, 0.01f, 0.05f, 0.35f));
            shade.raycastTarget = false;

            UIFactory.Label(root, "WUSHEN", 150, new Color(0.85f, 0.7f, 1f), TextAnchor.MiddleCenter, new Vector2(0, 350), new Vector2(1400, 200), FontStyle.Bold);
            UIFactory.Label(root, "1v1 ARENA FIGHTER", 34, UIFactory.MutedText, TextAnchor.MiddleCenter, new Vector2(0, 245), new Vector2(1000, 60));

            float y = 160;
            Vector2 size = new Vector2(460, 70);
            UIFactory.Button(root, "TRAINING", new Vector2(0, y), size, () => Training?.Invoke());
            UIFactory.Button(root, "VERSUS BOT", new Vector2(0, y -= 84), size, () => VersusBot?.Invoke());
            UIFactory.Button(root, "ARCADE", new Vector2(0, y -= 84), size, () => Arcade?.Invoke());
            UIFactory.Button(root, "FIGHT THE BEAST", new Vector2(0, y -= 84), size, () => Beast?.Invoke());
            UIFactory.Button(root, "ONLINE", new Vector2(0, y -= 84), size, () => Online?.Invoke());
            UIFactory.Button(root, "CONTROLS", new Vector2(0, y -= 84), size, () => Controls?.Invoke());
            UIFactory.Button(root, "QUIT", new Vector2(0, y -= 84), size, () => Quit?.Invoke());

            UIFactory.Label(root, "Developed by Pixtel Studios", 24, UIFactory.MutedText, TextAnchor.MiddleCenter, new Vector2(0, -500), new Vector2(1000, 40));

            RectTransform creditsButton = UIFactory.Rect(root, "Credits Button", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 30), new Vector2(200, 56));
            UIFactory.Button(creditsButton, "CREDITS", Vector2.zero, new Vector2(200, 56), () => _credits.SetActive(true), 24);

            _credits = BuildCredits(root);
            _credits.SetActive(false);
        }

        private readonly GameObject _credits;

        private const string CharactersText =
            "<b>CHARACTERS</b>\n\n" +
            "<b>1. PBR Shadowkin Mage (Rigged)</b>\n" +
            "Creator: Ferocious Industries\n" +
            "Source: https://skfb.ly/oLvGM\n" +
            "Licence: Creative Commons Attribution 4.0 (CC BY 4.0)\n" +
            "https://creativecommons.org/licenses/by/4.0/\n\n" +
            "<b>2. Female Warrior</b>\n" +
            "Creator: Chenchanchong\n" +
            "Source: https://skfb.ly/pB8K7\n" +
            "Licence: Creative Commons Attribution 4.0 (CC BY 4.0)\n" +
            "https://creativecommons.org/licenses/by/4.0/\n\n" +
            "<b>3. Arcane Inspired Character</b>\n" +
            "Creator: schs05\n" +
            "Source: https://skfb.ly/oPwnF\n" +
            "Licence: Creative Commons Attribution 4.0 (CC BY 4.0)\n" +
            "https://creativecommons.org/licenses/by/4.0/\n\n" +
            "<b>4. Gorath (the Beast)</b>\n" +
            "Model by Pixtel Studios, made with Modly.\n" +
            "A skeleton was added so it can be animated.\n\n" +
            "<b>5. Bram, the Dragon Mage, and his staff</b>\n" +
            "Models by Pixtel Studios, made with Modly.\n" +
            "A skeleton was added so it can be animated.";

        private const string EnvironmentText =
            "<b>ENVIRONMENT</b>\n\n" +
            "<b>6. Floating Island With Roots And Rocks</b>\n" +
            "Creator: Dennis\n" +
            "Source: https://skfb.ly/pFGQN\n" +
            "Licence: Creative Commons Attribution 4.0 (CC BY 4.0)\n" +
            "https://creativecommons.org/licenses/by/4.0/\n\n" +
            "<b>7. Low Poly Lava Fighting Arena/Stage</b>\n" +
            "Creator: Umar\n" +
            "Source: https://skfb.ly/pzYyM\n" +
            "Licence: Creative Commons Attribution 4.0 (CC BY 4.0)\n" +
            "https://creativecommons.org/licenses/by/4.0/\n\n" +
            "<b>8. Celestial Sanctuary map</b>\n" +
            "Concept, layout and models by Pixtel Studios, models made with Modly.\n\n" +
            "<b>ANIMATIONS</b>\n\n" +
            "<b>9. KinemIQ Animation Library</b>\n" +
            "Source: https://kinemiq.app/\n" +
            "Licence: Depends on the individual animation used.\n" +
            "Attribution: Required for animations licensed under CC BY.\n" +
            "Not required for CC0 animations.\n" +
            "This game uses the KinemIQ Free Animation Pack (CC0).";

        private GameObject BuildCredits(RectTransform root)
        {
            RectTransform panel = UIFactory.Stretch(root, "Credits");
            UIFactory.Image(UIFactory.Stretch(panel, "Shade"), new Color(0f, 0f, 0f, 0.85f));
            UIFactory.Label(panel, "CREDITS", 64, UIFactory.TextColor, TextAnchor.MiddleCenter, new Vector2(0, 450), new Vector2(800, 90), FontStyle.Bold);
            UIFactory.Label(panel, "WUSHEN  |  Developed by Pixtel Studios", 28, UIFactory.Accent, TextAnchor.MiddleCenter, new Vector2(0, 385), new Vector2(1200, 44), FontStyle.Bold);
            Text left = UIFactory.Label(panel, CharactersText, 20, UIFactory.TextColor, TextAnchor.UpperLeft, new Vector2(-440, -30), new Vector2(820, 760));
            left.supportRichText = true;
            Text right = UIFactory.Label(panel, EnvironmentText, 20, UIFactory.TextColor, TextAnchor.UpperLeft, new Vector2(440, -30), new Vector2(820, 760));
            right.supportRichText = true;
            UIFactory.Button(panel, "CLOSE", new Vector2(0, -470), new Vector2(320, 70), () => _credits.SetActive(false));
            return panel.gameObject;
        }

        public void Show(bool visible)
        {
            Root.SetActive(visible);
        }
    }
}
