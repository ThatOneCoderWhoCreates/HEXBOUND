using System;
using HEXBOUND.Simulation;

namespace HEXBOUND.Netcode
{
    public enum NetMatchState
    {
        WaitingForConnection,
        Handshaking,
        CharacterSelect,
        InMatch,
        Disconnected,
    }

    /// <summary>
    /// Everything that happens over one connection: version handshake, character select,
    /// match start, the rollback session itself, pings, and rematches.
    /// The host is always player 0 and decides the random seed.
    /// </summary>
    public sealed class NetMatch
    {
        public const double TickSeconds = 1.0 / GameConfig.TickRate;
        private const double PingInterval = 0.5;
        private const double TimeoutSeconds = 8.0;
        private const double HelloInterval = 0.5;

        private readonly INetTransport _transport;
        private readonly PacketWriter _writer = new PacketWriter(256);
        private readonly int _inputDelay;

        public readonly bool IsHost;

        public NetMatchState State { get; private set; } = NetMatchState.WaitingForConnection;
        public string Message { get; private set; } = string.Empty;

        public int LocalIndex
        {
            get { return IsHost ? 0 : 1; }
        }

        public int LocalCharacter { get; private set; }
        public bool LocalReady { get; private set; }
        public int RemoteCharacter { get; private set; } = -1;
        public bool RemoteReady { get; private set; }

        public RollbackSession Session { get; private set; }

        /// <summary>Smoothed round-trip time in milliseconds.</summary>
        public double RttMs { get; private set; } = 60;

        /// <summary>Incremented every time a new match starts (so the app can rebuild views).</summary>
        public int MatchCounter { get; private set; }

        private double _clock;
        private double _accumulator;
        private double _lastReceive;
        private double _nextPing;
        private double _nextHello;
        private bool _helloReceived;
        private uint _seedCounter;

        public NetMatch(INetTransport transport, bool isHost, int inputDelay = 2, uint seedBase = 1)
        {
            _transport = transport;
            IsHost = isHost;
            _inputDelay = inputDelay;
            _seedCounter = seedBase;
        }

        public void SetCharacter(int characterId, bool ready)
        {
            if (State != NetMatchState.CharacterSelect) return;
            LocalCharacter = characterId;
            LocalReady = ready;
            PacketWriter w = _writer.Begin(Protocol.CharacterSelect);
            w.WriteByte((byte)characterId);
            w.WriteByte((byte)(ready ? 1 : 0));
            _transport.SendReliable(w.Buffer, w.Length);
            TryStartAsHost();
        }

        /// <summary>After a match ends, both players go back to character select.</summary>
        public void ReturnToSelect()
        {
            if (State != NetMatchState.InMatch) return;
            EnterSelect();
            PacketWriter w = _writer.Begin(Protocol.BackToSelect);
            _transport.SendReliable(w.Buffer, w.Length);
        }

        public void Leave()
        {
            if (State == NetMatchState.Disconnected) return;
            PacketWriter w = _writer.Begin(Protocol.Leave);
            _transport.SendReliable(w.Buffer, w.Length);
            _transport.Update();
            _transport.Disconnect();
            SetDisconnected("You left the match.");
        }

        /// <summary>Call every frame with the real elapsed time. sampleInput is called once per simulated tick.</summary>
        public void Update(double deltaSeconds, Func<InputFrame> sampleInput)
        {
            _clock += deltaSeconds;
            _transport.Update();

            if (State == NetMatchState.Disconnected) return;

            if (_transport.Status == TransportStatus.Failed || _transport.Status == TransportStatus.Disconnected)
            {
                SetDisconnected(string.IsNullOrEmpty(_transport.StatusMessage) ? "Connection lost." : _transport.StatusMessage);
                return;
            }

            if (_transport.Status != TransportStatus.Connected)
            {
                _lastReceive = _clock;
                return;
            }

            if (State == NetMatchState.WaitingForConnection)
            {
                State = NetMatchState.Handshaking;
                _lastReceive = _clock;
            }

            Receive();
            if (State == NetMatchState.Disconnected) return;

            if (State == NetMatchState.Handshaking && _clock >= _nextHello)
            {
                _nextHello = _clock + HelloInterval;
                PacketWriter w = _writer.Begin(Protocol.Hello);
                w.WriteInt(Protocol.Version);
                w.WriteULong(GameContent.ContentHash);
                w.WriteByte((byte)(_helloReceived ? 1 : 0));
                _transport.SendReliable(w.Buffer, w.Length);
            }

            if (_clock >= _nextPing)
            {
                _nextPing = _clock + PingInterval;
                PacketWriter w = _writer.Begin(Protocol.Ping);
                w.WriteULong((ulong)(_clock * 1000.0));
                _transport.SendUnreliable(w.Buffer, w.Length);
            }

            if (_clock - _lastReceive > TimeoutSeconds)
            {
                SetDisconnected("Opponent timed out.");
                return;
            }

            if (State == NetMatchState.InMatch && Session != null)
            {
                _accumulator += deltaSeconds;
                if (_accumulator > TickSeconds * 10) _accumulator = TickSeconds * 10;
                double rttTicks = RttMs / 1000.0 * GameConfig.TickRate;
                while (_accumulator >= TickSeconds)
                {
                    if (!Session.AdvanceTick(sampleInput(), rttTicks))
                    {
                        // Stalled or skipping: keep a little time so we resume promptly.
                        if (_accumulator > TickSeconds * 2) _accumulator = TickSeconds * 2;
                        _accumulator -= TickSeconds;
                        break;
                    }
                    _accumulator -= TickSeconds;
                }
            }
        }

