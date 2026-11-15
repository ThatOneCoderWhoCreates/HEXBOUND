using System;
using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace HEXBOUND.UI
{
    public enum SelectMode
    {
        Training,
        VersusBot,
        Online,
        /// <summary>Fight the learning beast: pick your fighter only.</summary>
        Beast,
        /// <summary>Arcade tower: pick your fighter, then beat everyone in a row and the beast.</summary>
        Arcade,
    }

    /// <summary>
    /// Pick fighters. A row of fighter cards on top; below it a turning 3D preview of the selected fighter
    /// with its stats and abilities. Offline modes also pick the opponent (or not, for Beast and Arcade).
    /// Online: pick yours and press Ready; the opponent's pick and ready state are shown live.
    /// </summary>
    public sealed class CharacterSelectScreen
    {
        public readonly GameObject Root;
        /// <summary>The App puts the 3D preview's render texture here.</summary>
        public readonly RawImage PreviewImage;

        /// <summary>Offline start: (your fighter, opponent fighter).</summary>
        public event Action<int, int> StartOffline;
        /// <summary>Online: (your fighter, ready).</summary>
        public event Action<int, bool> OnlinePick;
        public event Action Back;
        public event Action BotModeClicked;
        public event Action InfiniteMagicClicked;
        public event Action ResetBeastClicked;
        public event Action DifficultyClicked;
        public event Action MapClicked;
        /// <summary>Raised when the selected fighter changes (to update the preview).</summary>
        public event Action<int> PlayerChanged;

        public SelectMode Mode { get; private set; }
        public int Player { get; private set; }
        public int Opponent { get; private set; } = 1;
        public bool Ready { get; private set; }

        private readonly Text _title;
        private readonly Image[] _playerCards;
        private readonly Image[] _opponentCards;
        private readonly GameObject _opponentRow;
        private readonly Button _startButton;
        private readonly Text _status;
        private readonly Button _botModeButton;
        private readonly Button _infiniteButton;
        private readonly Button _resetBeastButton;
        private readonly Button _difficultyButton;
        private readonly Button _mapButton;
        private readonly Text _beastInfo;
        private readonly Text _detailName;
        private readonly Text _detailText;

        public CharacterSelectScreen(Transform canvas)
        {
            RectTransform root = UIFactory.Stretch(canvas, "Character Select");
            Root = root.gameObject;
            UIFactory.Image(UIFactory.Stretch(root, "Shade"), new Color(0.02f, 0.01f, 0.05f, 0.6f)).raycastTarget = false;

            _title = UIFactory.Label(root, "CHOOSE YOUR FIGHTER", 52, UIFactory.TextColor, TextAnchor.MiddleCenter, new Vector2(0, 485), new Vector2(1600, 70), FontStyle.Bold);

            CharacterDef[] chars = GameContent.Characters;
            int playable = GameContent.PlayableCount;
            _playerCards = new Image[playable];
            const float spacing = 350f;
            for (int i = 0; i < playable; i++)
            {
                int index = i;
                float x = (i - (playable - 1) * 0.5f) * spacing;
                _playerCards[i] = Card(root, chars[i], new Vector2(x, 360), new Vector2(330, 130), () => PickPlayer(index));
            }

            // Preview and details of the selected fighter.
            RectTransform previewBox = UIFactory.Centered(root, "Preview Frame", new Vector2(-470, 30), new Vector2(470, 470));
            UIFactory.Image(previewBox, new Color(0.08f, 0.05f, 0.13f, 0.9f)).raycastTarget = false;
            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            previewGo.transform.SetParent(previewBox, false);
            var prt = (RectTransform)previewGo.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(6, 6);
            prt.offsetMax = new Vector2(-6, -6);
            PreviewImage = previewGo.GetComponent<RawImage>();
            PreviewImage.raycastTarget = false;

            RectTransform detailBox = UIFactory.Centered(root, "Details", new Vector2(270, 30), new Vector2(980, 470));
            UIFactory.Image(detailBox, UIFactory.PanelColor).raycastTarget = false;
            _detailName = UIFactory.Label(detailBox, string.Empty, 44, UIFactory.TextColor, TextAnchor.UpperLeft, new Vector2(0, 190), new Vector2(920, 60), FontStyle.Bold);
            _detailName.supportRichText = true;
            _detailText = UIFactory.Label(detailBox, string.Empty, 22, UIFactory.TextColor, TextAnchor.UpperLeft, new Vector2(0, -35), new Vector2(920, 380));
            _detailText.supportRichText = true;

            _opponentRow = new GameObject("Opponent Row", typeof(RectTransform));
            _opponentRow.transform.SetParent(root, false);
            UIFactory.Label(_opponentRow.transform, "OPPONENT", 28, UIFactory.MutedText, TextAnchor.MiddleCenter, new Vector2(0, -245), new Vector2(800, 40), FontStyle.Bold);
            _opponentCards = new Image[playable];
            for (int i = 0; i < playable; i++)
            {
                int index = i;
                float x = (i - (playable - 1) * 0.5f) * 330f;
                RectTransform rt = UIFactory.Centered(_opponentRow.transform, "Opponent " + chars[i].Name, new Vector2(x, -295), new Vector2(310, 56));
                _opponentCards[i] = UIFactory.Image(rt, UIFactory.AccentDark);
                Button b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = _opponentCards[i];
                b.onClick.AddListener(() => PickOpponent(index));
                UIFactory.LabelOn(UIFactory.Stretch(rt, "Label"), chars[i].Name + "  (" + chars[i].Weapon + ")", 24, UIFactory.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            _beastInfo = UIFactory.Label(root, string.Empty, 26, new Color(0.85f, 0.65f, 1f), TextAnchor.MiddleCenter, new Vector2(0, -275), new Vector2(1700, 110));
            _beastInfo.supportRichText = true;

            _botModeButton = UIFactory.Button(root, "BOT: IDLE", new Vector2(-260, -372), new Vector2(420, 60), () => BotModeClicked?.Invoke(), 24);
            _infiniteButton = UIFactory.Button(root, "INFINITE MAGIC: OFF", new Vector2(220, -372), new Vector2(460, 60), () => InfiniteMagicClicked?.Invoke(), 24);
            _resetBeastButton = UIFactory.Button(root, "RESET BEAST", new Vector2(0, -372), new Vector2(360, 60), () => ResetBeastClicked?.Invoke(), 24);
            _difficultyButton = UIFactory.Button(root, "DIFFICULTY: NORMAL", new Vector2(0, -372), new Vector2(460, 60), () => DifficultyClicked?.Invoke(), 24);
            _mapButton = UIFactory.Button(root, "MAP: CELESTIAL SANCTUARY", new Vector2(690, -372), new Vector2(440, 60), () => MapClicked?.Invoke(), 22);

            _status = UIFactory.Label(root, string.Empty, 28, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleCenter, new Vector2(0, -300), new Vector2(1600, 50));
            UIFactory.Button(root, "BACK", new Vector2(-260, -460), new Vector2(320, 72), () => Back?.Invoke());
            _startButton = UIFactory.Button(root, "FIGHT", new Vector2(260, -460), new Vector2(320, 72), OnStart);
        }

        private Image Card(Transform parent, CharacterDef c, Vector2 pos, Vector2 size, Action onClick)
        {
            RectTransform rt = UIFactory.Centered(parent, "Card " + c.Name, pos, size);
            Image bg = UIFactory.Image(rt, UIFactory.PanelColor);
            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => onClick());

            var color = new Color(c.ColorR / 255f, c.ColorG / 255f, c.ColorB / 255f);
            UIFactory.Image(UIFactory.Rect(rt, "Stripe", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 8)), color).raycastTarget = false;
            UIFactory.Label(rt, c.Name.ToUpperInvariant(), 40, color, TextAnchor.MiddleCenter, new Vector2(0, 18), new Vector2(310, 52), FontStyle.Bold);
            UIFactory.Label(rt, c.Title + "  -  " + c.Weapon + (c.SecondaryWeapon != null ? " / " + c.SecondaryWeapon : string.Empty), 18, UIFactory.MutedText, TextAnchor.MiddleCenter, new Vector2(0, -32), new Vector2(320, 30));
            return bg;
        }

        public void Show(bool visible)
        {
            Root.SetActive(visible);
        }

        public void Open(SelectMode mode)
        {
            Mode = mode;
            Ready = false;
            bool offline = mode != SelectMode.Online;
            bool pickOpponent = mode == SelectMode.Training || mode == SelectMode.VersusBot;
            _opponentRow.SetActive(pickOpponent);
            _beastInfo.gameObject.SetActive(mode == SelectMode.Beast || mode == SelectMode.Arcade);
            _resetBeastButton.gameObject.SetActive(mode == SelectMode.Beast);
            _difficultyButton.gameObject.SetActive(mode == SelectMode.VersusBot);
            _botModeButton.gameObject.SetActive(mode == SelectMode.Training);
            _infiniteButton.gameObject.SetActive(mode == SelectMode.Training);
            switch (mode)
            {
                case SelectMode.Training: _title.text = "TRAINING - CHOOSE FIGHTERS"; break;
                case SelectMode.VersusBot: _title.text = "VERSUS BOT - CHOOSE FIGHTERS"; break;
                case SelectMode.Beast: _title.text = "FIGHT THE BEAST - CHOOSE YOUR FIGHTER"; break;
                case SelectMode.Arcade: _title.text = "ARCADE - CHOOSE YOUR FIGHTER"; break;
                default: _title.text = "ONLINE - CHOOSE YOUR FIGHTER"; break;
            }
            if (mode == SelectMode.Arcade)
            {
                SetBeastInfo("<b>ARCADE TOWER</b>\nBeat every other fighter in a row, each one sharper than the last, then face the Beast.\nLose a fight and you can retry that stage.");
            }
            UIFactory.SetButtonLabel(_startButton, offline ? "FIGHT" : "READY");
            _status.text = string.Empty;
            Refresh();
            Show(true);
            PlayerChanged?.Invoke(Player);
        }

        public void SetTrainingOptions(string botMode, bool infiniteMagic)
        {
            UIFactory.SetButtonLabel(_botModeButton, "BOT: " + botMode.ToUpperInvariant());
            UIFactory.SetButtonLabel(_infiniteButton, "INFINITE MAGIC: " + (infiniteMagic ? "ON" : "OFF"));
        }

        /// <summary>Map button label; not clickable when the other player picks the map.</summary>
        public void SetMap(string mapName, bool clickable)
        {
            UIFactory.SetButtonLabel(_mapButton, "MAP: " + mapName.ToUpperInvariant());
            _mapButton.interactable = clickable;
        }

        public void SetDifficulty(string difficulty)
        {
            UIFactory.SetButtonLabel(_difficultyButton, "DIFFICULTY: " + difficulty.ToUpperInvariant());
        }

        /// <summary>Beast and Arcade modes: info text under the preview.</summary>
        public void SetBeastInfo(string text)
        {
            _beastInfo.text = text ?? string.Empty;
        }

        /// <summary>Online: shows what the opponent picked.</summary>
        public void SetRemote(int remoteCharacter, bool remoteReady, string connectionInfo)
        {
            if (Mode != SelectMode.Online) return;
            string pick = remoteCharacter >= 0 ? GameContent.GetCharacter(remoteCharacter).Name : "...";
            _status.text = "Opponent: " + pick + (remoteReady ? "  (READY)" : "  (choosing)") + (Ready ? "     You: READY" : string.Empty) + "     " + connectionInfo;
        }

        private void PickPlayer(int index)
        {
            Player = index;
            if (Mode == SelectMode.Online)
            {
                Ready = false;
                UIFactory.SetButtonLabel(_startButton, "READY");
                OnlinePick?.Invoke(Player, false);
            }
            Refresh();
            PlayerChanged?.Invoke(Player);
        }

        private void PickOpponent(int index)
        {
            Opponent = index;
            Refresh();
        }

        private void OnStart()
        {
            if (Mode == SelectMode.Online)
            {
                Ready = !Ready;
                UIFactory.SetButtonLabel(_startButton, Ready ? "CANCEL" : "READY");
                OnlinePick?.Invoke(Player, Ready);
                return;
            }
            StartOffline?.Invoke(Player, Opponent);
        }

        private void Refresh()
        {
            for (int i = 0; i < _playerCards.Length; i++)
            {
                _playerCards[i].color = i == Player ? new Color(0.3f, 0.17f, 0.48f, 0.97f) : UIFactory.PanelColor;
            }
            for (int i = 0; i < _opponentCards.Length; i++)
            {
                _opponentCards[i].color = i == Opponent ? new Color(0.45f, 0.16f, 0.2f, 0.95f) : UIFactory.AccentDark;
            }

            CharacterDef c = GameContent.GetCharacter(Player);
            string hex = ColorUtility.ToHtmlStringRGB(new Color(c.ColorR / 255f, c.ColorG / 255f, c.ColorB / 255f));
            _detailName.text = "<color=#" + hex + ">" + c.Name.ToUpperInvariant() + "</color>  <size=24>" + c.Title + "</size>";
            float jump = c.JumpVelocity.ToFloat() * c.JumpVelocity.ToFloat() / (2f * c.Gravity.ToFloat());
            string text = c.Description + "\n\n";
            text += "<b>Health</b> " + c.MaxHealth + "    <b>Magic</b> " + c.MaxMagic + "    <b>Speed</b> " + Mathf.RoundToInt(c.MoveSpeed.ToFloat() * 600f) + "    <b>Jump</b> " + jump.ToString("0.0") + " m\n";
            text += "<b>Weapon</b> " + c.Weapon + (c.SecondaryWeapon != null ? " and " + c.SecondaryWeapon + " (mouse wheel)" : string.Empty) + "\n";
            foreach (AbilityInfo a in c.Abilities)
            {
                text += "\n<b>" + a.Key + "  " + a.Name + "</b>  " + a.Description;
            }
            _detailText.text = text;
        }
    }
}
