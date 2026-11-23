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

        /// <summary>"menu": the capture takes only the menu's frames (-realmsCaptureOnly menu).</summary>
        string captureOnly;
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
                if (args[i] == "-realmsCaptureOnly")
                    captureOnly = args[i + 1];
                // The promo is staged like the captures: a new map, nothing kept.
                if (args[i] == "-realmsPromo")
                    captureDir = promoDir = args[i + 1];
            }
            // Sound (the sound plan of 2026-11-11 with its fal.ai changes of 2026-11-12): silent
            // in batchmode, the captures and the promo unless -realmsSound asks for it, and the
            // captures write every sound played to sounds.log beside their frames; a silent
            // capture also records its mix, unheard, to mix.wav.
            Sounds.Create(gameObject);
            Music.Create(gameObject);
            Ambience.Create(gameObject);
            bool silent = (Application.isBatchMode || captureDir != null) && Array.IndexOf(args, "-realmsSound") < 0;
            if (silent)
                AudioListener.volume = 0f;
            // The captures and the promo show the game as it comes: the settings' defaults for this
            // run, the players' own kept as they are.
            if (captureDir != null)
                GameSettings.UseDefaultsForThisRun();
            if (captureDir != null)
            {
                Sounds.LogPath = System.IO.Path.Combine(captureDir, "sounds.log");
                if (silent && !Application.isBatchMode)
                {
                    // The plan's mix numbers are for the sliders' defaults.
                    SoundSettings.UseDefaultsForThisRun();
                    MixRecorder.Attach(System.IO.Path.Combine(captureDir, "mix.wav"));
                }
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
            hud.SettingsPicked += hud.ShowSettings;
            hud.AboutPicked += ShowAbout;
            hud.HintsAgainPicked += GuideAgain;
            hud.Help += () => hud.ToggleHelp(!hud.HelpVisible);
            hud.MinimapPoint += p => mapCamera.FocusKeepZoom(p);
            hud.KingdomPicked += FocusKingdom;
            hud.CityPicked += FocusCity;
            hud.BuildField += cell =>
            {
                Stock income = Economy.Income(state.World, state.World.Player);
                if (Done(state.Build(state.World.Player, cell), cell, null, income))
                    LearnBuild();
            };
            hud.BuildTown += (city, building) =>
            {
                Stock income = Economy.Income(state.World, state.World.Player);
                if (Done(state.Build(state.World.Player, city, building), state.World.Cities[city].Cell, building, income))
                    Learn(Learned.TownBuild);
            };
            hud.RaiseBuild += city => RaiseStage(city);
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

        /// <summary>A new game on the map with the number (-seed, the captures); the game left keeps itself first.</summary>
        void NewWorld(int seed, MapSize size = MapSize.Medium)
        {
            AutoSave(false);
            World world = WorldGen.Generate(seed, WorldSettings.Of(size));
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
            // Behind the main menu or the new game's window the menu stays, under the loading screen.
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
            // The ambience weighs the land in view, and counts the unseen as fog.
            Ambience.Bind(view, mapCamera, cell => state != null && cell >= 0 && cell < state.Seen.Length && state.Seen[cell]);
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
            // interface plan's 3.3): no greeting toast. The flag was chosen in the new game's
            // window before the map was drawn (RealmsTitle.EditSetupFlag).
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

        /// <summary>
        /// Where the players are, for the ambience and the music (the sound plan's П5 and П6):
        /// the main menu, the map, a town or the hero screen, a battle; how far the camera is,
        /// and whether anyone is at the game (the music rests after six minutes without input).
        /// </summary>
        void SoundPlace()
        {
            Ambience.Place place = loading ? Ambience.Place.Off
                : behindTitle ? Ambience.Place.Menu
                : state == null ? Ambience.Place.Off
                : battleView.Active ? Ambience.Place.Battle
                : mode == Mode.Town ? Ambience.Place.Town
                : mode == Mode.Hero ? Ambience.Place.Hero
                : Ambience.Place.Map;
            Ambience.Now = place;
            if (place == Ambience.Place.Menu)
                Music.Menu();
            else if (place is Ambience.Place.Map or Ambience.Place.Town or Ambience.Place.Hero)
                Music.Map();
            else if (place == Ambience.Place.Off)
                Music.Stop();
            Sounds.Far = Mathf.InverseLerp(mapCamera.minDistance, mapCamera.maxDistance, mapCamera.Distance);
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                || (mouse != null && (mouse.rightButton.wasPressedThisFrame || mouse.scroll.ReadValue().y != 0f)))
                Sounds.Touch();
        }

        void EndTurn()
        {
            if (busy)
                return;
            // «Следующий ход» stays in the top bar beside the menu: a click on it closes the menu.
            hud.ShowMenu(false);
            // The last turn's show ends before the next one's begins.
            hud.FinishTurnShow();
            Rise.FinishAll();
            Stock before = state.Treasury[state.World.Player];
            var watch = System.Diagnostics.Stopwatch.StartNew();
            TurnReport report = state.EndTurn();
            long played = watch.ElapsedMilliseconds;
            CountTurn();
            // The neighbours' new quarters and the groups come or gone lay the ground round them
            // again, as the players' own towns do.
            var changed = new List<int>(report.Changed);
            changed.AddRange(report.Reshaped);
            // The towns' new houses wait flat and rise one after another, in the turn's show.
            List<(int City, bool Level)> grown = Grown(report);
            PropBuilder props = view.Props;
            props.RisingHouses = new Dictionary<int, (int From, float Delay)>();
            for (int i = 0; i < grown.Count; i++)
            {
                City city = state.World.Cities[grown[i].City];
                props.RisingHouses[city.Index] = (city.Houses - 1, Hud.FirstHouse + i * Hud.HouseGap);
            }
            bool laid;
            try
            {
                laid = view.Refresh(changed);
            }
            finally
            {
                props.RisingHouses = null;
            }
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
            hud.ShowTurn(before, report.Income, grown);
            // The gems the turn set fly into the crown with the day's events (the visual plan's 12).
            var gems = new List<Gem>();
            foreach (TurnEvent e in report.Events)
            {
                if (e.Kind == TurnEventKind.GemSet && e.Country == state.World.Player)
                    gems.Add(e.Gem);
            }
            hud.GemsThisTurn(gems);
            TurnSounds(report);
            lastReport = report;
            AutoSave();
            ShowCrownEnd();
            // The plan's measure of a turn's wait in the live game (the economy plan's step 2).
            Debug.Log($"Turn {state.Turn - 1} ended in {watch.ElapsedMilliseconds} ms: the rules {played}, " +
                $"{report.Changed.Count} cells dressed, {report.Reshaped.Count} new quarters of the neighbours, the ground laid again: {laid}");
        }

        /// <summary>
        /// The players' towns that got a house this turn, with whether each reached a new level:
        /// those in view first, the nearest to the middle of the screen first, so that the houses
        /// the players look at rise first.
        /// </summary>
        List<(int City, bool Level)> Grown(TurnReport report)
        {
            var grown = new List<(int City, bool Level, float Away)>();
            Camera eye = Camera.main;
            foreach (TurnEvent e in report.Events)
            {
                if (e.Kind != TurnEventKind.Grew && e.Kind != TurnEventKind.LevelUp)
                    continue;
                int cell = state.World.Cities[e.City].Cell;
                float x = state.World.CenterX(cell), z = state.World.CenterZ(cell);
                float away = 100f;
                if (eye != null)
                {
                    Vector3 v = eye.WorldToViewportPoint(new Vector3(x, view.Field.Height(x, z), z));
                    bool seen = v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
                    away = (new Vector2(v.x, v.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude + (seen ? 0f : 10f);
                }
                grown.Add((e.City, e.Kind == TurnEventKind.LevelUp, away));
            }
            grown.Sort((a, b) => a.Away.CompareTo(b.Away));
            return grown.ConvertAll(g => (g.City, g.Level));
        }

        /// <summary>
        /// Any key or click ends the turn's show and every rising building at once: the show never
        /// keeps the players waiting (the visual plan of 2026-11-11). The frame the show began on
        /// does not end it.
        /// </summary>
        void CutShows(Keyboard keyboard, Mouse mouse)
        {
            if (!hud.TurnShowing && !Rise.Any)
                return;
            bool pressed = (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
                    || mouse.middleButton.wasPressedThisFrame));
            if (!pressed || Time.frameCount == hud.TurnShowFrame)
                return;
            hud.FinishTurnShow();
            Rise.FinishAll();
        }

        /// <summary>
        /// After a build: show the cell again, the new building rising out of the ground (a town's
        /// <paramref name="building"/>, or else the field building on the cell), what it adds to the
        /// income over it when <paramref name="incomeBefore"/> is given, pay the tasks it finished,
        /// or say why it failed; true when built.
        /// </summary>
        bool Done(Refusal refusal, int cell, TownBuilding? building = null, Stock? incomeBefore = null)
        {
            return Done(refusal, cell, building, incomeBefore, -1);
        }

        /// <summary>
        /// The players raise the next stage of the capital's Great Build: it grows on the map out
        /// of the stage below (PropBuilder.RisingStage), and its gold floats up as a building's does.
        /// </summary>
        bool RaiseStage(int city)
        {
            Stock income = Economy.Income(state.World, state.World.Player);
            return Done(state.RaiseBuild(state.World.Player, city), state.World.Cities[city].Cell, null, income, city);
        }

        /// <summary>As the other Done; <paramref name="stageTown"/> is the capital whose Great Build just rose a stage, or -1.</summary>
        bool Done(Refusal refusal, int cell, TownBuilding? building, Stock? incomeBefore, int stageTown)
        {
            if (refusal != Refusal.None)
            {
                Sounds.Play(Sfx.ClickOff);
                hud.Toast(Words.Refusal(refusal));
                return false;
            }
            Sounds.Play(BuildSound(state.World, cell, building));
            // A town's new building may stand on any of its cells.
            int town = state.World.Cells[cell].City;
            var changed = new List<int> { cell };
            if (town >= 0)
                changed.AddRange(state.World.Cities[town].Districts);
            PropBuilder props = view.Props;
            props.RisingCell = building == null && stageTown < 0 ? cell : -1;
            props.RisingTown = building != null ? town : -1;
            props.RisingBuilding = building ?? default;
            props.RisingStage = stageTown;
            bool laid = view.Refresh(changed);
            props.RisingCell = props.RisingTown = props.RisingStage = -1;
            if (laid)
                heroes.Refresh();
            if (incomeBefore.HasValue)
                FloatIncome(incomeBefore.Value, cell);
            List<Quest> finished = state.CheckQuests();
            hud.Refresh();
            hud.RefreshInfo();
            if (finished.Count > 0)
                QuestToasts(finished);
            CrownAfterDeed();
            return true;
        }

        /// <summary>
        /// «+2 еды за ход» rising over a new building: what it adds to the kingdom's income (the
        /// interface plan's 3.4 of 2026-11-11), so the players see what the build was for.
        /// </summary>
        void FloatIncome(Stock before, int cell)
        {
            Stock gain = Economy.Income(state.World, state.World.Player) - before;
            Stock[] goods = { new Stock(gain.Gold, 0, 0, 0), new Stock(0, gain.Food, 0, 0), new Stock(0, 0, gain.Wood, 0), new Stock(0, 0, 0, gain.Stone) };
            var parts = new List<string>();
            foreach (Stock one in goods)
            {
                string words = Words.Goods(one);
                if (words.Length > 0)
                    parts.Add("+" + words);
            }
            if (parts.Count == 0)
                return;
            string all = string.Join(", ", parts);
            float x = state.World.CenterX(cell), z = state.World.CenterZ(cell);
            var point = new Vector3(x, view.Field.Height(x, z) + 0.45f, z);
            hud.Float(T($"{all} за ход", $"{all} a turn"), "float-income", point, 1.8f, hud.AboveName(point, cell));
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
                Sounds.Play(Sfx.ClickOff);
                hud.Toast(Words.StakeRefused);
                return;
            }
            Sounds.Play(cell >= 0 ? Sfx.Stake : Sfx.Back);
            view.UpdateMarks();
            if (cell >= 0)
                Learn(Learned.Stake);
            List<Quest> finished = state.CheckQuests();
            hud.Refresh();
            hud.RefreshInfo();
            if (finished.Count > 0)
                QuestToasts(finished);
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
            SoundPlace();
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
            CutShows(keyboard, mouse);
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
                        // window goes back; the main menu has nothing to go back to.
                        if (!hud.CloseMenuList())
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
                else if (title && enter && !help && !typing && !hud.MenuListOpen)
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
                    if (keyboard.cKey.wasPressedThisFrame)
                        hud.ShowEvents();
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
