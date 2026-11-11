using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The game: makes a world, shows it, and handles turns, building, selection and
    /// the keys (K opens the kingdoms list). The left button selects, the right one orders the
    /// chosen hero; the heroes' moves, meetings, fights and finds are in RealmsHeroes.cs, the town
    /// and hero screens in RealmsScreens.cs. With <c>-seed N</c> it opens that map; with <c>-realmsCapture DIR</c> it
    /// saves a fixed set of screenshots there (RealmsCapture.cs) and quits.
    /// </summary>
    public sealed partial class RealmsGame : MonoBehaviour
    {
        public MapView view;
        public MapCamera mapCamera;
        public Hud hud;

        GameState state;
        ArtLibrary art;
        HeroView heroes;
        BattleView battleView;
        Portraits portraits;
        int hover = -1, select = -1, cityCursor = -1;
        string captureDir;
        bool capturing;

        /// <summary>The neighbours' pace for the next new game, kept between runs; the captures play at the normal one.</summary>
        Difficulty difficulty = Difficulty.Normal;

        const string DifficultyKey = "Realms.Difficulty";

        /// <summary>Where the right button went down: released near there it is an order, else it dragged the map.</summary>
        Vector2 rightDown;
        bool rightArmed;

        void Start()
        {
            Application.targetFrameRate = 60;
            FitScreen();
            art = ArtLibrary.Load();
            string[] args = Environment.GetCommandLineArgs();
            int seed = Environment.TickCount & 0xFFFFF;
            bool seeded = false;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-seed" && int.TryParse(args[i + 1], out int s))
                {
                    seed = s;
                    seeded = true;
                }
                if (args[i] == "-realmsCapture")
                    captureDir = args[i + 1];
                // The promo is staged like the captures: a new map, nothing kept.
                if (args[i] == "-realmsPromo")
                    captureDir = promoDir = args[i + 1];
            }
            if (captureDir == null)
            {
                int kept = PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Normal);
                if (kept >= (int)Difficulty.Calm && kept <= (int)Difficulty.Race)
                    difficulty = (Difficulty)kept;
            }
            LoadGuide();
            heroes = gameObject.AddComponent<HeroView>();
            hud.HeroFigure = heroes.FigureAt;
            battleView = gameObject.AddComponent<BattleView>();
            battleView.material = view.propMaterial;
            battleView.art = art;
            battleView.hud = hud;
            battleView.view = view;
            battleView.mapCamera = mapCamera;

            hud.NextTurn += EndTurn;
            hud.MenuPicked += () =>
            {
                // Not while a hero walks or a town is founded: a new game would cut it short.
                if (busy)
                    return;
                if (OnScreen)
                    LeaveScreen();
                hud.ShowMenu(!hud.MenuOpen);
            };
            BindTitle();
            hud.LanguagePicked += SwitchLanguage;
            hud.QuitPicked += Quit;
            hud.SavePicked += ShowSaveList;
            hud.LoadPicked += ShowLoadList;
            hud.Help += () => hud.ToggleHelp(!hud.HelpVisible);
            hud.MinimapPoint += p => mapCamera.FocusKeepZoom(p);
            hud.KingdomPicked += FocusKingdom;
            hud.CityPicked += FocusCity;
            hud.BuildField += cell =>
            {
                if (Done(state.Build(state.World.Player, cell), cell))
                    LearnBuild();
            };
            hud.BuildTown += (city, building) =>
            {
                if (Done(state.Build(state.World.Player, city, building), state.World.Cities[city].Cell, building))
                    Learn(Learned.TownBuild);
            };
            hud.ExpandTown += ExpandTown;
            hud.PlaceStake += PlaceStake;
            hud.HeroPicked += index => ChooseHero(state.Heroes[index], true);
            hud.LevelUpPicked += () =>
            {
                if (!busy && hero != null)
                    StartCoroutine(LevelUps(hero));
            };
            hud.Recruit += Recruit;
            hud.HireHero += HireHero;
            hud.GatherSettlers += GatherSettlers;
            hud.FoundPicked += FoundPicked;
            hud.FlagPicked += FlagPicked;
            hud.SwapPicked += with =>
            {
                if (!busy && hero != null)
                    StartCoroutine(SwapThenFree(hero, with >= 0 ? state.Heroes[with] : null));
            };
            BindScreens();
            // The main menu with nothing drawn behind it; a map asked by its number, or the
            // captures', starts anew. After a change of language the game left comes back.
            if (captureDir != null || (seeded && !Lang.Restarted))
                NewWorld(seed);
            else
            {
                Launch();
                if (Lang.BackIntoGame)
                {
                    Lang.BackIntoGame = false;
                    Continue();
                }
            }
            if (promoDir != null)
                StartCoroutine(Promo());
            else if (captureDir != null)
                StartCoroutine(Capture());
        }

        /// <summary>
        /// Fullscreen at the display's own size. The player keeps the last window's size between
        /// runs, and fullscreen at a smaller size draws a smaller picture than the screen, with the
        /// pointer no longer over what it seems to point at.
        /// </summary>
        static void FitScreen()
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
                return;
            int width = Display.main.systemWidth, height = Display.main.systemHeight;
            if (width > 0 && height > 0 && (Screen.width != width || Screen.height != height))
                Screen.SetResolution(width, height, Screen.fullScreenMode);
        }

        /// <summary>
        /// Quitting asks nothing: the game keeps itself as it closes (OnApplicationQuit), and in a
        /// battle the save before it brings the battle back.
        /// </summary>
        void Quit() => Application.Quit();

        /// <summary>A new game on the map with the number; the game left keeps itself first.</summary>
        void NewWorld(int seed)
        {
            AutoSave(false);
            World world = WorldGen.Generate(seed);
            LoadFlag(world);
            Begin(new GameState(world) { Difficulty = difficulty }, null);
        }

        /// <summary>
        /// Shows the game, new or <paramref name="loaded"/> from a save: one way for both, so all
        /// the map's pictures are built from the state alone.
        /// </summary>
        void Begin(GameState started, SaveGame loaded)
        {
            foreach (float _ in BeginSteps(started, loaded))
            {
            }
        }

        /// <summary>
        /// <see cref="Begin"/> in steps, telling how far it has come, 0 to 1, between the map's
        /// parts, so that the loading screen is drawn while it works (<see cref="Loading"/>).
        /// </summary>
        IEnumerable<float> BeginSteps(GameState started, SaveGame loaded)
        {
            if (battleView.Active)
                battleView.Close();
            // Behind the main menu or the new game's screen the column stays.
            if (!behindTitle)
                hud.ShowMenu(false);
            LeaveScreen();
            heroes.Clear();
            World world = started.World;
            FlagArt.Bind(world, art, view.flagMaterial);
            state = started;
            view.LairTroops = state.LairTroops;
            foreach (float part in view.ShowSteps(world, art))
                yield return 0.95f * part;
            view.SetSeen(state.Seen, true);
            RenderPortraits();
            heroes.Show(state, view, art, view.propMaterial);
            mapCamera.SetBounds(new Rect(world.MinX + 2f, world.MinZ + 1f, world.MaxX - world.MinX - 4f, world.MaxZ - world.MinZ - 2f));
            hover = select = cityCursor = -1;
            hero = null;
            path = null;
            busy = fighting = false;
            gameId = loaded?.Header.GameId ?? NewGameId();
            played = loaded?.Header.Played ?? 0;
            lastReport = loaded?.Report;
            // A crown set before the save was shown then.
            firstCrownShown = loaded != null && state.Winner >= 0;
            ownCrownShown = loaded != null && state.Crowned[world.Player] > 0;
            hud.Bind(state, view, art, mapCamera);
            if (loaded != null)
            {
                Resume(loaded);
                yield return 1f;
                yield break;
            }
            List<Hero> mine = state.HeroesOf(world.Player);
            ChooseHero(mine.Count > 0 ? mine[0] : null, false);
            FocusCapital(26f, true);
            // The map's number is in the menu now, and the guiding lines show the way (the
            // interface plan's 3.3): no greeting toast. A new game's preview asks for the flag
            // once it is started (RealmsTitle.StartGame).
            if (captureDir == null && !behindTitle)
                StartCoroutine(EditFlag(true));
            yield return 1f;
        }

        void FocusCapital(float distance, bool snap = false)
        {
            World world = state.World;
            City capital = world.Cities[world.Countries[world.Player].Capital];
            mapCamera.Focus(mapCamera.Nearer(new Vector2(world.CenterX(capital.Cell), world.CenterZ(capital.Cell)), 1f), distance, snap);
        }

        void FocusKingdom(int country)
        {
            World world = state.World;
            City capital = world.Cities[world.Countries[country].Capital];
            if (!state.Seen[capital.Cell])
            {
                hud.Toast(T("Это королевство ещё скрыто туманом: разведай земли героем", "This kingdom is still hidden in the fog: scout the land with a hero"));
                return;
            }
            Select(capital.Cell);
            mapCamera.Focus(mapCamera.Nearer(new Vector2(world.CenterX(capital.Cell), world.CenterZ(capital.Cell)), 0.8f), Mathf.Min(mapCamera.Distance, 20f));
        }

        void FocusCity(int index)
        {
            City city = state.World.Cities[index];
            Select(city.Cell);
            mapCamera.Focus(mapCamera.Nearer(new Vector2(state.World.CenterX(city.Cell), state.World.CenterZ(city.Cell)), 0.6f), Mathf.Min(mapCamera.Distance, 18f));
        }

        void EndTurn()
        {
            if (busy)
                return;
            // «Следующий ход» stays in the top bar beside the menu: a click on it closes the menu.
            hud.ShowMenu(false);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            TurnReport report = state.EndTurn();
            long played = watch.ElapsedMilliseconds;
            CountTurn();
            // The neighbours' new quarters and the groups come or gone lay the ground round them
            // again, as the players' own towns do.
            var changed = new List<int>(report.Changed);
            changed.AddRange(report.Reshaped);
            bool laid = view.Refresh(changed);
            if (laid)
                heroes.Refresh();
            var taken = new List<int>();
            foreach (TurnEvent e in report.Events)
            {
                if (e.Kind == TurnEventKind.Border)
                    taken.Add(e.Cell);
            }
            view.FlashTaken(taken);
            // The moves are back: the way's turns count from now.
            if (hero != null && path != null)
                path = state.Path(hero, path.Target);
            heroes.ShowPath(hero, path);
            hud.ShowHero(hero, path);
            hud.RebuildLabels();
            // The towns' land grew: the minimap and the kingdoms' land bars too.
            hud.WorldChanged();
            hud.RefreshInfo();
            hud.ShowReport(report);
            lastReport = report;
            AutoSave();
            ShowCrownEnd();
            // The plan's measure of a turn's wait in the live game (the economy plan's step 2).
            Debug.Log($"Turn {state.Turn - 1} ended in {watch.ElapsedMilliseconds} ms: the rules {played}, " +
                $"{report.Changed.Count} cells dressed, {report.Reshaped.Count} new quarters of the neighbours, the ground laid again: {laid}");
        }

        /// <summary>
        /// After a build: show the cell again, the new building rising out of the ground (a town's
        /// <paramref name="building"/>, or else the field building on the cell), pay the tasks it
        /// finished, or say why it failed; true when built.
        /// </summary>
        bool Done(Refusal refusal, int cell, TownBuilding? building = null)
        {
            if (refusal != Refusal.None)
            {
                hud.Toast(Words.Refusal(refusal));
                return false;
            }
            // A town's new building may stand on any of its cells.
            int town = state.World.Cells[cell].City;
            var changed = new List<int> { cell };
            if (town >= 0)
                changed.AddRange(state.World.Cities[town].Districts);
            PropBuilder props = view.Props;
            props.RisingCell = building == null ? cell : -1;
            props.RisingTown = building != null ? town : -1;
            props.RisingBuilding = building ?? default;
            bool laid = view.Refresh(changed);
            props.RisingCell = props.RisingTown = -1;
            if (laid)
                heroes.Refresh();
            List<Quest> finished = state.CheckQuests();
            hud.Refresh();
            hud.RefreshInfo();
            if (finished.Count > 0)
                hud.Toast(Words.QuestDone(finished[0]), 5f);
            CrownAfterDeed();
            return true;
        }

        /// <summary>
        /// The players set their stake on the cell, or took it away with -1: the town nearest to it
        /// takes the cells on its way first (the territory plan's П3).
        /// </summary>
        void PlaceStake(int cell)
        {
            if (busy)
                return;
            World world = state.World;
            int player = world.Player;
            if (!state.SetStake(player, cell))
            {
                hud.Toast(Words.StakeRefused);
                return;
            }
            view.UpdateMarks();
            if (cell >= 0)
                Learn(Learned.Stake);
            List<Quest> finished = state.CheckQuests();
            hud.Refresh();
            hud.RefreshInfo();
            if (finished.Count > 0)
                hud.Toast(Words.QuestDone(finished[0]), 5f);
            else if (cell < 0)
                hud.Toast(Words.StakeToast(null, 0));
            else
            {
                int turns = Influence.StakeTurns(world, player, cell, out int town);
                hud.Toast(Words.StakeToast(world.Cities[town].Label, turns), 4f);
            }
        }

        void Select(int cell)
        {
            select = cell;
            view.SetSelect(cell);
            hud.ShowInfo(cell >= 0 ? cell : hover, cell >= 0);
        }

        void NextCity()
        {
            Country player = state.World.Countries[state.World.Player];
            if (player.Cities.Count == 0)
                return;
            Learn(Learned.HintTab);
            cityCursor = (cityCursor + 1) % player.Cities.Count;
            City city = state.World.Cities[player.Cities[cityCursor]];
            Select(city.Cell);
            mapCamera.Focus(mapCamera.Nearer(new Vector2(state.World.CenterX(city.Cell), state.World.CenterZ(city.Cell)), 0.6f), Mathf.Min(mapCamera.Distance, 18f));
        }

        void Update()
        {
            // Under the loading screen a map is being drawn: nothing takes input.
            if (loading)
                return;
            if (state == null)
            {
                KeysWithoutGame();
                return;
            }
            if (!behindTitle)
                CountPlayed();
            if (captureDir == null && !behindTitle)
                UpdateGuide();
            // The battle turns the map's grid off for its field (BattleView); this would turn it
            // back on every frame.
            if (!battleView.Active)
                view.SetZoom(mapCamera.Distance);
            if (capturing || battleView.Active)
                return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool help = hud.HelpVisible, modal = hud.Modal, menu = hud.MenuOpen;
            mapCamera.KeyboardBlocked = help || modal || menu;
            if (OnScreen)
            {
                if (keyboard != null && !modal)
                    ScreenKeys(keyboard, help);
                // The map takes no clicks; the pointer over the panel does not zoom the camera.
                mapCamera.PointerOverUi = mouse == null || help || modal || hud.PointerOverUi(mouse.position.ReadValue());
                return;
            }
            if (keyboard != null && !modal)
            {
                // A map's number being typed keeps its keys.
                bool typing = hud.Typing, title = menu && hud.Mode != MenuMode.Pause;
                bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                if (keyboard.f1Key.wasPressedThisFrame || (keyboard.hKey.wasPressedThisFrame && !typing))
                    hud.ToggleHelp(!help);
                else if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    // The help, the lists, the way, the selection and the hero, and only then the menu.
                    if (help)
                        hud.ToggleHelp(false);
                    else if (menu)
                    {
                        // A list of saves goes back to the menu's buttons first; the new game's
                        // screen goes back; the main menu has nothing to go back to.
                        if (!hud.CloseSaves())
                        {
                            if (hud.Mode == MenuMode.NewGame)
                                LeaveNewGame();
                            else if (hud.Mode == MenuMode.Pause)
                                hud.ShowMenu(false);
                        }
                    }
                    else if (!hud.CloseLists() && !Back() && !busy)
                        hud.ShowMenu(true);
                }
                else if (title && enter && !help && !typing && !hud.SavesOpen)
                    TitleEnter();
                else if (!help && !menu && !busy)
                {
                    if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                        EndTurn();
                    if (keyboard.tabKey.wasPressedThisFrame)
                        NextCity();
                    if (keyboard.spaceKey.wasPressedThisFrame || keyboard.homeKey.wasPressedThisFrame)
                        FocusCapital(Mathf.Min(mapCamera.Distance, 26f));
                    if (keyboard.backspaceKey.wasPressedThisFrame)
                        mapCamera.FaceDefault();
                    if (keyboard.kKey.wasPressedThisFrame)
                        hud.ToggleKingdoms();
                    if (keyboard.fKey.wasPressedThisFrame)
                        NextHero();
                    if (keyboard.tKey.wasPressedThisFrame)
                        TownKey();
                    if (keyboard.rKey.wasPressedThisFrame && hero != null)
                        EnterHero(hero);
                    if (keyboard.gKey.wasPressedThisFrame && hero != null && path != null)
                        StartCoroutine(Walk());
                }
            }

            // The map takes no input while the menu is open beside it, nor from a pointer left over
            // it while the captures or the promo run (its ring and card would follow the camera).
            if (mouse == null || hud.MenuOpen || mapCamera.Scripted)
            {
                mapCamera.PointerOverUi = true;
                return;
            }
            Vector2 pointer = mouse.position.ReadValue();
            bool overUi = help || modal || hud.PointerOverUi(pointer);
            mapCamera.PointerOverUi = overUi;
            int under = -1;
            if (!overUi && view.Field.Raycast(Camera.main.ScreenPointToRay(pointer), out Vector3 ground))
                under = view.CellAt(ground);
            // Over the panels the last cell stays, so its card can be read below.
            if (under != hover && !overUi)
            {
                hover = under;
                view.SetHover(hover);
                if (select < 0)
                    hud.ShowInfo(hover);
            }
            if (!overUi && !busy && mouse.leftButton.wasPressedThisFrame)
                Click(hover);
            if (mouse.rightButton.wasPressedThisFrame)
            {
                rightDown = pointer;
                rightArmed = !overUi;
            }
            if (mouse.rightButton.wasReleasedThisFrame && rightArmed)
            {
                rightArmed = false;
                if (!overUi && !busy && (pointer - rightDown).sqrMagnitude < 8f * 8f)
                    Order(hover);
            }
        }
    }
}
