using System;
using System.Collections.Generic;
using System.Text;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

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
    /// The main menu and the new game's window (the interface plan's 3.2, 2026-11-11). The main
    /// menu stands over the game's picture without a panel: the logo, the crown with six faces
    /// and the game's name in one picture, in the middle a little above the centre, and under it
    /// the buttons, «Продолжить» with the last game's kingdom and turn (gold, Enter), «Новая игра»
    /// (gold when there is nothing to continue), «Загрузить», «Настройки», «Как играть» and
    /// «Выход» (the players asked for it so, 2026-11-11; the settings came on 2026-11-12). The
    /// new game's window is a dark panel over the same picture, where the settings stand: the
    /// kingdom's shield and name with «Изменить флаг», a small picture of the map with its number,
    /// «Другая карта» and a number to type, the map's size, the neighbours' pace, «Играть» and
    /// «Назад». Nothing of the map is built until «Играть»: the players found the game hanging
    /// after they raised their flag on a map already drawn, and asked for the choices first and
    /// the loading screen after them (2026-11-12). The loading screen covers the seconds a map
    /// takes to draw: the picture, the logo where the main menu has it, and a bar.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The players chose on the main menu or the new game's window, or left a game for the main menu.</summary>
        public event Action ContinuePicked, TitleNewPicked, OtherMapPicked, StartPicked, NewGameBackPicked, ToTitlePicked, NewFlagPicked;

        /// <summary>The players typed a map's number and asked to see it.</summary>
        public event Action<int> MapNumberPicked;

        /// <summary>The players chose another size of the map.</summary>
        public event Action<MapSize> MapSizePicked;

        /// <summary>The paces and the sizes in the order of their buttons.</summary>
        static readonly Difficulty[] Paces = { Difficulty.Calm, Difficulty.Normal, Difficulty.Race };
        static readonly MapSize[] Sizes = { MapSize.Small, MapSize.Medium, MapSize.Large };

        MenuMode menuMode;
        VisualElement titleArt, titleLogo, titleLogoCrown, titleButtons, newGamePanel, titleNewTwin;
        VisualElement newGameShield, newGamePicture;
        VisualElement loadingScreen, loadingLogo, loadingBar;
        Label titleLogoName, loadingLine;
        Button continueButton, titleNew, menuToTitle, newGamePlay;
        Label continueLine, titleNewKey, newGameMap, newGameKingdom, newGamePictureNote, sizeNote, paceNote;
        TextField mapNumber;
        Texture2D newGameTexture;
        Difficulty chosenPace = Difficulty.Normal;
        MapSize chosenSize = MapSize.Medium;
        bool typing;
        int typedFrame = -10;
        VideoPlayer titleVideo;
        RenderTexture titleFrames;
        bool titleVideoBroken, titleVideoLooped;
        float titleVideoAsked;

        public MenuMode Mode => menuMode;

        /// <summary>The main menu shows the moving version of its picture now.</summary>
        public bool TitleVideoShown { get; private set; }

        /// <summary>The neighbours' pace chosen in the new game's window.</summary>
        public Difficulty ChosenPace => chosenPace;

        /// <summary>The map's size chosen in the new game's window.</summary>
        public MapSize ChosenSize => chosenSize;

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
            MenuButton(titleButtons, Words.MenuSettings, "button", null, () => SettingsPicked?.Invoke());
            MenuButton(titleButtons, Words.MenuHelp, "button", "F1", () => Help?.Invoke());
            MenuButton(titleButtons, Words.MenuAbout, "button", null, () => AboutPicked?.Invoke());
            MenuButton(titleButtons, Words.TitleQuit, "button", null, () => QuitPicked?.Invoke()).AddToClassList("menu-danger");
            titleButtons.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// The new game's window, built once: the kingdom with its shield, its name and «Изменить
        /// флаг»; the map's picture beside its number, «Другая карта» and a number to type; the
        /// size and the neighbours' pace as the settings' strips, each with a line of what it
        /// means; «Играть» (gold, Enter) and «Назад» (Esc).
        /// </summary>
        void BuildNewGame(VisualElement column)
        {
            newGamePanel = Child(column, "menu-list");
            newGamePanel.AddToClassList("new-game");
            Text(newGamePanel, "menu-list-title", Words.MenuNewGame, black);

            VisualElement kingdom = Child(newGamePanel, "new-game-kingdom");
            newGameShield = Child(kingdom, "new-game-shield");
            newGameShield.pickingMode = PickingMode.Ignore;
            VisualElement kingdomWords = Child(kingdom, "new-game-kingdom-words");
            newGameKingdom = Text(kingdomWords, "new-game-kingdom-name", "", black);
            Text(kingdomWords, "new-game-note", Words.NewGameFlagNote, regular).pickingMode = PickingMode.Ignore;
            SettingsButton(kingdomWords, Words.NewGameFlag, () => NewFlagPicked?.Invoke()).AddToClassList("new-game-flag");

            SettingsPart(newGamePanel, Words.NewGameMapPart);
            VisualElement mapRow = Child(newGamePanel, "new-game-map-row");
            newGamePicture = Child(mapRow, "new-game-picture");
            newGamePicture.pickingMode = PickingMode.Ignore;
            newGamePictureNote = Text(newGamePicture, "new-game-picture-note", "", bold);
            VisualElement side = Child(mapRow, "new-game-map-side");
            newGameMap = Text(side, "new-game-map", "", black);
            SettingsButton(side, Words.NewGameOther, () => OtherMapPicked?.Invoke()).AddToClassList("new-game-other");
            Text(side, "new-game-label", Words.NewGameNumber, bold);
            VisualElement row = Child(side, "new-game-number");
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
            SettingsButton(row, Words.NewGameShow, ShowNumber);
            Text(side, "new-game-note", Words.NewGamePictureNote, regular).pickingMode = PickingMode.Ignore;

            SettingsPart(newGamePanel, Words.NewGameSize);
            ChoiceRow(newGamePanel, "", Array.ConvertAll(Sizes, Words.MapSize), () => Array.IndexOf(Sizes, chosenSize), k =>
            {
                chosenSize = Sizes[k];
                ShowChoiceNotes();
                MapSizePicked?.Invoke(chosenSize);
            }).AddToClassList("new-game-choice");
            sizeNote = Text(newGamePanel, "new-game-choice-note", "", regular);
            SettingsPart(newGamePanel, Words.DifficultyTitle);
            ChoiceRow(newGamePanel, "", Array.ConvertAll(Paces, Words.Difficulty), () => Array.IndexOf(Paces, chosenPace), k =>
            {
                chosenPace = Paces[k];
                ShowChoiceNotes();
            }).AddToClassList("new-game-choice");
            paceNote = Text(newGamePanel, "new-game-choice-note", "", regular);

            Child(newGamePanel, "new-game-gap");
            // «Назад» at the left, «Играть» gold at the right, side by side under the choices.
            VisualElement buttons = Child(newGamePanel, "new-game-buttons");
            MenuButton(buttons, Words.NewGameBack, "button", "Esc", () => NewGameBackPicked?.Invoke());
            newGamePlay = MenuButton(buttons, Words.NewGamePlay, "button-primary", "Enter", () => StartPicked?.Invoke());
            newGamePanel.style.display = DisplayStyle.None;
        }

        void ShowNumber()
        {
            typedFrame = Time.frameCount;
            mapNumber.Blur();
            if (int.TryParse(mapNumber.value, out int number))
                MapNumberPicked?.Invoke(number);
        }

        /// <summary>The lines under the size's and the pace's strips, for the ones chosen.</summary>
        void ShowChoiceNotes()
        {
            sizeNote.text = Words.MapSizeText(chosenSize);
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

        /// <summary>
        /// The new game's window for <paramref name="setup"/>, the game it would start, made but
        /// not drawn; null while its map is being made, when the window waits with «Играть» off.
        /// </summary>
        public void ShowNewGame(GameState setup, MapSize size, Difficulty pace)
        {
            chosenSize = size;
            chosenPace = pace;
            ShowChoiceNotes();
            RefreshSettings();
            if (!MenuOpen || menuMode != MenuMode.NewGame)
            {
                mapNumber.SetValueWithoutNotify("");
                ShowMenu(true, MenuMode.NewGame);
            }
            bool ready = setup != null;
            newGamePlay.SetEnabled(ready);
            newGamePictureNote.text = ready ? "" : Words.NewGameMaking;
            if (!ready)
            {
                newGamePicture.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                return;
            }
            World world = setup.World;
            Country country = world.Countries[world.Player];
            newGameShield.style.backgroundImage = new StyleBackground(FlagArt.Image(country.Flag, FlagArt.Shape.Shield, 92, 104));
            newGameKingdom.text = country.Label;
            newGameMap.text = Words.NewGameMap(world.Seed);
            newGameTexture = MapPicture.Paint(setup, newGameTexture);
            newGamePicture.style.backgroundImage = new StyleBackground(newGameTexture);
            newGamePicture.MarkDirtyRepaint();
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
                    // The window has its own head: the kingdom it would start with.
                    break;
                default:
                    bool battle = InBattle;
                    SetIcon(menuShield, FlagIcon(state.World.Player));
                    menuKingdom.text = state.World.Countries[state.World.Player].Label;
                    menuMap.text = Words.MenuMap(state.World, state.Turn);
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
                // The picture under the loading screen does not move while the map is drawn.
                StopTitleVideo();
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

        /// <summary>
        /// The picture behind the main menu, when the art has it; the live map otherwise. When the
        /// art also has the picture's moving version (title_loop: its flags, trees and boar stir),
        /// that takes the picture's place from its first drawn frame; until then, and wherever it
        /// cannot play, the still picture stays.
        /// </summary>
        void ShowTitleArt(bool show)
        {
            Texture2D picture = show ? IconTexture("title_art") : null;
            if (picture != null && !TitleVideoShown)
                titleArt.style.backgroundImage = new StyleBackground(picture);
            titleArt.style.display = picture != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (picture != null)
                PlayTitleVideo();
            else
                StopTitleVideo();
        }

        void PlayTitleVideo()
        {
            if (titleVideoBroken || Application.isBatchMode || LoadingShown)
                return;
            if (titleVideo == null)
            {
                var clip = Resources.Load<VideoClip>("title_loop");
                if (clip == null)
                {
                    titleVideoBroken = true;
                    return;
                }
                titleFrames = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "Main menu picture",
                };
                titleFrames.Create();
                titleVideo = gameObject.AddComponent<VideoPlayer>();
                titleVideo.playOnAwake = false;
                titleVideo.source = VideoSource.VideoClip;
                titleVideo.clip = clip;
                titleVideo.isLooping = true;
                titleVideo.skipOnDrop = true;
                titleVideo.waitForFirstFrame = true;
                titleVideo.audioOutputMode = VideoAudioOutputMode.None;
                titleVideo.renderMode = VideoRenderMode.RenderTexture;
                titleVideo.targetTexture = titleFrames;
                titleVideo.aspectRatio = VideoAspectRatio.Stretch;
                titleVideo.frameReady += TitleFrameReady;
                titleVideo.errorReceived += TitleVideoError;
                titleVideo.loopPointReached += TitleVideoLooped;
            }
            if (titleVideo.isPlaying)
                return;
            titleVideoAsked = Time.realtimeSinceStartup;
            titleVideo.sendFrameReadyEvents = true;
            titleVideo.Play();
        }

        /// <summary>Stops the moving picture, also while it is still being prepared, and puts the still one back.</summary>
        void StopTitleVideo()
        {
            if (titleVideo != null)
                titleVideo.Stop();
            if (!TitleVideoShown)
                return;
            TitleVideoShown = false;
            Texture2D picture = IconTexture("title_art");
            if (picture != null)
                titleArt.style.backgroundImage = new StyleBackground(picture);
        }

        void TitleFrameReady(VideoPlayer player, long frame)
        {
            // Only the first frame is wanted: the event makes the processor wait for the GPU.
            player.sendFrameReadyEvents = false;
            if (TitleVideoShown)
                return;
            // Hidden while it was being prepared: it does not play on unseen.
            if (titleArt.style.display != DisplayStyle.Flex || LoadingShown)
            {
                player.Stop();
                return;
            }
            titleArt.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(titleFrames));
            TitleVideoShown = true;
            Debug.Log($"Realms: the main menu's picture moves, {(Time.realtimeSinceStartup - titleVideoAsked) * 1000f:0} ms after it was asked for.");
        }

        void TitleVideoLooped(VideoPlayer player)
        {
            if (titleVideoLooped)
                return;
            titleVideoLooped = true;
            Debug.Log($"Realms: the main menu's picture went round once ({player.clip.length:0.00} s).");
        }

        void TitleVideoError(VideoPlayer player, string message)
        {
            titleVideoBroken = true;
            Debug.LogWarning($"Realms: the main menu's moving picture cannot play ({message}); the still picture stays.");
            StopTitleVideo();
        }
    }
}
