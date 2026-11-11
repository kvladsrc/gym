using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Realms
{
    /// <summary>
    /// The guiding lines (the interface plan's 3.3, 2026-11-11), in place of a wall of text: the
    /// first task on offer that teaches a gesture (Core/Guide.Taught) gets a thin «как» line under
    /// it, gold rings on the cells that fit and a gold outline on the button to press; the end-turn
    /// button calls when no task on offer can move on this turn; and four hints come once each,
    /// when they are needed. What the players have done themselves is kept in the settings
    /// (PlayerPrefs), not in the save: a gesture done is not shown again unless they stand at its
    /// task for <see cref="LearnedIdle"/> seconds. The farm teaches building with the line, the
    /// lumber camp repeats it with the rings and the outline only. The conditions on the game are
    /// in Core/Guide.cs; the captures start with nothing learned and keep nothing.
    /// </summary>
    public sealed partial class RealmsGame
    {
        /// <summary>The gestures done and the one-time hints shown, as bits kept in the settings.</summary>
        [System.Flags]
        enum Learned
        {
            None = 0,
            BuildOnce = 1,
            Build = 2,
            Order = 4,
            Chest = 8,
            Stake = 16,
            TownBuild = 32,
            HintAlt = 64,
            HintCamera = 128,
            HintArmy = 256,
            HintTab = 512,
            EndTurn = 1024,
        }

        const string LearnedKey = "Realms.Guide.Learned", TurnsKey = "Realms.Guide.Turns", SeenKey = "Realms.Guide.Seen";

        /// <summary>How often the step is worked out again, in seconds.</summary>
        const float GuideEvery = 0.3f;

        /// <summary>A gesture done shows again after this long without a deed.</summary>
        const float LearnedIdle = 15f;

        /// <summary>In the first turns of a newcomer the end-turn button calls after this long without a deed.</summary>
        const float CallIdle = 30f;

        /// <summary>On the first turn the camera hint comes after this long with the camera still.</summary>
        const float CameraIdle = 20f;

        /// <summary>How long a one-time hint stays.</summary>
        const float HintSeconds = 8f;

        /// <summary>The turns a newcomer's end-turn button calls after <see cref="CallIdle"/>.</summary>
        const int NewcomerTurns = 3;

        Learned learned;

        /// <summary>The lines the players have seen, for the help's page of hints.</summary>
        Learned seen;

        /// <summary>After a break of more than three days the camera and order hints come once more.</summary>
        bool afterBreak;

        /// <summary>A long break between plays: the hints players forget first come once more (Hodent, p. 49–50).</summary>
        const double BreakDays = 3;

        int turnsEver, monsterClicks;
        float guideAt, lastDeed, hintUntil, cameraStill;
        string hint;
        readonly List<int> guideCells = new List<int>();

        /// <summary>The easy group last worked out, and for which hero, place, turn and army: the odds cost.</summary>
        Hero easyHero;
        int easyCell = -1, easyCellOf = -1, easyTurn = -1;
        float easyArmy = -1f;

        void LoadGuide()
        {
            lastDeed = Time.unscaledTime;
            if (captureDir != null)
                return;
            learned = (Learned)PlayerPrefs.GetInt(LearnedKey, 0);
            seen = (Learned)PlayerPrefs.GetInt(SeenKey, 0);
            turnsEver = PlayerPrefs.GetInt(TurnsKey, 0);
            hud.SetSeenHints(SeenLines());
        }

        /// <summary>The line of the help's hints page for each thing seen, in the order a game teaches them.</summary>
        List<string> SeenLines()
        {
            var lines = new List<string>();
            void Add(Learned what, string line)
            {
                if ((seen & what) != 0)
                    lines.Add(line);
            }
            Add(Learned.Build, Words.SeenBuild);
            Add(Learned.Order, Words.SeenOrder);
            Add(Learned.EndTurn, Words.SeenEndTurn);
            Add(Learned.HintCamera, Words.HintCamera);
            Add(Learned.HintArmy, Words.HintArmy);
            Add(Learned.Chest, Words.SeenChest);
            Add(Learned.Stake, Words.SeenStake);
            Add(Learned.TownBuild, Words.SeenTownBuild);
            Add(Learned.HintAlt, Words.HintAlt);
            Add(Learned.HintTab, Words.HintTab);
            return lines;
        }

        /// <summary>A line was shown: its hint joins the help's page of hints.</summary>
        void See(Learned what)
        {
            if ((seen & what) == what)
                return;
            seen |= what;
            hud.SetSeenHints(SeenLines());
            if (captureDir != null)
                return;
            PlayerPrefs.SetInt(SeenKey, (int)seen);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// A game opened after more than <see cref="BreakDays"/> days: the camera hint and the
        /// hero's order are shown once more (the interface plan's 3.3).
        /// </summary>
        void AfterBreak(System.DateTime written)
        {
            if ((System.DateTime.UtcNow - written).TotalDays <= BreakDays)
                return;
            afterBreak = true;
            learned &= ~(Learned.HintCamera | Learned.Order);
            if (captureDir != null)
                return;
            PlayerPrefs.SetInt(LearnedKey, (int)learned);
            PlayerPrefs.Save();
        }

        bool Knows(Learned what) => (learned & what) == what;

        /// <summary>The players did it themselves (or saw the hint): kept, except in the captures.</summary>
        void Learn(Learned what)
        {
            if (Knows(what))
                return;
            learned |= what;
            guideAt = 0f;
            if (captureDir != null)
                return;
            PlayerPrefs.SetInt(LearnedKey, (int)learned);
            PlayerPrefs.Save();
        }

        /// <summary>A field building done by the players: the first teaches, the second learns.</summary>
        void LearnBuild() => Learn(Knows(Learned.BuildOnce) ? Learned.Build : Learned.BuildOnce);

        /// <summary>A turn ended by the players: the newcomer's first turns are counted.</summary>
        void CountTurn()
        {
            if (captureDir != null || turnsEver >= 1000)
                return;
            turnsEver++;
            PlayerPrefs.SetInt(TurnsKey, turnsEver);
            PlayerPrefs.Save();
        }

        /// <summary>A click on a monster's cell: the third brings the Alt hint once.</summary>
        void CountMonsterClick(int cell)
        {
            if (cell < 0 || !state.Seen[cell] || state.World.Cells[cell].Monster == Monster.None)
                return;
            if (++monsterClicks >= 3 && !Knows(Learned.HintAlt))
                ShowHint(Words.HintAlt, Learned.HintAlt);
        }

        void ShowHint(string text, Learned what)
        {
            hint = text;
            hintUntil = Time.unscaledTime + HintSeconds;
            Learn(what);
            See(what);
        }

        /// <summary>Each frame: the deeds and the camera are watched; the step is worked out every <see cref="GuideEvery"/> s.</summary>
        void UpdateGuide()
        {
            float now = Time.unscaledTime;
            WatchDeeds(now);
            if (now < guideAt)
                return;
            guideAt = now + GuideEvery;
            GuideNow();
        }

        void WatchDeeds(float now)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool moved = false;
            if (keyboard != null)
            {
                if (keyboard.anyKey.wasPressedThisFrame)
                    lastDeed = now;
                // The players who hold Alt know it.
                if (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed)
                    Learn(Learned.HintAlt);
                moved = keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed
                    || keyboard.upArrowKey.isPressed || keyboard.downArrowKey.isPressed || keyboard.leftArrowKey.isPressed
                    || keyboard.rightArrowKey.isPressed || keyboard.qKey.isPressed || keyboard.eKey.isPressed;
            }
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                    lastDeed = now;
                moved |= mouse.scroll.ReadValue().y != 0f
                    || (mouse.rightButton.isPressed || mouse.middleButton.isPressed) && mouse.delta.ReadValue().sqrMagnitude > 4f;
            }
            if (moved)
            {
                cameraStill = 0f;
                Learn(Learned.HintCamera);
            }
            else
                cameraStill += Time.unscaledDeltaTime;
        }

        /// <summary>Works the step out and shows it: the line, the rings and the outlined button.</summary>
        void GuideNow()
        {
            if (state == null)
                return;
            float now = Time.unscaledTime;
            if (battleView.Active || hud.MenuOpen || hud.Modal || busy)
            {
                hud.SetGuide(null, null, null, false);
                view.SetGuide(null);
                return;
            }
            OneTimeHints(now);
            guideCells.Clear();
            string line = null, button = null;
            Quest? under = null;
            bool idle = now - lastDeed >= LearnedIdle, call;
            if (Guide.NothingToDo(state))
            {
                // The button always calls then; the line only for a newcomer or one who waits.
                call = true;
                if (turnsEver < NewcomerTurns || idle)
                {
                    line = Words.GuideEndTurn;
                    See(Learned.EndTurn);
                }
            }
            else
            {
                foreach (Quest quest in state.Offered())
                {
                    if (Step(quest, idle, ref line, ref button))
                    {
                        under = quest;
                        if (line != null)
                            See(Taught(quest));
                        break;
                    }
                }
                call = turnsEver < NewcomerTurns && now - lastDeed >= CallIdle;
            }
            if (hint != null && now < hintUntil)
            {
                line = hint;
                under = null;
            }
            else
                hint = null;
            hud.SetGuide(under, line, button, call);
            view.SetGuide(OnScreen ? null : guideCells);
        }

        /// <summary>The one-time hints, when their moment comes: two towns, a hero without an army, a still camera on the first turn.</summary>
        void OneTimeHints(float now)
        {
            if (hint != null && now < hintUntil)
                return;
            World world = state.World;
            if (!Knows(Learned.HintTab) && world.Countries[world.Player].Cities.Count >= 2)
                ShowHint(Words.HintTab, Learned.HintTab);
            else if (!Knows(Learned.HintArmy) && hero != null && hero.Army.Count == 0)
                ShowHint(Words.HintArmy, Learned.HintArmy);
            else if (!Knows(Learned.HintCamera) && (state.Turn == 1 || afterBreak) && cameraStill >= CameraIdle && !OnScreen)
                ShowHint(Words.HintCamera, Learned.HintCamera);
        }

        /// <summary>
        /// The task's step, when it teaches a gesture not yet learned (or waited at) and can move on
        /// this turn: the line, the rings into <see cref="guideCells"/>, the button's name.
        /// </summary>
        bool Step(Quest quest, bool idle, ref string line, ref string button)
        {
            World world = state.World;
            int player = world.Player;
            switch (quest)
            {
                case Quest.Farm:
                case Quest.LumberCamp:
                {
                    if (Knows(Learned.Build) && !idle || !Guide.CanAdvance(state, quest))
                        return false;
                    FieldBuilding building = Guide.FieldFor(quest);
                    // The second time the gesture is shown without words.
                    bool quiet = Knows(Learned.BuildOnce) && !idle;
                    if (select >= 0 && Fits(select, building))
                    {
                        button = Words.Field(building);
                        line = quiet ? null : Words.GuidePress(button);
                    }
                    else
                    {
                        guideCells.AddRange(Guide.BuildSpots(state, player, building));
                        line = quiet ? null : Words.GuideBuildCell;
                    }
                    return true;
                }
                case Quest.Fight:
                    if (Knows(Learned.Order) && !idle || !Guide.CanAdvance(state, quest))
                        return false;
                    return FightStep(ref line);
                case Quest.Chest:
                {
                    if (Knows(Learned.Chest) && !idle || !Guide.CanAdvance(state, quest))
                        return false;
                    int capital = world.Countries[player].Capital;
                    if (hero == null && capital < 0)
                        return false;
                    Hex from = world.Cells[hero != null ? hero.Cell : world.Cities[capital].Cell].Hex;
                    List<int> chests = Guide.SeenChests(state, from);
                    if (chests.Count == 0)
                    {
                        line = Words.GuideChestFog;
                        return true;
                    }
                    guideCells.Add(chests[0]);
                    line = hero == null ? Words.GuidePickHero
                        : path != null && path.Target == chests[0] ? Words.GuideGo : Words.GuideChest;
                    return true;
                }
                case Quest.Stake:
                {
                    if (Knows(Learned.Stake) && !idle || !Guide.CanAdvance(state, quest))
                        return false;
                    if (select >= 0 && Stakeable(select))
                    {
                        button = Words.StakeSet;
                        line = Words.GuidePress(Words.StakeSet);
                        return true;
                    }
                    int spot = Guide.StakeSpot(state, player);
                    if (spot < 0)
                        return false;
                    guideCells.Add(spot);
                    line = Words.GuideStakeCell;
                    return true;
                }
                case Quest.Barracks:
                    if (Knows(Learned.TownBuild) && !idle || !Guide.CanAdvance(state, quest))
                        return false;
                    return TownStep(TownBuilding.Barracks, ref line, ref button);
                default:
                    return false;
            }
        }

        /// <summary>The gesture a task's step teaches.</summary>
        static Learned Taught(Quest quest) =>
            quest == Quest.Fight ? Learned.Order : quest == Quest.Chest ? Learned.Chest : quest == Quest.Stake ? Learned.Stake
            : quest == Quest.Barracks ? Learned.TownBuild : Learned.Build;

        /// <summary>A cell of the players' that can take the field building now.</summary>
        bool Fits(int cell, FieldBuilding building)
        {
            World world = state.World;
            Cell c = world.Cells[cell];
            return c.Resource != Resource.None && Economy.BuildingFor(c.Resource) == building
                && Economy.CanBuild(world, state.Treasury[world.Player], world.Player, cell) == Refusal.None;
        }

        /// <summary>A seen cell whose card offers «Поставить вешку» (the players have no stake yet).</summary>
        bool Stakeable(int cell)
        {
            World world = state.World;
            return state.Seen[cell] && world.Countries[world.Player].Stake < 0 && Influence.CanStake(world, world.Player, cell);
        }

        /// <summary>The fight: the hero first, then the group he surely beats, then the second right click.</summary>
        bool FightStep(ref string line)
        {
            World world = state.World;
            if (hero == null)
            {
                foreach (Hero mine in state.HeroesOf(world.Player))
                {
                    if (mine.Moves > 0 && mine.Army.Count > 0)
                    {
                        guideCells.Add(mine.Cell);
                        line = Words.GuidePickHero;
                        return true;
                    }
                }
                return false;
            }
            // A hero without an army has the one-time hint instead.
            if (hero.Army.Count == 0 || hero.Moves <= 0)
                return false;
            if (path != null && path.Target >= 0 && world.Cells[path.Target].Monster != Monster.None)
            {
                line = Words.GuideGo;
                return true;
            }
            int easy = EasyGroup(hero);
            if (easy >= 0)
            {
                guideCells.Add(easy);
                line = Words.GuideFight;
            }
            else
                line = Words.GuideScout;
            return true;
        }

        /// <summary><see cref="Guide.EasyGroup"/>, worked out again only when the hero, his place, the turn or his army changed.</summary>
        int EasyGroup(Hero chosen)
        {
            float army = Armies.Worth(chosen.Army);
            if (chosen != easyHero || chosen.Cell != easyCellOf || state.Turn != easyTurn || army != easyArmy)
            {
                easyHero = chosen;
                easyCellOf = chosen.Cell;
                easyTurn = state.Turn;
                easyArmy = army;
                easyCell = Guide.EasyGroup(state, chosen);
            }
            // The group may have gone since.
            return easyCell >= 0 && state.World.Cells[easyCell].Monster != Monster.None ? easyCell : -1;
        }

        /// <summary>A town building: the town with a ring, then «Войти в город», then the tab and the building on its screen.</summary>
        bool TownStep(TownBuilding building, ref string line, ref string button)
        {
            World world = state.World;
            int town = Guide.TownToBuild(state, world.Player, building);
            if (town < 0)
                return false;
            City city = world.Cities[town];
            if (OnScreen)
            {
                if (hud.TownScreenCity != town)
                    return false;
                if (!hud.TownBuildingsTab)
                {
                    button = Words.TabBuildings;
                    line = Words.GuideTownTab;
                }
                else
                {
                    button = Words.Town(building);
                    line = Words.GuideTownPress(button);
                }
                return true;
            }
            if (select >= 0 && (world.Cells[select].City == town || world.Cells[select].District == town))
            {
                button = Words.EnterTown;
                line = Words.GuideEnterTown;
                return true;
            }
            guideCells.Add(city.Cell);
            line = Words.GuideTownCell;
            return true;
        }
    }
}
