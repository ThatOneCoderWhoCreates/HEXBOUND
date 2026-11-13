namespace HEXBOUND.Netcode
{
    public enum TransportStatus
    {
        Idle,
        Connecting,
        Connected,
        Disconnected,
        Failed,
    }

    /// <summary>
    /// A connection to exactly one remote peer. Implemented with Unity Transport in the game
    /// and with an in-memory simulated network in tests.
    /// </summary>
    public interface INetTransport
    {
        TransportStatus Status { get; }

        /// <summary>Human-readable reason for Failed or Disconnected.</summary>
        string StatusMessage { get; }

        /// <summary>Pumps the network. Call once per frame before reading.</summary>
        void Update();

        /// <summary>Delivery not guaranteed (inputs, checksums, pings).</summary>
        void SendUnreliable(byte[] data, int length);

        /// <summary>Guaranteed, ordered delivery (handshake and menu messages).</summary>
        void SendReliable(byte[] data, int length);

        /// <summary>Returns the next received packet, or false when none are waiting. The buffer is only valid until the next call.</summary>
        bool TryReceive(out byte[] data, out int length);

        void Disconnect();
    }
}
