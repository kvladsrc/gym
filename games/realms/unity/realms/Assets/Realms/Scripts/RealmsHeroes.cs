using System.Collections;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The heroes' part of the game: choosing a hero, laying its way with a right click and
    /// walking it with a second one, what it finds on the way (piles, shrines, chests, towers),
    /// meeting a fellow hero to exchange units, the fights with the monsters (played on the
    /// battlefield or quickly), the level-ups, and hiring units and heroes in the towns.
    /// </summary>
    public sealed partial class RealmsGame
    {
        Hero hero;
        HeroPath path;

        /// <summary>A hero walks or fights: the map takes no clicks and the turn cannot end.</summary>
        bool busy;

        void ChooseHero(Hero chosen, bool focus)
        {
            hero = chosen;
            path = null;
            heroes.Choose(hero);
            heroes.ShowPath(null, null);
            hud.ShowHero(hero, null);
            if (hero == null)
                return;
            Select(hero.Cell);
            if (focus)
                mapCamera.Focus(mapCamera.Nearer(new Vector2(state.World.CenterX(hero.Cell), state.World.CenterZ(hero.Cell)), 0.6f), Mathf.Min(mapCamera.Distance, 22f));
        }

        void NextHero()
        {
            List<Hero> mine = state.HeroesOf(state.World.Player);
            if (mine.Count == 0)
                return;
            int k = hero == null ? 0 : (mine.IndexOf(hero) + 1) % mine.Count;
            ChooseHero(mine[k], true);
        }

        /// <summary>
        /// The left button looks: a click on another of the players' heroes chooses him, any other
        /// click selects the cell and a second one lets it go; a double click on one of the
        /// players' towns or heroes opens its screen. The chosen hero and his way stay.
        /// </summary>
        void Click(int cell)
        {
            if (cell < 0)
                return;
            CountMonsterClick(cell);
            if (DoubleClick(cell) && OpenScreenAt(cell))
                return;
            Hero there = state.HeroAt(cell);
            if (there != null && there.Country == state.World.Player && there != hero)
            {
                ChooseHero(there, false);
                return;
            }
            Select(cell == select ? -1 : cell);
        }

        /// <summary>
        /// The right button orders the chosen hero: the first click on a cell lays his way there
        /// (the panel shows what waits at its end), a second one walks it; a way to a fellow hero
        /// ends beside him with the exchange of units. A click on the hero himself drops the way.
        /// </summary>
        void Order(int cell)
        {
            if (cell < 0)
                return;
            if (hero == null)
            {
                hud.Toast(state.HeroesOf(state.World.Player).Count > 0
                    ? T("Сначала выбери героя: щёлкни по нему или нажми F", "First select a hero: click them or press F") : T("Героев нет: найми героя в таверне", "No heroes: hire a hero in a tavern"));
                return;
            }
            if (cell == hero.Cell)
            {
                path = null;
                heroes.ShowPath(null, null);
                hud.ShowHero(hero, null);
                Select(cell);
                return;
            }
            if (path != null && path.Target == cell)
            {
                StartCoroutine(Walk());
                return;
            }
            path = state.Path(hero, cell);
            heroes.ShowPath(hero, path);
            hud.ShowHero(hero, path);
            Select(cell);
            if (path != null)
                KeepAboveDock(cell);
            if (path == null && !state.World.Cells[cell].IsWater)
                hud.Toast(state.Seen[cell] ? T("Сюда герою не пройти", "The hero can't get there") : T("Земля в тумане: подойди ближе, и герой её разведает", "The land is in the fog: come closer, and the hero will scout it"));
        }

        /// <summary>
        /// Pans the map a little when a cell stands under the bottom panel or just above it: the
        /// goal of a way was half hidden there, its ring and its number of turns under the panel.
        /// It keeps the cell about 120 pixels of a 900-pixel screen above the panel.
        /// </summary>
        void KeepAboveDock(int cell)
        {
            Camera camera = Camera.main;
            float dock = hud.DockTop, x = state.World.CenterX(cell), z = state.World.CenterZ(cell);
            if (camera == null || dock <= 0f)
                return;
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(x, Mathf.Max(view.Field.Height(x, z), 0f), z));
            float wanted = dock + 0.135f * Screen.height;
            if (screen.z <= 0f || screen.y >= wanted)
                return;
            // The camera moves by the ground between where the cell is and where it should be.
            if (mapCamera.GroundUnder(screen, out Vector3 now) && mapCamera.GroundUnder(new Vector2(screen.x, wanted), out Vector3 there))
                mapCamera.FocusKeepZoom(mapCamera.Target + new Vector2(now.x - there.x, now.z - there.z));
        }

        /// <summary>Esc: first the selected cell and the way, then the hero; false when nothing was chosen.</summary>
        bool Back()
        {
            if (hero != null && (path != null || (select >= 0 && select != hero.Cell)))
            {
                path = null;
                heroes.ShowPath(null, null);
                hud.ShowHero(hero, null);
                Select(-1);
            }
            else if (hero != null)
            {
                ChooseHero(null, false);
                Select(-1);
            }
            else if (select >= 0)
                Select(-1);
            else
                return false;
            return true;
        }

        /// <summary>The chosen hero walks its way as far as its moves go, stopping for a fight or a chest.</summary>
        IEnumerator Walk()
        {
            if (busy || hero == null || path == null)
                yield break;
            busy = true;
            Learn(Learned.Order);
            Hero walker = hero;
            HeroPath way = path;
            int target = way.Target, fightAt = -1;
            Hero met = null;
            bool changed = false;
            for (int k = 0; k < way.Cells.Count; k++)
            {
                int cell = way.Cells[k];
                StepResult step = state.Step(walker, cell);
                if (step.Kind == StepKind.Fight)
                {
                    fightAt = cell;
                    break;
                }
                if (step.Kind == StepKind.Meet)
                {
                    met = state.HeroAt(cell);
                    break;
                }
                if (step.Kind == StepKind.NoMoves)
                {
                    hud.Toast(k == 0 ? T("Ходы кончились: герой пойдёт в следующий ход", "Out of moves: the hero will go next turn") : T("Ходы кончились: дальше — в следующий ход", "Out of moves: the rest of the way next turn"));
                    break;
                }
                if (step.Kind == StepKind.Blocked)
                {
                    hud.Toast(walker.Army.Count == 0 && state.World.Cells[cell].Monster != Monster.None
                        ? T("Без войска в бой не пойти", "No army, no battle") : T("Путь закрыт", "The way is blocked"));
                    break;
                }
                heroes.ShowPath(walker, way, k + 1);
                yield return heroes.Hop(walker, cell);
                view.UpdateFog();
                hud.ShowHero(walker, way);
                if (step.Kind == StepKind.Visited || step.Garrison || step.Settlers)
                {
                    // The ground waits for the turn's end, not to stall the walk.
                    view.Refresh(new[] { cell }, false);
                    if (step.Settlers)
                        heroes.Refresh();
                    changed = true;
                    string found = Words.Visit(step);
                    if (found != null)
                        hud.Toast(found, 4f);
                    if (!step.Gained.IsEmpty)
                        hud.Refresh();
                }
                if (step.Chest)
                {
                    yield return OpenChest();
                    break;
                }
            }
            if (fightAt >= 0)
                yield return Fight(walker, fightAt);
            else if (met != null)
            {
                path = null;
                heroes.ShowPath(null, null);
            }
            else
            {
                path = walker.Cell != target ? state.Path(walker, target) : null;
                heroes.ShowPath(walker, path);
            }
            AfterMove(changed || fightAt >= 0);
            if (met != null)
                yield return Swap(walker, met);
            busy = false;
        }

        IEnumerator SwapThenFree(Hero a, Hero b)
        {
            busy = true;
            yield return Swap(a, b);
            busy = false;
        }

        /// <summary>The exchange of units between the hero and a fellow hero beside him, or (no fellow) his town.</summary>
        IEnumerator Swap(Hero a, Hero b)
        {
            City town = b == null ? state.TownOf(a) : null;
            if (b == null && town == null)
                yield break;
            bool closed = false;
            hud.Exchange(a.Label, a.Army, b != null ? b.Label : town.Label, b != null ? b.Army : town.Garrison, () => closed = true);
            while (!closed)
                yield return null;
            if (hero != null && path != null)
                path = state.Path(hero, path.Target);
            hud.ShowHero(hero, path);
            hud.RefreshInfo();
        }

        /// <summary>The interface after a hero moved: fog, labels, minimap, card and the finished tasks.</summary>
        void AfterMove(bool labels)
        {
            view.UpdateFog();
            if (labels)
                hud.RebuildLabels();
            hud.WorldChanged();
            hud.ShowHero(hero, path);
            List<Quest> finished = state.CheckQuests();
            if (finished.Count > 0)
            {
                hud.Refresh();
                hud.Toast(Words.QuestDone(finished[0]), 5f);
            }
            CrownAfterDeed();
            if (hero != null)
                Select(path != null ? path.Target : hero.Cell);
            else
                hud.RefreshInfo();
        }

        /// <summary>A dialog that waits for its answer.</summary>
        IEnumerator AskAndWait(string title, string icon, string text, string[] buttons, System.Action<int> chosen,
            System.Action<UnityEngine.UIElements.VisualElement> body = null)
        {
            int answer = -1;
            hud.Ask(title, icon, text, buttons, c => answer = c, body);
            while (answer < 0)
                yield return null;
            chosen(answer);
        }

        IEnumerator OpenChest()
        {
            Chest chest = state.Chest;
            if (chest == null)
                yield break;
            Hero finder = state.Heroes[chest.Hero];
            bool gold = true;
            yield return AskAndWait(T("Сундук!", "A chest!"), "coins", T("Внутри золото, а под ним старая книга. Что взять?", "Gold inside, and an old book beneath it. What will you take?"),
                new[] { T($"Золото: {chest.Gold}", $"Gold: {chest.Gold}"), T($"Опыт: {chest.Experience}", $"Experience: {chest.Experience}") }, c => gold = c == 0);
            state.TakeChest(gold);
            Learn(Learned.Chest);
            hud.Refresh();
            hud.Toast(gold ? T($"В казну: {chest.Gold} золота", $"To the treasury: {chest.Gold} gold") : T($"{finder.Label} получает {chest.Experience} опыта", $"{finder.Label} gains {chest.Experience} experience"), 3.5f);
            if (finder.LevelUps > 0)
                yield return LevelUps(finder);
        }

        /// <summary>A card for each level the hero gained: +1 attack or +1 defence.</summary>
        IEnumerator LevelUps(Hero leveled)
        {
            while (leveled.LevelUps > 0)
            {
                bool attack = true;
                int level = leveled.Level - leveled.LevelUps + 1;
                yield return AskAndWait(T($"{leveled.Label}: уровень {level}!", $"{leveled.Label}: level {level}!"), "crown", T("Что усилить?", "What shall we strengthen?"),
                    new[] { T("Атака +1", "Attack +1"), T("Защита +1", "Defence +1") }, c => attack = c == 0, body =>
                    {
                        hud.BodyLine(body, T("Атака", "Attack"), T($"{leveled.Attack} — войско бьёт сильнее", $"{leveled.Attack} — the army strikes harder"));
                        hud.BodyLine(body, T("Защита", "Defend"), T($"{leveled.Defence} — войско получает меньше урона", $"{leveled.Defence} — the army takes less damage"));
                    });
                GameState.ChooseLevelUp(leveled, attack);
                hud.ShowHero(hero, path);
            }
        }

        /// <summary>
        /// A monster group blocks the way: the odds and the expected losses, then the battlefield
        /// or a quick fight, the result, the level-ups.
        /// </summary>
        IEnumerator Fight(Hero fighter, int cell)
        {
            Cell c = state.World.Cells[cell];
            string title = Words.Monster(c.Monster, c.Lair);
            Odds odds = state.OddsOf(fighter, cell);
            int choice = 2;
            string oddsClass = odds.Win >= Odds.Easy ? "good" : odds.Win >= Odds.Even ? "warn" : "danger";
            yield return AskAndWait(title, "swords", T($"Путь преграждают {Words.Count(Units.Of(c.Monster), c.Count)}.", $"The way is blocked by {Words.Count(Units.Of(c.Monster), c.Count)}."),
                new[] { T("В бой!", "Fight!"), T("Быстрый бой", "Quick battle"), T("Назад", "Back") }, k => choice = k, body =>
                {
                    hud.BodyLine(body, T("Бой", "Battle"), Words.Odds(odds), oddsClass);
                    hud.BodyLine(body, Words.FightReward, Words.RewardFactor(Odds.RewardFactor(odds.Win)));
                    hud.RewardChips(body, state.RewardOf(cell, odds));
                    hud.BodyLine(body, T("Потери, скорее всего", "Likely losses"), "");
                    hud.BodyArmy(body, fighter.Army, odds.Losses);
                });
            if (choice == 2)
            {
                path = state.Path(fighter, cell);
                heroes.ShowPath(fighter, path);
                yield break;
            }
            var start = new List<Core.Stack>();
            foreach (Core.Stack s in fighter.Army)
                start.Add(new Core.Stack(s.Unit, s.Count));
            // The save before the battle: quitting in it comes back here, and the chance kept in
            // the save gives the same battle again (the interface plan's 3.1).
            AutoSave();
            fighting = true;
            Battle battle = state.StartBattle(fighter, cell, odds);
            if (choice == 0)
                yield return battleView.Play(battle, title);
            else
            {
                // The quick battle asks too when the monsters offer to give up.
                while (battle.Outcome == Outcome.Running)
                {
                    if (battle.Offered)
                        yield return AskAndWait(Words.YieldTitle(Units.Of(state.World.Cells[cell].Monster)), "swords",
                            Words.YieldOffer, Words.YieldAnswers, c =>
                            {
                                if (c == 0)
                                    battle.Accept();
                                else
                                    battle.FightOn();
                            });
                    else
                        battle.Auto();
                }
            }
            var lost = new int[start.Count];
            foreach (Fighter f in battle.Fighters)
            {
                if (f.Side == Side.Players && f.Source >= 0 && f.Source < lost.Length)
                    lost[f.Source] = f.StartCount - f.Count;
            }
            FightReport report = state.EndBattle(fighter, cell, battle);
            fighting = false;
            yield return Result(fighter, report, start, lost);
            if (battleView.Active)
                battleView.Close();
            view.Refresh(new[] { cell });
            heroes.Refresh();
            heroes.Choose(hero);
            path = null;
            heroes.ShowPath(null, null);
            // A cleared cell may have gone into the kingdom's land: the minimap and the land bars too.
            if (report.Took)
                hud.WorldChanged();
            else
                hud.Refresh();
            if (report.Visit != null)
            {
                string found = Words.Visit(report.Visit);
                if (found != null)
                    hud.Toast(found, 4f);
            }
            if (state.Chest != null)
                yield return OpenChest();
            if (fighter.LevelUps > 0)
                yield return LevelUps(fighter);
            if (report.Home >= 0)
                mapCamera.Focus(mapCamera.Nearer(new Vector2(state.World.CenterX(report.Home), state.World.CenterZ(report.Home)), 0.6f), Mathf.Min(mapCamera.Distance, 22f));
        }

        IEnumerator Result(Hero fighter, FightReport report, List<Core.Stack> start, int[] lost)
        {
            bool won = report.Outcome == Outcome.Won;
            string text;
            if (won && report.Surrendered)
                text = Words.LetGo(report.Monster, report.Lair);
            else if (won)
                text = report.Lair ? T("Логово разорено!", "The lair is cleared!") : T("Путь свободен!", "The way is clear!");
            else
            {
                Cell home = state.World.Cells[report.Home];
                int city = home.City >= 0 ? home.City : state.World.Countries[fighter.Country].Capital;
                text = Words.Lost(state.World.Cities[city].Label, report.Retreated, report.Militia);
            }
            yield return AskAndWait(won ? T("Победа!", "Victory!") : Words.LostTitle(report.Retreated), won ? "crown" : "swords", text, new[] { T("Дальше", "Continue") }, _ => { }, body =>
            {
                hud.BodyLine(body, T("Побеждено врагов", "Enemies defeated"), report.Killed.ToString());
                hud.BodyLine(body, T("Опыт героя", "Hero's experience"), $"+{report.Experience}", "good");
                if (won)
                {
                    if (report.RewardFactor > 1)
                        hud.BodyLine(body, Words.FightReward, Words.RewardFactor(report.RewardFactor));
                    hud.RewardChips(body, report.Reward);
                }
                hud.BodyLine(body, T("Твоё войско", "Your army"), "");
                hud.BodyArmy(body, start, null, lost);
            });
        }

        void Recruit(int city, Unit unit, int count)
        {
            Refusal refusal = state.Recruit(state.World.Player, city, unit, count);
            if (refusal != Refusal.None)
            {
                hud.Toast(Words.Refusal(refusal));
                return;
            }
            Hero inTown = state.HeroAt(state.World.Cities[city].Cell);
            hud.Toast(inTown != null ? T($"{Words.Count(unit, count)} — в войске героя {inTown.Label}", $"{Words.Count(unit, count)} — in {inTown.Label}'s army")
                : T($"Наняты {Words.Count(unit, count)}: ждут героя в городе", $"Hired {Words.Count(unit, count)}: waiting for a hero in town"), 4f);
            hud.Refresh();
            hud.RefreshInfo();
            hud.ShowHero(hero, path);
        }

        void HireHero(int city)
        {
            Refusal refusal = state.HireHero(state.World.Player, city);
            if (refusal != Refusal.None)
            {
                hud.Toast(Words.Refusal(refusal));
                return;
            }
            heroes.Refresh();
            view.UpdateFog();
            hud.WorldChanged();
            Hero hired = state.HeroAt(state.World.Cities[city].Cell);
            ChooseHero(hired, false);
            if (hired != null)
                hud.Toast(T($"Новый герой: {hired.Label}!", $"New hero: {hired.Label}!"), 4f);
        }
    }
}
