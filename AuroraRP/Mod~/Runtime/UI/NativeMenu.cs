using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSLZ.Bonelab;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AuroraRP
{
    /// <summary>
    /// Меню AuroraRP в РОДНОЙ панели BONELAB — как у LabFusion и SpectrumB:
    ///
    ///   1. в родной сетке настроек появляется кнопка «AuroraRP»;
    ///   2. она открывает страницу AuroraRP в родной панели: сначала пробуем клон родной
    ///      страницы настроек (тогда вид/звук/анимации ровно как в игре), а если клон не
    ///      получился — страницу, собранную кодом (тоже родным шрифтом игры);
    ///   3. в радиальном меню игры (кнопка меню) появляется пункт «AuroraRP» — как в SpectrumB;
    ///   4. кнопки разделов открывают голографическое меню мода над левой рукой.
    ///
    /// Всё в try/catch: если в этой версии игры нет нужного узла UI, мод просто работает
    /// как раньше (жест Y + A / F8).
    /// </summary>
    public static class NativeMenu
    {
        private const string PageName = "page_AuroraRP";
        private const string EntryName = "button_AuroraRP";
        private const string RadialName = "AuroraRP";

        private static readonly string[] Captions =
        {
            "Роли", "Кошелёк", "Магазин", "Двери", "Контракты", "Настройки"
        };

        private static readonly MenuPage[] Targets =
        {
            MenuPage.Roles, MenuPage.Wallet, MenuPage.Shop, MenuPage.Doors, MenuPage.Contracts, MenuPage.Settings
        };

        private static bool _done;
        private static bool _radialDone;
        private static int _attempts;
        private static float _timer;
        private static float _refreshTimer;

        private static GameObject _entry;
        private static GameObject _page;
        private static int _pageIndex = -1;
        private static bool _codePage;

        private static TextMeshProUGUI _statusText;
        private static TextMeshProUGUI _balanceText;

        private static Feedback_Tactile _feedbackTactile;
        private static Feedback_Audio _feedbackAudio;

        // ------------------------------------------------------------------ жизненный цикл

        /// <summary>Раз в 2 секунды пробуем встроиться в родное меню (пока не получится).</summary>
        public static void Tick(float dt)
        {
            if (!_done)
            {
                _timer += dt;

                if (_timer >= 2f)
                {
                    _timer = 0f;

                    if (_attempts++ > 30)
                    {
                        _done = true;
                        AuroraLog.Warn("Родное меню игры не найдено (UIRig) — работаю только по жесту Y + A / F8.");
                    }
                    else
                    {
                        try
                        {
                            if (Install())
                            {
                                _done = true;
                            }
                        }
                        catch (Exception e)
                        {
                            _done = true;
                            AuroraLog.Exception(e, "native menu");
                        }
                    }
                }
            }

            // Роль и баланс обновляем только на собранной кодом странице.
            if (!_codePage || _page == null)
            {
                return;
            }

            _refreshTimer += dt;

            if (_refreshTimer < 0.75f)
            {
                return;
            }

            _refreshTimer = 0f;

            try
            {
                if (_page.activeInHierarchy)
                {
                    RefreshDynamic();
                }
            }
            catch (Exception)
            {
            }
        }

        public static void Shutdown()
        {
            try
            {
                if (_entry != null)
                {
                    UnityEngine.Object.Destroy(_entry);
                    _entry = null;
                }

                if (_page != null)
                {
                    UnityEngine.Object.Destroy(_page);
                    _page = null;
                }
            }
            catch (Exception)
            {
            }

            _statusText = null;
            _balanceText = null;
            _pageIndex = -1;
            _codePage = false;
            _done = false;
            _radialDone = false;
            _attempts = 0;
        }

        // ------------------------------------------------------------------ установка

        private static bool Install()
        {
            var uiRig = UIRig.Instance;

            if (uiRig == null)
            {
                return false;
            }

            var popUpMenu = uiRig.popUpMenu;

            if (popUpMenu == null)
            {
                return false;
            }

            var panelView = popUpMenu.preferencesPanelView;

            if (panelView == null)
            {
                return false;
            }

            var root = panelView.transform;

            if (root == null)
            {
                return false;
            }

            // --- 1. страница AuroraRP: сначала клон родной страницы настроек, иначе — своя
            if (_page == null || _pageIndex < 0)
            {
                if (!TryCloneNativePage(root, panelView))
                {
                    TryBuildCodePage(root, panelView);
                }
            }

            // --- 2. кнопка «AuroraRP» в родной сетке настроек
            if (_entry == null)
            {
                var grid = root.Find("page_OPTIONS/grid_Options");
                var control = grid != null ? grid.Find("button_Control") : null;

                if (control != null)
                {
                    var clone = UnityEngine.Object.Instantiate(control.gameObject, control.parent, false);
                    clone.name = EntryName;

                    SetLabel(clone, "AuroraRP");
                    Hook(clone, OpenPage);

                    _entry = clone;
                }
                else
                {
                    AuroraLog.Warn("Родное меню: не нашёл кнопку настроек (button_Control) — страница AuroraRP доступна из радиального меню.");
                }
            }

            // --- 3. пункт «AuroraRP» в радиальном меню (как в SpectrumB)
            if (!_radialDone)
            {
                _radialDone = true;

                try
                {
                    var radial = popUpMenu.radialPageView;
                    var home = radial != null ? radial.m_HomePage : null;
                    var items = home != null ? home.items : null;

                    if (items != null)
                    {
                        bool exists = false;

                        for (int i = 0; i < items.Count; i++)
                        {
                            var item = items[i];

                            if (item != null && item.name == RadialName)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            var direction = (PageItem.Directions)0;

                            for (int d = 0; d < 8; d++)
                            {
                                var candidate = (PageItem.Directions)d;
                                bool used = false;

                                for (int i = 0; i < items.Count; i++)
                                {
                                    var item = items[i];

                                    if (item != null && item.direction == candidate)
                                    {
                                        used = true;
                                        break;
                                    }
                                }

                                if (!used)
                                {
                                    direction = candidate;
                                    break;
                                }
                            }

                            System.Action handler = OnRadialSelected;

                            items.Add(new PageItem(RadialName, direction, handler));

                            AuroraLog.Info("Пункт «AuroraRP» добавлен в радиальное меню игры.");
                        }
                    }
                }
                catch (Exception e)
                {
                    AuroraLog.Warn("Не удалось добавить пункт в радиальное меню: " + e.Message);
                }
            }

            if (_page != null)
            {
                AuroraLog.Info("AuroraRP встроен в родное меню игры: «AuroraRP» в настройках → страница с разделами.");
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ страница: клон родной

        private static bool TryCloneNativePage(Transform root, PreferencesPanelView panelView)
        {
            try
            {
                var template = root.Find("page_OPTIONS");

                if (template == null)
                {
                    return false;
                }

                var clone = UnityEngine.Object.Instantiate(template.gameObject, root, false);
                clone.name = PageName;

                PreparePage(clone);

                if (!RegisterPage(panelView, clone))
                {
                    UnityEngine.Object.Destroy(clone);
                    return false;
                }

                _page = clone;
                _codePage = false;
                return true;
            }
            catch (Exception e)
            {
                AuroraLog.Warn("Не удалось клонировать родную страницу: " + e.Message);
                return false;
            }
        }

        private static void PreparePage(GameObject page)
        {
            SetTitle(page, "Aurora RP");

            var grid = page.transform.Find("grid_Options");

            if (grid == null)
            {
                AuroraLog.Warn("Родная страница AuroraRP: нет grid_Options — кнопки не переименованы.");
                return;
            }

            int index = 0;

            for (int i = 0; i < grid.childCount; i++)
            {
                var child = grid.GetChild(i).gameObject;

                if (child.GetComponent<Button>() == null)
                {
                    continue;
                }

                if (index < Captions.Length)
                {
                    var target = Targets[index];

                    SetLabel(child, Captions[index]);
                    Hook(child, () => OpenSection(target));

                    index++;
                }
                else
                {
                    // Лишние родные кнопки (звук/графика игры) прячем — это страница мода.
                    child.SetActive(false);
                }
            }

            AuroraLog.Info("Родная страница AuroraRP: разделов {0} из {1}.", index, Captions.Length);
        }

        // ------------------------------------------------------------------ страница: своя

        private static void TryBuildCodePage(Transform root, PreferencesPanelView panelView)
        {
            try
            {
                var pages = panelView.pages;

                if (pages == null || pages.Length == 0)
                {
                    AuroraLog.Warn("Родная панель: в ней нет страниц — встроиться некуда.");
                    return;
                }

                GameObject template = null;

                for (int i = 0; i < pages.Length; i++)
                {
                    if (pages[i] != null)
                    {
                        template = pages[i];
                        break;
                    }
                }

                if (template == null)
                {
                    AuroraLog.Warn("Родная панель: все страницы пустые (null).");
                    return;
                }

                var page = new GameObject(PageName);
                page.layer = template.layer;
                page.transform.SetParent(root, false);

                var rect = page.GetComponent<RectTransform>();

                if (rect == null)
                {
                    rect = page.AddComponent<RectTransform>();
                }

                var source = template.GetComponent<RectTransform>();

                if (source != null)
                {
                    rect.anchorMin = source.anchorMin;
                    rect.anchorMax = source.anchorMax;
                    rect.pivot = source.pivot;
                    rect.sizeDelta = source.sizeDelta;
                    rect.anchoredPosition3D = source.anchoredPosition3D;
                    rect.localRotation = source.localRotation;
                    rect.localScale = source.localScale;
                }

                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                BuildCodeContent(page, rect);

                if (!RegisterPage(panelView, page))
                {
                    UnityEngine.Object.Destroy(page);
                    return;
                }

                _page = page;
                _codePage = true;

                AuroraLog.Info("Страница AuroraRP собрана кодом и вставлена в родное меню (страница №{0}).", _pageIndex);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "native code page");
            }
        }

        /// <summary>Дописывает страницу в родной массив и запоминает её индекс.</summary>
        private static bool RegisterPage(PreferencesPanelView panelView, GameObject page)
        {
            var pages = panelView.pages;

            if (pages == null || pages.Length == 0)
            {
                return false;
            }

            var length = pages.Length + 1;
            var newPages = new Il2CppReferenceArray<GameObject>(length);

            for (int i = 0; i < pages.Length; i++)
            {
                newPages[i] = pages[i];
            }

            newPages[length - 1] = page;

            panelView.pages = newPages;

            _pageIndex = length - 1;
            return true;
        }

        // ------------------------------------------------------------------ содержимое своей страницы

        private static void BuildCodeContent(GameObject page, RectTransform rect)
        {
            float width = Mathf.Abs(rect.rect.width);
            float height0 = Mathf.Abs(rect.rect.height);
            float unit = Mathf.Min(width, height0);

            if (unit < 1f)
            {
                unit = 800f;
            }

            if (width < 1f)
            {
                width = unit * 1.2f;
            }

            var backdrop = NewRect("Backdrop", page.transform, page.layer);
            Stretch(backdrop);

            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.sprite = UiTheme.RoundedPanel;
            backdropImage.type = Image.Type.Sliced;
            backdropImage.color = UiTheme.Panel;
            backdropImage.raycastTarget = false;

            var content = NewRect("Content", page.transform, page.layer);
            Stretch(content);

            float margin = unit * 0.05f;
            float y = margin;

            var title = AddText(content, "Title", "AURORA RP", unit * 0.075f, UiTheme.Text, ref y, margin,
                unit * 0.16f, TextAlignmentOptions.Left);

            if (title != null)
            {
                title.fontStyle = FontStyles.Bold;
            }

            _statusText = AddText(content, "Status", "", unit * 0.036f, UiTheme.TextDim, ref y, margin,
                unit * 0.055f, TextAlignmentOptions.Left);

            _balanceText = AddText(content, "Balance", "", unit * 0.036f, UiTheme.Money, ref y, margin,
                unit * 0.055f, TextAlignmentOptions.Left);

            y += margin * 0.4f;

            float height = unit * 0.085f;
            float rowWidth = width - margin * 2f;

            for (int i = 0; i < Captions.Length; i++)
            {
                var target = Targets[i];

                AddRow(content, "Row_" + Captions[i], Captions[i], rowWidth, height, margin, ref y, () => OpenSection(target));
            }

            AddText(content, "Hint", "Y + A или F8 — голографическое меню над левой рукой",
                unit * 0.030f, UiTheme.TextDim, ref y, margin, unit * 0.05f, TextAlignmentOptions.Left);
        }

        private static void AddRow(RectTransform parent, string name, string caption, float width, float height,
            float margin, ref float y, Action action)
        {
            var row = NewRect(name, parent, parent.gameObject.layer);
            row.anchorMin = new Vector2(0.5f, 1f);
            row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(width, height);
            row.anchoredPosition3D = new Vector3(0f, -y, 0f);

            var background = row.gameObject.AddComponent<Image>();
            background.sprite = UiTheme.RoundedSoft;
            background.type = Image.Type.Sliced;
            background.color = UiTheme.Row;
            background.raycastTarget = true;

            // Коллайдер: по нему бьёт и лазер родного меню, и наш указатель.
            var collider = row.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, 12f);
            collider.isTrigger = true;

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.35f, 1.6f, 1f);
            colors.pressedColor = new Color(0.75f, 0.85f, 1.05f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityEngine.Events.UnityAction)action);

            AttachNativeFeedback(button);

            var label = NewRect("Label", row, row.gameObject.layer);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(margin * 0.6f, 0f);
            label.offsetMax = new Vector2(-margin * 0.6f, 0f);

            var text = label.gameObject.AddComponent<TextMeshProUGUI>();

            if (UiTheme.Font != null)
            {
                text.font = UiTheme.Font;
            }

            text.text = caption;
            text.fontSize = height * 0.42f;
            text.color = UiTheme.Text;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;

            y += height + margin * 0.25f;
        }

        // ------------------------------------------------------------------ вспомогательные узлы

        private static RectTransform NewRect(string name, Transform parent, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var rect = go.GetComponent<RectTransform>();

            if (rect == null)
            {
                rect = go.AddComponent<RectTransform>();
            }

            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI AddText(RectTransform parent, string name, string value, float size, Color color,
            ref float y, float margin, float height, TextAlignmentOptions align)
        {
            var rect = NewRect(name, parent, parent.gameObject.layer);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(margin, 0f);
            rect.offsetMax = new Vector2(-margin, 0f);
            rect.sizeDelta = new Vector2(-margin * 2f, height);
            rect.anchoredPosition3D = new Vector3(0f, -y, 0f);

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            if (UiTheme.Font != null)
            {
                text.font = UiTheme.Font;
            }

            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;

            y += height + margin * 0.15f;

            return text;
        }

        /// <summary>Родные звук и отдача кнопок игры (как делает SpectrumB и Fusion).</summary>
        private static void AttachNativeFeedback(Button button)
        {
            try
            {
                if (_feedbackTactile == null || _feedbackAudio == null)
                {
                    var uiRig = UIRig.Instance;

                    if (uiRig == null)
                    {
                        return;
                    }

                    var controlFeedback = uiRig.transform.Find("DATAMANAGER/CONTROL_FEEDBACK");

                    if (controlFeedback == null)
                    {
                        return;
                    }

                    _feedbackTactile = controlFeedback.GetComponent<Feedback_Tactile>();
                    _feedbackAudio = controlFeedback.GetComponent<Feedback_Audio>();
                }

                var hover = button.gameObject.AddComponent<ButtonHoverClick>();
                hover.feedback_tactile = _feedbackTactile;
                hover.feedback_audio = _feedbackAudio;
                hover.confirmer = true;
            }
            catch (Exception e)
            {
                AuroraLog.Warn("Родная отдача кнопок недоступна: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ действия

        private static void OnRadialSelected()
        {
            OpenPage();
        }

        private static void OpenPage()
        {
            try
            {
                var uiRig = UIRig.Instance;

                if (uiRig == null || uiRig.popUpMenu == null || _pageIndex < 0)
                {
                    AuroraRuntime.Menu?.Open(MenuPage.Main);
                    return;
                }

                uiRig.popUpMenu.preferencesPanelView.PAGESELECT(_pageIndex);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Click);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "native open page");
            }
        }

        /// <summary>Закрываем родное меню и открываем нужный раздел голографического меню.</summary>
        private static void OpenSection(MenuPage page)
        {
            try
            {
                var uiRig = UIRig.Instance;

                if (uiRig != null && uiRig.popUpMenu != null)
                {
                    uiRig.popUpMenu.Deactivate();
                }
            }
            catch (Exception)
            {
            }

            AuroraRuntime.Menu?.Open(page);
        }

        private static void RefreshDynamic()
        {
            var record = AuroraRuntime.LocalPlayer;

            if (_statusText != null)
            {
                _statusText.text = record != null ? "Роль: " + AuroraL.Role(record.Role) : "Роль: —";
            }

            if (_balanceText != null)
            {
                long money = AuroraRuntime.Wallet != null ? AuroraRuntime.Wallet.GetBalance(AuroraRuntime.LocalId) : 0L;
                _balanceText.text = "Баланс: $" + money;
            }
        }

        // ------------------------------------------------------------------ мелочи

        private static void SetTitle(GameObject page, string title)
        {
            try
            {
                var grid = page.transform.Find("grid_Options");
                var texts = page.GetComponentsInChildren<TMP_Text>(true);

                for (int i = 0; i < texts.Length; i++)
                {
                    var text = texts[i];

                    if (text == null || (grid != null && text.transform.IsChildOf(grid)))
                    {
                        continue;
                    }

                    if (UiTheme.Font != null)
                    {
                        text.font = UiTheme.Font;
                    }

                    text.text = title;
                    return;
                }
            }
            catch (Exception)
            {
            }
        }

        private static void SetLabel(GameObject button, string label)
        {
            try
            {
                var child = button.transform.Find("text_Control");

                TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;

                if (text == null)
                {
                    var texts = button.GetComponentsInChildren<TMP_Text>(true);

                    if (texts != null && texts.Length > 0)
                    {
                        text = texts[0];
                    }
                }

                if (text == null)
                {
                    return;
                }

                if (UiTheme.Font != null)
                {
                    text.font = UiTheme.Font;
                }

                text.text = label;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "native label");
            }
        }

        private static void Hook(GameObject button, Action action)
        {
            try
            {
                var component = button.GetComponent<Button>();

                if (component == null)
                {
                    return;
                }

                component.onClick = new Button.ButtonClickedEvent();
                component.onClick.AddListener((UnityEngine.Events.UnityAction)action);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "native hook");
            }
        }
    }
}
