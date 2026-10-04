using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AuroraRP
{
    /// <summary>
    /// Материалы, создаваемые в рантайме (для курсора и лазера указателя).
    /// </summary>
    public static class UiMaterials
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        private static readonly string[] UnlitShaders =
        {
            "Universal Render Pipeline/Unlit",
            "Unlit/Color",
            "Sprites/Default",
            "Legacy Shaders/Transparent/Diffuse"
        };

        private static readonly string[] TransparentShaders =
        {
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "Legacy Shaders/Particles/Alpha Blended Premultiply",
            "Unlit/Transparent"
        };

        public static Material Unlit(Color color)
        {
            string key = "unlit_" + ColorUtility.ToHtmlStringRGBA(color);
            if (Cache.TryGetValue(key, out var mat))
            {
                return mat;
            }

            mat = Create(UnlitShaders, color);
            Cache[key] = mat;
            return mat;
        }

        public static Material Transparent(Color color)
        {
            string key = "trans_" + ColorUtility.ToHtmlStringRGBA(color);
            if (Cache.TryGetValue(key, out var mat))
            {
                return mat;
            }

            mat = Create(TransparentShaders, color);
            Cache[key] = mat;
            return mat;
        }

        private static Material Create(string[] shaderNames, Color color)
        {
            foreach (var name in shaderNames)
            {
                try
                {
                    var shader = Shader.Find(name);
                    if (shader == null)
                    {
                        continue;
                    }

                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", color);
                    }

                    if (mat.HasProperty("_Color"))
                    {
                        mat.SetColor("_Color", color);
                    }

                    mat.color = color;
                    return mat;
                }
                catch (Exception)
                {
                    // пробуем следующий шейдер
                }
            }

            AuroraLog.Warn("Не удалось создать материал для UI-курсора (шейдеры не найдены)");
            return null;
        }
    }

    /// <summary>
    /// Указатель правой руки: лазер + «палец». Наводится на кнопки, нажимает их
    /// триггером или прямым тычком (как в ТЗ: «правой рукой выбирать»).
    /// </summary>
    public class UiPointer
    {
        private readonly Transform _root;
        private readonly LineRenderer _laser;
        private readonly Transform _cursor;
        private readonly MeshRenderer _cursorRenderer;

        private AuroraButton _hovered;
        private AuroraButton _pressed;
        private float _hoverStart;
        private float _pokeDistance;

        public bool Visible { get; private set; }

        public UiPointer(Transform parent)
        {
            var rootGo = new GameObject("AuroraRP_Pointer");
            _root = rootGo.transform;
            _root.SetParent(parent, false);

            _laser = rootGo.AddComponent<LineRenderer>();
            _laser.widthMultiplier = 0.004f;
            _laser.positionCount = 2;
            _laser.useWorldSpace = true;
            _laser.textureMode = LineTextureMode.Stretch;
            _laser.numCapVertices = 4;
            var mat = UiMaterials.Transparent(UiTheme.Accent.WithAlpha(0.85f));
            if (mat != null)
            {
                _laser.material = mat;
            }

            _laser.startColor = UiTheme.Accent.WithAlpha(0.9f);
            _laser.endColor = UiTheme.Accent.WithAlpha(0.15f);
            _laser.enabled = false;

            var cursorGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cursorGo.name = "AuroraRP_Cursor";
            UnityEngine.Object.Destroy(cursorGo.GetComponent<Collider>());
            cursorGo.transform.SetParent(_root, false);
            cursorGo.transform.localScale = Vector3.one * 0.014f;
            _cursor = cursorGo.transform;
            _cursorRenderer = cursorGo.GetComponent<MeshRenderer>();

            var cursorMat = UiMaterials.Unlit(UiTheme.Accent);
            if (cursorMat != null)
            {
                _cursorRenderer.material = cursorMat;
            }

            cursorGo.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;

            if (_laser != null)
            {
                _laser.enabled = visible;
            }

            if (_cursor != null)
            {
                _cursor.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                SetHover(null);
            }
        }

        public void Tick(float dt, RectTransform menuRect)
        {
            if (!Visible || menuRect == null)
            {
                return;
            }

            if (!TryGetPointer(out Vector3 origin, out Vector3 direction))
            {
                return;
            }

            // 1) Луч по кнопкам
            AuroraButton hit = Raycast(origin, direction, 2.5f, out float distance);

            // 2) Тычок пальцем: если рука совсем рядом с кнопкой
            if (hit == null)
            {
                hit = Poke(origin, out distance);
                _pokeDistance = distance;
            }

            SetHover(hit);

            Vector3 end = hit != null
                ? origin + direction * distance
                : origin + direction * 1.2f;

            if (_laser != null)
            {
                _laser.SetPosition(0, origin);
                _laser.SetPosition(1, end);
                _laser.startColor = (_hovered != null ? UiTheme.Accent : UiTheme.AccentDeep).WithAlpha(0.85f);
                _laser.endColor = UiTheme.Accent.WithAlpha(0.1f);
            }

            if (_cursor != null)
            {
                _cursor.position = end;
                float scale = _hovered != null ? 0.019f : 0.013f;
                _cursor.localScale = Vector3.Lerp(_cursor.localScale, Vector3.one * scale, 1f - Mathf.Exp(-18f * dt));
            }

            // Нажатие триггером по наведённой кнопке
            if (_hovered != null && IsTriggerDown())
            {
                if (_pressed != _hovered)
                {
                    _pressed = _hovered;
                    _hovered.Press();
                    AuroraRuntime.Input?.Haptic(RightHandedness(), 0.4f, 0.08f);
                }
            }
            else
            {
                _pressed = null;
            }

            // Тычок (рука вплотную) — нажатие с задержкой 0.18 с, чтобы не срабатывало случайно
            if (_hovered != null && _pokeDistance < 0.055f && _hovered != _pressed)
            {
                if (Time.realtimeSinceStartup - _hoverStart > 0.18f)
                {
                    _pressed = _hovered;
                    _hovered.Press();
                    AuroraRuntime.Input?.Haptic(RightHandedness(), 0.35f, 0.07f);
                }
            }
        }

        private static AuroraButton Raycast(Vector3 origin, Vector3 direction, float maxDistance, out float distance)
        {
            distance = maxDistance;
            AuroraButton best = null;

            var ray = new Ray(origin, direction);
            var buttons = UiHitRegistry.All;

            for (int i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                if (button == null || !button.Interactable || button.Rect == null)
                {
                    continue;
                }

                var collider = button.Rect.GetComponent<Collider>();
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (collider.Raycast(ray, out var hit, maxDistance) && hit.distance < distance)
                {
                    distance = hit.distance;
                    best = button;
                }
            }

            return best;
        }

        /// <summary>Тычок пальцем: ищем ближайшую кнопку в радиусе 6 см от кончика пальца.</summary>
        private static AuroraButton Poke(Vector3 fingerPosition, out float distance)
        {
            distance = float.MaxValue;
            AuroraButton best = null;

            var buttons = UiHitRegistry.All;
            for (int i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                if (button == null || !button.Interactable || button.Rect == null)
                {
                    continue;
                }

                var collider = button.Rect.GetComponent<Collider>();
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                Vector3 closest = collider.ClosestPoint(fingerPosition);
                float d = Vector3.Distance(closest, fingerPosition);
                if (d < 0.06f && d < distance)
                {
                    distance = d;
                    best = button;
                }
            }

            return best;
        }

        private void SetHover(AuroraButton button)
        {
            if (_hovered == button)
            {
                return;
            }

            _hovered?.SetHovered(false);
            _hovered = button;
            _hoverStart = Time.realtimeSinceStartup;
            _pressed = null;

            if (_hovered != null)
            {
                _hovered.SetHovered(true);
                AuroraRuntime.Input?.Haptic(RightHandedness(), 0.12f, 0.04f);
            }
        }

        // ------------------------------------------------------------- источники

        private static bool TryGetPointer(out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            var hand = BoneLib.Player.RightHand;
            if (hand != null)
            {
                // Кончик указательного пальца ≈ ладонь + вперёд.
                origin = hand.transform.position + hand.transform.forward * 0.075f;
                direction = hand.transform.forward;
                return true;
            }

            var controller = BoneLib.Player.RightController;
            if (controller != null)
            {
                origin = controller.transform.position;
                direction = controller.transform.forward;
                return true;
            }

            return false;
        }

        private static bool IsTriggerDown()
        {
            var hand = BoneLib.Player.RightHand;
            return hand != null && hand.GetIndexButtonDown();
        }

        private static Il2CppSLZ.Marrow.Interaction.Handedness RightHandedness()
        {
            return Il2CppSLZ.Marrow.Interaction.Handedness.RIGHT;
        }
    }
}
