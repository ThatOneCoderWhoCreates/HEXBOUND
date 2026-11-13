using System.Collections.Generic;
using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace HEXBOUND.UI
{
    /// <summary>In-match heads-up display. Everything is read from the game state each frame.</summary>
    public sealed class MatchHud
    {
        private sealed class SidePanel
        {
            public Text Name;
            public Image Health;
            public Image HealthLag;
            public Image Magic;
            public Text MagicText;
            public Image[] Pips;
            public Text Combo;
            public float LagValue = 1f;
            public float ComboTimer;
            public int LastCombo;
        }

        private sealed class AbilitySlot
        {
            public Image Background;
            public Image Cooldown;
            public Text Timer;
            public Text Cost;
            public Text Name;
        }

        private sealed class PopupText
        {
            public Text Text;
            public Vector3 World;
            public float Age;
            public float Life;
            public float Size;
            public Color Color;
        }

        public readonly GameObject Root;

        private readonly SidePanel[] _sides = new SidePanel[2];
        private readonly Text _timer;
        private readonly Text _round;
        private readonly Text _announce;
        private readonly Text _subAnnounce;
        private readonly AbilitySlot[] _slots = new AbilitySlot[6];
        private readonly Text _netInfo;
        private readonly Text _trainingInfo;
        private readonly Text _latency;
        private readonly Image _lockReticle;
        private readonly Text _buffText;
        private readonly RectTransform _rootRect;
        private readonly List<PopupText> _popups = new List<PopupText>();
        private readonly Text _hint;
        private int _builtFor = -1;
        private float _hintTimer = 8f;

        public MatchHud(Transform canvas)
        {
            _rootRect = UIFactory.Stretch(canvas, "HUD");
            Root = _rootRect.gameObject;

            _sides[0] = BuildSide(_rootRect, false);
            _sides[1] = BuildSide(_rootRect, true);

            RectTransform timerBox = UIFactory.Rect(_rootRect, "Timer Box", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(170, 120));
            UIFactory.Image(timerBox, new Color(0.05f, 0.03f, 0.09f, 0.8f));
            _timer = UIFactory.LabelOn(UIFactory.Stretch(timerBox, "Time"), "99", 72, UIFactory.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _round = UIFactory.LabelOn(UIFactory.Rect(_rootRect, "Round", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -145), new Vector2(400, 40)), "ROUND 1", 26, UIFactory.MutedText, TextAnchor.MiddleCenter, FontStyle.Bold);

            _announce = UIFactory.Label(_rootRect, string.Empty, 130, new Color(1f, 0.88f, 0.45f), TextAnchor.MiddleCenter, new Vector2(0, 120), new Vector2(1800, 180), FontStyle.Bold);
            _subAnnounce = UIFactory.Label(_rootRect, string.Empty, 44, UIFactory.TextColor, TextAnchor.MiddleCenter, new Vector2(0, 10), new Vector2(1600, 70), FontStyle.Bold);
            _buffText = UIFactory.Label(_rootRect, string.Empty, 30, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleCenter, new Vector2(0, -330), new Vector2(800, 50), FontStyle.Bold);

            string[] keys = { "Q", "E", "R", "M", "Z", "SHIFT" };
            for (int i = 0; i < _slots.Length; i++)
            {
                float x = (i - 2.5f) * 132f;
                RectTransform rt = UIFactory.Rect(_rootRect, "Slot " + keys[i], new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 34), new Vector2(118, 118));
                var slot = new AbilitySlot();
                slot.Background = UIFactory.Image(rt, new Color(0.1f, 0.06f, 0.18f, 0.9f));
                RectTransform cd = UIFactory.Stretch(rt, "Cooldown");
                slot.Cooldown = UIFactory.Image(cd, new Color(0f, 0f, 0f, 0.7f));
                slot.Cooldown.type = Image.Type.Filled;
                slot.Cooldown.fillMethod = Image.FillMethod.Vertical;
                slot.Cooldown.fillOrigin = 0;
                slot.Cooldown.fillAmount = 0f;
                UIFactory.LabelOn(UIFactory.Rect(rt, "Key", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(0, 34)), keys[i], 26, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
                slot.Timer = UIFactory.LabelOn(UIFactory.Stretch(rt, "Timer"), string.Empty, 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                slot.Cost = UIFactory.LabelOn(UIFactory.Rect(rt, "Cost", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(0, 28)), string.Empty, 20, new Color(0.55f, 0.75f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
                slot.Name = UIFactory.LabelOn(UIFactory.Rect(rt, "Name", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(40, 30)), string.Empty, 18, UIFactory.MutedText, TextAnchor.MiddleCenter);
                _slots[i] = slot;
            }

            _netInfo = UIFactory.LabelOn(UIFactory.Rect(_rootRect, "Net", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 20), new Vector2(520, 110)), string.Empty, 22, UIFactory.MutedText, TextAnchor.LowerRight);
            _latency = UIFactory.LabelOn(UIFactory.Rect(_rootRect, "Latency", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -146), new Vector2(300, 36)), string.Empty, 26, new Color(0.3f, 1f, 0.4f), TextAnchor.MiddleLeft, FontStyle.Bold);
            _trainingInfo = UIFactory.LabelOn(UIFactory.Rect(_rootRect, "Training", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 20), new Vector2(620, 140)), string.Empty, 22, UIFactory.MutedText, TextAnchor.LowerLeft);
            _hint = UIFactory.LabelOn(UIFactory.Rect(_rootRect, "Hint", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(1400, 40)), "Esc pause  |  LMB light  RMB heavy  Q E skills  R ultimate  Shift dodge  Ctrl block/parry  F grab  M recharge  Tab lock-on", 22, UIFactory.MutedText, TextAnchor.MiddleCenter);

            RectTransform reticle = UIFactory.Rect(_rootRect, "Lock Reticle", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
            _lockReticle = UIFactory.Image(reticle, new Color(1f, 0.35f, 0.3f, 0.85f));
            reticle.localRotation = Quaternion.Euler(0, 0, 45f);
            RectTransform hole = UIFactory.Stretch(reticle, "Hole");
            hole.sizeDelta = new Vector2(-14f, -14f);
            UIFactory.Image(hole, new Color(0f, 0f, 0f, 0.25f));
            _lockReticle.gameObject.SetActive(false);
        }

        private static SidePanel BuildSide(RectTransform root, bool right)
        {
            float sign = right ? -1f : 1f;
            Vector2 anchor = right ? new Vector2(1, 1) : new Vector2(0, 1);
            Vector2 pivot = right ? new Vector2(1, 1) : new Vector2(0, 1);
            var side = new SidePanel();
            side.Name = UIFactory.LabelOn(UIFactory.Rect(root, "Name", anchor, anchor, pivot, new Vector2(30 * sign, -18), new Vector2(700, 46)), "Name", 34, UIFactory.TextColor, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.Bold);
            side.HealthLag = UIFactory.Bar(root, anchor, pivot, new Vector2(30 * sign, -66), new Vector2(700, 40), new Color(1f, 0.85f, 0.85f, 0.9f), right);
            side.Health = UIFactory.Bar(root, anchor, pivot, new Vector2(30 * sign, -66), new Vector2(700, 40), new Color(0.95f, 0.25f, 0.3f), right);
            side.Health.transform.parent.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            side.Magic = UIFactory.Bar(root, anchor, pivot, new Vector2(30 * sign, -112), new Vector2(520, 22), new Color(0.35f, 0.55f, 1f), right);
            side.MagicText = UIFactory.LabelOn(UIFactory.Rect(root, "Magic", anchor, anchor, pivot, new Vector2(560 * sign, -106), new Vector2(170, 34)), "500", 24, new Color(0.6f, 0.75f, 1f), right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.Bold);
            side.Pips = new Image[GameConfig.WinsNeeded];
            for (int i = 0; i < side.Pips.Length; i++)
            {
                RectTransform pip = UIFactory.Rect(root, "Pip", anchor, anchor, pivot, new Vector2((30 + 700 - 26 - i * 34) * sign, -20), new Vector2(26, 26));
                pip.localRotation = Quaternion.Euler(0, 0, 45f);
                side.Pips[i] = UIFactory.Image(pip, new Color(0.2f, 0.18f, 0.25f));
            }
            side.Combo = UIFactory.LabelOn(UIFactory.Rect(root, "Combo", right ? new Vector2(1, 0.5f) : new Vector2(0, 0.5f), right ? new Vector2(1, 0.5f) : new Vector2(0, 0.5f), pivot, new Vector2(40 * sign, 160), new Vector2(400, 120)), string.Empty, 54, new Color(1f, 0.8f, 0.35f), right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.Bold);
            return side;
        }

        /// <summary>Shows the online round-trip latency in the top-left corner. Pass a negative value to hide it (offline).</summary>
        public void SetLatency(int milliseconds)
        {
            _latency.text = milliseconds >= 0 ? "Ping " + milliseconds + " ms" : string.Empty;
        }

        public void Show(bool visible)
        {
            Root.SetActive(visible);
            if (!visible)
            {
                foreach (PopupText p in _popups)
                {
                    if (p.Text != null) Object.Destroy(p.Text.gameObject);
                }
                _popups.Clear();
            }
        }

        public void AddPopup(Vector3 world, string text, Color color, float size)
        {
            Text t = UIFactory.Label(_rootRect, text, Mathf.RoundToInt(34 * size), color, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(600, 80), FontStyle.Bold);
            RectTransform rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            world += new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0f, 0.2f), Random.Range(-0.25f, 0.25f));
            _popups.Add(new PopupText { Text = t, World = world, Life = 0.9f, Size = size, Color = color });
        }

        public void Update(GameState s, int localIndex, Camera cam, string netInfo, string trainingInfo, float deltaTime)
        {
            if (_builtFor != s.Setup.Character0 * 10 + s.Setup.Character1 + localIndex * 100)
            {
                _builtFor = s.Setup.Character0 * 10 + s.Setup.Character1 + localIndex * 100;
                _hintTimer = 10f;
                for (int i = 0; i < 2; i++)
                {
                    CharacterDef c = GameContent.GetCharacter(s.Fighters[i].CharacterId);
                    string you = i == localIndex ? "  (YOU)" : string.Empty;
                    _sides[i].Name.text = i == 1 ? you.Trim() + "  " + c.Name.ToUpperInvariant() : c.Name.ToUpperInvariant() + you;
                    _sides[i].LagValue = 1f;
                }
            }

            bool training = s.Setup.Mode == MatchMode.Training;
            for (int i = 0; i < 2; i++)
            {
                UpdateSide(_sides[i], ref s.Fighters[i], s.GetWins(i), training, deltaTime, ref s.Fighters[1 - i]);
            }

            _timer.text = training ? "--" : Mathf.CeilToInt(s.RoundTicksLeft / (float)GameConfig.TickRate).ToString();
            _round.text = training ? "TRAINING" : "ROUND " + s.Round;
            UpdateAnnouncements(s, localIndex);
            UpdateSlots(ref s.Fighters[localIndex]);

            FighterState me = s.Fighters[localIndex];
            _buffText.text = me.Buff == BuffId.None ? string.Empty : (me.Buff == BuffId.TwinBlades ? "TWIN BLADES " : "LEVITATE ") + Mathf.CeilToInt(me.BuffTicks / 60f) + "s";

            _netInfo.text = netInfo ?? string.Empty;
            _trainingInfo.text = trainingInfo ?? string.Empty;
            _hintTimer -= deltaTime;
            _hint.gameObject.SetActive(_hintTimer > 0f);

            FighterState opp = s.Fighters[1 - localIndex];
            bool locked = me.LockOn && cam != null;
            if (locked)
            {
                CharacterDef oc = GameContent.GetCharacter(opp.CharacterId);
                Vector3 world = new Vector3(opp.Position.X.ToFloat(), opp.Position.Y.ToFloat() + oc.Height.ToFloat() * 0.6f, opp.Position.Z.ToFloat());
                Vector3 screen = cam.WorldToScreenPoint(world);
                locked = screen.z > 0f;
                if (locked) _lockReticle.rectTransform.anchoredPosition = ScreenToCanvas(screen);
            }
            _lockReticle.gameObject.SetActive(locked);

            UpdatePopups(cam, deltaTime);
        }

        private Vector2 ScreenToCanvas(Vector3 screen)
        {
            float scale = _rootRect.rect.width / Mathf.Max(1f, Screen.width);
            return new Vector2(screen.x * scale, screen.y * scale);
        }

        private void UpdateSide(SidePanel side, ref FighterState f, int wins, bool training, float dt, ref FighterState opponent)
        {
            CharacterDef c = GameContent.GetCharacter(f.CharacterId);
            float health = Mathf.Clamp01(f.Health / (float)c.MaxHealth);
            side.Health.fillAmount = health;
            if (side.LagValue < health) side.LagValue = health;
            side.LagValue = Mathf.MoveTowards(side.LagValue, health, dt * 0.35f);
            side.HealthLag.fillAmount = side.LagValue;
            side.Magic.fillAmount = f.Magic / (float)GameConfig.MaxMagic;
            side.MagicText.text = f.Magic.ToString();
            for (int i = 0; i < side.Pips.Length; i++)
            {
                side.Pips[i].color = i < wins ? new Color(1f, 0.82f, 0.3f) : new Color(0.2f, 0.18f, 0.25f);
                side.Pips[i].gameObject.SetActive(!training);
            }

            // Combo counter for this side's attacks: read from the opponent's combo count.
            int combo = opponent.ComboCount;
            if (combo >= 2)
            {
                side.Combo.text = combo + " HITS\n<size=30>" + opponent.ComboDamage + " DMG</size>";
                side.Combo.supportRichText = true;
                side.ComboTimer = 1.2f;
                side.LastCombo = combo;
            }
            else
            {
                side.ComboTimer -= dt;
                if (side.ComboTimer <= 0f) side.Combo.text = string.Empty;
            }
        }

        private void UpdateAnnouncements(GameState s, int localIndex)
        {
            string big = string.Empty;
            string small = string.Empty;
            switch (s.Phase)
            {
                case MatchPhase.Intro:
                    if (s.PhaseTicks < GameConfig.FightCallTick)
                    {
                        bool final = s.Wins0 == GameConfig.WinsNeeded - 1 && s.Wins1 == GameConfig.WinsNeeded - 1;
                        big = final ? "FINAL ROUND" : "ROUND " + s.Round;
                    }
                    else
                    {
                        big = "FIGHT!";
                    }
                    break;
                case MatchPhase.RoundOver:
                    if (s.Setup.Mode == MatchMode.Training)
                    {
                        big = "RING OUT!";
                        small = "Resetting...";
                        break;
                    }
                    switch (s.LastRoundEnd)
                    {
                        case RoundEndReason.KO: big = "K.O."; break;
                        case RoundEndReason.RingOut: big = "RING OUT!"; break;
                        case RoundEndReason.Timeout: big = "TIME!"; break;
                        case RoundEndReason.DoubleKO: big = "DOUBLE K.O."; break;
                    }
                    if (s.PhaseTicks > 60)
                    {
                        if (s.RoundWinner == 2) small = "DRAW";
                        else if (s.RoundWinner >= 0)
                        {
                            string name = GameContent.GetCharacter(s.Fighters[s.RoundWinner].CharacterId).Name.ToUpperInvariant();
                            small = (s.RoundWinner == localIndex ? "YOU WIN THE ROUND" : name + " WINS THE ROUND");
                        }
                    }
                    break;
            }
            _announce.text = big;
            _subAnnounce.text = small;
        }

        private void UpdateSlots(ref FighterState f)
        {
            CharacterDef c = GameContent.GetCharacter(f.CharacterId);
            SetSlot(_slots[0], c.Skill1, f.CooldownSkill1, f.Magic);
            SetSlot(_slots[1], c.Skill2, f.CooldownSkill2, f.Magic);
            SetSlot(_slots[2], c.Ultimate, f.CooldownUltimate, f.Magic);
            SetSlot(_slots[3], c.Recharge, f.CooldownRecharge, int.MaxValue);
            SetSlot(_slots[4], c.Heal, f.CooldownHeal, int.MaxValue);
            SetSlot(_slots[5], c.Dodge, f.CooldownDodge, int.MaxValue);
            _slots[3].Cost.text = "+30";
            _slots[4].Cost.text = "+5%";
            _slots[5].Cost.text = string.Empty;
        }

        private static void SetSlot(AbilitySlot slot, MoveDef move, int cooldown, int magic)
        {
            if (move == null) return;
            slot.Name.text = move.Name;
            slot.Cost.text = move.MagicCost > 0 ? move.MagicCost.ToString() : string.Empty;
            float fill = move.Cooldown > 0 ? Mathf.Clamp01(cooldown / (float)move.Cooldown) : 0f;
            slot.Cooldown.fillAmount = fill;
            slot.Timer.text = cooldown > 0 ? Mathf.CeilToInt(cooldown / 60f).ToString() : string.Empty;
            bool affordable = magic >= move.MagicCost;
            slot.Background.color = cooldown <= 0 && affordable ? new Color(0.24f, 0.13f, 0.42f, 0.95f) : new Color(0.08f, 0.05f, 0.12f, 0.9f);
        }

        private void UpdatePopups(Camera cam, float dt)
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                PopupText p = _popups[i];
                p.Age += dt;
                if (p.Age >= p.Life || cam == null)
                {
                    Object.Destroy(p.Text.gameObject);
                    _popups.RemoveAt(i);
                    continue;
                }
                Vector3 world = p.World + Vector3.up * (p.Age * 1.2f);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                {
                    p.Text.enabled = false;
                    continue;
                }
                p.Text.enabled = true;
                p.Text.rectTransform.anchoredPosition = ScreenToCanvas(screen);
                float t = p.Age / p.Life;
                Color c = p.Color;
                c.a = 1f - t * t;
                p.Text.color = c;
                float pop = t < 0.12f ? Mathf.Lerp(1.5f, 1f, t / 0.12f) : 1f;
                p.Text.rectTransform.localScale = Vector3.one * pop;
            }
        }
    }
}
