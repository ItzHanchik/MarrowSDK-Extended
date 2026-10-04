using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AuroraRP
{
    /// <summary>
    /// Логика одной двери: таблички с двух сторон, покупка по B (в руке) и продажа по B×3.
    /// </summary>
    public class DoorBehaviour : IAuroraTickable, IAuroraDisposable
    {
        public string Hash { get; }
        public string Label { get; }
        public GameObject Root { get; }
        public DoorRecord Record { get; private set; }

        public bool IsOwned => Record != null && Record.IsOwned;

        /// <summary>Мировые границы двери — по ним хост понимает, что игрок без мода её держит.</summary>
        public Bounds WorldBounds
        {
            get
            {
                if (_boundsCached)
                {
                    return _bounds;
                }

                _bounds = _rootTransform != null
                    ? ComputeBounds(_rootTransform)
                    : new Bounds(Root != null ? Root.transform.position : Vector3.zero, Vector3.one);

                _boundsCached = true;
                return _bounds;
            }
        }

        private Bounds _bounds;
        private bool _boundsCached;
        public byte OwnerId => Record?.ownerId ?? 255;
        public string OwnerName => string.IsNullOrEmpty(Record?.ownerName) ? AuroraL.Get("common.nobody") : Record.ownerName;

        public string PriceLine => AuroraL.Get("door.price", AuroraUtils.Money(AuroraConfig.Current.doorPrice));

        public string FooterHint
        {
            get
            {
                if (IsOwned && OwnerId == AuroraRuntime.LocalId)
                {
                    return AuroraL.Get("door.howto.yours");
                }

                if (IsOwned)
                {
                    return AuroraL.Get("door.owned.by", OwnerName);
                }

                return AuroraL.Get("door.howto.buy");
            }
        }

        private readonly List<GameObject> _signs = new List<GameObject>();
        private readonly List<TextMeshProUGUI> _titleTexts = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _bodyTexts = new List<TextMeshProUGUI>();
        private readonly List<Image> _accentImages = new List<Image>();

        private int _sellPresses;
        private float _lastPressTime;
        private float _pulse;
        private Transform _rootTransform;

        public DoorBehaviour(GameObject root, string hash, string label, DoorRecord record)
        {
            Root = root;
            Hash = hash;
            Label = label;
            Record = record;
            _rootTransform = root != null ? root.transform : null;

            BuildSigns();
            Refresh();
        }

        // ------------------------------------------------------------------ таблички

        private void BuildSigns()
        {
            if (_rootTransform == null)
            {
                return;
            }

            Bounds bounds = ComputeBounds(_rootTransform);
            Vector3 forward = _rootTransform.forward;
            Vector3 right = _rootTransform.right;
            Vector3 up = Vector3.up;

            float width = Mathf.Clamp(bounds.size.magnitude * 0.45f, 0.45f, 1.1f);
            float height = width * 0.55f;

            Vector3 frontPos = bounds.center + right * 0f + up * (bounds.extents.y * 0.25f);
            Vector3 backPos = bounds.center;

            // Две стороны: спереди и сзади двери.
            CreateSign(frontPos + forward * (bounds.extents.z + 0.06f), Quaternion.LookRotation(forward, up), width, height, 0);
            CreateSign(backPos - forward * (bounds.extents.z + 0.06f), Quaternion.LookRotation(-forward, up), width, height, 1);
        }

        private static Bounds ComputeBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return new Bounds(root.position + Vector3.up, new Vector3(0.9f, 2f, 0.2f));
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.gameObject.name.StartsWith("AuroraRP_Sign", StringComparison.Ordinal))
                {
                    continue;
                }

                bounds.Encapsulate(r.bounds);
            }

            return bounds;
        }

        private void CreateSign(Vector3 worldPos, Quaternion rotation, float width, float height, int index)
        {
            try
            {
                var go = new GameObject("AuroraRP_Sign" + index);
                go.transform.SetParent(_rootTransform, true);
                go.transform.position = worldPos;
                go.transform.rotation = rotation;

                var canvas = UiKit.NewCanvas("Canvas", go.transform, new Vector2(512f, 256f), width / 512f, 4100 + index);
                var rect = canvas.GetComponent<RectTransform>();

                UiKit.Panel("Bg", rect, new Vector2(500f, 250f), UiTheme.Panel, 0.94f, true);

                var accent = UiKit.NewImage("Accent", rect, UiTheme.RoundedSoft, UiTheme.Accent, Image.Type.Sliced);
                accent.rectTransform.sizeDelta = new Vector2(480f, 8f);
                accent.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                accent.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                accent.rectTransform.pivot = new Vector2(0.5f, 1f);
                accent.rectTransform.anchoredPosition = new Vector2(0f, -20f);

                var title = UiKit.NewText("Title", rect, "", 52f, UiTheme.Text, TextAlignmentOptions.Midline);
                title.rectTransform.sizeDelta = new Vector2(470f, 70f);
                title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                title.rectTransform.pivot = new Vector2(0.5f, 1f);
                title.rectTransform.anchoredPosition = new Vector2(0f, -32f);
                title.fontStyle = FontStyles.Bold;

                var body = UiKit.NewText("Body", rect, "", 30f, UiTheme.TextDim, TextAlignmentOptions.Top);
                body.rectTransform.sizeDelta = new Vector2(470f, 150f);
                body.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                body.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                body.rectTransform.pivot = new Vector2(0.5f, 1f);
                body.rectTransform.anchoredPosition = new Vector2(0f, -104f);

                _signs.Add(go);
                _titleTexts.Add(title);
                _bodyTexts.Add(body);
                _accentImages.Add(accent);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "door sign");
            }
        }

        // ------------------------------------------------------------------ состояние

        public void ApplyRecord(DoorRecord record)
        {
            Record = record;
            Refresh();
        }

        public void Refresh()
        {
            bool owned = IsOwned;

            string title = owned
                ? AuroraL.Get("door.owned.by", AuroraUtils.Truncate(OwnerName, 22))
                : AuroraL.Get("door.for.sale");

            string body = owned
                ? PriceLine + "\n" + (OwnerId == AuroraRuntime.LocalId ? AuroraL.Get("door.howto.sell") : AuroraL.Get("door.help"))
                : PriceLine + "\n" + AuroraL.Get("door.howto.buy");

            Color accent = owned
                ? (OwnerId == AuroraRuntime.LocalId ? UiTheme.Success : RoleCatalog.ColorOf(AuroraRuntime.State.Get(OwnerId)?.Role ?? AuroraRoleId.Citizen))
                : UiTheme.Accent;

            for (int i = 0; i < _titleTexts.Count; i++)
            {
                _titleTexts[i].text = title;
                _bodyTexts[i].text = body;
                _accentImages[i].color = accent;
            }

            _pulse = 1f;

            if (Root != null)
            {
                Root.name = "Door_" + Label;
            }
        }

        public void Tick(AuroraComponent component, float dt)
        {
            _pulse = Mathf.Max(0f, _pulse - dt * 0.8f);

            for (int i = 0; i < _accentImages.Count; i++)
            {
                var img = _accentImages[i];
                if (img != null)
                {
                    img.color = Color.Lerp(img.color, img.color.WithAlpha(0.8f + 0.2f * _pulse), dt * 6f);
                }
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _signs.Count; i++)
            {
                if (_signs[i] != null)
                {
                    UnityEngine.Object.Destroy(_signs[i]);
                }
            }

            _signs.Clear();
            _titleTexts.Clear();
            _bodyTexts.Clear();
            _accentImages.Clear();
        }

        // ------------------------------------------------------------------ кнопка B

        /// <summary>Обработка нажатия B, когда дверь в руке. Возвращает true, если что-то произошло.</summary>
        public bool OnButtonB()
        {
            float now = Time.realtimeSinceStartup;
            float window = AuroraConfig.Current.doorSellPressWindow;

            if (now - _lastPressTime > window)
            {
                _sellPresses = 0;
            }

            _lastPressTime = now;
            _sellPresses++;

            if (IsOwned && OwnerId == AuroraRuntime.LocalId)
            {
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Click);

                if (_sellPresses >= AuroraConfig.Current.doorSellPresses)
                {
                    _sellPresses = 0;
                    AuroraRuntime.Doors.RequestSale(this);
                    return true;
                }

                AuroraNotifications.Send(
                    AuroraL.Get("door.sell.progress", _sellPresses),
                    UiTheme.Warning);

                return true;
            }

            // Покупка одним нажатием.
            _sellPresses = 0;
            AuroraRuntime.Doors.RequestPurchase(this);
            return true;
        }
    }
}
