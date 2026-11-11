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
            Button quit = MenuButton(buttons, Words.MenuQuit, "button", null, () => QuitPicked?.Invoke());
            quit.AddToClassList("menu-danger");
            menuQuit = quit.Q<Label>(className: "button-caption");
            BuildTitle(menu);
            BuildNewGame(menu);
            BuildSaveList();
            Child(menu, "menu-gap");
            // «Русский / English» at the foot, in their own words: the language shows on the
            // first screen, as players seldom open settings before they play (the interface plan's 3.6).
            // One strip with the chosen language lit gold and ticked, as in the players' reference
            // (2026-11-11); the gold takes the strip's cut corners at the end it stands at.
            menuLangs = Child(menu, "menu-langs");
            Bevel.Attach(menuLangs);
            foreach (Language language in new[] { Language.Russian, Language.English })
            {
                bool now = language == Language.Russian ? Lang.Russian : !Lang.Russian;
                Button choice = Btn(menuLangs, Lang.SelfName(language), now ? "button-primary" : "button", () => LanguagePicked?.Invoke(language));
                choice.AddToClassList("menu-lang");
                choice.AddToClassList(language == Language.Russian ? "menu-lang-first" : "menu-lang-last");
                UseFont(choice.Q<Label>(className: "button-caption"), now ? black : bold);
                if (now)
                    choice.Insert(0, Tick());
                Bevel.Attach(choice);
            }
            // The version: on the main menu a plate like the languages' strip, in the column a plain line.
            VisualElement foot = Child(menu, "menu-foot");
            foot.pickingMode = PickingMode.Ignore;
            Bevel.Attach(foot);
            Text(foot, "menu-foot-text", Words.Version(Application.version), regular).pickingMode = PickingMode.Ignore;
            menu.style.display = DisplayStyle.None;
        }

        /// <summary>The chosen language's tick, drawn in its text's colour: the game's font has no such sign.</summary>
        static VisualElement Tick()
        {
            var tick = new VisualElement { pickingMode = PickingMode.Ignore };
            tick.AddToClassList("menu-lang-tick");
            tick.generateVisualContent += context =>
            {
                Vector2 size = tick.layout.size;
                Painter2D painter = context.painter2D;
                painter.strokeColor = tick.resolvedStyle.color;
                painter.lineWidth = size.y * 0.2f;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(new Vector2(size.x * 0.1f, size.y * 0.55f));
                painter.LineTo(new Vector2(size.x * 0.38f, size.y * 0.86f));
                painter.LineTo(new Vector2(size.x * 0.9f, size.y * 0.14f));
                painter.Stroke();
            };
            return tick;
        }

        Button MenuButton(VisualElement parent, string text, string cls, string key, Action clicked)
        {
            Button button = Btn(parent, cls == "button-primary" ? Loud(text) : text, cls, clicked);
            button.AddToClassList("menu-button");
            MenuCaptionFont(button, cls == "button-primary");
            Bevel.Attach(button);
            if (key != null)
            {
                // The key on a small plate of the button's own shape (the players' reference, 2026-11-11).
                var plate = new VisualElement { pickingMode = PickingMode.Ignore };
                plate.AddToClassList("key-plate");
                Bevel.Attach(plate);
                button.Add(plate);
                Text(plate, cls == "button-primary" ? "key-hint" : "key-hint-dark", Loud(key), bold).pickingMode = PickingMode.Ignore;
                // The reference's caption is in the button's middle whatever key stands at its end:
                // an unseen twin of the plate at the other end keeps it there, and gives way to a
                // caption too long for the room between the two.
                var twin = new VisualElement { pickingMode = PickingMode.Ignore };
                twin.AddToClassList("key-plate");
                twin.AddToClassList("key-twin");
                button.Insert(0, twin);
                Text(twin, "key-hint-dark", Loud(key), bold).pickingMode = PickingMode.Ignore;
            }
            return button;
        }

        /// <summary>
        /// The reference's captions are a weight lighter than the game's other buttons, and the
        /// gold button's a weight heavier than the rest.
        /// </summary>
        void MenuCaptionFont(Button button, bool gold) =>
            UseFont(button.Q<Label>(className: "button-caption"), gold ? black : bold);

        /// <summary>The gold buttons' captions and the keys are in capitals, as in the players' reference.</summary>
        static string Loud(string text) => text.ToUpperInvariant();

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
