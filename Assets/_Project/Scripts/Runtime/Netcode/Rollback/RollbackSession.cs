using System;
using HEXBOUND.Simulation;

namespace HEXBOUND.Netcode
{
    /// <summary>
    /// GGPO-style rollback for two players.
    /// Each peer simulates every tick immediately, predicting the remote player's input as
    /// "same as the last one received". When the real input arrives and differs, the game
    /// restores the saved state from that tick and re-simulates up to the present.
    /// Only inputs are sent over the network.
    /// </summary>
    public sealed class RollbackSession
    {
        public const int BufferSize = 128;
        /// <summary>The local simulation may run at most this many ticks ahead of the last confirmed remote input.</summary>
        public const int MaxPrediction = 8;
        public const int ChecksumInterval = 30;
        private const int MaxInputsPerPacket = 32;

        public readonly int LocalIndex;
        public readonly int InputDelay;
        public readonly GameState State = new GameState();

        /// <summary>Events produced by ticks since the last ClearEvents call (including re-simulated ticks).</summary>
        public readonly EventBuffer Events = new EventBuffer(512);

        private readonly GameState[] _snapshots = new GameState[BufferSize];
        private readonly InputFrame[] _localInputs = new InputFrame[BufferSize];
        private readonly InputFrame[] _remoteInputs = new InputFrame[BufferSize];
        private readonly InputFrame[] _usedRemote = new InputFrame[BufferSize];
        private readonly ulong[] _localChecksums = new ulong[BufferSize];
        private readonly int[] _localChecksumTicks = new int[BufferSize];
        private readonly ulong[] _remoteChecksums = new ulong[BufferSize];
        private readonly int[] _remoteChecksumTicks = new int[BufferSize];
        private readonly EventBuffer _tickEvents = new EventBuffer(128);

        private readonly Action<byte[], int> _sendUnreliable;
        private readonly PacketWriter _writer = new PacketWriter(512);

        /// <summary>Number of ticks simulated so far. The next tick to simulate has this index.</summary>
        public int CurrentTick { get; private set; }

        public int LastConfirmedRemoteTick { get; private set; } = -1;
        public int LastLocalInputTick { get; private set; }
        public int RemoteAckedLocalTick { get; private set; } = -1;

        /// <summary>Remote peer's current tick as reported in its last packet.</summary>
        public int RemoteReportedTick { get; private set; }

        public bool Desynced { get; private set; }
        public int DesyncTick { get; private set; } = -1;
        public int LastRollbackLength { get; private set; }
        public int TotalRollbacks { get; private set; }
        public bool Stalled { get; private set; }

        private int _firstMispredictedTick = -1;
        private int _lastChecksumSent = -1;
        private int _ticksSinceSkip;

        public RollbackSession(int localIndex, MatchSetup setup, int inputDelay, Action<byte[], int> sendUnreliable)
        {
            LocalIndex = localIndex;
            InputDelay = Math.Max(0, Math.Min(inputDelay, 6));
            _sendUnreliable = sendUnreliable;
            for (int i = 0; i < BufferSize; i++)
            {
                _snapshots[i] = new GameState();
                _localChecksumTicks[i] = -1;
                _remoteChecksumTicks[i] = -1;
            }
            GameSimulation.Initialize(State, setup, Events);
            // Ticks before the input delay use empty local input.
            LastLocalInputTick = InputDelay - 1;
        }

        public void ClearEvents()
        {
            Events.Clear();
        }

        /// <summary>Frames the local simulation is ahead of the remote one (positive means we should slow down).</summary>
        public int FrameAdvantage(double rttTicks)
        {
            int estimatedRemote = RemoteReportedTick + (int)(rttTicks / 2);
            return CurrentTick - estimatedRemote;
        }

        public bool CanAdvance()
        {
            return CurrentTick <= LastConfirmedRemoteTick + MaxPrediction;
        }

        /// <summary>
        /// Advances one tick if allowed. Returns false when stalled waiting for the remote player
        /// or when skipping a tick to let the remote catch up.
        /// </summary>
        public bool AdvanceTick(InputFrame localInput, double rttTicks)
        {
            ApplyRollback();

            if (!CanAdvance())
            {
                Stalled = true;
                SendInputs();
                return false;
            }
            Stalled = false;

            _ticksSinceSkip++;
            if (FrameAdvantage(rttTicks) >= 2 && _ticksSinceSkip > 6 && CurrentTick > 30)
            {
                _ticksSinceSkip = 0;
                SendInputs();
                return false;
            }

            int inputTick = CurrentTick + InputDelay;
            if (inputTick > LastLocalInputTick)
            {
                _localInputs[inputTick % BufferSize] = localInput;
                LastLocalInputTick = inputTick;
            }
            SendInputs();

            SimulateTick(CurrentTick);
            CurrentTick++;
            return true;
        }

