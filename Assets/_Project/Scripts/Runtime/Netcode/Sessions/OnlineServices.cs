using System;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace HEXBOUND.Netcode
{
    /// <summary>
    /// Unity Gaming Services: anonymous sign-in and Relay allocations.
    /// The Relay join code is used as the room code players share.
    /// Requires the project to be linked to a Unity Cloud project (Edit > Project Settings > Services).
    /// </summary>
    public static class OnlineServices
    {
        private const string RelayConnectionType = "dtls";

        public struct HostResult
        {
            public string JoinCode;
            public RelayServerData RelayData;
        }

        private static bool _initialized;

        public static async Task EnsureSignedInAsync()
        {
            if (!_initialized || UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                // A random profile lets two copies of the game on one PC sign in as different players.
                options.SetProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 12));
                await UnityServices.InitializeAsync(options);
                _initialized = true;
            }
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        public static async Task<HostResult> CreateRoomAsync()
        {
            await EnsureSignedInAsync();
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            return new HostResult
            {
                JoinCode = code,
                RelayData = allocation.ToRelayServerData(RelayConnectionType),
            };
        }

        public static async Task<RelayServerData> JoinRoomAsync(string joinCode)
        {
            await EnsureSignedInAsync();
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim().ToUpperInvariant());
            return allocation.ToRelayServerData(RelayConnectionType);
        }

        /// <summary>Turns service exceptions into a short message for players.</summary>
        public static string Explain(Exception e)
        {
            string text = e.Message ?? string.Empty;
            if (text.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Online rooms need Unity Cloud. In Unity: Edit > Project Settings > Services, link this project, then try again. Direct IP works without it.";
            }
            if (e is RelayServiceException)
            {
                return "Relay error: " + text + " Check the room code and try again.";
            }
            if (e is AuthenticationException || e is RequestFailedException)
            {
                return "Could not sign in to Unity services: " + text;
            }
            return text;
        }
    }
}
