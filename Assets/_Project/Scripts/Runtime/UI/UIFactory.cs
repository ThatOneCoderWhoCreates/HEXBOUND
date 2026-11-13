using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HEXBOUND.UI
{
    /// <summary>Builds uGUI elements in code, using Unity's built-in font, so no UI assets are needed.</summary>
    public static class UIFactory
    {
        public static readonly Color Accent = new Color(0.62f, 0.38f, 1f);
        public static readonly Color AccentDark = new Color(0.22f, 0.12f, 0.38f, 0.95f);
        public static readonly Color PanelColor = new Color(0.06f, 0.04f, 0.1f, 0.88f);
        public static readonly Color TextColor = new Color(0.93f, 0.9f, 1f);
        public static readonly Color MutedText = new Color(0.65f, 0.6f, 0.75f);

        private static Font _font;
        private static Sprite _white;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    Texture2D tex = Texture2D.whiteTexture;
                    _white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                return _white;
            }
        }

        public static Canvas CreateCanvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>A rect anchored at the center of its parent.</summary>
        public static RectTransform Centered(Transform parent, string name, Vector2 position, Vector2 size)
        {
            return Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            RectTransform rt = Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Image(RectTransform rt, Color color)
        {
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = White;
            img.color = color;
            return img;
        }

        public static Image Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            return Image(Centered(parent, name, position, size), color);
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align, Vector2 position, Vector2 boxSize, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = Centered(parent, "Text", position, boxSize);
            return LabelOn(rt, text, size, color, align, style);
        }

        public static Text LabelOn(RectTransform rt, string text, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            Shadow shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public static Button Button(Transform parent, string label, Vector2 position, Vector2 size, Action onClick, int fontSize = 30)
        {
            RectTransform rt = Centered(parent, "Button " + label, position, size);
            Image bg = Image(rt, AccentDark);
            Button button = rt.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.6f, 1.4f, 2f);
            colors.pressedColor = new Color(0.7f, 0.6f, 0.9f);
            colors.selectedColor = new Color(1.3f, 1.2f, 1.6f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.colorMultiplier = 1.6f;
            button.colors = colors;
            button.targetGraphic = bg;

            RectTransform border = Stretch(rt, "Border");
            border.sizeDelta = new Vector2(-6f, -6f);
            Image inner = Image(border, new Color(0.1f, 0.06f, 0.16f, 0.9f));
            inner.raycastTarget = false;

            RectTransform textRt = Stretch(rt, "Label");
            LabelOn(textRt, label, fontSize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            Text t = button.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }

        public static InputField InputField(Transform parent, string placeholder, Vector2 position, Vector2 size, int fontSize = 30)
        {
            RectTransform rt = Centered(parent, "Input", position, size);
            Image bg = Image(rt, new Color(0.02f, 0.02f, 0.05f, 0.95f));
            InputField field = rt.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;

            RectTransform textRt = Stretch(rt, "Text");
            textRt.sizeDelta = new Vector2(-24f, -10f);
            Text text = LabelOn(textRt, string.Empty, fontSize, TextColor, TextAnchor.MiddleLeft);
            text.supportRichText = false;

            RectTransform phRt = Stretch(rt, "Placeholder");
            phRt.sizeDelta = new Vector2(-24f, -10f);
            Text ph = LabelOn(phRt, placeholder, fontSize, new Color(0.5f, 0.45f, 0.6f), TextAnchor.MiddleLeft, FontStyle.Italic);

            field.textComponent = text;
            field.placeholder = ph;
            return field;
        }

        /// <summary>A horizontal bar. Returns the fill image (use fillAmount 0-1).</summary>
        public static Image Bar(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color fill, bool rightToLeft = false)
        {
            RectTransform back = Rect(parent, "Bar", anchor, anchor, pivot, position, size);
            Image(back, new Color(0f, 0f, 0f, 0.6f));
            RectTransform fillRt = Stretch(back, "Fill");
            fillRt.sizeDelta = new Vector2(-6f, -6f);
            Image img = Image(fillRt, fill);
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            img.fillOrigin = rightToLeft ? 1 : 0;
            img.fillAmount = 1f;
            img.raycastTarget = false;
            return img;
        }
    }
}