        private void ApplyRollback()
        {
            if (_firstMispredictedTick < 0) return;
            int from = _firstMispredictedTick;
            _firstMispredictedTick = -1;
            if (from >= CurrentTick) return;
            if (CurrentTick - from >= BufferSize) return;

            State.CopyFrom(_snapshots[from % BufferSize]);
            LastRollbackLength = CurrentTick - from;
            TotalRollbacks++;
            for (int t = from; t < CurrentTick; t++)
            {
                SimulateTick(t);
            }
        }

        private void SimulateTick(int tick)
        {
            int slot = tick % BufferSize;
            _snapshots[slot].CopyFrom(State);

            bool confirmed = tick <= LastConfirmedRemoteTick;
            InputFrame remote = confirmed ? _remoteInputs[slot] : Predict();
            _usedRemote[slot] = remote;
            InputFrame local = _localInputs[slot];

            _tickEvents.Clear();
            if (LocalIndex == 0) GameSimulation.Tick(State, local, remote, _tickEvents);
            else GameSimulation.Tick(State, remote, local, _tickEvents);
            for (int i = 0; i < _tickEvents.Count; i++)
            {
                SimEvent e = _tickEvents[i];
                Events.Add(e.Tick, e.Type, e.Fighter, e.Param, e.Position);
            }

            if (confirmed && tick % ChecksumInterval == 0 && tick > _lastChecksumSent)
            {
                ulong checksum = State.Checksum();
                _localChecksums[slot] = checksum;
                _localChecksumTicks[slot] = tick;
                _lastChecksumSent = tick;
                PacketWriter w = _writer.Begin(Protocol.Checksum);
                w.WriteInt(tick);
                w.WriteULong(checksum);
                _sendUnreliable(w.Buffer, w.Length);
                // Send twice: a lost checksum only delays desync detection.
                _sendUnreliable(w.Buffer, w.Length);
                CompareChecksum(slot);
            }
        }

        private InputFrame Predict()
        {
            if (LastConfirmedRemoteTick < 0) return InputFrame.Empty;
            return _remoteInputs[LastConfirmedRemoteTick % BufferSize];
        }

        private void SendInputs()
        {
            int start = Math.Max(RemoteAckedLocalTick + 1, LastLocalInputTick - MaxInputsPerPacket + 1);
            if (start < 0) start = 0;
            int count = LastLocalInputTick - start + 1;
            if (count <= 0) return;

            PacketWriter w = _writer.Begin(Protocol.Input);
            w.WriteInt(LastConfirmedRemoteTick);
            w.WriteInt(CurrentTick);
            w.WriteInt(start);
            w.WriteByte((byte)count);
            for (int t = start; t <= LastLocalInputTick; t++)
            {
                w.WriteUInt(t < InputDelay ? 0u : _localInputs[t % BufferSize].Pack());
            }
            _sendUnreliable(w.Buffer, w.Length);
        }

        /// <summary>Handles Input and Checksum packets. Returns true if the packet was consumed.</summary>
        public bool HandlePacket(ref PacketReader r, byte type)
        {
            if (type == Protocol.Input)
            {
                int ack = r.ReadInt();
                int remoteTick = r.ReadInt();
                int start = r.ReadInt();
                int count = r.ReadByte();
                if (r.Failed) return true;
                if (ack > RemoteAckedLocalTick) RemoteAckedLocalTick = ack;
                if (remoteTick > RemoteReportedTick) RemoteReportedTick = remoteTick;

                for (int i = 0; i < count; i++)
                {
                    uint packed = r.ReadUInt();
                    if (r.Failed) return true;
                    int t = start + i;
                    if (t != LastConfirmedRemoteTick + 1) continue;
                    if (t - CurrentTick >= BufferSize - 1) break;
                    InputFrame input = InputFrame.Unpack(packed);
                    _remoteInputs[t % BufferSize] = input;
                    LastConfirmedRemoteTick = t;
                    if (t < CurrentTick && !_usedRemote[t % BufferSize].Equals(input))
                    {
                        if (_firstMispredictedTick < 0 || t < _firstMispredictedTick) _firstMispredictedTick = t;
                    }
                }
                return true;
            }

            if (type == Protocol.Checksum)
            {
                int tick = r.ReadInt();
                ulong checksum = r.ReadULong();
                if (r.Failed || tick < 0) return true;
                int slot = tick % BufferSize;
                _remoteChecksums[slot] = checksum;
                _remoteChecksumTicks[slot] = tick;
                CompareChecksum(slot);
                return true;
            }

            return false;
        }

        private void CompareChecksum(int slot)
        {
            if (_localChecksumTicks[slot] < 0 || _localChecksumTicks[slot] != _remoteChecksumTicks[slot]) return;
            if (_localChecksums[slot] != _remoteChecksums[slot] && !Desynced)
            {
                Desynced = true;
                DesyncTick = _localChecksumTicks[slot];
            }
        }
    }
}
