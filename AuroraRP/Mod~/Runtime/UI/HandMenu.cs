using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AuroraRP
{
    public enum MenuPage
    {
        Main = 0,
        Roles = 1,
        Wallet = 2,
        Shop = 3,
        Doors = 4,
        Contracts = 5,
        Settings = 6
    }

    /// <summary>
    /// Главное меню мода: красивая голографическая панель, появляющаяся над рукой.
    /// Открывается двойным щелчком обоих триггеров, выбирается правой рукой.
    /// </summary>
    public class HandMenu
    {
        public bool IsOpen { get; private set; }

        public MenuPage CurrentPage { get; private set; } = MenuPage.Main;

        private GameObject _rootGo;
        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _panel;
        private RectTransform _header;
        private RectTransform _tabBar;
        private RectTransform _content;
        private RectTransform _footer;
        private RectTransform _toastArea;
        private RectTransform _hud;

        private TextMeshProUGUI _headerTitle;
        private TextMeshProUGUI _headerStatus;
        private TextMeshProUGUI _headerBalance;
        private TextMeshProUGUI _footerText;
        private Image _accentBar;
        private Image _hudBalance;
        private Image _hudProgressFill;
        private AuroraBar _hudBar;
        private TextMeshProUGUI _hudHint;

        private readonly List<AuroraButton> _tabs = new List<AuroraButton>();
        private readonly List<AuroraButton> _liveElements = new List<AuroraButton>();
        private readonly List<TextMeshProUGUI> _liveTexts = new List<TextMeshProUGUI>();
        private readonly List<Image> _toastBars = new List<Image>();
        private readonly List<TextMeshProUGUI> _toastTexts = new List<TextMeshProUGUI>();

        private UiPointer _pointer;
        private float _openK;
        private float _layoutCursor;
        private float _contentHeight;
        private float _hintTimer;

        private int _shopPage;
        private int _rolePage;
        private int _playerPage;
        private int _contractPage;
        private int _doorPage;

        private Transform _handAnchor;

        private const int MaxRowsPerView = 5;

        private List<AuroraButton> _pageElements = new List<AuroraButton>();

        // ------------------------------------------------------------ жизненный цикл

        public void Initialize()
        {
            if (_rootGo != null)
            {
                return;
            }

            var hand = BoneLib.Player.LeftHand;
            Transform parent = hand != null ? hand.transform : (AuroraDriver.Instance != null ? AuroraDriver.Instance.transform : null);

            if (parent == null)
            {
                AuroraLog.Warn("Меню: рука игрока ещё не найдена, отложим создание");
                return;
            }

            _handAnchor = parent;

            _rootGo = new GameObject("AuroraRP_Menu");
            _rootGo.transform.SetParent(parent, false);

            _canvas = UiKit.NewCanvas("Canvas", _rootGo.transform, new Vector2(UiTheme.PanelWidth, UiTheme.PanelHeight), 0.0011f * UiTheme.Scale);
            _group = _canvas.GetComponent<CanvasGroup>();
            _panel = _canvas.GetComponent<RectTransform>();

            BuildPanel();
            BuildTabs();
            BuildHud();

            _pointer = new UiPointer(_rootGo.transform);

            _openK = 0f;
            _panel.localScale = Vector3.one * 0.01f;
            _group.alpha = 0f;

            if (AuroraRuntime.Input != null)
            {
                AuroraRuntime.Input.OnMenuGesture += Toggle;
                AuroraRuntime.Input.OnXPressed += OpenTransfer;
            }

            var state = AuroraRuntime.State;
            if (state != null)
            {
                state.OnChanged += MarkDirty;
            }

            RefreshPage();
            AuroraLog.Info("Меню создано");
        }

        public void Shutdown()
        {
            if (AuroraRuntime.Input != null)
            {
                AuroraRuntime.Input.OnMenuGesture -= Toggle;
                AuroraRuntime.Input.OnXPressed -= OpenTransfer;
            }

            var state = AuroraRuntime.State;
            if (state != null)
            {
                state.OnChanged -= MarkDirty;
            }

            if (_rootGo != null)
            {
                UnityEngine.Object.Destroy(_rootGo);
                _rootGo = null;
            }

            UiHitRegistry.Clear();
        }

        private bool _dirty = true;

        public void MarkDirty()
        {
            _dirty = true;
        }

        // ------------------------------------------------------------------ сборка

        private void BuildPanel()
        {
            // Тень и фон
            UiKit.Panel("Backdrop", _panel, new Vector2(UiTheme.PanelWidth, UiTheme.PanelHeight), UiTheme.Backdrop, 1f, true);
            var core = UiKit.Panel("Core", _panel, new Vector2(UiTheme.PanelWidth, UiTheme.PanelHeight), UiTheme.Panel, 1f, false);

            // Скин меню из палета (MenuSkin): если художник его положил — используем.
            var skin = AuroraVisuals.GetSprite("MenuSkin");
            if (skin != null)
            {
                core.sprite = skin;
                core.type = Image.Type.Sliced;
                core.color = new Color(1f, 1f, 1f, 0.98f);
            }

            // Верхний градиентный блик
            var gradient = UiKit.NewImage("Gradient", core.rectTransform, UiTheme.Gradient, UiTheme.Accent.WithAlpha(0.14f));
            gradient.rectTransform.sizeDelta = new Vector2(UiTheme.PanelWidth, UiTheme.PanelHeight * 0.5f);
            gradient.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            gradient.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            gradient.rectTransform.pivot = new Vector2(0.5f, 1f);
            gradient.rectTransform.anchoredPosition = Vector2.zero;

            // Акцентная линия слева
            _accentBar = UiKit.AccentLine(core.rectTransform, new Vector2(6f, UiTheme.PanelHeight - 40f), UiTheme.Accent);
            _accentBar.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _accentBar.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _accentBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            _accentBar.rectTransform.anchoredPosition = new Vector2(10f, 0f);

            BuildHeader(core.rectTransform);
            BuildContent(core.rectTransform);
            BuildFooter(core.rectTransform);

            _canvas.transform.localPosition = new Vector3(0.12f, 0.30f, 0.10f);
        }

        private void BuildHeader(RectTransform core)
        {
            _header = UiKit.NewRect("Header", core);
            _header.sizeDelta = new Vector2(UiTheme.PanelWidth - 40f, UiTheme.HeaderHeight);
            _header.anchorMin = new Vector2(0.5f, 1f);
            _header.anchorMax = new Vector2(0.5f, 1f);
            _header.pivot = new Vector2(0.5f, 1f);
            _header.anchoredPosition = new Vector2(0f, -14f);

            UiKit.Panel("HeaderBg", _header, new Vector2(UiTheme.PanelWidth - 40f, UiTheme.HeaderHeight), UiTheme.PanelLight, 1f, false);

            var logo = UiKit.NewImage("Logo", _header, UiTheme.Icon("coin"), UiTheme.Accent);
            logo.rectTransform.sizeDelta = new Vector2(58f, 58f);
            logo.rectTransform.anchorMin = new Vector2(0f, 1f);
            logo.rectTransform.anchorMax = new Vector2(0f, 1f);
            logo.rectTransform.pivot = new Vector2(0f, 1f);
            logo.rectTransform.anchoredPosition = new Vector2(18f, -18f);

            _headerTitle = UiKit.NewText("Title", _header, AuroraL.Get("menu.title"), 44f, UiTheme.Text);
            _headerTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _headerTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _headerTitle.rectTransform.pivot = new Vector2(0f, 1f);
            _headerTitle.rectTransform.offsetMin = new Vector2(88f, 0f);
            _headerTitle.rectTransform.offsetMax = new Vector2(-18f, 0f);
            _headerTitle.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            _headerTitle.enableWordWrapping = false;
            _headerTitle.fontStyle = FontStyles.Bold;

            _headerStatus = UiKit.NewText("Status", _header, "", 24f, UiTheme.TextDim);
            _headerStatus.rectTransform.anchorMin = new Vector2(0f, 1f);
            _headerStatus.rectTransform.anchorMax = new Vector2(1f, 1f);
            _headerStatus.rectTransform.pivot = new Vector2(0f, 1f);
            _headerStatus.rectTransform.offsetMin = new Vector2(88f, 0f);
            _headerStatus.rectTransform.offsetMax = new Vector2(-18f, 0f);
            _headerStatus.rectTransform.anchoredPosition = new Vector2(0f, -64f);

            _headerBalance = UiKit.NewText("Balance", _header, "", 34f, UiTheme.Money, TextAlignmentOptions.MidlineRight);
            _headerBalance.rectTransform.anchorMin = new Vector2(1f, 0f);
            _headerBalance.rectTransform.anchorMax = new Vector2(1f, 0f);
            _headerBalance.rectTransform.pivot = new Vector2(1f, 0f);
            _headerBalance.rectTransform.sizeDelta = new Vector2(320f, 40f);
            _headerBalance.rectTransform.anchoredPosition = new Vector2(-18f, 10f);

            _toastArea = UiKit.NewRect("Toasts", _header);
            _toastArea.sizeDelta = new Vector2(UiTheme.PanelWidth - 60f, 60f);
            _toastArea.anchorMin = new Vector2(0.5f, 0f);
            _toastArea.anchorMax = new Vector2(0.5f, 0f);
            _toastArea.pivot = new Vector2(0.5f, 0f);
            _toastArea.anchoredPosition = new Vector2(0f, 6f);

            for (int i = 0; i < 2; i++)
            {
                var bar = UiKit.NewImage("Toast" + i, _toastArea, UiTheme.RoundedSoft, UiTheme.PanelLight, Image.Type.Sliced);
                bar.rectTransform.sizeDelta = new Vector2(UiTheme.PanelWidth - 60f, 44f);
                bar.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                bar.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                bar.rectTransform.pivot = new Vector2(0.5f, 1f);
                bar.rectTransform.anchoredPosition = new Vector2(0f, -i * 48f);

                var text = UiKit.NewText("ToastText" + i, bar.rectTransform, "", 22f, UiTheme.Text, TextAlignmentOptions.MidlineLeft);
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(14f, 0f);
                text.rectTransform.offsetMax = new Vector2(-14f, 0f);

                _toastBars.Add(bar);
                _toastTexts.Add(text);
            }
        }

        private void BuildContent(RectTransform core)
        {
            _content = UiKit.NewRect("Content", core);
            _content.sizeDelta = new Vector2(UiTheme.PanelWidth - UiTheme.TabWidth - 54f, UiTheme.PanelHeight - UiTheme.HeaderHeight - 100f);
            _content.anchorMin = new Vector2(1f, 0.5f);
            _content.anchorMax = new Vector2(1f, 0.5f);
            _content.pivot = new Vector2(1f, 0.5f);
            _content.anchoredPosition = new Vector2(-26f, -22f);
        }

        private void BuildFooter(RectTransform core)
        {
            _footer = UiKit.NewRect("Footer", core);
            _footer.sizeDelta = new Vector2(UiTheme.PanelWidth - 40f, 46f);
            _footer.anchorMin = new Vector2(0.5f, 0f);
            _footer.anchorMax = new Vector2(0.5f, 0f);
            _footer.pivot = new Vector2(0.5f, 0f);
            _footer.anchoredPosition = new Vector2(0f, 12f);

            _footerText = UiKit.NewText("FooterText", _footer, AuroraL.Get("menu.hint.open"), 20f, UiTheme.TextDim, TextAlignmentOptions.MidlineCenter);
            _footerText.rectTransform.anchorMin = Vector2.zero;
            _footerText.rectTransform.anchorMax = Vector2.one;
            _footerText.rectTransform.offsetMin = Vector2.zero;
            _footerText.rectTransform.offsetMax = Vector2.zero;
        }

        private void BuildTabs()
        {
            _tabBar = UiKit.NewRect("Tabs", _panel);
            _tabBar.sizeDelta = new Vector2(UiTheme.TabWidth, UiTheme.PanelHeight - UiTheme.HeaderHeight - 110f);
            _tabBar.anchorMin = new Vector2(0f, 0.5f);
            _tabBar.anchorMax = new Vector2(0f, 0.5f);
            _tabBar.pivot = new Vector2(0f, 0.5f);
            _tabBar.anchoredPosition = new Vector2(26f, -24f);

            AddTab(MenuPage.Main, AuroraL.Get("menu.tab.main"), "info");
            AddTab(MenuPage.Roles, AuroraL.Get("menu.tab.roles"), "users");
            AddTab(MenuPage.Wallet, AuroraL.Get("menu.tab.wallet"), "wallet");
            AddTab(MenuPage.Shop, AuroraL.Get("menu.tab.shop"), "gun");
            AddTab(MenuPage.Doors, AuroraL.Get("menu.tab.doors"), "door");
            AddTab(MenuPage.Contracts, AuroraL.Get("menu.tab.contracts"), "contract");
            AddTab(MenuPage.Settings, AuroraL.Get("menu.tab.settings"), "gear");
        }

        private void AddTab(MenuPage page, string label, string icon)
        {
            float y = -_tabs.Count * 96f;
            var button = UiKit.Button(_tabBar, label, icon, new Vector2(UiTheme.TabWidth, 88f), () => SwitchPage(page), AuroraButton.Style.Tab);
            button.Rect.anchorMin = new Vector2(0.5f, 1f);
            button.Rect.anchorMax = new Vector2(0.5f, 1f);
            button.Rect.pivot = new Vector2(0.5f, 1f);
            button.Rect.anchoredPosition = new Vector2(0f, y);
            button.Payload = page;
            _tabs.Add(button);
            _liveElements.Add(button);
        }

        private void BuildHud()
        {
            if (!AuroraConfig.Current.wristHud)
            {
                return;
            }

            _hud = UiKit.NewRect("Hud", _panel);
            _hud.sizeDelta = new Vector2(320f, 92f);
            _hud.anchorMin = new Vector2(0.5f, 0f);
            _hud.anchorMax = new Vector2(0.5f, 0f);
            _hud.pivot = new Vector2(0.5f, 0f);
            _hud.anchoredPosition = new Vector2(0f, -108f);

            UiKit.Panel("HudBg", _hud, new Vector2(320f, 92f), UiTheme.Panel, 0.95f, true);

            _hudBalance = UiKit.NewImage("HudIcon", _hud, UiTheme.Icon("cash"), UiTheme.Money);
            _hudBalance.rectTransform.sizeDelta = new Vector2(46f, 46f);
            _hudBalance.rectTransform.anchorMin = new Vector2(0f, 1f);
            _hudBalance.rectTransform.anchorMax = new Vector2(0f, 1f);
            _hudBalance.rectTransform.pivot = new Vector2(0f, 1f);
            _hudBalance.rectTransform.anchoredPosition = new Vector2(14f, -14f);

            _hudHint = UiKit.NewText("HudHint", _hud, "", 26f, UiTheme.Text, TextAlignmentOptions.MidlineLeft);
            _hudHint.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _hudHint.rectTransform.anchorMax = new Vector2(1f, 1f);
            _hudHint.rectTransform.offsetMin = new Vector2(72f, 0f);
            _hudHint.rectTransform.offsetMax = new Vector2(-12f, -8f);

            var barBg = UiKit.NewImage("HudBarBg", _hud, UiTheme.RoundedSoft, AuroraUtils.Hex("#0A1226CC"), Image.Type.Sliced);
            barBg.rectTransform.sizeDelta = new Vector2(292f, 18f);
            barBg.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            barBg.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            barBg.rectTransform.pivot = new Vector2(0.5f, 0f);
            barBg.rectTransform.anchoredPosition = new Vector2(0f, 14f);

            _hudProgressFill = UiKit.NewImage("HudBarFill", barBg.rectTransform, UiTheme.RoundedSoft, UiTheme.Accent, Image.Type.Sliced);
            _hudProgressFill.rectTransform.sizeDelta = new Vector2(286f, 12f);
            _hudProgressFill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _hudProgressFill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _hudProgressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _hudProgressFill.rectTransform.anchoredPosition = new Vector2(3f, 0f);

            _hudBar = new AuroraBar(_hudProgressFill, 286f);
        }

        // ------------------------------------------------------------------ страницы

        private void SwitchPage(MenuPage page)
        {
            CurrentPage = page;
            _shopPage = 0;
            _rolePage = 0;
            _playerPage = 0;
            _contractPage = 0;
            _doorPage = 0;
            RefreshPage();
        }

        private void RefreshPage()
        {
            if (_content == null)
            {
                return;
            }

            // Чистим прошлую страницу
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            }

            _pageElements.Clear();
            _liveElements.RemoveAll(b => b.Rect == null || b.Rect.parent == _content);
            _layoutCursor = 0f;

            switch (CurrentPage)
            {
                case MenuPage.Main:
                    BuildMainPage();
                    break;
                case MenuPage.Roles:
                    BuildRolesPage();
                    break;
                case MenuPage.Wallet:
                    BuildWalletPage();
                    break;
                case MenuPage.Shop:
                    BuildShopPage();
                    break;
                case MenuPage.Doors:
                    BuildDoorsPage();
                    break;
                case MenuPage.Contracts:
                    BuildContractsPage();
                    break;
                case MenuPage.Settings:
                    BuildSettingsPage();
                    break;
            }

            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].SetSelected(_tabs[i].Payload is MenuPage p && p == CurrentPage);
            }

            _dirty = false;
        }

        private void OnPageButton(AuroraButton button)
        {
            if (button != null && button.Rect != null && button.Rect.parent == _content)
            {
                _pageElements.Add(button);
                _liveElements.Add(button);
            }
        }

        // ------------------------------------------------------------- элементы

        internal AuroraButton AddRow(string label, string icon, string subtitle, Action onClick,
            AuroraButton.Style style = AuroraButton.Style.Default, bool interactable = true, bool selected = false)
        {
            var button = UiKit.Button(_content, label, icon, new Vector2(_content.sizeDelta.x, UiTheme.RowHeight), onClick, style, subtitle);
            PlaceRow(button.Rect, UiTheme.RowHeight);
            button.SetInteractable(interactable);
            button.SetSelected(selected);
            OnPageButton(button);
            return button;
        }

        internal TextMeshProUGUI AddSection(string text, Color color)
        {
            var tmp = UiKit.NewText("Section_" + text, _content, text, 24f, color.WithAlpha(0.85f));
            tmp.rectTransform.sizeDelta = new Vector2(_content.sizeDelta.x, 40f);
            tmp.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            tmp.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            tmp.rectTransform.pivot = new Vector2(0.5f, 1f);
            tmp.rectTransform.anchoredPosition = new Vector2(0f, -_layoutCursor);
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 6f;

            _layoutCursor += 44f;
            return tmp;
        }

        internal TextMeshProUGUI AddInfo(string text, Color color, float height = 42f)
        {
            var tmp = UiKit.NewText("Info", _content, text, 22f, color);
            tmp.rectTransform.sizeDelta = new Vector2(_content.sizeDelta.x, height);
            tmp.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            tmp.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            tmp.rectTransform.pivot = new Vector2(0.5f, 1f);
            tmp.rectTransform.anchoredPosition = new Vector2(0f, -_layoutCursor);

            _layoutCursor += height + 6f;
            return tmp;
        }

        internal void AddSpacer(float height = 16f)
        {
            _layoutCursor += height;
        }

        /// <summary>Две кнопки в одну строку (например, «+100» и «-100»).</summary>
        internal void AddPairRow(string leftLabel, Action leftAction, string rightLabel, Action rightAction,
            string icon = null, bool leftInteractable = true, bool rightInteractable = true)
        {
            float width = _content.sizeDelta.x;
            float half = (width - 12f) * 0.5f;

            float savedCursor = _layoutCursor;

            var left = UiKit.Button(_content, leftLabel, icon, new Vector2(half, UiTheme.RowHeight), leftAction);
            var right = UiKit.Button(_content, rightLabel, icon, new Vector2(half, UiTheme.RowHeight), rightAction);

            left.Rect.anchorMin = new Vector2(0.5f, 1f);
            left.Rect.anchorMax = new Vector2(0.5f, 1f);
            left.Rect.pivot = new Vector2(0.5f, 1f);
            left.Rect.anchoredPosition = new Vector2(-(half * 0.5f + 3f), -savedCursor);

            right.Rect.anchorMin = new Vector2(0.5f, 1f);
            right.Rect.anchorMax = new Vector2(0.5f, 1f);
            right.Rect.pivot = new Vector2(0.5f, 1f);
            right.Rect.anchoredPosition = new Vector2(half * 0.5f + 3f, -savedCursor);

            left.SetInteractable(leftInteractable);
            right.SetInteractable(rightInteractable);

            left.Label.fontSize = 26f;
            right.Label.fontSize = 26f;

            OnPageButton(left);
            OnPageButton(right);

            _layoutCursor += UiTheme.RowHeight + 12f;
        }

        /// <summary>Пересчитывает масштаб канваса (изменение размера меню в настройках).</summary>
        public void ApplyScale()
        {
            if (_canvas != null)
            {
                _canvas.transform.localScale = Vector3.one * (0.0011f * UiTheme.Scale);
            }
        }

        internal void AddProgress(string caption, float value, Color color, out AuroraBar bar)
        {
            var bg = UiKit.NewImage("ProgBg", _content, UiTheme.RoundedSoft, AuroraUtils.Hex("#0A1226CC"), Image.Type.Sliced);
            bg.rectTransform.sizeDelta = new Vector2(_content.sizeDelta.x, 22f);
            bg.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            bg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            bg.rectTransform.pivot = new Vector2(0.5f, 1f);
            bg.rectTransform.anchoredPosition = new Vector2(0f, -_layoutCursor);

            var fill = UiKit.NewImage("ProgFill", bg.rectTransform, UiTheme.RoundedSoft, color, Image.Type.Sliced);
            fill.rectTransform.sizeDelta = new Vector2(_content.sizeDelta.x - 6f, 16f);
            fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(3f, 0f);

            bar = new AuroraBar(fill, _content.sizeDelta.x - 6f);
            bar.SetValue(value);

            _layoutCursor += 30f;
        }

        private void PlaceRow(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -_layoutCursor);
            _layoutCursor += height + 12f;
        }

        internal float RemainingSpace => _content.sizeDelta.y - _layoutCursor;

        internal bool HasRoom(float needed = 100f) => _content.sizeDelta.y - _layoutCursor > needed;

        // ------------------------------------------------------------------ тик

        public void Tick(float dt)
        {
            if (_rootGo == null)
            {
                if (GameHooks.PlayerReady)
                {
                    Initialize();
                }

                return;
            }

            AuroraNotifications.Tick();
            UiHitRegistry.Sweep();

            bool shouldOpen = IsOpen;
            _openK = Mathf.Lerp(_openK, shouldOpen ? 1f : 0f, 1f - Mathf.Exp(-11f * dt));

            UpdateTransform(dt);
            UpdateHeaderAndHud();
            UpdateToasts();

            if (_dirty && _openK > 0.5f && !IsBusyWithPoke())
            {
                RefreshPage();
            }

            float k = Mathf.SmoothStep(0f, 1f, _openK);
            _group.alpha = k;
            _panel.localScale = Vector3.one * (0.65f + 0.35f * k);

            _pointer?.SetVisible(k > 0.35f);
            _pointer?.Tick(dt, _panel);

            for (int i = 0; i < _liveElements.Count; i++)
            {
                _liveElements[i]?.Animate(dt);
            }

            if (_content != null)
            {
                for (int i = 0; i < _content.childCount; i++)
                {
                    var child = _content.GetChild(i);
                    child.localScale = Vector3.one;
                }
            }

            // Подсказка внизу меняется по ситуации
            _hintTimer += dt;
            if (_hintTimer > 0.5f)
            {
                _hintTimer = 0f;
                UpdateFooterHint();
            }
        }

        private bool IsBusyWithPoke()
        {
            var transfers = AuroraRuntime.Transfers;
            return transfers != null && transfers.IsHolding;
        }

        private void UpdateTransform(float dt)
        {
            var hand = BoneLib.Player.LeftHand;
            if (hand != null && _handAnchor != hand.transform)
            {
                // Игрок пересоздался (новый уровень) — переносим меню на новую руку.
                _rootGo.transform.SetParent(hand.transform, false);
                _handAnchor = hand.transform;
                _canvas.transform.localPosition = new Vector3(0.12f, 0.30f, 0.10f);
            }

            var head = BoneLib.Player.Head;
            if (head != null)
            {
                Vector3 toHead = head.position - _canvas.transform.position;
                toHead.y *= 0.35f;

                if (toHead.sqrMagnitude > 0.0004f)
                {
                    Quaternion target = Quaternion.LookRotation(-toHead.normalized, Vector3.up);
                    _canvas.transform.rotation = Quaternion.Slerp(_canvas.transform.rotation, target, 1f - Mathf.Exp(-9f * dt));
                }
            }

            _hintTimer += dt;
        }

        private void UpdateHeaderAndHud()
        {
            var rec = AuroraRuntime.LocalPlayer;
            var role = RoleCatalog.Get(rec.Role);
            var net = AuroraRuntime.Net;

            string status = string.Format("{0}  ·  <color=#{1}>{2}</color>",
                rec.name,
                ColorUtility.ToHtmlStringRGB(role.Color),
                role.Name);

            if (net != null && net.IsConnected)
            {
                status += string.Format("  ·  <color=#4DE1FF>{0}</color> ({1})",
                    net.IsHost ? "HOST" : "CLIENT", net.PeerCount + 1);
            }
            else
            {
                status += "  ·  <color=#93A7C4>SOLO</color>";
            }

            if (_headerStatus != null)
            {
                _headerStatus.text = status;
            }

            if (_headerBalance != null)
            {
                _headerBalance.text = AuroraUtils.Money(rec.balance);
            }

            if (_accentBar != null)
            {
                _accentBar.color = role.Color;
            }

            if (_hudHint != null)
            {
                var transfers = AuroraRuntime.Transfers;
                if (transfers != null && transfers.IsHolding)
                {
                    _hudHint.text = string.Format("{0}  {1}%", transfers.HoldTargetName, Mathf.RoundToInt(transfers.HoldProgress * 100f));
                    _hudBar?.SetValue(transfers.HoldProgress);
                    _hudBar?.SetColor(UiTheme.Success);
                }
                else if (transfers != null && transfers.PendingAmount > 0)
                {
                    _hudHint.text = string.Format("{0}: {1}", AuroraL.Get("common.amount"), AuroraUtils.Money(transfers.PendingAmount));
                    _hudBar?.SetValue(0f);
                }
                else
                {
                    _hudHint.text = string.Format("<color=#{0}>{1}</color>\n{2}",
                        ColorUtility.ToHtmlStringRGB(role.Color), role.Name, AuroraUtils.Money(rec.balance));
                    _hudBar?.SetValue(0f);
                }
            }
        }

        private void UpdateToasts()
        {
            var toasts = AuroraNotifications.Toasts;

            for (int i = 0; i < _toastBars.Count; i++)
            {
                bool has = i < toasts.Count;
                var bar = _toastBars[i];
                var text = _toastTexts[i];

                if (bar == null || text == null)
                {
                    continue;
                }

                bar.gameObject.SetActive(has);
                if (!has)
                {
                    continue;
                }

                // Свежие — снизу, старые уходят вверх.
                var toast = toasts[toasts.Count - 1 - i];
                text.text = toast.Text;

                float life = Mathf.Clamp01((toast.ExpireAt - Time.realtimeSinceStartup) / 1.2f);
                float appear = Mathf.Clamp01((Time.realtimeSinceStartup - toast.CreatedAt) / 0.18f);

                bar.color = UiTheme.PanelLight.WithAlpha(0.95f * life);
                text.color = toast.Color.WithAlpha(Mathf.Min(life, appear));

                var rect = bar.rectTransform;
                rect.anchoredPosition = new Vector2(0f, -i * 48f - (1f - appear) * 14f);
            }
        }

        private void UpdateFooterHint()
        {
            if (_footerText == null)
            {
                return;
            }

            var transfers = AuroraRuntime.Transfers;
            if (transfers != null && transfers.PendingAmount > 0f)
            {
                _footerText.text = AuroraL.Get("money.transfer.howto");
                _footerText.color = UiTheme.Warning;
                return;
            }

            var door = AuroraRuntime.Doors?.HeldDoor;
            if (door != null)
            {
                _footerText.text = door.FooterHint;
                _footerText.color = UiTheme.Accent;
                return;
            }

            _footerText.text = AuroraL.Get("menu.hint.open") + "   ·   " + AuroraL.Get("menu.hint.poke");
            _footerText.color = UiTheme.TextDim;
        }

        // --------------------------------------------------------------- открытие

        public void Toggle()
        {
            if (_rootGo == null && GameHooks.PlayerReady)
            {
                Initialize();
            }

            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open(MenuPage page = MenuPage.Main)
        {
            IsOpen = true;
            CurrentPage = page;
            RefreshPage();
            AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Open);
            AuroraRuntime.Input?.Haptic(Il2CppSLZ.Marrow.Interaction.Handedness.BOTH, 0.2f, 0.1f);
        }

        public void Close()
        {
            IsOpen = false;
            AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Close);
            _pointer?.SetVisible(false);
        }

        /// <summary>Кнопка X: быстрая панель сумм перевода.</summary>
        public void OpenTransfer()
        {
            if (!IsOpen)
            {
                Open(MenuPage.Wallet);
                return;
            }

            SwitchPage(MenuPage.Wallet);
        }

        // ------------------------------------------------------------ страницы (реализация в HandMenuPages)

        private void BuildMainPage() => HandMenuPages.BuildMain(this);
        private void BuildRolesPage() => HandMenuPages.BuildRoles(this);
        private void BuildWalletPage() => HandMenuPages.BuildWallet(this);
        private void BuildShopPage() => HandMenuPages.BuildShop(this);
        private void BuildDoorsPage() => HandMenuPages.BuildDoors(this);
        private void BuildContractsPage() => HandMenuPages.BuildContracts(this);
        private void BuildSettingsPage() => HandMenuPages.BuildSettings(this);

        internal GameObject RootObject => _rootGo;
        internal RectTransform PanelRect => _panel;
        internal RectTransform ContentRect => _content;

        internal ref int ShopPageIndex => ref _shopPage;
        internal ref int RolePageIndex => ref _rolePage;
        internal ref int PlayerPageIndex => ref _playerPage;
        internal ref int ContractPageIndex => ref _contractPage;
        internal ref int DoorPageIndex => ref _doorPage;

        internal const int PageSize = MaxRowsPerView;

        internal void Rebuild() => RefreshPage();
    }
}
