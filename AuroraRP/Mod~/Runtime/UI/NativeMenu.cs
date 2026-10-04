using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSLZ.Bonelab;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AuroraRP
{
    /// <summary>
    /// Встраивание AuroraRP в РОДНОЕ меню BONELAB — так же, как это сделано в LabFusion:
    ///
    ///   1. в родной сетке кнопок настроек появляется кнопка «AuroraRP»;
    ///   2. она открывает родную страницу AuroraRP — это клон страницы настроек игры,
    ///      поэтому выглядят и звучат кнопки ровно как в игре (шрифт, иконки, звуки, анимации);
    ///   3. кнопки страницы открывают нужный раздел нашего голографического меню.
    ///
    /// Всё завёрнуто в try/catch: если в этой версии игры нет нужного узла UI,
    /// мод просто работает как раньше (жест Y+A / F8). Ничего не ломаем.
    /// </summary>
    public static class NativeMenu
    {
        private const string PageName = "page_AuroraRP";
        private const string EntryName = "button_AuroraRP";

        private static readonly string[] Captions =
        {
            "Роли", "Кошелёк", "Магазин", "Двери", "Контракты", "Настройки"
        };

        private static readonly MenuPage[] Targets =
        {
            MenuPage.Roles, MenuPage.Wallet, MenuPage.Shop, MenuPage.Doors, MenuPage.Contracts, MenuPage.Settings
        };

        private static bool _done;
        private static int _attempts;
        private static float _timer;

        private static GameObject _entry;
        private static GameObject _page;
        private static int _pageIndex = -1;

        // ------------------------------------------------------------------ жизненный цикл

        /// <summary>Раз в 2 секунды пробуем встроиться в родное меню (пока не получится).</summary>
        public static void Tick(float dt)
        {
            if (_done)
            {
                return;
            }

            _timer += dt;

            if (_timer < 2f)
            {
                return;
            }

            _timer = 0f;

            if (_attempts++ > 30)
            {
                _done = true;
                AuroraLog.Warn("Родное меню игры не найдено (UIRig) — работаю только по жесту Y + A / F8.");
                return;
            }

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

            _pageIndex = -1;
            _done = false;
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

            // --- 1. страница AuroraRP = клон родной страницы настроек
            if (_page == null || _pageIndex < 0)
            {
                var template = root.Find("page_OPTIONS");

                if (template == null)
                {
                    AuroraLog.Warn("Родное меню: не нашёл страницу настроек (page_OPTIONS).");
                    return false;
                }

                var clone = UnityEngine.Object.Instantiate(template.gameObject, root, false);
                clone.name = PageName;

                PreparePage(clone);

                var pages = panelView.pages;
                var length = pages.Length + 1;
                var newPages = new Il2CppReferenceArray<GameObject>(length);

                for (int i = 0; i < pages.Length; i++)
                {
                    newPages[i] = pages[i];
                }

                newPages[length - 1] = clone;

                panelView.pages = newPages;

                _page = clone;
                _pageIndex = length - 1;
            }

            // --- 2. кнопка «AuroraRP» в родной сетке настроек
            if (_entry == null)
            {
                var grid = root.Find("page_OPTIONS/grid_Options");
                var control = grid != null ? grid.Find("button_Control") : null;

                if (control == null)
                {
                    AuroraLog.Warn("Родное меню: не нашёл кнопку настроек (button_Control) — страница AuroraRP доступна только из меню мода.");
                    return true;
                }

                var clone = UnityEngine.Object.Instantiate(control.gameObject, control.parent, false);
                clone.name = EntryName;

                SetLabel(clone, "AuroraRP");
                Hook(clone, OpenPage);

                _entry = clone;
            }

            AuroraLog.Info("AuroraRP встроен в родное меню игры: «AuroraRP» в настройках → страница с разделами.");
            return true;
        }

        // ------------------------------------------------------------------ страница

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
                else if (child.activeSelf)
                {
                    // Лишние родные кнопки (звук/графика игры) прячем — это страница мода.
                    child.SetActive(false);
                }
            }

            AuroraLog.Info("Родная страница AuroraRP: разделов {0} из {1}.", index, Captions.Length);
        }

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

                    text.font = UiTheme.Font;
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

        // ------------------------------------------------------------------ действия

        private static void OpenPage()
        {
            try
            {
                if (_pageIndex < 0)
                {
                    return;
                }

                var uiRig = UIRig.Instance;

                if (uiRig == null || uiRig.popUpMenu == null)
                {
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

        /// <summary>Закрываем родное меню и открываем наш голографический раздел.</summary>
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
    }
}
