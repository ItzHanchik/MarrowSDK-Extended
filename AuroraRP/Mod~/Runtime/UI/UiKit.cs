using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AuroraRP
{
    /// <summary>
    /// Мелкий конструктор UI: панели, тексты, кнопки, полосы, иконки.
    /// Всё строится кодом и приводится в движение анимациями (без внешних ассетов).
    /// </summary>
    public static class UiKit
    {
        // ------------------------------------------------------------------ база

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);

            RectTransform rect = null;

            if (parent != null)
            {
                // Под UI-родителем Unity сам превращает Transform в RectTransform.
                go.transform.SetParent(parent, false);
                rect = go.GetComponent<RectTransform>();
            }

            if (rect == null)
            {
                rect = go.AddComponent<RectTransform>();

                if (parent != null)
                {
                    rect.SetParent(parent, false);
                }
            }

            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        public static Canvas NewCanvas(string name, Transform parent, Vector2 size, float worldScale, int sortingOrder = 4000)
        {
            var go = new GameObject(name);

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var rect = go.GetComponent<RectTransform>();

            if (rect == null)
            {
                rect = go.AddComponent<RectTransform>();
            }

            rect.localScale = new Vector3(worldScale, worldScale, worldScale);
            rect.sizeDelta = size;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;
            canvas.overrideSorting = true;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 1f;
            scaler.referencePixelsPerUnit = 100f;

            var raycaster = go.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false; // нам не нужен Unity-овский raycast: выбираем вручную рукой

            go.AddComponent<CanvasGroup>();

            return canvas;
        }

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
        {
            var rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = type;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rect = NewRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = UiTheme.Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.richText = true;
            return tmp;
        }

        /// <summary>Панель со скруглёнными углами (9-slice) и мягкой тенью.</summary>
        public static Image Panel(string name, Transform parent, Vector2 size, Color color, float alpha = 1f, bool shadow = true, bool soft = false)
        {
            var parentRect = parent as RectTransform;

            if (shadow && parentRect != null)
            {
                var sh = NewImage(name + "_Shadow", parent, UiTheme.Shadow, new Color(0f, 0f, 0f, 0.55f * alpha));
                sh.rectTransform.sizeDelta = new Vector2(size.x + 70f, size.y + 70f);
                sh.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            }

            var image = NewImage(name, parent, soft ? UiTheme.RoundedSoft : UiTheme.RoundedPanel, color.WithAlpha(color.a * alpha), Image.Type.Sliced);
            image.rectTransform.sizeDelta = size;
            image.type = Image.Type.Sliced;
            return image;
        }

        public static Image AccentLine(Transform parent, Vector2 size, Color color)
        {
            var image = NewImage("Accent", parent, UiTheme.RoundedSoft, color, Image.Type.Sliced);
            image.rectTransform.sizeDelta = size;
            return image;
        }

        // ------------------------------------------------------------------ кнопки

        public static AuroraButton Button(Transform parent, string label, string icon, Vector2 size, Action onClick,
            AuroraButton.Style style = AuroraButton.Style.Default, string subtitle = null)
        {
            var button = new AuroraButton(parent, label, icon, size, onClick, style, subtitle);
            UiHitRegistry.Register(button);
            return button;
        }

        /// <summary>Разделитель.</summary>
        public static Image Divider(Transform parent, float width)
        {
            var image = NewImage("Divider", parent, UiTheme.RoundedSoft, UiTheme.Divider, Image.Type.Sliced);
            image.rectTransform.sizeDelta = new Vector2(width, 3f);
            return image;
        }

        /// <summary>Полоса прогресса (например, удержание 5 секунд).</summary>
        public static AuroraBar Bar(Transform parent, Vector2 size, Color fillColor)
        {
            var background = NewImage("Bar_Bg", parent, UiTheme.RoundedSoft, AuroraUtils.Hex("#3A0407CC"), Image.Type.Sliced);
            background.rectTransform.sizeDelta = size;

            var fill = NewImage("Bar_Fill", background.rectTransform, UiTheme.RoundedSoft, fillColor, Image.Type.Sliced);
            fill.rectTransform.sizeDelta = new Vector2(size.x - 6f, size.y - 6f);
            fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(3f, 0f);

            return new AuroraBar(fill, size.x - 6f);
        }
    }

    /// <summary>Полоса прогресса.</summary>
    public sealed class AuroraBar
    {
        private readonly Image _fill;
        private readonly float _fullWidth;

        public AuroraBar(Image fill, float fullWidth)
        {
            _fill = fill;
            _fullWidth = fullWidth;
        }

        public void SetValue(float k)
        {
            k = Mathf.Clamp01(k);
            if (_fill != null)
            {
                _fill.rectTransform.sizeDelta = new Vector2(_fullWidth * k, _fill.rectTransform.sizeDelta.y);
                _fill.enabled = k > 0.001f;
            }
        }

        public void SetColor(Color color)
        {
            if (_fill != null)
            {
                _fill.color = color;
            }
        }
    }

    /// <summary>
    /// Кнопка меню: фон, подсветка, иконка, заголовок и подпись.
    /// Анимации плавные, состояние наведения/нажатия — из UiPointer.
    /// </summary>
    public sealed class AuroraButton
    {
        public enum Style
        {
            Default = 0,
            Primary = 1,
            Danger = 2,
            Ghost = 3,
            Tab = 4
        }

        public RectTransform Rect { get; }
        public Image Background { get; }
        public Image IconImage { get; }
        public TextMeshProUGUI Label { get; }
        public TextMeshProUGUI Subtitle { get; }
        public Image Glow { get; }
        public Style ButtonStyle { get; }
        public Action OnClick { get; set; }
        public object Payload { get; set; }
        public bool Interactable { get; private set; } = true;

        private Color _baseColor;
        private Color _accent;
        private float _hover;
        private float _press;
        private float _pulse;
        private bool _hovered;
        private bool _selected;

        public AuroraButton(Transform parent, string label, string icon, Vector2 size, Action onClick, Style style, string subtitle)
        {
            ButtonStyle = style;
            OnClick = onClick;

            var container = UiKit.NewRect("Button_" + label, parent);
            container.sizeDelta = size;
            Rect = container;

            _accent = style switch
            {
                Style.Primary => UiTheme.Accent,
                Style.Danger => UiTheme.Danger,
                Style.Tab => UiTheme.Violet,
                _ => UiTheme.AccentDeep
            };

            _baseColor = style switch
            {
                Style.Primary => AuroraUtils.Hex("#C2161FF0"),
                Style.Danger => AuroraUtils.Hex("#6E0A0FE6"),
                Style.Ghost => AuroraUtils.Hex("#00000000"),
                Style.Tab => AuroraUtils.Hex("#8E0F14E6"),
                _ => UiTheme.Row
            };

            // Свечение (для hover/press)
            Glow = UiKit.NewImage("Glow", container, UiTheme.Glow, _accent.WithAlpha(0f));
            Glow.rectTransform.sizeDelta = size * 1.25f;

            Background = UiKit.NewImage("Bg", container, UiTheme.RoundedSoft, _baseColor, Image.Type.Sliced);
            Background.rectTransform.sizeDelta = size;

            var border = UiKit.NewImage("Border", container, UiTheme.RoundedSoft, _accent.WithAlpha(0.28f), Image.Type.Sliced);
            border.rectTransform.sizeDelta = size + new Vector2(4f, 4f);

            if (!string.IsNullOrEmpty(icon))
            {
                IconImage = UiKit.NewImage("Icon", container, UiTheme.Icon(icon), UiTheme.Text.WithAlpha(0.92f));
                IconImage.rectTransform.sizeDelta = new Vector2(size.y * 0.48f, size.y * 0.48f);
                IconImage.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                IconImage.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                IconImage.rectTransform.pivot = new Vector2(0f, 0.5f);
                IconImage.rectTransform.anchoredPosition = new Vector2(20f, string.IsNullOrEmpty(subtitle) ? 0f : 6f);
            }

            float textLeft = string.IsNullOrEmpty(icon) ? 22f : 20f + size.y * 0.5f + 14f;

            Label = UiKit.NewText("Label", container, label, style == Style.Tab ? 26f : 30f, UiTheme.Text,
                TextAlignmentOptions.MidlineLeft);
            Label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            Label.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            Label.rectTransform.pivot = new Vector2(0f, 0.5f);
            Label.rectTransform.offsetMin = new Vector2(textLeft, string.IsNullOrEmpty(subtitle) ? -18f : 2f);
            Label.rectTransform.offsetMax = new Vector2(-18f, string.IsNullOrEmpty(subtitle) ? 18f : 42f);

            if (!string.IsNullOrEmpty(subtitle))
            {
                Subtitle = UiKit.NewText("Subtitle", container, subtitle, 22f, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
                Subtitle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                Subtitle.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                Subtitle.rectTransform.pivot = new Vector2(0f, 0.5f);
                Subtitle.rectTransform.offsetMin = new Vector2(textLeft, -34f);
                Subtitle.rectTransform.offsetMax = new Vector2(-18f, -4f);
            }

            // Коллайдер для выбора рукой.
            var collider = container.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 12f);
            collider.isTrigger = true;

            AuroraComponent.Attach(container.gameObject, "uitarget", new UiHitTargetHandler(this));
        }

        public void SetLabel(string label)
        {
            if (Label != null)
            {
                Label.text = label;
            }
        }

        public void SetColors(Color? background = null, Color? accent = null)
        {
            if (background.HasValue)
            {
                _baseColor = background.Value;
            }

            if (accent.HasValue)
            {
                _accent = accent.Value;
            }
        }

        public void SetInteractable(bool interactable)
        {
            Interactable = interactable;
            var c = Rect.GetComponent<Collider>();
            if (c != null)
            {
                c.enabled = interactable;
            }
        }

        /// <summary>Отметка «выбранного» пункта (для вкладок и текущей роли).</summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
        }

        public void SetHovered(bool hovered)
        {
            if (_hovered == hovered)
            {
                return;
            }

            _hovered = hovered;

            if (hovered && Interactable)
            {
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Hover);
            }
        }

        public void Press()
        {
            if (!Interactable)
            {
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                return;
            }

            _press = 1f;
            _pulse = 1f;
            AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Click);
            AuroraRuntime.Input?.Haptic(MarrowHandedness(), 0.25f, 0.06f);

            try
            {
                OnClick?.Invoke();
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "button click " + Label?.text);
            }
        }

        private static Il2CppSLZ.Marrow.Interaction.Handedness MarrowHandedness()
        {
            return Il2CppSLZ.Marrow.Interaction.Handedness.RIGHT;
        }

        public void Animate(float dt)
        {
            _press = Mathf.Max(0f, _press - dt * 5f);
            _pulse = Mathf.Max(0f, _pulse - dt * 2.2f);

            float target = (_hovered && Interactable) ? 1f : 0f;
            _hover = Mathf.Lerp(_hover, target, 1f - Mathf.Exp(-14f * dt));

            if (Background != null)
            {
                Color baseCol = Interactable ? _baseColor : _baseColor.WithAlpha(_baseColor.a * 0.45f);
                Color hoverCol = Color.Lerp(baseCol, UiTheme.RowHover, _hover * 0.85f);
                hoverCol = Color.Lerp(hoverCol, UiTheme.RowPressed, _press * 0.6f);

                if (_selected)
                {
                    hoverCol = Color.Lerp(hoverCol, _accent.WithAlpha(0.55f), 0.35f);
                }

                Background.color = hoverCol;
            }

            if (Glow != null)
            {
                float a = _hover * 0.35f + _press * 0.5f + _pulse * 0.4f + (_selected ? 0.18f : 0f);
                Glow.color = _accent.WithAlpha(Mathf.Clamp01(a));
            }

            float scale = 1f + _hover * 0.028f - _press * 0.045f;
            Rect.localScale = Vector3.Lerp(Rect.localScale, Vector3.one * scale, 1f - Mathf.Exp(-16f * dt));

            if (Label != null)
            {
                Color c = Interactable ? UiTheme.Text : UiTheme.TextDim.WithAlpha(0.55f);
                Label.color = Color.Lerp(c, Color.white, _hover * 0.8f);
            }

            if (IconImage != null)
            {
                IconImage.color = Color.Lerp(UiTheme.Text.WithAlpha(0.9f), _accent, _hover);
            }
        }
    }

    /// <summary>Обработчик выбора пункта меню (луч/палец правой руки).</summary>
    public sealed class UiHitTargetHandler
    {
        public AuroraButton Button { get; }

        public UiHitTargetHandler(AuroraButton button)
        {
            Button = button;
        }
    }

    /// <summary>Реестр всех кликабельных элементов — по нему бьёт указатель.</summary>
    public static class UiHitRegistry
    {
        private static readonly List<AuroraButton> Buttons = new List<AuroraButton>();
        private static readonly List<AuroraButton> ToRemove = new List<AuroraButton>();

        public static IReadOnlyList<AuroraButton> All => Buttons;

        public static void Register(AuroraButton button)
        {
            if (button != null && !Buttons.Contains(button))
            {
                Buttons.Add(button);
            }
        }

        public static void Unregister(AuroraComponent component)
        {
            if (component == null || component.Handler is not UiHitTargetHandler handler)
            {
                return;
            }

            ToRemove.Add(handler.Button);
        }

        /// <summary>Чистка удалённых элементов (вызывается каждый кадр меню).</summary>
        public static void Sweep()
        {
            for (int i = Buttons.Count - 1; i >= 0; i--)
            {
                var b = Buttons[i];
                if (b == null || b.Rect == null)
                {
                    Buttons.RemoveAt(i);
                }
            }

            ToRemove.Clear();
        }

        public static void Clear()
        {
            Buttons.Clear();
            ToRemove.Clear();
        }
    }
}
