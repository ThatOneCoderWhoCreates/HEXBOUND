using System;
using HEXBOUND.Core;
using HEXBOUND.Input;
using HEXBOUND.Netcode;
using HEXBOUND.Presentation;
using HEXBOUND.Simulation;
using HEXBOUND.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HEXBOUND.App
{
    /// <summary>
    /// The game's composition root. Put this on one GameObject in an empty scene and press Play.
    /// It builds the camera, arena, UI and audio in code, runs offline matches directly,
    /// and runs online matches through NetMatch (rollback netcode).
    /// </summary>
    public sealed class HexboundGame : MonoBehaviour
    {
        private enum ScreenId
        {
            Menu,
            Select,
            Online,
            Match,
        }

        private const double TickSeconds = 1.0 / GameConfig.TickRate;
        private const ushort DirectPort = 7777;

        private ThirdPersonCamera _camera;
        private ArenaView _arena;
        private SfxPlayer _sfx;
        private MusicPlayer _music;
        private MatchPresenter _presenter;
        private readonly LocalInputReader _input = new LocalInputReader();

        private MainMenuScreen _menu;
        private CharacterSelectScreen _select;
        private OnlineScreen _online;
        private OverlayScreens _overlay;
        private MatchHud _hud;

        private ScreenId _screen;
        private bool _paused;
        private bool _resultsShown;
        private int _localIndex;

        // Offline match.
        private GameState _offline;
        private readonly EventBuffer _offlineEvents = new EventBuffer(512);
        private TrainingBot _bot;
        private readonly TutorialChecklist _tutorial = new TutorialChecklist();
        private BeastBrain _beast;
        private BeastMemory _beastMemory;
        private bool _beastRecorded;
        private FighterPreview _preview;

        // Arcade tower.
        private bool _arcade;
        private int[] _arcadeOrder;
        private int _arcadeStage;
        private int _arcadePlayer;
        private bool _arcadeAdvance;
        private BeastMemory _arcadeMemory;
        private MatchSetup _offlineSetup;
        private SelectMode _offlineMode;
        private BotMode _trainingBotMode = BotMode.Idle;
        private bool _infiniteMagic;
        private double _accumulator;

        // Online match.
        private UnityTransportLink _link;
        private NetMatch _net;
        private int _netMatchCounter;
        private int _onlineAttempt;
        private string _disconnectMessage = string.Empty;

        private bool IsOnline
        {
            get { return _net != null; }
        }

        private GameState CurrentState
        {
            get
            {
                if (IsOnline) return _net.Session != null ? _net.Session.State : null;
                return _offline;
            }
        }

        private void Awake()
        {
            ApplyGraphicsQuality();
            Application.targetFrameRate = 144;
            QualitySettings.vSyncCount = 1;
            Application.runInBackground = true;

            Camera existing = Camera.main;
            if (existing != null) existing.gameObject.SetActive(false);

            _camera = new ThirdPersonCamera(transform);
            _arena = new ArenaView(transform, GameSettings.Map);
            ApplyArenaCamera();
            _sfx = new SfxPlayer(transform);
            _music = new MusicPlayer(transform);
            _presenter = new MatchPresenter(transform, _sfx);
            _presenter.ShowHitboxes = GameSettings.ShowHitboxes;
            _preview = new FighterPreview(transform);

            Canvas canvas = UIFactory.CreateCanvas(transform, "UI", 10);
            UIFactory.EnsureEventSystem(transform);
            _hud = new MatchHud(canvas.transform);
            _menu = new MainMenuScreen(canvas.transform);
            _select = new CharacterSelectScreen(canvas.transform);
            _select.PreviewImage.texture = _preview.Texture;
            _online = new OnlineScreen(canvas.transform, GameSettings.LastIp);
            _overlay = new OverlayScreens(canvas.transform);
            _overlay.SetHitboxLabel(_presenter.ShowHitboxes);
            _overlay.SetSensitivityLabel(GameSettings.MouseSensitivity);

            _presenter.Effects.Popup += (pos, text, color, size) => _hud.AddPopup(pos, text, color, size);
            _presenter.Effects.Shake += amount => _camera.AddShake(amount);

            WireMenus();
            ShowMenu();
        }

        private void WireMenus()
        {
            _menu.Training += () => OpenOfflineSelect(SelectMode.Training);
            _menu.VersusBot += () => OpenOfflineSelect(SelectMode.VersusBot);
            _menu.Beast += () => OpenOfflineSelect(SelectMode.Beast);
            _menu.Arcade += () => OpenOfflineSelect(SelectMode.Arcade);
            _select.PlayerChanged += id => _preview.Show(id);
            _select.MapClicked += () =>
            {
                Click();
                GameSettings.Map = (GameSettings.Map + 1) % Arena.MapCount;
                if (_net != null && _net.IsHost) _net.Map = GameSettings.Map;
                RefreshMapButton();
            };
            _select.DifficultyClicked += () =>
            {
                Click();
                GameSettings.BotDifficulty = (GameSettings.BotDifficulty + 1) % 3;
                _select.SetDifficulty(DifficultyName(GameSettings.BotDifficulty));
            };
            _menu.Online += OpenOnline;
            _menu.Controls += () =>
            {
                Click();
                _overlay.Controls.SetActive(true);
            };
            _menu.Quit += Quit;

            _select.Back += () =>
            {
                _sfx.Play(Sfx.UiBack, 1f, 0f);
                if (_select.Mode == SelectMode.Online) LeaveOnline("You left the lobby.");
                else ShowMenu();
            };
            _select.StartOffline += StartOffline;
            _select.OnlinePick += (character, ready) =>
            {
                Click();
                if (_net != null) _net.SetCharacter(character, ready);
            };
            _select.BotModeClicked += () =>
            {
                Click();
                _trainingBotMode = (BotMode)(((int)_trainingBotMode + 1) % 3);
                _select.SetTrainingOptions(_trainingBotMode.ToString(), _infiniteMagic);
            };
            _select.ResetBeastClicked += () =>
            {
                Click();
                _beastMemory = new BeastMemory();
                GameSettings.BeastMemory = _beastMemory.Serialize();
                _select.SetBeastInfo(BeastSummary());
            };
            _select.InfiniteMagicClicked += () =>
            {
                Click();
                _infiniteMagic = !_infiniteMagic;
                _select.SetTrainingOptions(_trainingBotMode.ToString(), _infiniteMagic);
            };

            _online.HostRoom += HostRoom;
            _online.JoinRoom += JoinRoom;
            _online.HostDirect += HostDirect;
            _online.JoinDirect += JoinDirect;
            _online.Cancel += () =>
            {
                _sfx.Play(Sfx.UiBack, 1f, 0f);
                CloseOnline();
                ShowMenu();
            };

            _overlay.Resume += () => SetPaused(false);
            _overlay.ShowControls += () =>
            {
                Click();
                _overlay.Controls.SetActive(true);
            };
            _overlay.CloseControls += () =>
            {
                Click();
                _overlay.Controls.SetActive(false);
            };
            _overlay.ToggleHitboxes += ToggleHitboxes;
            _overlay.SensitivityChanged += delta =>
            {
                GameSettings.MouseSensitivity = GameSettings.MouseSensitivity + delta;
                _overlay.SetSensitivityLabel(GameSettings.MouseSensitivity);
            };
            _overlay.LeaveMatch += () =>
            {
                SetPaused(false);
                if (IsOnline) LeaveOnline(string.Empty);
                else EndOfflineToMenu();
            };
            _overlay.Rematch += () =>
            {
                Click();
                _overlay.Results.SetActive(false);
                if (IsOnline)
                {
                    _net.ReturnToSelect();
                }
                else if (_arcade)
                {
                    if (_arcadeStage < 0) StartArcade(_arcadePlayer);
                    else
                    {
                        if (_arcadeAdvance) _arcadeStage++;
                        StartArcadeStage();
                    }
                }
                else
                {
                    StartOffline(_offlineSetup.Character0, _offlineSetup.Character1);
                }
            };
            _overlay.ToMenu += () =>
            {
                Click();
                _overlay.Results.SetActive(false);
                if (IsOnline) LeaveOnline(string.Empty);
                else EndOfflineToMenu();
            };
        }

        private void Click()
        {
            _sfx.Play(Sfx.UiClick, 1f, 0f);
        }

        // ---------------------------------------------------------------- Screens

        private static string MapName(int map)
        {
            return map == Arena.MapSanctuary ? "Celestial Sanctuary" : map == Arena.MapRuins ? "Sky Ruins" : "Lava Pit";
        }

        /// <summary>Map button on the select screen. Online, only the host chooses.</summary>
        private void RefreshMapButton()
        {
            bool client = _select.Mode == SelectMode.Online && _net != null && !_net.IsHost;
            _select.SetMap(client ? "HOST PICKS" : MapName(GameSettings.Map), !client);
        }

        /// <summary>
        /// Smoother edges (4x MSAA) and shadows far enough for the long Sky Ruins courtyard. Only in built games: in the
        /// editor a change to the pipeline asset would be saved into the project.
        /// </summary>
        private static void ApplyGraphicsQuality()
        {
#if !UNITY_EDITOR
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp == null) return;
            urp.msaaSampleCount = 4;
            urp.shadowDistance = 70f;
#endif
        }

        /// <summary>Camera background for the current arena: its skybox, or a plain color.</summary>
        private void ApplyArenaCamera()
        {
            if (_camera == null || _arena == null) return;
            _camera.Camera.clearFlags = _arena.UsesSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            _camera.Camera.backgroundColor = _arena.BackgroundColor;
        }

        private static string DifficultyName(int difficulty)
        {
            return difficulty == 0 ? "Easy" : difficulty == 2 ? "Hard" : "Normal";
        }

        private void HideAll()
        {
            if (_preview != null) _preview.Hide();
            _menu.Show(false);
            _select.Show(false);
            _online.Show(false);
            _hud.Show(false);
            _overlay.Pause.SetActive(false);
            _overlay.Results.SetActive(false);
        }

        private void ShowMenu()
        {
            HideAll();
            _presenter.Clear();
            _offline = null;
            _screen = ScreenId.Menu;
            _menu.Show(true);
        }

        private void OpenOfflineSelect(SelectMode mode)
        {
            Click();
            HideAll();
            _screen = ScreenId.Select;
            _select.Open(mode);
            RefreshMapButton();
            _select.SetTrainingOptions(_trainingBotMode.ToString(), _infiniteMagic);
            _select.SetDifficulty(DifficultyName(GameSettings.BotDifficulty));
            if (mode == SelectMode.Beast)
            {
                LoadBeast();
                _select.SetBeastInfo(BeastSummary());
            }
        }

        // ---------------------------------------------------------------- Beast

        private void LoadBeast()
        {
            if (_beastMemory == null) _beastMemory = BeastMemory.Deserialize(GameSettings.BeastMemory);
        }

        private void SaveBeast()
        {
            if (_beastMemory != null) GameSettings.BeastMemory = _beastMemory.Serialize();
        }

        private string BeastSummary()
        {
            LoadBeast();
            BeastMemory m = _beastMemory;
            string text = "<b>THE BEAST  -  LEVEL " + m.Level + " / " + BeastMemory.MaxLevel + "</b>";
            if (m.Matches == 0)
            {
                text += "\nIt has never fought you. It learns your habits as you fight, and gets harder every time you beat it.";
            }
            else
            {
                text += "\nRecord: you " + m.PlayerWins + " - " + m.BeastWins + " beast.  It has studied about " + Mathf.RoundToInt(m.Observations) + " of your moves.";
                PlayerAct fav = m.FavoriteAct();
                if (fav != PlayerAct.None) text += "\nIt expects your favorite: " + ActName(fav) + ".";
            }
            return text;
        }

        private static string ActName(PlayerAct act)
        {
            switch (act)
            {
                case PlayerAct.Light: return "light attacks";
                case PlayerAct.Heavy: return "heavy attacks";
                case PlayerAct.Skill1: return "your Q skill";
                case PlayerAct.Skill2: return "your E skill";
                case PlayerAct.Ultimate: return "your ultimate";
                case PlayerAct.Grab: return "grabs";
                case PlayerAct.Dodge: return "dodging";
                case PlayerAct.Jump: return "jumping";
                case PlayerAct.Block: return "blocking";
                case PlayerAct.CrouchAttack: return "low sweeps";
                case PlayerAct.CrouchGuard: return "crouching";
                case PlayerAct.AirAttack: return "air attacks";
                case PlayerAct.Heal: return "healing";
                case PlayerAct.Recharge: return "recharging magic";
                default: return act.ToString();
            }
        }

        /// <summary>Saves what the beast learned. A finished match also counts toward its level.</summary>
        private void RecordBeast(GameState s, bool finished)
        {
            if (_arcade || _beast == null || _beastMemory == null || s == null || s.Setup.Mode != MatchMode.Beast || _beastRecorded) return;
            if (finished)
            {
                _beastMemory.RecordMatch(s.MatchWinner == _localIndex);
                _beastRecorded = true;
            }
            SaveBeast();
        }

        private void OpenOnline()
        {
            Click();
            HideAll();
            _screen = ScreenId.Online;
            _online.Reset();
            if (!string.IsNullOrEmpty(_disconnectMessage)) _online.SetStatus(_disconnectMessage);
            _disconnectMessage = string.Empty;
            _online.Show(true);
        }

        // ---------------------------------------------------------------- Offline

        private void StartOffline(int player, int opponent)
        {
            Click();
            _offlineMode = _select.Mode;
            if (_offlineMode == SelectMode.Arcade)
            {
                StartArcade(player);
                return;
            }
            _arcade = false;
            bool training = _offlineMode == SelectMode.Training;
            bool beast = _offlineMode == SelectMode.Beast;
            if (training) _tutorial.Reset();
            if (beast)
            {
                LoadBeast();
                opponent = GameContent.BeastId;
                _beastMemory.Fade(0.85f);
            }
            _offlineSetup = new MatchSetup
            {
                Character0 = player,
                Character1 = opponent,
                Seed = (uint)Environment.TickCount | 1u,
                Mode = training ? MatchMode.Training : (beast ? MatchMode.Beast : MatchMode.Versus),
                InfiniteMagic = training && _infiniteMagic,
                BeastLevel = beast ? _beastMemory.Level : 0,
                Map = GameSettings.Map,
            };
            _beast = beast ? new BeastBrain(_beastMemory, _offlineSetup.Seed * 17u + 3u) : null;
            _beastRecorded = false;
            if (_offlineMode == SelectMode.VersusBot && GameSettings.BotDifficulty > 0)
            {
                // Normal and Hard use the learning brain (it learns only during this match; nothing is saved).
                var memory = new BeastMemory { Level = GameSettings.BotDifficulty == 2 ? 9 : 3 };
                _beast = new BeastBrain(memory, _offlineSetup.Seed * 23u + 5u);
            }
            _offline = new GameState();
            _offlineEvents.Clear();
            GameSimulation.Initialize(_offline, _offlineSetup, _offlineEvents);
            _bot = new TrainingBot(_offlineSetup.Seed * 31u + 7u)
            {
                Mode = training ? _trainingBotMode : BotMode.Fight,
                ReactionTicks = _offlineMode == SelectMode.VersusBot ? 11 : 9,
            };
            _localIndex = 0;
            _accumulator = 0;
            BeginMatchView(_offline);
        }

        // ---------------------------------------------------------------- Arcade

        /// <summary>Starts an arcade run: every other fighter in a shuffled order, then the beast.</summary>
        private void StartArcade(int player)
        {
            _arcade = true;
            _arcadePlayer = player;
            _arcadeStage = 0;
            _arcadeMemory = new BeastMemory();
            var order = new System.Collections.Generic.List<int>();
            for (int i = 0; i < GameContent.PlayableCount; i++)
            {
                if (i != player) order.Add(i);
            }
            uint rng = (uint)Environment.TickCount | 1u;
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = DeterministicRandom.Range(ref rng, 0, i + 1);
                int t = order[i];
                order[i] = order[j];
                order[j] = t;
            }
            order.Add(GameContent.BeastId);
            _arcadeOrder = order.ToArray();
            StartArcadeStage();
        }

        /// <summary>One arcade fight. The opponent's brain keeps learning across the whole run and gets sharper each stage.</summary>
        private void StartArcadeStage()
        {
            int opponent = _arcadeOrder[_arcadeStage];
            bool final = _arcadeStage == _arcadeOrder.Length - 1;
            _arcadeMemory.Level = Mathf.Min(BeastMemory.MaxLevel, 1 + _arcadeStage * 2);
            _offlineSetup = new MatchSetup
            {
                Character0 = _arcadePlayer,
                Character1 = opponent,
                Seed = (uint)Environment.TickCount | 1u,
                Mode = final ? MatchMode.Beast : MatchMode.Versus,
                BeastLevel = final ? _arcadeMemory.Level : 0,
                Map = GameSettings.Map,
            };
            _offline = new GameState();
            _offlineEvents.Clear();
            GameSimulation.Initialize(_offline, _offlineSetup, _offlineEvents);
            _beast = new BeastBrain(_arcadeMemory, _offlineSetup.Seed * 29u + 7u);
            _bot = new TrainingBot(_offlineSetup.Seed * 31u + 7u) { Mode = BotMode.Fight };
            _beastRecorded = false;
            _localIndex = 0;
            _accumulator = 0;
            BeginMatchView(_offline);
        }

        private string ArcadeLabel()
        {
            if (!_arcade || _arcadeOrder == null) return string.Empty;
            CharacterDef opp = GameContent.GetCharacter(_arcadeOrder[_arcadeStage]);
            bool final = _arcadeStage == _arcadeOrder.Length - 1;
            return "ARCADE  Stage " + (_arcadeStage + 1) + " / " + _arcadeOrder.Length + "  -  " + (final ? "FINAL: " : "vs ") + opp.Name;
        }

        private void RestartTraining()
        {
            if (_offline == null) return;
            _offlineEvents.Clear();
            GameSimulation.Initialize(_offline, _offlineSetup, _offlineEvents);
        }

        private void EndOfflineToMenu()
        {
            RecordBeast(_offline, false);
            _arcade = false;
            _beast = null;
            _offline = null;
            ShowMenu();
        }

        private void BeginMatchView(GameState state)
        {
            HideAll();
            _screen = ScreenId.Match;
            _paused = false;
            _resultsShown = false;
            _presenter.Build(state, _localIndex);
            _hud.Show(true);
            _camera.SnapBehind(state.Fighters[_localIndex].Facing * 360f / FixMath.FullTurn);
        }

        private void UpdateOffline(double dt)
        {
            if (_offline == null || _paused) return;
            _accumulator += dt;
            if (_accumulator > TickSeconds * 8) _accumulator = TickSeconds * 8;
            while (_accumulator >= TickSeconds)
            {
                InputFrame local = SampleLocal();
                InputFrame bot = _beast != null ? _beast.Think(_offline, 1) : _bot.Think(_offline, 1);
                GameSimulation.Tick(_offline, local, bot, _offlineEvents);
                _accumulator -= TickSeconds;
            }
        }

        /// <summary>Crosshair for an armed gunner (where the shot will go) and the "pick up weapon" hint.</summary>
        private void UpdateAimHud(GameState state)
        {
            FighterView me = _presenter.Fighters[_localIndex];
            if (me == null || state == null)
            {
                _hud.SetCrosshair(false, Vector2.zero, false);
                _hud.SetPrompt(string.Empty);
                return;
            }
            ref FighterState f = ref state.Fighters[_localIndex];
            CharacterDef c = GameContent.GetCharacter(f.CharacterId);
            bool aiming = c.Stance == FighterStance.Gunner && FighterLogic.IsArmed(ref f, c) && f.Action != FighterAction.RingOut && f.Action != FighterAction.KO;
            if (aiming && !_paused && !_resultsShown)
            {
                float yaw, pitch;
                LocalInputReader.AimFromCamera(_camera.Yaw, _camera.Pitch, out yaw, out pitch);
                Vector3 dir = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                Vector3 muzzle = me.Position + Vector3.up * 1.1f;
                FighterView opp = _presenter.Fighters[1 - _localIndex];
                bool onTarget = false;
                float distance = 20f;
                if (opp != null)
                {
                    Vector3 to = opp.Chest - muzzle;
                    if (to.magnitude <= GameConfig.SoftTargetRange.ToFloat() * 4f && Vector3.Angle(dir, to) <= 7f)
                    {
                        onTarget = true;
                        dir = to.normalized;
                        distance = to.magnitude;
                    }
                }
                Vector3 screen = _camera.Camera.WorldToScreenPoint(muzzle + dir * distance);
                float reload = f.WeaponMode == 0 ? Mathf.Clamp01(f.CooldownShot / (float)LauncherFighter.RocketReloadTicks) : 0f;
                _hud.SetCrosshair(screen.z > 0f, new Vector2(screen.x, screen.y), onTarget, reload);
            }
            else
            {
                _hud.SetCrosshair(false, Vector2.zero, false);
            }
            _hud.SetPrompt(string.Empty);
        }

        private InputFrame SampleLocal()
        {
            return _input.Sample(_camera.Yaw, _camera.Pitch);
        }

        // ---------------------------------------------------------------- Online

        private async void HostRoom()
        {
            Click();
            CloseOnline();
            int attempt = ++_onlineAttempt;
            _online.SetBusy(true);
            _online.SetStatus("Signing in and creating a room...");
            try
            {
                OnlineServices.HostResult result = await OnlineServices.CreateRoomAsync();
                if (attempt != _onlineAttempt || _screen != ScreenId.Online) return;
                _link = UnityTransportLink.HostRelay(result.RelayData);
                _net = new NetMatch(_link, true, GameSettings.InputDelay, (uint)Environment.TickCount) { Map = GameSettings.Map };
                _online.SetRoomCode(result.JoinCode);
                _online.SetStatus("Share this code with your opponent. Waiting for them to join...");
            }
            catch (Exception e)
            {
                if (attempt != _onlineAttempt) return;
                Debug.LogWarning("HEXBOUND: create room failed: " + e);
                _online.SetBusy(false);
                _online.SetStatus(OnlineServices.Explain(e));
            }
        }

        private async void JoinRoom(string code)
        {
            Click();
            if (string.IsNullOrWhiteSpace(code))
            {
                _online.SetStatus("Enter the room code first.");
                return;
            }
            CloseOnline();
            int attempt = ++_onlineAttempt;
            _online.SetBusy(true);
            _online.SetStatus("Joining room " + code.Trim().ToUpperInvariant() + "...");
            try
            {
                var relay = await OnlineServices.JoinRoomAsync(code);
                if (attempt != _onlineAttempt || _screen != ScreenId.Online) return;
                _link = UnityTransportLink.JoinRelay(relay);
                _net = new NetMatch(_link, false, GameSettings.InputDelay);
                _online.SetStatus("Connecting...");
            }
            catch (Exception e)
            {
                if (attempt != _onlineAttempt) return;
                Debug.LogWarning("HEXBOUND: join room failed: " + e);
                _online.SetBusy(false);
                _online.SetStatus(OnlineServices.Explain(e));
            }
        }

        private void HostDirect()
        {
            Click();
            CloseOnline();
            _onlineAttempt++;
            _link = UnityTransportLink.HostDirect(DirectPort);
            _net = new NetMatch(_link, true, GameSettings.InputDelay, (uint)Environment.TickCount) { Map = GameSettings.Map };
            _online.SetBusy(_link.Status != TransportStatus.Failed);
            _online.SetStatus(_link.StatusMessage + "  Your opponent joins with your IP address.");
        }

        private void JoinDirect(string ip)
        {
            Click();
            CloseOnline();
            _onlineAttempt++;
            string address = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
            GameSettings.LastIp = address;
            _link = UnityTransportLink.JoinDirect(address, DirectPort);
            _net = new NetMatch(_link, false, GameSettings.InputDelay);
            _online.SetBusy(_link.Status != TransportStatus.Failed);
            _online.SetStatus(_link.StatusMessage);
        }

        /// <summary>Closes any connection without changing screens.</summary>
        private void CloseOnline()
        {
            if (_net != null && _net.State != NetMatchState.Disconnected) _net.Leave();
            _net = null;
            if (_link != null) _link.Dispose();
            _link = null;
            _netMatchCounter = 0;
        }

        private void LeaveOnline(string message)
        {
            CloseOnline();
            _presenter.Clear();
            _disconnectMessage = message;
            if (string.IsNullOrEmpty(message)) ShowMenu();
            else OpenOnline();
        }

        private void UpdateOnline(double dt)
        {
            if (_net == null) return;
            _net.Update(dt, SampleLocal);

            if (_net.State == NetMatchState.Disconnected)
            {
                string msg = _net.Message;
                LeaveOnline(string.IsNullOrEmpty(msg) ? "Disconnected." : msg);
                return;
            }

            if (_net.State == NetMatchState.CharacterSelect && (_screen != ScreenId.Select || _select.Mode != SelectMode.Online))
            {
                HideAll();
                _presenter.Clear();
                _screen = ScreenId.Select;
                _select.Open(SelectMode.Online);
                RefreshMapButton();
                _net.SetCharacter(_select.Player, false);
            }

            if (_screen == ScreenId.Select && _select.Mode == SelectMode.Online)
            {
                _select.SetRemote(_net.RemoteCharacter, _net.RemoteReady, (_net.IsHost ? "You are host" : "Connected") + "  |  Ping " + Mathf.RoundToInt((float)_net.RttMs) + " ms");
            }

            if (_net.State == NetMatchState.InMatch && _net.MatchCounter != _netMatchCounter)
            {
                _netMatchCounter = _net.MatchCounter;
                _localIndex = _net.LocalIndex;
                BeginMatchView(_net.Session.State);
            }

            if (_screen == ScreenId.Online && _link != null && (_link.Status == TransportStatus.Failed))
            {
                _online.SetBusy(false);
                _online.SetStatus(_link.StatusMessage);
                CloseOnline();
            }
        }

        // ---------------------------------------------------------------- Frame loop

        private void Update()
        {
            double dt = Math.Min(Time.unscaledDeltaTime, 0.1);
            bool inMatch = _screen == ScreenId.Match;

            _input.GameplayEnabled = inMatch && !_paused && !_resultsShown;
            _input.Update();
            HandleHotkeys(inMatch);

            if (IsOnline) UpdateOnline(dt);
            else if (inMatch) UpdateOffline(dt);

            if (_select.Root.activeSelf) _preview.Update((float)dt);
            GameState shownState = CurrentState;
            int map = _screen == ScreenId.Match && shownState != null ? shownState.Setup.Map : (IsOnline && _net != null && !_net.IsHost ? _arena.Map : GameSettings.Map);
            if (_arena.SetMap(map)) ApplyArenaCamera();
            _music.SetMap(map);
            _arena.SyncPillars(_screen == ScreenId.Match ? shownState : null);
            _arena.Update((float)dt);
            _music.SetMood(_screen == ScreenId.Match ? MusicMood.Match : (_screen == ScreenId.Menu ? MusicMood.Menu : MusicMood.Lobby));
            _music.Update((float)dt);

            GameState state = CurrentState;
            if (_screen == ScreenId.Match && state != null)
            {
                EventBuffer events = IsOnline ? _net.Session.Events : _offlineEvents;
                float alpha = IsOnline ? _net.Interpolation : (float)(_accumulator / TickSeconds);
                _presenter.Render(state, events, Mathf.Clamp01(alpha), (float)dt);
                events.Clear();
                _hud.Update(state, _localIndex, _camera.Camera, NetInfo(), TrainingInfo(), (float)dt);
                bool tutorial = !IsOnline && _offline != null && _offline.Setup.Mode == MatchMode.Training;
                if (tutorial) _tutorial.Observe(state, _localIndex);
                _hud.SetTutorial(tutorial ? _tutorial.Text() : string.Empty);
                _hud.SetLatency(IsOnline && _net != null ? Mathf.RoundToInt((float)_net.RttMs) : -1);
                UpdateAimHud(state);

                if (state.Phase == MatchPhase.MatchOver && state.PhaseTicks > 90 && !_resultsShown) ShowResults(state);
            }

            bool lockCursor = _screen == ScreenId.Match && !_paused && !_resultsShown && !_overlay.Controls.activeSelf;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            GameState state = CurrentState;
            if (_screen == ScreenId.Match && state != null && _presenter.HasMatch)
            {
                if (state.Phase == MatchPhase.Intro && state.PhaseTicks <= 2)
                {
                    _camera.SnapBehind(state.Fighters[_localIndex].Facing * 360f / FixMath.FullTurn);
                }
                FighterView me = _presenter.Fighters[_localIndex];
                FighterView opp = _presenter.Fighters[1 - _localIndex];
                Vector2 look = (_paused || _resultsShown) ? Vector2.zero : _input.ReadLook(GameSettings.MouseSensitivity, dt);
                _camera.UpdateFollow(me.Position, opp.Position, state.Fighters[_localIndex].LockOn, look, GameSettings.InvertY, dt);
            }
            else
            {
                _camera.UpdateMenu(dt);
            }
        }

        private void HandleHotkeys(bool inMatch)
        {
            if (LocalInputReader.PausePressed())
            {
                if (_overlay.Controls.activeSelf) _overlay.Controls.SetActive(false);
                else if (inMatch && !_resultsShown) SetPaused(!_paused);
            }
            if (!inMatch) return;
            if (LocalInputReader.KeyPressed(Key.F1)) ToggleHitboxes();
            bool training = !IsOnline && _offline != null && _offline.Setup.Mode == MatchMode.Training;
            if (training && LocalInputReader.KeyPressed(Key.F2))
            {
                _trainingBotMode = (BotMode)(((int)_trainingBotMode + 1) % 3);
                _bot.Mode = _trainingBotMode;
                Click();
            }
            if (training && LocalInputReader.KeyPressed(Key.F3)) _tutorial.Visible = !_tutorial.Visible;
            if (training && LocalInputReader.KeyPressed(Key.F4))
            {
                RestartTraining();
                Click();
            }
        }

        private void ToggleHitboxes()
        {
            _presenter.ShowHitboxes = !_presenter.ShowHitboxes;
            GameSettings.ShowHitboxes = _presenter.ShowHitboxes;
            _overlay.SetHitboxLabel(_presenter.ShowHitboxes);
        }

        private void SetPaused(bool paused)
        {
            _paused = paused;
            _overlay.Pause.SetActive(paused);
            if (!paused) _overlay.Controls.SetActive(false);
            Click();
        }

        private void ShowResults(GameState s)
        {
            _resultsShown = true;
            string title;
            Color color;
            if (s.MatchWinner == 2)
            {
                title = "DRAW";
                color = new Color(0.8f, 0.8f, 0.9f);
            }
            else if (s.MatchWinner == _localIndex)
            {
                title = "VICTORY";
                color = new Color(1f, 0.85f, 0.4f);
            }
            else
            {
                title = "DEFEAT";
                color = new Color(1f, 0.4f, 0.4f);
            }
            CharacterDef c0 = GameContent.GetCharacter(s.Fighters[0].CharacterId);
            CharacterDef c1 = GameContent.GetCharacter(s.Fighters[1].CharacterId);
            string detail = c0.Name + "  " + s.Wins0 + " - " + s.Wins1 + "  " + c1.Name;
            if (!IsOnline && _arcade)
            {
                bool won = s.MatchWinner == _localIndex;
                bool final = _arcadeStage == _arcadeOrder.Length - 1;
                _arcadeAdvance = won;
                if (won && final)
                {
                    title = "ARCADE CLEAR";
                    detail += "\nYou beat every fighter and the Beast!";
                    _arcadeStage = -1;
                    _overlay.ShowResults(title, color, detail, "PLAY AGAIN");
                    return;
                }
                if (won)
                {
                    title = "STAGE " + (_arcadeStage + 1) + " CLEAR";
                    detail += "\nNext: " + GameContent.GetCharacter(_arcadeOrder[_arcadeStage + 1]).Name + (_arcadeStage + 1 == _arcadeOrder.Length - 1 ? " (final)" : string.Empty);
                    _overlay.ShowResults(title, color, detail, "NEXT FIGHT");
                    return;
                }
                detail += "\nStage " + (_arcadeStage + 1) + " of " + _arcadeOrder.Length + ". Your opponents remember how you fight.";
                _overlay.ShowResults(title, color, detail, "RETRY STAGE");
                return;
            }
            if (!IsOnline && s.Setup.Mode == MatchMode.Beast)
            {
                int before = _beastMemory != null ? _beastMemory.Level : 0;
                RecordBeast(s, true);
                if (_beastMemory != null)
                {
                    detail += _beastMemory.Level > before ? "\nThe beast learned from you. It is now level " + _beastMemory.Level + "."
                        : "\nThe beast stays at level " + _beastMemory.Level + ". It keeps learning your habits.";
                }
            }
            _overlay.ShowResults(title, color, detail, IsOnline ? "CHARACTER SELECT" : "REMATCH");
        }

        private string NetInfo()
        {
            if (!IsOnline || _net.Session == null) return string.Empty;
            RollbackSession s = _net.Session;
            string text = "Ping " + Mathf.RoundToInt((float)_net.RttMs) + " ms   Delay " + s.InputDelay + "   Rollback " + s.LastRollbackLength + "f";
            if (s.Stalled) text += "\nWaiting for opponent...";
            if (s.Desynced) text += "\n<color=#ff6060>DESYNC at tick " + s.DesyncTick + "</color>";
            return text;
        }

        private string TrainingInfo()
        {
            if (!IsOnline && _offline != null && _arcade) return ArcadeLabel();
            if (!IsOnline && _offline != null && _offline.Setup.Mode == MatchMode.Beast && _beastMemory != null)
            {
                return "THE BEAST  Level " + _beastMemory.Level + "\nLearning from your moves...";
            }
            if (IsOnline || _offline == null || _offline.Setup.Mode != MatchMode.Training) return string.Empty;
            return "TRAINING   Bot: " + _trainingBotMode + " (F2)\nInfinite magic: " + (_offline.Setup.InfiniteMagic ? "ON" : "OFF") + "   F1 hitboxes   F3 checklist   F4 reset";
        }

        private void Quit()
        {
            Click();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            CloseOnline();
        }

        private void OnApplicationQuit()
        {
            RecordBeast(_offline, false);
            CloseOnline();
        }
    }
}
