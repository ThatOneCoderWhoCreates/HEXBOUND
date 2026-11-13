using System;
using UnityEngine;
using UnityEngine.UI;

namespace HEXBOUND.UI
{
    /// <summary>Host or join an online match, by room code (Unity Relay) or by IP address.</summary>
    public sealed class OnlineScreen
    {
        public readonly GameObject Root;

        public event Action HostRoom;
        public event Action<string> JoinRoom;
        public event Action HostDirect;
        public event Action<string> JoinDirect;
        public event Action Cancel;

        private readonly InputField _code;
        private readonly InputField _ip;
        private readonly Text _status;
        private readonly Text _roomCode;
        private readonly Button _copy;
        private readonly GameObject _choices;
        private string _currentCode = string.Empty;

        public OnlineScreen(Transform canvas, string lastIp)
        {
            RectTransform root = UIFactory.Stretch(canvas, "Online");
            Root = root.gameObject;
            UIFactory.Image(UIFactory.Stretch(root, "Shade"), new Color(0.02f, 0.01f, 0.05f, 0.55f)).raycastTarget = false;
            UIFactory.Label(root, "ONLINE MATCH", 64, UIFactory.TextColor, TextAnchor.MiddleCenter, new Vector2(0, 430), new Vector2(1200, 90), FontStyle.Bold);

            var choices = new GameObject("Choices", typeof(RectTransform));
            choices.transform.SetParent(root, false);
            _choices = choices;
            Transform c = choices.transform;

            UIFactory.Panel(c, "Room Panel", new Vector2(-400, 120), new Vector2(720, 420), UIFactory.PanelColor);
            UIFactory.Label(c, "ROOM CODE  (internet)", 34, UIFactory.Accent, TextAnchor.MiddleCenter, new Vector2(-400, 290), new Vector2(680, 50), FontStyle.Bold);
            UIFactory.Label(c, "Uses Unity Relay. Needs the project linked to Unity Cloud.", 22, UIFactory.MutedText, TextAnchor.MiddleCenter, new Vector2(-400, 245), new Vector2(680, 40));
            UIFactory.Button(c, "CREATE ROOM", new Vector2(-400, 170), new Vector2(520, 76), () => HostRoom?.Invoke());
            _code = UIFactory.InputField(c, "Enter room code", new Vector2(-480, 60), new Vector2(360, 70));
            _code.characterLimit = 12;
            UIFactory.Button(c, "JOIN", new Vector2(-170, 60), new Vector2(200, 70), () => JoinRoom?.Invoke(_code.text));

            UIFactory.Panel(c, "Direct Panel", new Vector2(400, 120), new Vector2(720, 420), UIFactory.PanelColor);
            UIFactory.Label(c, "DIRECT IP  (LAN or same PC)", 34, UIFactory.Accent, TextAnchor.MiddleCenter, new Vector2(400, 290), new Vector2(680, 50), FontStyle.Bold);
            UIFactory.Label(c, "Port 7777. Use 127.0.0.1 to test two copies on one PC.", 22, UIFactory.MutedText, TextAnchor.MiddleCenter, new Vector2(400, 245), new Vector2(680, 40));
            UIFactory.Button(c, "HOST", new Vector2(400, 170), new Vector2(520, 76), () => HostDirect?.Invoke());
            _ip = UIFactory.InputField(c, "IP address", new Vector2(320, 60), new Vector2(360, 70));
            _ip.text = lastIp;
            UIFactory.Button(c, "JOIN", new Vector2(630, 60), new Vector2(200, 70), () => JoinDirect?.Invoke(_ip.text));

            _roomCode = UIFactory.Label(root, string.Empty, 80, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter, new Vector2(0, -150), new Vector2(1200, 110), FontStyle.Bold);
            _copy = UIFactory.Button(root, "COPY CODE", new Vector2(0, -250), new Vector2(320, 64), () => GUIUtility.systemCopyBuffer = _currentCode, 26);
            _copy.gameObject.SetActive(false);
            _status = UIFactory.Label(root, string.Empty, 30, UIFactory.TextColor, TextAnchor.MiddleCenter, new Vector2(0, -330), new Vector2(1600, 100));
            UIFactory.Button(root, "BACK", new Vector2(0, -440), new Vector2(320, 76), () => Cancel?.Invoke());
        }

        public void Show(bool visible)
        {
            Root.SetActive(visible);
        }

        public void Reset()
        {
            _choices.SetActive(true);
            SetRoomCode(string.Empty);
            SetStatus(string.Empty);
        }

        public void SetBusy(bool busy)
        {
            _choices.SetActive(!busy);
        }

        public void SetStatus(string text)
        {
            _status.text = text;
        }

        public void SetRoomCode(string code)
        {
            _currentCode = code ?? string.Empty;
            _roomCode.text = string.IsNullOrEmpty(code) ? string.Empty : "ROOM  " + code;
            _copy.gameObject.SetActive(!string.IsNullOrEmpty(code));
        }
    }
}
