using System;
using System.Collections.Generic;

namespace HEXBOUND.Netcode
{
    /// <summary>
    /// In-memory network between two endpoints with configurable latency, jitter and packet loss.
    /// Used by tests to check that rollback keeps both peers in sync under bad conditions.
    /// Time is advanced manually, so tests are fully deterministic.
    /// </summary>
    public sealed class SimulatedNetwork
    {
        private struct InFlight
        {
            public double DeliverAt;
            public long Order;
            public byte[] Data;
        }

        public double LatencyMs = 40;
        public double JitterMs = 10;
        /// <summary>Chance (0 to 1) that an unreliable packet is dropped.</summary>
        public double LossChance = 0.05;

        public double NowMs;

        private readonly List<InFlight>[] _queues = { new List<InFlight>(), new List<InFlight>() };
        private readonly Random _random;
        private long _order;

        public readonly Endpoint A;
        public readonly Endpoint B;

        public SimulatedNetwork(int seed)
        {
            _random = new Random(seed);
            A = new Endpoint(this, 0);
            B = new Endpoint(this, 1);
        }

        private void Enqueue(int toSide, byte[] data, int length, bool reliable)
        {
            if (!reliable && _random.NextDouble() < LossChance) return;
            var copy = new byte[length];
            Array.Copy(data, copy, length);
            double delay = LatencyMs + (_random.NextDouble() * 2 - 1) * JitterMs;
            if (delay < 0) delay = 0;
            double at = NowMs + delay;
            if (reliable)
            {
                // Reliable packets keep their order: never deliver before an earlier packet.
                List<InFlight> q = _queues[toSide];
                for (int i = 0; i < q.Count; i++)
                {
                    if (q[i].DeliverAt > at) at = q[i].DeliverAt;
                }
            }
            _queues[toSide].Add(new InFlight { DeliverAt = at, Order = _order++, Data = copy });
        }

        private bool Dequeue(int side, out byte[] data)
        {
            List<InFlight> q = _queues[side];
            int best = -1;
            for (int i = 0; i < q.Count; i++)
            {
                if (q[i].DeliverAt > NowMs) continue;
                if (best < 0 || q[i].DeliverAt < q[best].DeliverAt || (q[i].DeliverAt == q[best].DeliverAt && q[i].Order < q[best].Order)) best = i;
            }
            if (best < 0)
            {
                data = null;
                return false;
            }
            data = q[best].Data;
            q.RemoveAt(best);
            return true;
        }

        public sealed class Endpoint : INetTransport
        {
            private readonly SimulatedNetwork _net;
            private readonly int _side;

            public Endpoint(SimulatedNetwork net, int side)
            {
                _net = net;
                _side = side;
                Status = TransportStatus.Connected;
            }

            public TransportStatus Status { get; private set; }

            public string StatusMessage
            {
                get { return string.Empty; }
            }

            public void Update()
            {
            }

            public void SendUnreliable(byte[] data, int length)
            {
                _net.Enqueue(1 - _side, data, length, false);
            }

            public void SendReliable(byte[] data, int length)
            {
                _net.Enqueue(1 - _side, data, length, true);
            }

            public bool TryReceive(out byte[] data, out int length)
            {
                if (_net.Dequeue(_side, out data))
                {
                    length = data.Length;
                    return true;
                }
                length = 0;
                return false;
            }

            public void Disconnect()
            {
                Status = TransportStatus.Disconnected;
            }
        }
    }
}
