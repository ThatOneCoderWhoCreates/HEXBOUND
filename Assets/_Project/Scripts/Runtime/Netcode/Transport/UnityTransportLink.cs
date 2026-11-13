using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;

namespace HEXBOUND.Netcode
{
    /// <summary>
    /// One-to-one connection over Unity Transport, either directly by IP or through Unity Relay.
    /// The host listens and accepts exactly one client.
    /// </summary>
    public sealed class UnityTransportLink : INetTransport, IDisposable
    {
        private NetworkDriver _driver;
        private NetworkPipeline _reliable;
        private NetworkPipeline _unreliable;
        private NetworkConnection _connection;
        private readonly bool _isHost;
        private readonly Queue<byte[]> _received = new Queue<byte[]>();
        private readonly Stack<byte[]> _pool = new Stack<byte[]>();
        private byte[] _lastReturned;
        private bool _disposed;

        public TransportStatus Status { get; private set; } = TransportStatus.Idle;
        public string StatusMessage { get; private set; } = string.Empty;

        private UnityTransportLink(bool isHost, NetworkSettings settings)
        {
            _isHost = isHost;
            _driver = NetworkDriver.Create(settings);
            _reliable = _driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            _unreliable = NetworkPipeline.Null;
        }

        public static UnityTransportLink HostDirect(ushort port)
        {
            var link = new UnityTransportLink(true, new NetworkSettings());
            NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
            if (link._driver.Bind(endpoint) != 0)
            {
                link.Fail("Could not open port " + port + ". Is another copy of the game already hosting?");
                return link;
            }
            link._driver.Listen();
            link.Status = TransportStatus.Connecting;
            link.StatusMessage = "Waiting for opponent on port " + port + "...";
            return link;
        }

        public static UnityTransportLink JoinDirect(string address, ushort port)
        {
            var link = new UnityTransportLink(false, new NetworkSettings());
            NetworkEndpoint endpoint;
            if (!NetworkEndpoint.TryParse(address, port, out endpoint))
            {
                link.Fail("Invalid IP address: " + address);
                return link;
            }
            link._connection = link._driver.Connect(endpoint);
            link.Status = TransportStatus.Connecting;
            link.StatusMessage = "Connecting to " + address + ":" + port + "...";
            return link;
        }

        public static UnityTransportLink HostRelay(RelayServerData relayData)
        {
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref relayData);
            var link = new UnityTransportLink(true, settings);
            if (link._driver.Bind(NetworkEndpoint.AnyIpv4) != 0)
            {
                link.Fail("Could not bind to the Relay server.");
                return link;
            }
            link._driver.Listen();
            link.Status = TransportStatus.Connecting;
            link.StatusMessage = "Waiting for opponent...";
            return link;
        }

        public static UnityTransportLink JoinRelay(RelayServerData relayData)
        {
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref relayData);
            var link = new UnityTransportLink(false, settings);
            if (link._driver.Bind(NetworkEndpoint.AnyIpv4) != 0)
            {
                link.Fail("Could not bind to the Relay server.");
                return link;
            }
            link._connection = link._driver.Connect(relayData.Endpoint);
            link.Status = TransportStatus.Connecting;
            link.StatusMessage = "Connecting through Relay...";
            return link;
        }

        public void Update()
        {
            if (_disposed || !_driver.IsCreated) return;
            if (Status == TransportStatus.Failed || Status == TransportStatus.Disconnected) return;

            if (_lastReturned != null)
            {
                _pool.Push(_lastReturned);
                _lastReturned = null;
            }

            _driver.ScheduleUpdate().Complete();

            if (_isHost)
            {
                NetworkConnection incoming;
                while ((incoming = _driver.Accept()) != default(NetworkConnection))
                {
                    if (_connection.IsCreated)
                    {
                        // Only one opponent allowed.
                        _driver.Disconnect(incoming);
                        continue;
                    }
                    _connection = incoming;
                    Status = TransportStatus.Connected;
                    StatusMessage = "Opponent connected.";
                }
            }

            if (!_connection.IsCreated) return;

            NetworkEvent.Type cmd;
            DataStreamReader stream;
            while ((cmd = _driver.PopEventForConnection(_connection, out stream)) != NetworkEvent.Type.Empty)
            {
                switch (cmd)
                {
                    case NetworkEvent.Type.Connect:
                        Status = TransportStatus.Connected;
                        StatusMessage = "Connected.";
                        break;
                    case NetworkEvent.Type.Data:
                        int length = stream.Length;
                        byte[] buffer = Rent(length);
                        for (int i = 0; i < length; i++)
                        {
                            buffer[i] = stream.ReadByte();
                        }
                        var packet = new byte[length];
                        Array.Copy(buffer, packet, length);
                        _pool.Push(buffer);
                        _received.Enqueue(packet);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        _connection = default(NetworkConnection);
                        if (Status == TransportStatus.Connecting) Fail("Could not reach the other player.");
                        else
                        {
                            Status = TransportStatus.Disconnected;
                            StatusMessage = "Connection closed.";
                        }
                        return;
                }
            }
        }

        private byte[] Rent(int length)
        {
            while (_pool.Count > 0)
            {
                byte[] b = _pool.Pop();
                if (b.Length >= length) return b;
            }
            return new byte[Math.Max(length, 256)];
        }

        public void SendUnreliable(byte[] data, int length)
        {
            Send(_unreliable, data, length);
        }

        public void SendReliable(byte[] data, int length)
        {
            Send(_reliable, data, length);
        }

        private void Send(NetworkPipeline pipeline, byte[] data, int length)
        {
            if (_disposed || Status != TransportStatus.Connected || !_connection.IsCreated) return;
            DataStreamWriter writer;
            if (_driver.BeginSend(pipeline, _connection, out writer) != 0) return;
            for (int i = 0; i < length; i++)
            {
                writer.WriteByte(data[i]);
            }
            _driver.EndSend(writer);
        }

        public bool TryReceive(out byte[] data, out int length)
        {
            if (_received.Count == 0)
            {
                data = null;
                length = 0;
                return false;
            }
            data = _received.Dequeue();
            length = data.Length;
            return true;
        }

        public void Disconnect()
        {
            if (_disposed) return;
            if (_connection.IsCreated && _driver.IsCreated)
            {
                _driver.Disconnect(_connection);
                _driver.ScheduleUpdate().Complete();
            }
            _connection = default(NetworkConnection);
            if (Status != TransportStatus.Failed) Status = TransportStatus.Disconnected;
        }

        private void Fail(string message)
        {
            Status = TransportStatus.Failed;
            StatusMessage = message;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Disconnect();
            if (_driver.IsCreated) _driver.Dispose();
            _disposed = true;
        }
    }
}
