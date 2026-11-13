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
    }

    /// <summary>
    /// Pick fighters. Offline: pick your fighter and the opponent. Online: pick yours and press Ready;
    /// the opponent's pick and ready state are shown live.
    /// </summary>
    public sealed class CharacterSelectScreen
    {
        public readonly GameObject Root;

        /// <summary>Offline start: (your fighter, opponent fighter).</summary>
        public event Action<int, int> StartOffline;
        /// <summary>Online: (your fighter, ready).</summary>
        public event Action<int, bool> OnlinePick;
        public event Action Back;
        public event Action BotModeClicked;
        public event Action InfiniteMagicClicked;
        public event Action ResetBeastClicked;

        public SelectMode Mode { get; private set; }
        public int Player { get; private set; }
        public int Opponent { get; private set; } = 1;
        public bool Ready { get; private set; }

        private readonly Text _title;
        private readonly Text _opponentTitle;
        private readonly Image[] _playerCards;
        private readonly Image[] _opponentCards;
        private readonly GameObject _opponentRow;
        private readonly Button _startButton;
        private readonly Text _status;
        private readonly Button _botModeButton;
        private readonly Button _infiniteButton;
        private readonly Button _resetBeastButton;
        private readonly Text _beastInfo;
        private readonly Text[] _details;

        public CharacterSelectScreen(Transform canvas)
        {
            RectTransform root = UIFactory.Stretch(canvas, "Character Select");
            Root = root.gameObject;
            UIFactory.Image(UIFactory.Stretch(root, "Shade"), new Color(0.02f, 0.01f, 0.05f, 0.55f)).raycastTarget = false;

            _title = UIFactory.Label(root, "CHOOSE YOUR FIGHTER", 56, UIFactory.TextColor, TextAnchor.MiddleCenter, new Vector2(0, 470), new Vector2(1600, 80), FontStyle.Bold);

            CharacterDef[] chars = GameContent.Characters;
            int playable = GameContent.PlayableCount;
            _playerCards = new Image[playable];
            _details = new Text[playable];
            float spacing = 560f;
            for (int i = 0; i < playable; i++)
            {
                int index = i;
                float x = (i - (playable - 1) * 0.5f) * spacing;
                _playerCards[i] = Card(root, chars[i], new Vector2(x, 150), new Vector2(520, 520), () => PickPlayer(index), out _details[i]);
            }

            _opponentRow = new GameObject("Opponent Row", typeof(RectTransform));
            _opponentRow.transform.SetParent(root, false);
            _opponentTitle = UIFactory.Label(_opponentRow.transform, "OPPONENT", 34, UIFactory.MutedText, TextAnchor.MiddleCenter, new Vector2(0, -170), new Vector2(800, 50), FontStyle.Bold);
            _opponentCards = new Image[playable];
            for (int i = 0; i < playable; i++)
            {
                int index = i;
                float x = (i - (playable - 1) * 0.5f) * 380f;
                RectTransform rt = UIFactory.Centered(_opponentRow.transform, "Opponent " + chars[i].Name, new Vector2(x, -245), new Vector2(340, 80));
                _opponentCards[i] = UIFactory.Image(rt, UIFactory.AccentDark);
                Button b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = _opponentCards[i];
                b.onClick.AddListener(() => PickOpponent(index));
                UIFactory.LabelOn(UIFactory.Stretch(rt, "Label"), chars[i].Name + "  (" + chars[i].Weapon + ")", 28, UIFactory.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            _botModeButton = UIFactory.Button(root, "BOT: IDLE", new Vector2(-300, -340), new Vector2(420, 64), () => BotModeClicked?.Invoke(), 26);
            _infiniteButton = UIFactory.Button(root, "INFINITE MAGIC: OFF", new Vector2(160, -340), new Vector2(460, 64), () => InfiniteMagicClicked?.Invoke(), 26);

            _beastInfo = UIFactory.Label(root, string.Empty, 28, new Color(0.85f, 0.65f, 1f), TextAnchor.MiddleCenter, new Vector2(0, -215), new Vector2(1700, 120));
            _beastInfo.supportRichText = true;
            _resetBeastButton = UIFactory.Button(root, "RESET BEAST", new Vector2(0, -340), new Vector2(360, 64), () => ResetBeastClicked?.Invoke(), 26);

            _status = UIFactory.Label(root, string.Empty, 30, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleCenter, new Vector2(0, -330), new Vector2(1400, 50));
            UIFactory.Button(root, "BACK", new Vector2(-260, -440), new Vector2(320, 76), () => Back?.Invoke());
            _startButton = UIFactory.Button(root, "FIGHT", new Vector2(260, -440), new Vector2(320, 76), OnStart);
        }

        private Image Card(Transform parent, CharacterDef c, Vector2 pos, Vector2 size, Action onClick, out Text details)
        {
            RectTransform rt = UIFactory.Centered(parent, "Card " + c.Name, pos, size);
            Image bg = UIFactory.Image(rt, UIFactory.PanelColor);
            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => onClick());

            var color = new Color(c.ColorR / 255f, c.ColorG / 255f, c.ColorB / 255f);
            UIFactory.Image(UIFactory.Rect(rt, "Stripe", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 10)), color).raycastTarget = false;
            UIFactory.Label(rt, c.Name.ToUpperInvariant(), 52, color, TextAnchor.MiddleCenter, new Vector2(0, 205), new Vector2(480, 64), FontStyle.Bold);
            UIFactory.Label(rt, c.Title + "  -  " + c.Weapon, 26, UIFactory.MutedText, TextAnchor.MiddleCenter, new Vector2(0, 160), new Vector2(480, 40));
            UIFactory.Label(rt, c.Description, 22, UIFactory.TextColor, TextAnchor.UpperCenter, new Vector2(0, 100), new Vector2(460, 70));

            string text = "Health " + c.MaxHealth + "    Speed " + Mathf.RoundToInt(c.MoveSpeed.ToFloat() * 600f) + "\n";
            foreach (AbilityInfo a in c.Abilities)
            {
                text += "\n<b>" + a.Key + "  " + a.Name + "</b>\n" + a.Description + "\n";
            }
            details = UIFactory.Label(rt, text, 21, UIFactory.TextColor, TextAnchor.UpperLeft, new Vector2(0, -95), new Vector2(460, 320));
            details.supportRichText = true;
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
            bool beast = mode == SelectMode.Beast;
            _opponentRow.SetActive(offline && !beast);
            _beastInfo.gameObject.SetActive(beast);
            _resetBeastButton.gameObject.SetActive(beast);
            _botModeButton.gameObject.SetActive(mode == SelectMode.Training);
            _infiniteButton.gameObject.SetActive(mode == SelectMode.Training);
            _title.text = mode == SelectMode.Training ? "TRAINING - CHOOSE FIGHTERS"
                : mode == SelectMode.VersusBot ? "VERSUS BOT - CHOOSE FIGHTERS"
                : mode == SelectMode.Beast ? "FIGHT THE BEAST - CHOOSE YOUR FIGHTER"
                : "ONLINE - CHOOSE YOUR FIGHTER";
            UIFactory.SetButtonLabel(_startButton, offline ? "FIGHT" : "READY");
            _status.text = string.Empty;
            Refresh();
            Show(true);
        }

        public void SetTrainingOptions(string botMode, bool infiniteMagic)
        {
            UIFactory.SetButtonLabel(_botModeButton, "BOT: " + botMode.ToUpperInvariant());
            UIFactory.SetButtonLabel(_infiniteButton, "INFINITE MAGIC: " + (infiniteMagic ? "ON" : "OFF"));
        }

        /// <summary>Beast mode: shows the beast's level and what it has learned about you.</summary>
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
                OnlinePick?.Invoke(Player, false);
            }
            Refresh();
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
                _playerCards[i].color = i == Player ? new Color(0.28f, 0.16f, 0.45f, 0.95f) : UIFactory.PanelColor;
            }
            for (int i = 0; i < _opponentCards.Length; i++)
            {
                _opponentCards[i].color = i == Opponent ? new Color(0.45f, 0.16f, 0.2f, 0.95f) : UIFactory.AccentDark;
            }
        }
    }
}
