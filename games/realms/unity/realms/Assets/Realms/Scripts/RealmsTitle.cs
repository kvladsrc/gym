using System;
using System.Collections;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Realms
{
    /// <summary>
    /// The main menu and the new game's screen (the interface plan's 3.2, 2026-11-11). The game
    /// opens on the main menu at once, with nothing drawn behind it: «Продолжить» names the last
    /// save from its header alone, and the game is loaded only when the players go into it, under
    /// a loading screen with a bar (the players found the start hanging while the last game loaded
    /// behind the menu, 2026-11-11). «Новая игра» draws the map a new game would start on beside
    /// the column, under the same loading screen: a preview, never saved; «Начать» starts it
    /// there, «Назад» brings back the game it replaced. A map asked by its number (-seed) and
    /// the captures start without the main menu, as before.
    /// </summary>
    public sealed partial class RealmsGame
    {
        /// <summary>The game is behind the main menu or the new game's screen: not played, counted or guided.</summary>
        bool behindTitle;

        /// <summary>The state is a new game's preview, not a game: it is never saved.</summary>
        bool previewing;

        /// <summary>A loaded game's return (its report, the hints after a break, a chest) waits until the players go in.</summary>
        bool returnPending;

        DateTime returnWritten;

        /// <summary>The game comes back as it was from the new game's screen: nothing to tell.</summary>
        bool restoring;

        /// <summary>The loading screen is up: a map is being drawn under it, and nothing takes input.</summary>
        bool loading;

        /// <summary>The main menu has a game to continue: the one behind it, or the newest save.</summary>
        bool canContinue;

        /// <summary>Where the new game's screen goes back to, and the game it replaced while open.</summary>
        MenuMode newGameFrom;

        SaveGame keptGame;

        /// <summary>A game of the players' is behind the menu, not a preview and not nothing.</summary>
        bool GameBehind => state != null && !previewing;

        void BindTitle()
        {
            hud.NewGamePicked += () => OpenNewGame(MenuMode.Pause);
            hud.TitleNewPicked += () => OpenNewGame(MenuMode.Title);
            hud.ContinuePicked += Continue;
            hud.OtherMapPicked += () => ShowMap(Environment.TickCount & 0xFFFFF);
            hud.MapNumberPicked += ShowMap;
            hud.StartPicked += StartGame;
            hud.NewGameBackPicked += LeaveNewGame;
            hud.ToTitlePicked += ToTitle;
        }

        /// <summary>The main menu at once; no game is drawn until the players choose one.</summary>
        void Launch()
        {
            behindTitle = true;
            hud.Prepare(art);
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
            previewing = restoring = false;
            keptGame = null;
            hud.Toast(Words.ProblemOf(SaveProblem.Damaged), 5f);
            ShowTitle();
        }

        /// <summary>A new game's map behind the menu's column, not played and not saved until «Начать».</summary>
        IEnumerable<float> PreviewSteps(int seed)
        {
            previewing = behindTitle = true;
            World world = WorldGen.Generate(seed);
            yield return 0.08f;
            LoadFlag(world);
            foreach (float part in BeginSteps(new GameState(world) { Difficulty = difficulty }, null))
                yield return 0.08f + 0.92f * part;
        }

        /// <summary>
        /// The new game's screen, its map drawn under the loading screen. From a game, the game
        /// keeps itself first and is held to come back with «Назад»; from the main menu the game
        /// behind it is in its autosave already, and «Продолжить» loads it again; a preview
        /// already drawn is reused.
        /// </summary>
        void OpenNewGame(MenuMode from)
        {
            if (busy || loading || battleView.Active)
                return;
            newGameFrom = from;
            if (previewing && state != null)
            {
                hud.ShowNewGame(state.World.Seed, difficulty);
                return;
            }
            keptGame = null;
            if (from == MenuMode.Pause)
            {
                AutoSave(false);
                keptGame = CanSave ? Snapshot(SaveKind.Auto) : null;
            }
            int seed = Environment.TickCount & 0xFFFFF;
            StartCoroutine(Loading(Words.NewGameMap(seed), PreviewSteps(seed), () => hud.ShowNewGame(seed, difficulty)));
        }

        /// <summary>Another map for the new game, or the one with the number typed.</summary>
        void ShowMap(int seed)
        {
            if (!previewing || busy || loading || seed == state.World.Seed)
                return;
            Difficulty pace = hud.ChosenPace;
            StartCoroutine(Loading(Words.NewGameMap(seed), PreviewSteps(seed), () => hud.ShowNewGame(seed, pace)));
        }

        /// <summary>«Начать»: the preview becomes the game, at the pace chosen, and the flag editor opens.</summary>
        void StartGame()
        {
            if (!previewing || busy || loading || state == null)
                return;
            previewing = behindTitle = returnPending = false;
            keptGame = null;
            difficulty = hud.ChosenPace;
            if (captureDir == null)
            {
                PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
                PlayerPrefs.Save();
            }
            state.Difficulty = difficulty;
            gameId = NewGameId();
            played = 0;
            hud.ShowMenu(false);
            if (captureDir == null)
                StartCoroutine(EditFlag(true));
        }

        /// <summary>«Назад» (Esc): the game the screen replaced comes back to play; from the main menu, the main menu.</summary>
        void LeaveNewGame()
        {
            if (busy || loading)
                return;
            if (keptGame == null)
            {
                if (newGameFrom == MenuMode.Title || previewing)
                    ShowTitle();
                else
                {
                    behindTitle = false;
                    hud.ShowMenu(false);
                }
                return;
            }
            SaveGame kept = keptGame;
            keptGame = null;
            previewing = false;
            restoring = true;
            string line = Words.TitleContinueLine(Names.Shown(kept.Header.Kingdom), kept.Header.Turn);
            StartCoroutine(Loading(line, BeginSteps(kept.State, kept), () =>
            {
                restoring = false;
                behindTitle = false;
                hud.ShowMenu(false);
            }));
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
            previewing = false;
            keptGame = null;
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

        /// <summary>The main menu's gold button for Enter: «Начать», «Продолжить», or «Новая игра» with nothing to continue.</summary>
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
        /// a list of saves or out of the help, Enter on the gold button.
        /// </summary>
        void KeysWithoutGame()
        {
            mapCamera.KeyboardBlocked = true;
            mapCamera.PointerOverUi = true;
            var keyboard = Keyboard.current;
            if (keyboard == null || hud.Modal)
                return;
            bool help = hud.HelpVisible;
            if (keyboard.f1Key.wasPressedThisFrame || keyboard.hKey.wasPressedThisFrame)
                hud.ToggleHelp(!help);
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (help)
                    hud.ToggleHelp(false);
                else
                    hud.CloseSaves();
            }
            else if (!help && hud.MenuOpen && !hud.SavesOpen
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
