using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The main menu and the new game's window (the interface plan's 3.2, 2026-11-11). The game
    /// opens on the main menu at once, with nothing drawn behind it: «Продолжить» names the last
    /// save from its header alone, and the game is loaded only when the players go into it, under
    /// a loading screen with a bar (the players found the start hanging while the last game loaded
    /// behind the menu, 2026-11-11). «Новая игра» opens a window of choices over the menu's
    /// picture: the kingdom's flag, the map (its picture and number), its size and the
    /// neighbours' pace. The map is made there, not drawn; «Играть» draws it under the loading
    /// screen, and the flag is raised before, so nothing is drawn twice (the players found the
    /// game hanging after «Поднять флаг» on a map already drawn, and asked for the choices first,
    /// 2026-11-12). «Назад» goes back where the window was opened, the game behind untouched. A
    /// map asked by its number (-seed) and the captures start without the main menu, as before.
    /// </summary>
    public sealed partial class RealmsGame
    {
        /// <summary>The game is behind the main menu or the new game's window: not played, counted or guided.</summary>
        bool behindTitle;

        /// <summary>A loaded game's return (its report, the hints after a break, a chest) waits until the players go in.</summary>
        bool returnPending;

        DateTime returnWritten;

        /// <summary>The players' last map size, kept between games like the pace.</summary>
        const string SizeKey = "Realms.MapSize";

        /// <summary>
        /// The game the new game's window would start: its world made and the flag raised on it,
        /// nothing drawn; null while it is being made or with the window closed.
        /// </summary>
        GameState setup;

        /// <summary>Counts the maps asked for in the window, so that one made after another was asked is dropped.</summary>
        int setupAsked;

        /// <summary>The number of the map asked for last, made or still being made.</summary>
        int setupSeed;

        /// <summary>The loading screen is up: a map is being drawn under it, and nothing takes input.</summary>
        bool loading;

        /// <summary>The main menu has a game to continue: the one behind it, or the newest save.</summary>
        bool canContinue;

        /// <summary>Where the new game's window goes back to: the menu over the game, or the main menu.</summary>
        MenuMode newGameFrom;

        /// <summary>A game of the players' is behind the menu.</summary>
        bool GameBehind => state != null;

        void BindTitle()
        {
            hud.NewGamePicked += () => OpenNewGame(MenuMode.Pause);
            hud.TitleNewPicked += () => OpenNewGame(MenuMode.Title);
            hud.ContinuePicked += Continue;
            hud.OtherMapPicked += () => MakeSetup(NewSeed(), hud.ChosenSize);
            hud.MapNumberPicked += seed => MakeSetup(seed, hud.ChosenSize);
            // Another size keeps the map's number: the number and the size together name a map.
            hud.MapSizePicked += size => MakeSetup(setupSeed, size);
            hud.NewFlagPicked += () => StartCoroutine(EditSetupFlag());
            hud.StartPicked += StartGame;
            hud.NewGameBackPicked += LeaveNewGame;
            hud.ToTitlePicked += ToTitle;
        }

        static int NewSeed() => Environment.TickCount & 0xFFFFF;

        /// <summary>The main menu at once; no game is drawn until the players choose one.</summary>
        void Launch()
        {
            behindTitle = true;
            hud.Prepare(art);
            // The new game's window paints flags before any game binds the art.
            FlagArt.UseArt(art);
            ShowTitle();
        }

        /// <summary>
        /// The main menu, with «Продолжить» naming the game behind it, or else the newest save
        /// read by its header alone; with neither, «Новая игра» is the gold one.
        /// </summary>
        void ShowTitle()
        {
            behindTitle = true;
            string line = "";
            if (GameBehind)
                line = Words.TitleContinueLine(state.World.Countries[state.World.Player].Label, state.Turn);
            else
            {
                SaveFile latest = Latest().Find(file => file.Header != null);
                if (latest != null)
                    line = Words.TitleContinueLine(Names.Shown(latest.Header.Kingdom), latest.Header.Turn);
            }
            canContinue = line.Length > 0;
            hud.ShowTitle(line);
        }

        /// <summary>The players' saves and the autosaves with their headers, the newest first.</summary>
        List<SaveFile> Latest()
        {
            var files = new List<SaveFile>(AutoFiles(true));
            foreach (SaveFile file in SlotFiles())
            {
                if (file != null)
                    files.Add(file);
            }
            files.Sort((a, b) => b.Time.CompareTo(a.Time));
            return files;
        }

        /// <summary>
        /// The loading screen over <paramref name="steps"/>, which draw a map in the frames under
        /// it, its bar following how far they have come; then <paramref name="done"/>. A step that
        /// fails brings the main menu back with the reason rather than leaving the screen up.
        /// </summary>
        IEnumerator Loading(string line, IEnumerable<float> steps, Action done)
        {
            loading = true;
            hud.ShowLoading(line, 0f);
            // The screen is on the display before the seconds of work begin.
            yield return null;
            yield return null;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var slice = System.Diagnostics.Stopwatch.StartNew();
            bool failed = false;
            using (IEnumerator<float> step = steps.GetEnumerator())
            {
                while (true)
                {
                    bool more;
                    try
                    {
                        more = step.MoveNext();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        failed = true;
                        more = false;
                    }
                    if (!more)
                        break;
                    // A frame every 60 ms or so: each frame drawn costs the work its own time.
                    if (slice.ElapsedMilliseconds < 60)
                        continue;
                    hud.ShowLoading(line, step.Current);
                    yield return null;
                    slice.Restart();
                }
            }
            Debug.Log($"Loaded under the loading screen in {watch.ElapsedMilliseconds} ms{(failed ? ", FAILED" : "")}: {line}");
            loading = false;
            hud.HideLoading();
            if (!failed)
            {
                done?.Invoke();
                yield break;
            }
            // Half a map is no game: back to the main menu with nothing behind it.
            busy = false;
            state = null;
            setup = null;
            hud.Toast(Words.ProblemOf(SaveProblem.Damaged), 5f);
            ShowTitle();
        }

        /// <summary>
        /// The new game's window, from the menu over a game or from the main menu, at the size and
        /// the pace chosen last. From a game, the game keeps itself first; it stays behind the
        /// window untouched until «Играть».
        /// </summary>
        void OpenNewGame(MenuMode from)
        {
            if (busy || loading || battleView.Active)
                return;
            newGameFrom = from;
            if (from == MenuMode.Pause && GameBehind)
                AutoSave(false);
            behindTitle = true;
            setup = null;
            MakeSetup(NewSeed(), SavedSize());
        }

        /// <summary>The size the players chose last; the captures' and a first game's is the medium one.</summary>
        MapSize SavedSize()
        {
            int kept = captureDir == null ? PlayerPrefs.GetInt(SizeKey, (int)MapSize.Medium) : (int)MapSize.Medium;
            return kept >= (int)MapSize.Small && kept <= (int)MapSize.Large ? (MapSize)kept : MapSize.Medium;
        }

        /// <summary>
        /// The window waits while the map numbered <paramref name="seed"/> of <paramref name="size"/>
        /// is made, then shows it; the same map again is shown at once.
        /// </summary>
        void MakeSetup(int seed, MapSize size)
        {
            if (busy || loading)
                return;
            Difficulty pace = hud.MenuOpen && hud.Mode == MenuMode.NewGame ? hud.ChosenPace : difficulty;
            if (setup != null && setup.World.Seed == seed && WorldSettings.SizeOf(setup.World) == size)
            {
                hud.ShowNewGame(setup, size, pace);
                return;
            }
            setup = null;
            setupSeed = seed;
            hud.ShowNewGame(null, size, pace);
            StartCoroutine(MakeSetupSteps(seed, size, ++setupAsked));
        }

        /// <summary>
        /// Makes the world aside from the frames, so the window stays alive while it is made, then
        /// raises the players' flag on it; nothing of it is drawn.
        /// </summary>
        IEnumerator MakeSetupSteps(int seed, MapSize size, int asked)
        {
            // «Карта рисуется…» is on the display before the work.
            yield return null;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            WorldSettings settings = WorldSettings.Of(size);
            Task<World> made = Task.Run(() => WorldGen.Generate(seed, settings));
            while (!made.IsCompleted)
                yield return null;
            if (asked != setupAsked || !hud.MenuOpen || hud.Mode != MenuMode.NewGame)
                yield break;
            if (made.IsFaulted || made.IsCanceled)
            {
                Debug.LogException(made.Exception);
                hud.Toast(Words.ProblemOf(SaveProblem.Damaged), 5f);
                yield break;
            }
            World world = made.Result;
            LoadFlag(world);
            setup = new GameState(world) { Difficulty = hud.ChosenPace };
            Debug.Log($"New game's map {seed} ({size}, {world.Width}×{world.Height}) made in {watch.ElapsedMilliseconds} ms");
            hud.ShowNewGame(setup, size, hud.ChosenPace);
        }

        /// <summary>
        /// «Изменить флаг» in the new game's window: the flag editor over it, the neighbours'
        /// colours those of the map made. A new field renames and recolours only that map, which is
        /// not drawn yet, so nothing hangs; the flag is kept for the next games.
        /// </summary>
        IEnumerator EditSetupFlag()
        {
            if (busy || loading || setup == null)
                yield break;
            GameState editing = setup;
            World world = editing.World;
            Country country = world.Countries[world.Player];
            busy = true;
            Hud.FlagDraft draft = null;
            int choice = 1;
            yield return AskAndWait(T("Флаг королевства", "Kingdom flag"), null,
                T("Собери флаг своего королевства: узор, цвета и знак. Цвет поля станет цветом королевства на карте и даст ему имя.", "Make your kingdom's flag: a pattern, colours and an emblem. The field's colour becomes the kingdom's colour on the map and gives it its name."),
                new[] { T("Поднять флаг", "Raise the flag"), T("Отмена", "Cancel") }, k => choice = k,
                body => flagDraft = draft = hud.FlagEditor(body, country.Flag, world));
            busy = false;
            if (choice != 0 || draft == null || draft.Flag.Equals(country.Flag))
                yield break;
            Flags.Claim(world, world.Player, draft.Flag);
            if (captureDir == null)
            {
                PlayerPrefs.SetString(FlagKey, draft.Flag.Code);
                PlayerPrefs.Save();
            }
            if (setup == editing)
                hud.ShowNewGame(setup, WorldSettings.SizeOf(world), hud.ChosenPace);
        }

        /// <summary>
        /// «Играть»: the map made becomes the game, drawn under the loading screen at the pace
        /// and size chosen, which are kept for the next game.
        /// </summary>
        void StartGame()
        {
            if (busy || loading || setup == null || !hud.MenuOpen || hud.Mode != MenuMode.NewGame)
                return;
            GameState started = setup;
            setup = null;
            setupAsked++;
            difficulty = hud.ChosenPace;
            started.Difficulty = difficulty;
            MapSize size = WorldSettings.SizeOf(started.World);
            if (captureDir == null)
            {
                PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
                PlayerPrefs.SetInt(SizeKey, (int)size);
                PlayerPrefs.Save();
            }
            behindTitle = true;
            returnPending = false;
            World world = started.World;
            string line = Words.NewGameLoading(world.Countries[world.Player].Label, world.Seed);
            StartCoroutine(Loading(line, BeginSteps(started, null), GoIn));
        }

        /// <summary>«Назад» (Esc): the menu over the game the window was opened from, or the main menu.</summary>
        void LeaveNewGame()
        {
            if (busy || loading)
                return;
            setup = null;
            setupAsked++;
            if (newGameFrom == MenuMode.Pause && GameBehind)
            {
                behindTitle = false;
                hud.ShowMenu(true);
            }
            else
                ShowTitle();
        }

        /// <summary>«В главное меню»: the game keeps itself and waits behind the main menu.</summary>
        void ToTitle()
        {
            if (busy || loading || battleView.Active)
                return;
            AutoSave(false);
            returnPending = false;
            ShowTitle();
        }

        /// <summary>
        /// «Продолжить»: into the game behind the main menu, or the newest save that opens loaded
        /// under the loading screen; then its return.
        /// </summary>
        void Continue()
        {
            if (busy || loading)
                return;
            if (GameBehind)
            {
                GoIn();
                return;
            }
            foreach (SaveFile file in Latest())
            {
                SaveGame game = ReadSave(file.Path, out _);
                if (game != null)
                {
                    Open(game);
                    return;
                }
            }
            ShowTitle();
        }

        /// <summary>A save read whole becomes the game under the loading screen, and the players go into it.</summary>
        void Open(SaveGame game)
        {
            setup = null;
            // Its return waits for the end of the loading (Resume behind the main menu).
            behindTitle = true;
            returnPending = false;
            string line = Words.TitleContinueLine(Names.Shown(game.Header.Kingdom), game.Header.Turn);
            StartCoroutine(Loading(line, BeginSteps(game.State, game), GoIn));
        }

        /// <summary>Into the game behind the main menu, with its return if it was loaded.</summary>
        void GoIn()
        {
            behindTitle = false;
            hud.ShowMenu(false);
            if (returnPending)
                ShowReturn();
        }

        /// <summary>The main menu's gold button for Enter: «Играть», «Продолжить», or «Новая игра» with nothing to continue.</summary>
        void TitleEnter()
        {
            if (hud.Mode == MenuMode.NewGame)
                StartGame();
            else if (canContinue)
                Continue();
            else
                OpenNewGame(MenuMode.Title);
        }

        /// <summary>
        /// The main menu's keys while no map is drawn behind it: the help (F1, H), Esc back from
        /// a list of saves, out of the help or out of the new game's window, Enter on the gold
        /// button; a map's number being typed keeps its keys.
        /// </summary>
        void KeysWithoutGame()
        {
            mapCamera.KeyboardBlocked = true;
            mapCamera.PointerOverUi = true;
            var keyboard = Keyboard.current;
            if (keyboard == null || hud.Modal)
                return;
            bool help = hud.HelpVisible, typing = hud.Typing;
            if (keyboard.f1Key.wasPressedThisFrame || (keyboard.hKey.wasPressedThisFrame && !typing))
                hud.ToggleHelp(!help);
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (help)
                    hud.ToggleHelp(false);
                else if (!hud.CloseMenuList() && hud.MenuOpen && hud.Mode == MenuMode.NewGame)
                    LeaveNewGame();
            }
            else if (!help && !typing && hud.MenuOpen && !hud.MenuListOpen
                && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                TitleEnter();
        }

        /// <summary>
        /// Another language (the interface plan's 3.6): a game being played keeps itself, the scene
        /// starts again with every word in the new language, and the game opens again under the
        /// loading screen; from the main menu, the main menu comes back.
        /// </summary>
        void SwitchLanguage(Language language)
        {
            if (busy || loading || battleView.Active || language == Lang.Now)
                return;
            bool inGame = GameBehind && !behindTitle;
            if (inGame && !AutoSave(false))
                return;
            Lang.Choose(language, captureDir == null);
            Lang.Restarted = true;
            Lang.BackIntoGame = inGame;
            Debug.Log($"Language: {language}, the scene starts again{(inGame ? " and the game opens again" : "")}");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Back in a loaded game: the last turn's report, the hints after a long break, the toast and a pending chest.</summary>
        void ShowReturn()
        {
            returnPending = false;
            // The last turn's report under «Ход N. В прошлый ход:», and after a long break the
            // hints players forget first come once more (the interface plan's 3.3).
            if (lastReport != null)
                hud.ShowReport(lastReport, Words.Returned(state.Turn));
            AfterBreak(returnWritten);
            hud.Toast(Words.Loaded(state.World.Countries[state.World.Player].Label, state.Turn), 5f);
            if (state.Chest != null)
                StartCoroutine(OpenChest());
        }
    }
}
