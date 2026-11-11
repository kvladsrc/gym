using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// The menu: a column along the left edge, as dark and square as the town screen, with the
    /// live map beside it. The top bar's «Меню» button opens it, and so does Esc once there is
    /// nothing left to close or put down; Esc again goes back. It took the place of the
    /// «Новая карта» button and the N key, with which one slip could end a game (the interface
    /// plan of 2026-11-11). In a battle it has no saves and no new game, its quit says the battle
    /// starts again (the save before it), and the battle waits while it is open. The same column
    /// holds the main menu and the new game's screen (HudTitle.cs).
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The players asked for the menu (the top bar's button), a new game or to quit.</summary>
        public event Action MenuPicked, NewGamePicked, QuitPicked;

        /// <summary>The players chose the other language (the interface plan's 3.6).</summary>
        public event Action<Language> LanguagePicked;

        VisualElement menuLangs;

        VisualElement menu, menuShield;
        Label menuKingdom, menuMap, menuBack;
        Button menuNew;

        public bool MenuOpen => menu != null && menu.style.display == DisplayStyle.Flex;

        /// <summary>The menu or the help is open: the game waits, a battle between its turns.</summary>
        public bool Paused => MenuOpen || HelpVisible;

        void BuildMenu()
        {
            menu = Child(root, "menu");
            VisualElement head = Child(menu, "menu-head");
            menuShield = Child(head, "menu-shield");
            VisualElement titles = Child(head, "screen-titles");
            menuKingdom = Text(titles, "screen-title", "", black);
            menuMap = Text(titles, "screen-subtitle", "", bold);
            VisualElement buttons = Child(menu, "menu-buttons");
            Button back = MenuButton(buttons, Words.MenuBack, "button-primary", "Esc", () => ShowMenu(false));
            menuBack = back.Q<Label>(className: "button-caption");
            BuildSaveButtons(buttons);
            menuNew = MenuButton(buttons, Words.MenuNewGame, "button", null, () => NewGamePicked?.Invoke());
            MenuButton(buttons, Words.MenuHelp, "button", "F1", () => Help?.Invoke());
            menuToTitle = MenuButton(buttons, Words.MenuToTitle, "button", null, () => ToTitlePicked?.Invoke());
            menuQuit = MenuButton(buttons, Words.MenuQuit, "button", null, () => QuitPicked?.Invoke()).Q<Label>(className: "button-caption");
            BuildTitle(menu);
            BuildNewGame(menu);
            BuildSaveList();
            Child(menu, "menu-gap");
            // «Русский / English» at the foot, in their own words: the language shows on the
            // first screen, as players seldom open settings before they play (the interface plan's 3.6).
            menuLangs = Child(menu, "menu-langs");
            foreach (Language language in new[] { Language.Russian, Language.English })
            {
                bool now = language == Language.Russian ? Lang.Russian : !Lang.Russian;
                Btn(menuLangs, Lang.SelfName(language), now ? "button-primary" : "button", () => LanguagePicked?.Invoke(language))
                    .AddToClassList("menu-lang");
            }
            Text(menu, "menu-foot", Words.Version(Application.version), regular);
            menu.style.display = DisplayStyle.None;
        }

        Button MenuButton(VisualElement parent, string text, string cls, string key, Action clicked)
        {
            Button button = Btn(parent, text, cls, clicked);
            button.AddToClassList("menu-button");
            if (key != null)
                Text(button, cls == "button-primary" ? "key-hint" : "key-hint-dark", key, bold).pickingMode = PickingMode.Ignore;
            return button;
        }

        /// <summary>Opens or closes the menu's column, as the menu over a game or as <paramref name="mode"/>; the map's lists close with it.</summary>
        public void ShowMenu(bool show, MenuMode mode = MenuMode.Pause)
        {
            if (menu == null)
                return;
            if (show)
            {
                CloseLists();
                ShowMenuMode(mode);
            }
            CloseSaves();
            menu.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            ShowTitleArt(show && menuMode == MenuMode.Title);
            root.EnableInClassList("on-menu", show);
            root.EnableInClassList("on-title", show && menuMode != MenuMode.Pause);
            // The main menu has no column: the logo and the buttons stand in the middle of its picture.
            root.EnableInClassList("on-main", show && menuMode == MenuMode.Title);
        }
    }
}
