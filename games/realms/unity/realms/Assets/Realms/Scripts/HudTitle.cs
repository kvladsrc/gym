using System;
using System.Collections.Generic;
using System.Text;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>What the menu's column shows: the menu over a game, the main menu, or a new game's choices.</summary>
    public enum MenuMode
    {
        Pause,
        Title,
        NewGame,
    }

    /// <summary>
    /// The main menu and the new game's screen (the interface plan's 3.2, 2026-11-11). The main
    /// menu stands over the game's picture without a panel: the logo, the crown with six faces
    /// and the game's name in one picture, in the middle a little above the centre, and under it
    /// the buttons, «Продолжить» with the last game's kingdom and turn (gold, Enter), «Новая игра»
    /// (gold when there is nothing to continue), «Загрузить», «Как играть» and «Выход» (the
    /// players asked for it so, 2026-11-11). The new game's screen is the dark column of the menu
    /// over a game, the map it would start on beside it: its number, «Другая карта», a number to
    /// type, the neighbours' pace, «Начать» and «Назад». The loading screen covers the seconds a
    /// map takes to draw: the picture, the logo where the main menu has it, and a bar.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The players chose on the main menu or the new game's screen, or left a game for the main menu.</summary>
        public event Action ContinuePicked, TitleNewPicked, OtherMapPicked, StartPicked, NewGameBackPicked, ToTitlePicked;

        /// <summary>The players typed a map's number and asked to see it.</summary>
        public event Action<int> MapNumberPicked;

        MenuMode menuMode;
        VisualElement titleArt, titleLogo, titleLogoCrown, titleButtons, newGamePanel, titleNewTwin;
        VisualElement loadingScreen, loadingLogo, loadingBar;
        Label titleLogoName, loadingLine;
        Button continueButton, titleNew, menuToTitle;
        Label continueLine, titleNewKey, newGameMap, paceNote;
        TextField mapNumber;
        readonly List<(Difficulty Pace, Button Button)> paceButtons = new List<(Difficulty, Button)>();
        Difficulty chosenPace = Difficulty.Normal;
        bool typing;
        int typedFrame = -10;

        public MenuMode Mode => menuMode;

        /// <summary>The neighbours' pace chosen on the new game's screen.</summary>
        public Difficulty ChosenPace => chosenPace;

        /// <summary>
        /// A map's number is being typed: its keys are the field's, and the Enter that showed the
        /// map does not also start the game.
        /// </summary>
        public bool Typing => MenuOpen && menuMode == MenuMode.NewGame && (typing || Time.frameCount - typedFrame <= 1);

        /// <summary>The interface built before any game, for the main menu shown while the last game loads.</summary>
        public void Prepare(ArtLibrary library)
        {
            art = library;
            if (root == null)
                Build();
        }

        /// <summary>The main menu's picture behind the column and its buttons, in the column.</summary>
        void BuildTitle(VisualElement column)
        {
            titleArt = Child(root, "title-art");
            titleArt.pickingMode = PickingMode.Ignore;
            titleArt.PlaceBehind(menu);
            titleArt.style.display = DisplayStyle.None;

            // The logo: the painted picture of the crown with the name when the art has it,
            // the crown and the name in letters while it has not.
            titleLogo = Child(column, "title-logo");
            titleLogo.pickingMode = PickingMode.Ignore;
            titleLogoCrown = Child(titleLogo, "title-logo-crown");
            titleLogoName = Text(titleLogo, "title-logo-name", Words.GameName, black);
            titleLogo.style.display = DisplayStyle.None;

            titleButtons = Child(column, "menu-buttons");
            continueButton = MenuButton(titleButtons, Words.TitleContinue, "button-primary", "Enter", () => ContinuePicked?.Invoke());
            continueButton.AddToClassList("menu-continue");
            // The caption with the game's line under it, in the caption's place between the key and its twin.
            Label caption = continueButton.Q<Label>(className: "button-caption");
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.AddToClassList("menu-continue-words");
            continueButton.Insert(continueButton.IndexOf(caption), words);
            words.Add(caption);
            continueLine = Text(words, "menu-continue-line", "", regular);
            continueLine.pickingMode = PickingMode.Ignore;
            titleNew = MenuButton(titleButtons, Words.MenuNewGame, "button", "Enter", () => TitleNewPicked?.Invoke());
            // The key's own label is the last one: its unseen twin comes before the caption.
            titleNewKey = titleNew.Query<Label>(className: "key-hint-dark").Last();
            titleNewTwin = titleNew.Q(className: "key-twin");
            MenuButton(titleButtons, Words.MenuLoad, "button", null, () => LoadPicked?.Invoke());
            MenuButton(titleButtons, Words.MenuHelp, "button", "F1", () => Help?.Invoke());
            MenuButton(titleButtons, Words.TitleQuit, "button", null, () => QuitPicked?.Invoke()).AddToClassList("menu-danger");
            titleButtons.style.display = DisplayStyle.None;
        }

        /// <summary>The new game's choices, in the column: the map, another map or one by its number, the pace, start or back.</summary>
        void BuildNewGame(VisualElement column)
        {
            newGamePanel = Child(column, "menu-buttons");
            newGamePanel.AddToClassList("new-game");
            newGameMap = Text(newGamePanel, "new-game-map", "", black);
            MenuButton(newGamePanel, Words.NewGameOther, "button", null, () => OtherMapPicked?.Invoke());
            Text(newGamePanel, "new-game-label", Words.NewGameNumber, bold);
            VisualElement row = Child(newGamePanel, "new-game-number");
            mapNumber = new TextField { maxLength = 7, isDelayed = false };
            mapNumber.AddToClassList("name-field");
            mapNumber.AddToClassList("number-field");
            UseFont(mapNumber, black);
            row.Add(mapNumber);
            // Digits only; Enter in the field shows the map.
            mapNumber.RegisterValueChangedCallback(e =>
            {
                string typed = e.newValue ?? "";
                var digits = new StringBuilder(typed.Length);
                foreach (char c in typed)
                {
                    if (c >= '0' && c <= '9')
                        digits.Append(c);
                }
                if (digits.Length != typed.Length)
                    mapNumber.SetValueWithoutNotify(digits.ToString());
            });
            mapNumber.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    ShowNumber();
                    e.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            mapNumber.RegisterCallback<FocusInEvent>(_ => typing = true);
            mapNumber.RegisterCallback<FocusOutEvent>(_ =>
            {
                typing = false;
                typedFrame = Time.frameCount;
            });
            Btn(row, Words.NewGameShow, "button", ShowNumber);
            Text(newGamePanel, "difficulty-title", Words.DifficultyTitle, black);
            VisualElement paces = Child(newGamePanel, "difficulty-row");
            foreach (Difficulty pace in new[] { Difficulty.Calm, Difficulty.Normal, Difficulty.Race })
            {
                Difficulty picked = pace;
                paceButtons.Add((pace, Btn(paces, Words.Difficulty(pace), "button", () =>
                {
                    chosenPace = picked;
                    ShowPace();
                })));
            }
            paceNote = Text(newGamePanel, "difficulty-note", "", regular);
            Child(newGamePanel, "new-game-gap");
            MenuButton(newGamePanel, Words.NewGameStart, "button-primary", "Enter", () => StartPicked?.Invoke());
            MenuButton(newGamePanel, Words.NewGameBack, "button", "Esc", () => NewGameBackPicked?.Invoke());
            newGamePanel.style.display = DisplayStyle.None;
        }

        void ShowNumber()
        {
            typedFrame = Time.frameCount;
            mapNumber.Blur();
            if (int.TryParse(mapNumber.value, out int number))
                MapNumberPicked?.Invoke(number);
        }

        void ShowPace()
        {
            foreach ((Difficulty pace, Button button) in paceButtons)
                button.EnableInClassList("button-on", pace == chosenPace);
            paceNote.text = Words.DifficultyText(chosenPace);
        }

        /// <summary>
        /// The main menu. <paramref name="line"/> is the game behind it, "Алое королевство, ход 34";
        /// null while the last game still loads, and empty when there is none, so «Новая игра» is the gold one.
        /// </summary>
        public void ShowTitle(string line)
        {
            bool loading = line == null, game = !string.IsNullOrEmpty(line);
            continueButton.style.display = game || loading ? DisplayStyle.Flex : DisplayStyle.None;
            continueLine.text = loading ? Words.TitleLoading : line;
            titleButtons.SetEnabled(!loading);
            bool fresh = !game && !loading;
            titleNew.EnableInClassList("button-primary", fresh);
            titleNew.EnableInClassList("button", !fresh);
            titleNewKey.EnableInClassList("key-hint", fresh);
            titleNewKey.EnableInClassList("key-hint-dark", !fresh);
            titleNewKey.parent.style.display = titleNewTwin.style.display = fresh ? DisplayStyle.Flex : DisplayStyle.None;
            titleNew.Q<Label>(className: "button-caption").text = fresh ? Loud(Words.MenuNewGame) : Words.MenuNewGame;
            MenuCaptionFont(titleNew, fresh);
            ShowMenu(true, MenuMode.Title);
        }

        /// <summary>The new game's screen beside the map it would start on, map number <paramref name="seed"/>.</summary>
        public void ShowNewGame(int seed, Difficulty pace)
        {
            chosenPace = pace;
            ShowPace();
            newGameMap.text = Words.NewGameMap(seed);
            mapNumber.SetValueWithoutNotify("");
            ShowMenu(true, MenuMode.NewGame);
        }

        /// <summary>The column's head and buttons for <paramref name="mode"/>.</summary>
        void ShowMenuMode(MenuMode mode)
        {
            menuMode = mode;
            switch (mode)
            {
                case MenuMode.Title:
                    ShowLogo();
                    break;
                case MenuMode.NewGame:
                    SetIcon(menuShield, FlagIcon(state.World.Player));
                    menuKingdom.text = state.World.Countries[state.World.Player].Label;
                    menuMap.text = Words.MenuNewGame;
                    break;
                default:
                    bool battle = InBattle;
                    SetIcon(menuShield, FlagIcon(state.World.Player));
                    menuKingdom.text = state.World.Countries[state.World.Player].Label;
                    menuMap.text = Words.MenuMap(state.World.Seed, state.Turn);
                    menuBack.text = Loud(battle ? Words.MenuBackBattle : Words.MenuBack);
                    menuQuit.text = battle ? Words.MenuQuitBattle : Words.MenuQuit;
                    DisplayStyle shown = battle ? DisplayStyle.None : DisplayStyle.Flex;
                    menuNew.style.display = menuSave.style.display = menuLoad.style.display = menuToTitle.style.display = shown;
                    break;
            }
            menuButtons.style.display = mode == MenuMode.Pause ? DisplayStyle.Flex : DisplayStyle.None;
            titleButtons.style.display = titleLogo.style.display = mode == MenuMode.Title ? DisplayStyle.Flex : DisplayStyle.None;
            newGamePanel.style.display = mode == MenuMode.NewGame ? DisplayStyle.Flex : DisplayStyle.None;
            // Another language starts the scene again: not in a battle, and not over a new game's preview.
            menuLangs.style.display = mode == MenuMode.NewGame || (mode == MenuMode.Pause && InBattle) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The buttons the column shows now, which a list of saves takes the place of.</summary>
        VisualElement MenuGroup => menuMode == MenuMode.Title ? titleButtons : menuMode == MenuMode.NewGame ? newGamePanel : menuButtons;

        /// <summary>The painted logo, or the crown over the name in letters without it.</summary>
        void ShowLogo()
        {
            Texture2D logo = IconTexture("title_logo");
            titleLogo.style.backgroundImage = logo != null ? new StyleBackground(logo) : new StyleBackground(StyleKeyword.None);
            titleLogoCrown.style.display = titleLogoName.style.display = logo != null ? DisplayStyle.None : DisplayStyle.Flex;
            if (logo == null)
                SetIcon(titleLogoCrown, IconTexture("title_crown") != null ? "title_crown" : "crown");
        }

        /// <summary>The loading screen is up: nothing under it is shown or takes the pointer.</summary>
        public bool LoadingShown => loadingScreen != null && loadingScreen.style.display == DisplayStyle.Flex;

        /// <summary>
        /// The loading screen over everything, its bar at <paramref name="progress"/> (0 to 1), with
        /// the game or map being drawn named under «Загрузка…».
        /// </summary>
        public void ShowLoading(string line, float progress)
        {
            if (root == null)
                return;
            if (loadingScreen == null)
            {
                loadingScreen = Child(root, "loading");
                loadingLogo = Child(loadingScreen, "loading-logo");
                Text(loadingScreen, "loading-title", Words.TitleLoading, black);
                loadingLine = Text(loadingScreen, "loading-line", "", bold);
                VisualElement track = Child(loadingScreen, "loading-track");
                loadingBar = Child(track, "loading-bar");
            }
            if (!LoadingShown)
            {
                Texture2D picture = IconTexture("title_art");
                loadingScreen.style.backgroundImage = picture != null ? new StyleBackground(picture) : new StyleBackground(StyleKeyword.None);
                Texture2D logo = IconTexture("title_logo") ?? IconTexture("title_crown");
                loadingLogo.style.backgroundImage = logo != null ? new StyleBackground(logo) : new StyleBackground(StyleKeyword.None);
                loadingScreen.BringToFront();
                loadingScreen.style.display = DisplayStyle.Flex;
            }
            loadingLine.text = line ?? "";
            loadingBar.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
        }

        public void HideLoading()
        {
            if (loadingScreen != null)
                loadingScreen.style.display = DisplayStyle.None;
        }

        /// <summary>The picture behind the main menu, when the art has it; the live map otherwise.</summary>
        void ShowTitleArt(bool show)
        {
            Texture2D picture = show ? IconTexture("title_art") : null;
            if (picture != null)
                titleArt.style.backgroundImage = new StyleBackground(picture);
            titleArt.style.display = picture != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
