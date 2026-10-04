using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Оформление меню: цвета, шрифт с кириллицей и все спрайты (скруглённые панели,
    /// градиенты, свечение, иконки). Всё генерируется кодом — паки ассетов не нужны.
    /// </summary>
    public static class UiTheme
    {
        // ------------------------------------------------------------------ цвета

        public static readonly Color Backdrop = AuroraUtils.Hex("#05070FEE");
        public static readonly Color Panel = AuroraUtils.Hex("#101A2EF2");
        public static readonly Color PanelLight = AuroraUtils.Hex("#17233DF5");
        public static readonly Color Row = AuroraUtils.Hex("#16223AED");
        public static readonly Color RowHover = AuroraUtils.Hex("#22375FF5");
        public static readonly Color RowPressed = AuroraUtils.Hex("#2E4C80FF");
        public static readonly Color Accent = AuroraUtils.Hex("#4DE1FF");
        public static readonly Color AccentDeep = AuroraUtils.Hex("#1F7BFF");
        public static readonly Color Violet = AuroraUtils.Hex("#8B5CF6");
        public static readonly Color Text = AuroraUtils.Hex("#EAF6FF");
        public static readonly Color TextDim = AuroraUtils.Hex("#93A7C4");
        public static readonly Color Success = AuroraUtils.Hex("#4ADE80");
        public static readonly Color Danger = AuroraUtils.Hex("#FF5C6C");
        public static readonly Color Warning = AuroraUtils.Hex("#FFC857");
        public static readonly Color Money = AuroraUtils.Hex("#B6FF9E");
        public static readonly Color Divider = AuroraUtils.Hex("#2A3A5CCC");

        // ------------------------------------------------------------------ размеры

        public const float PanelWidth = 620f;
        public const float PanelHeight = 760f;
        public const float Padding = 26f;
        public const float RowHeight = 84f;
        public const float TabWidth = 132f;
        public const float HeaderHeight = 128f;

        public static float Scale => AuroraConfig.Current.menuScale;

        // ------------------------------------------------------------------ шрифт

        private static TMP_FontAsset _font;

        /// <summary>
        /// Шрифт с поддержкой кириллицы. Сначала пробуем системный (Segoe UI/Arial),
        /// потом — шрифт игры, чтобы меню всё равно работало.
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null)
                {
                    return _font;
                }

                _font = TryCreateOsFont();
                if (_font != null)
                {
                    AuroraLog.Info("UI: используется системный шрифт (поддержка кириллицы)");
                    return _font;
                }

                _font = TryFindGameFont();
                if (_font != null)
                {
                    AuroraLog.Warn("UI: системный шрифт недоступен, берём шрифт игры — часть русских букв может не отображаться");
                    return _font;
                }

                AuroraLog.Error("UI: не удалось найти ни одного TMP-шрифта, тексты будут пустыми");
                return null;
            }
        }

        private static TMP_FontAsset TryCreateOsFont()
        {
            string[] candidates =
            {
                "Segoe UI", "Arial", "Tahoma", "Verdana", "Noto Sans", "DejaVu Sans", "Roboto", "Liberation Sans"
            };

            try
            {
                var osFont = Font.CreateDynamicFontFromOSFont(candidates, 64);
                if (osFont == null)
                {
                    // По одному имени (первое доступное).
                    foreach (var name in candidates)
                    {
                        osFont = Font.CreateDynamicFontFromOSFont(name, 64);
                        if (osFont != null)
                        {
                            break;
                        }
                    }
                }

                if (osFont == null)
                {
                    return null;
                }

                return TMP_FontAsset.CreateFontAsset(osFont);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "OS font");
                return null;
            }
        }

        private static TMP_FontAsset TryFindGameFont()
        {
            try
            {
                if (TMP_Settings.defaultFontAsset != null)
                {
                    return TMP_Settings.defaultFontAsset;
                }

                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (all != null && all.Length > 0)
                {
                    return all[0];
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "game font");
            }

            return null;
        }

        // ------------------------------------------------------------------ спрайты

        private static Sprite _roundedPanel;
        private static Sprite _roundedSoft;
        private static Sprite _circle;
        private static Sprite _glow;
        private static Sprite _gradient;
        private static Sprite _shadow;
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

        /// <summary>Скруглённая панель (9-slice, радиус 22 недоступен — используем 9-slice с border 24).</summary>
        public static Sprite RoundedPanel => _roundedPanel ??= BuildRounded("aurora_panel", 96, 24, 1f, Color.white);

        /// <summary>Сильно скруглённая «пилюля» (для кнопок).</summary>
        public static Sprite RoundedSoft => _roundedSoft ??= BuildRounded("aurora_soft", 96, 40, 1f, Color.white);

        public static Sprite Circle => _circle ??= BuildCircle("aurora_circle", 128);

        public static Sprite Glow => _glow ??= BuildGlow("aurora_glow", 128);

        public static Sprite Gradient => _gradient ??= BuildGradient("aurora_gradient", 256);

        public static Sprite Shadow => _shadow ??= BuildShadow("aurora_shadow", 128);

        /// <summary>Иконка по имени (рисуется кодом).</summary>
        public static Sprite Icon(string name)
        {
            if (Icons.TryGetValue(name, out var sprite))
            {
                return sprite;
            }

            sprite = IconPainter.Draw(name);
            Icons[name] = sprite;
            return sprite;
        }

        // ------------------------------------------------------- генерация текстур

        private static Sprite BuildRounded(string name, int size, int radius, float fill, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            radius = Mathf.Clamp(radius, 1, size / 2 - 1);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundedAlpha(x, y, size, radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * fill));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static float RoundedAlpha(int x, int y, int size, int radius)
        {
            // Suр-сэмплинг 2x2 для гладких краёв.
            float a = 0f;
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    float px = x + 0.25f + i * 0.5f;
                    float py = y + 0.25f + j * 0.5f;

                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));

                    a += d <= radius ? 1f : 0f;
                }
            }

            return a / 4f;
        }

        private static Sprite BuildCircle(string name, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            float r = size * 0.5f - 1f;
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float a = Mathf.Clamp01(r - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildGlow(string name, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            float center = size * 0.5f;
            float maxDist = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / maxDist;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildGradient(string name, int size)
        {
            var tex = new Texture2D(4, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                float k = y / (float)(size - 1);
                var c = new Color(1f, 1f, 1f, Mathf.Lerp(0.05f, 0.55f, k));
                for (int x = 0; x < 4; x++)
                {
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildShadow(string name, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float maxDist = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / maxDist;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 3f) * 0.75f;
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>Мини-«редактор» иконок: рисуем пиксельные глифы прямо в текстуру.</summary>
    internal static class IconPainter
    {
        private const int Size = 96;

        public static Sprite Draw(string name)
        {
            var painter = new Painter(Size);

            switch (name)
            {
                case "coin":
                    painter.Circle(48, 48, 34, 1f);
                    painter.Ring(48, 48, 34, 3f, Theme());
                    painter.Text("$", 48, 46);
                    break;
                case "wallet":
                    painter.RoundedRect(14, 24, 68, 48, 12);
                    painter.Rect(54, 40, 26, 16, 0f);
                    break;
                case "gun":
                    painter.Rect(16, 46, 56, 12);
                    painter.Rect(52, 32, 16, 16);
                    painter.Rect(30, 36, 14, 12, 0.55f);
                    painter.Rect(20, 34, 10, 26, 0.55f);
                    break;
                case "door":
                    painter.RoundedRect(26, 12, 44, 72, 8);
                    painter.Circle(60, 46, 5, 0f);
                    break;
                case "skull":
                    painter.Circle(48, 56, 26, 1f);
                    painter.Rect(34, 22, 28, 20, 1f);
                    painter.Circle(38, 58, 7, 0f);
                    painter.Circle(58, 58, 7, 0f);
                    break;
                case "star":
                    painter.Star(48, 48, 34, 14, 5);
                    break;
                case "gear":
                    painter.Circle(48, 48, 26, 1f);
                    painter.Circle(48, 48, 10, 0f);
                    for (int i = 0; i < 8; i++)
                    {
                        painter.Radial(48, 48, 34, 28, i * 45f, 14f);
                    }
                    break;
                case "users":
                    painter.Circle(36, 60, 16, 1f);
                    painter.RoundedRect(18, 22, 36, 30, 8);
                    painter.Circle(64, 62, 12, 0.65f);
                    painter.RoundedRect(50, 28, 30, 24, 8, 0.65f);
                    break;
                case "contract":
                    painter.RoundedRect(24, 10, 48, 76, 8);
                    painter.Rect(34, 68, 28, 5, 0f);
                    painter.Rect(34, 54, 28, 5, 0f);
                    painter.Rect(34, 40, 18, 5, 0f);
                    break;
                case "shield":
                    painter.Shield(48, 12, 76, 30);
                    break;
                case "info":
                    painter.Circle(48, 48, 34, 1f);
                    painter.Rect(44, 28, 8, 26, 0f);
                    painter.Circle(48, 64, 5, 0f);
                    break;
                case "cash":
                    painter.RoundedRect(10, 22, 76, 52, 10);
                    painter.Circle(48, 48, 16, 0f);
                    break;
                case "printer":
                    painter.RoundedRect(14, 30, 68, 40, 10);
                    painter.Rect(26, 20, 44, 12, 1f);
                    painter.Rect(30, 64, 36, 18, 1f);
                    break;
                default:
                    painter.Circle(48, 48, 30, 1f);
                    break;
            }

            var tex = painter.ToTexture("aurora_icon_" + name);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Color Theme() => new Color(0f, 0f, 0f, 0f);

        private sealed class Painter
        {
            private readonly float[] _alpha;
            private readonly int _size;

            public Painter(int size)
            {
                _size = size;
                _alpha = new float[size * size];
            }

            private void Blend(int x, int y, float a)
            {
                if (x < 0 || y < 0 || x >= _size || y >= _size)
                {
                    return;
                }

                int i = y * _size + x;
                _alpha[i] = Mathf.Max(_alpha[i], Mathf.Clamp01(a));
            }

            public void Rect(int x, int y, int w, int h, float value = 1f)
            {
                for (int py = y; py < y + h; py++)
                {
                    for (int px = x; px < x + w; px++)
                    {
                        Blend(px, py, value);
                    }
                }
            }

            public void RoundedRect(int x, int y, int w, int h, int radius, float value = 1f)
            {
                for (int py = y; py < y + h; py++)
                {
                    for (int px = x; px < x + w; px++)
                    {
                        float cx = Mathf.Clamp(px, x + radius, x + w - radius);
                        float cy = Mathf.Clamp(py, y + radius, y + h - radius);
                        float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                        if (d <= radius)
                        {
                            Blend(px, py, value);
                        }
                    }
                }
            }

            public void Circle(int cx, int cy, int radius, float value = 1f)
            {
                for (int py = cy - radius; py <= cy + radius; py++)
                {
                    for (int px = cx - radius; px <= cx + radius; px++)
                    {
                        float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                        float a = Mathf.Clamp01(radius - d);
                        Blend(px, py, a * value);
                    }
                }
            }

            public void Ring(int cx, int cy, int radius, float thickness, Color ignored)
            {
                for (int py = cy - radius; py <= cy + radius; py++)
                {
                    for (int px = cx - radius; px <= cx + radius; px++)
                    {
                        float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                        if (Mathf.Abs(d - radius) <= thickness)
                        {
                            Blend(px, py, Mathf.Clamp01(thickness - Mathf.Abs(d - radius)) * 0f);
                        }
                    }
                }
            }

            /// <summary>Луч из центра: используется для зубцов шестерёнки.</summary>
            public void Radial(int cx, int cy, float from, float to, float angle, float width)
            {
                float rad = angle * Mathf.Deg2Rad;
                int steps = Mathf.CeilToInt(from);
                int half = Mathf.CeilToInt(width * 0.5f);

                for (int i = (int)to; i <= steps; i++)
                {
                    int px = cx + Mathf.RoundToInt(Mathf.Cos(rad) * i);
                    int py = cy + Mathf.RoundToInt(Mathf.Sin(rad) * i);
                    for (int o = -half; o <= half; o++)
                    {
                        int ox = Mathf.RoundToInt(-Mathf.Sin(rad) * o);
                        int oy = Mathf.RoundToInt(Mathf.Cos(rad) * o);
                        Blend(px + ox, py + oy, 1f);
                    }
                }
            }

            public void Star(int cx, int cy, int outer, int inner, int points)
            {
                for (int i = 0; i < points; i++)
                {
                    float a1 = i * (360f / points);
                    float a2 = a1 + (360f / points) * 0.5f;
                    Line(cx + Mathf.Cos(a1 * Mathf.Deg2Rad) * outer, cy + Mathf.Sin(a1 * Mathf.Deg2Rad) * outer,
                         cx + Mathf.Cos(a2 * Mathf.Deg2Rad) * inner, cy + Mathf.Sin(a2 * Mathf.Deg2Rad) * inner, 4);
                }
            }

            public void Shield(int cx, int y, int height, int width)
            {
                for (int py = 0; py < height; py++)
                {
                    float k = py / (float)height;
                    int halfWidth = Mathf.RoundToInt(width * 0.5f * Mathf.Sqrt(Mathf.Clamp01(1f - k * k * 0.85f)));
                    for (int px = cx - halfWidth; px <= cx + halfWidth; px++)
                    {
                        Blend(px, y + py, 1f);
                    }
                }
            }

            public void Line(float x1, float y1, float x2, float y2, int thickness)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2))) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    int px = Mathf.RoundToInt(Mathf.Lerp(x1, x2, t));
                    int py = Mathf.RoundToInt(Mathf.Lerp(y1, y2, t));
                    for (int ox = -thickness; ox <= thickness; ox++)
                    {
                        for (int oy = -thickness; oy <= thickness; oy++)
                        {
                            if (ox * ox + oy * oy <= thickness * thickness)
                            {
                                Blend(px + ox, py + oy, 1f);
                            }
                        }
                    }
                }
            }

            public void Text(string text, int cx, int cy)
            {
                // Простая заглушка для символа «$» — рисуем двумя штрихами.
                if (text == "$")
                {
                    Line(cx + 8, cy - 16, cx - 6, cy - 2, 3);
                    Line(cx - 6, cy - 2, cx + 8, cy + 6, 3);
                    Line(cx + 8, cy + 6, cx - 8, cy + 18, 3);
                }
            }

            public Texture2D ToTexture(string name)
            {
                var tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;

                for (int y = 0; y < _size; y++)
                {
                    for (int x = 0; x < _size; x++)
                    {
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, _alpha[y * _size + x]));
                    }
                }

                tex.Apply();
                return tex;
            }
        }
    }
}