        /// <summary>Fraction of the way to the next tick, for smooth rendering.</summary>
        public float Interpolation
        {
            get { return (float)(_accumulator / TickSeconds); }
        }

        private void Receive()
        {
            byte[] data;
            int length;
            while (_transport.TryReceive(out data, out length))
            {
                if (length <= 0) continue;
                _lastReceive = _clock;
                var r = new PacketReader(data, length);
                byte type = r.ReadByte();
                switch (type)
                {
                    case Protocol.Hello:
                        HandleHello(ref r);
                        break;
                    case Protocol.CharacterSelect:
                        RemoteCharacter = r.ReadByte();
                        RemoteReady = r.ReadByte() != 0;
                        TryStartAsHost();
                        break;
                    case Protocol.Start:
                        if (!IsHost) HandleStart(ref r);
                        break;
                    case Protocol.BackToSelect:
                        if (State == NetMatchState.InMatch) EnterSelect();
                        break;
                    case Protocol.Leave:
                        _transport.Disconnect();
                        SetDisconnected("Opponent left the match.");
                        return;
                    case Protocol.Ping:
                    {
                        ulong stamp = r.ReadULong();
                        PacketWriter w = _writer.Begin(Protocol.Pong);
                        w.WriteULong(stamp);
                        _transport.SendUnreliable(w.Buffer, w.Length);
                        break;
                    }
                    case Protocol.Pong:
                    {
                        double sent = r.ReadULong() / 1000.0;
                        double rtt = (_clock - sent) * 1000.0;
                        if (rtt >= 0 && rtt < 5000) RttMs = RttMs * 0.8 + rtt * 0.2;
                        break;
                    }
                    case Protocol.Input:
                    case Protocol.Checksum:
                        if (Session != null && State == NetMatchState.InMatch) Session.HandlePacket(ref r, type);
                        break;
                }
            }
        }

        private void HandleHello(ref PacketReader r)
        {
            int version = r.ReadInt();
            ulong hash = r.ReadULong();
            if (r.Failed) return;
            if (version != Protocol.Version || hash != GameContent.ContentHash)
            {
                _transport.Disconnect();
                SetDisconnected("Game version mismatch. Both players need the same build.");
                return;
            }
            _helloReceived = true;
            if (State == NetMatchState.Handshaking)
            {
                // Reply once more so the other side also sees our hello, then move on.
                PacketWriter w = _writer.Begin(Protocol.Hello);
                w.WriteInt(Protocol.Version);
                w.WriteULong(GameContent.ContentHash);
                w.WriteByte(1);
                _transport.SendReliable(w.Buffer, w.Length);
                EnterSelect();
            }
        }

        private void EnterSelect()
        {
            State = NetMatchState.CharacterSelect;
            LocalReady = false;
            RemoteReady = false;
            Session = null;
            Message = string.Empty;
        }

        private void TryStartAsHost()
        {
            if (!IsHost || State != NetMatchState.CharacterSelect) return;
            if (!LocalReady || !RemoteReady || RemoteCharacter < 0) return;

            _seedCounter = _seedCounter * 1664525u + 1013904223u;
            var setup = new MatchSetup
            {
                Character0 = LocalCharacter,
                Character1 = RemoteCharacter,
                Seed = _seedCounter == 0 ? 1u : _seedCounter,
                Mode = MatchMode.Versus,
            };
            PacketWriter w = _writer.Begin(Protocol.Start);
            w.WriteUInt(setup.Seed);
            w.WriteByte((byte)setup.Character0);
            w.WriteByte((byte)setup.Character1);
            _transport.SendReliable(w.Buffer, w.Length);
            BeginMatch(setup);
        }

        private void HandleStart(ref PacketReader r)
        {
            var setup = new MatchSetup
            {
                Seed = r.ReadUInt(),
                Character0 = r.ReadByte(),
                Character1 = r.ReadByte(),
                Mode = MatchMode.Versus,
            };
            if (r.Failed) return;
            BeginMatch(setup);
        }

        private void BeginMatch(MatchSetup setup)
        {
            Session = new RollbackSession(LocalIndex, setup, _inputDelay, (data, length) => _transport.SendUnreliable(data, length));
            State = NetMatchState.InMatch;
            _accumulator = 0;
            MatchCounter++;
        }

        private void SetDisconnected(string message)
        {
            State = NetMatchState.Disconnected;
            Message = message;
        }
    }
}
