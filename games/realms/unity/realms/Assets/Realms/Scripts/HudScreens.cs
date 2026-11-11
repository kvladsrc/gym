using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The town and hero screens' panels. The town screen (left) has the town's houses, growth
    /// and income at the top and three tabs: buildings (the slots and what can be built), hiring
    /// and the army (the garrison, exchanged with a hero in the town). The hero screen (right)
    /// has the hero's level, attack, defence and moves, a level-up button when one waits, and his
    /// army with each unit's numbers. Both end with buttons to the previous and next town or hero
    /// and back to the map. The map's panels hide while a screen is open.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The players asked for a town's screen, the chosen hero's screen, the map, or the next (+1) or previous (−1) one.</summary>
        public event Action<int> TownScreenPicked;
        public event Action HeroScreenPicked;
        public event Action ScreenClosed;
        public event Action<int> ScreenStep;

        /// <summary>The players asked a town for a settlers' cart.</summary>
        public event Action<int> GatherSettlers;

        enum TownTab
        {
            Buildings,
            Hiring,
            Army,
        }

        VisualElement townScreen, townIcon, townStats, townTabs, townBody;
        ScrollView townScroll;
        VisualElement heroScreen, heroScreenPortrait, heroScreenBody;
        Label townTitle, townSubtitle, heroScreenTitle, heroScreenSubtitle;
        readonly Button[] townTabButtons = new Button[3];
        TownTab townTab = TownTab.Buildings;
        int screenCity = -1;
        Hero screenHero;

        /// <summary>A town or hero screen is open.</summary>
        public bool ScreenOpen => screenCity >= 0 || screenHero != null;

        void BuildScreens()
        {
            townScreen = Child(root, "screen");
            townScreen.AddToClassList("town-screen");
            VisualElement head = Child(townScreen, "screen-head");
            townIcon = Child(head, "screen-icon");
            VisualElement titles = Child(head, "screen-titles");
            townTitle = Text(titles, "screen-title", "", black);
            townSubtitle = Text(titles, "screen-subtitle", "", bold);
            townStats = Child(townScreen, "screen-stats");
            townTabs = Child(townScreen, "screen-tabs");
            string[] names = { Words.TabBuildings, T("Наём", "Recruit"), T("Войско", "Army") };
            for (int k = 0; k < names.Length; k++)
            {
                TownTab tab = (TownTab)k;
                townTabButtons[k] = Btn(townTabs, names[k], "screen-tab", () =>
                {
                    townTab = tab;
                    FillTownScreen();
                });
                Guided(townTabButtons[k], names[k]);
            }
            townScroll = new ScrollView(ScrollViewMode.Vertical);
            townScroll.AddToClassList("screen-scroll");
            townScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            townScreen.Add(townScroll);
            townBody = townScroll.contentContainer;
            Nav(townScreen, "Tab");
            townScreen.style.display = DisplayStyle.None;

            heroScreen = Child(root, "screen");
            heroScreen.AddToClassList("hero-screen");
            VisualElement heroHead = Child(heroScreen, "screen-head");
            heroScreenPortrait = Child(heroHead, "screen-portrait");
            PortraitFrame(heroScreenPortrait);
            VisualElement heroTitles = Child(heroHead, "screen-titles");
            heroScreenTitle = Text(heroTitles, "screen-title", "", black);
            heroScreenSubtitle = Text(heroTitles, "screen-subtitle", "", bold);
            var heroScroll = new ScrollView(ScrollViewMode.Vertical);
            heroScroll.AddToClassList("screen-scroll");
            heroScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            heroScreen.Add(heroScroll);
            heroScreenBody = heroScroll.contentContainer;
            Nav(heroScreen, "F");
            heroScreen.style.display = DisplayStyle.None;
        }

        /// <summary>The screen's last row: previous, back to the map (Esc), next (<paramref name="nextKey"/>).</summary>
        void Nav(VisualElement screen, string nextKey)
        {
            VisualElement nav = Child(screen, "screen-nav");
            Btn(nav, "‹", "screen-step", () => ScreenStep?.Invoke(-1)).tooltip = T("Предыдущий", "Previous");
            Button back = Btn(nav, T("На карту", "To the map"), "button-primary", () => ScreenClosed?.Invoke());
            back.AddToClassList("screen-back");
            Text(back, "key-hint", "Esc", bold).pickingMode = PickingMode.Ignore;
            Btn(nav, "›", "screen-step", () => ScreenStep?.Invoke(1)).tooltip = T($"Следующий ({nextKey})", $"Next ({nextKey})");
        }

        public void ShowTownScreen(int city)
        {
            if (city != screenCity)
                townTab = TownTab.Buildings;
            screenCity = city;
            screenHero = null;
            heroScreen.style.display = DisplayStyle.None;
            townScreen.style.display = DisplayStyle.Flex;
            root.AddToClassList("on-screen");
            FillTownScreen();
        }

        /// <summary>Opens a tab of the town screen: 0 buildings, 1 hiring, 2 the army.</summary>
        public void ChooseTownTab(int tab)
        {
            townTab = (TownTab)Mathf.Clamp(tab, 0, townTabButtons.Length - 1);
            if (screenCity >= 0)
                FillTownScreen();
        }

        /// <summary>Scrolls the town screen's tab to its end (for the captures).</summary>
        public void ScrollTownToEnd() => townScroll.scrollOffset = new Vector2(0, float.MaxValue);

        public void ShowHeroScreen(Hero shown)
        {
            screenHero = shown;
            screenCity = -1;
            townScreen.style.display = DisplayStyle.None;
            heroScreen.style.display = DisplayStyle.Flex;
            root.AddToClassList("on-screen");
            FillHeroScreen();
        }

        public void CloseScreen()
        {
            screenCity = -1;
            screenHero = null;
            // The ring the town screen's expand button put on the map goes with the screen.
            view?.SetHover(-1);
            if (townScreen == null)
                return;
            townScreen.style.display = DisplayStyle.None;
            heroScreen.style.display = DisplayStyle.None;
            root.RemoveFromClassList("on-screen");
        }

        /// <summary>The open screen anew, after the treasury, the town or the hero changed.</summary>
        void RefreshScreen()
        {
            if (state == null)
                return;
            if (screenCity >= 0)
                FillTownScreen();
            else if (screenHero != null)
                FillHeroScreen();
        }

        // ----- The town screen -----

        void FillTownScreen()
        {
            World world = state.World;
            City city = world.Cities[screenCity];
            SetIcon(townIcon, city.Capital ? "crown" : FlagIcon(city.Country));
            townTitle.text = city.Label;
            string level = Words.Level(Towns.Level(city));
            townSubtitle.text = city.Capital ? T($"Столица · {level.ToLowerInvariant()}", $"Capital · {level.ToLowerInvariant()}") : level;

            townStats.Clear();
            Line(T("Дома", "Houses"), city.Houses.ToString(), null, townStats);
            Growth(city, townStats);
            VisualElement chips = Child(townStats, "info-chips");
            Chips(Economy.CityYield(city), T("Даёт за ход", "Gives each turn"), false, chips);
            Hero standing = state.HeroAt(city.Cell);
            if (standing != null && standing.Country == world.Player)
                Line(T("В городе", "In town"), T($"герой {standing.Label}", $"hero {standing.Label}"), null, townStats);

            for (int k = 0; k < townTabButtons.Length; k++)
                townTabButtons[k].EnableInClassList("screen-tab-on", (int)townTab == k);
            townBody.Clear();
            switch (townTab)
            {
                case TownTab.Buildings:
                    TownBuildings(city);
                    break;
                case TownTab.Hiring:
                    TownHiring(city);
                    break;
                default:
                    TownArmy(city, standing != null && standing.Country == world.Player ? standing : null);
                    break;
            }
        }

        /// <summary>
        /// The town's places (its houses, its buildings and the free ones), the buildings it can
        /// still have, and its fortifications (walls and castle), which take no place.
        /// </summary>
        void TownBuildings(City city)
        {
            List<TownBuilding> slotted = city.Buildings.FindAll(Economy.TakesSlot);
            int places = Towns.Places(city), free = Towns.Free(city);
            Text(townBody, "screen-section", T($"Места · {Towns.Taken(city)} из {places}", $"Places · {Towns.Taken(city)} of {places}"), black);
            string cells = city.Districts.Count == 0 ? T("одна клетка", "one tile")
                : T($"{city.Districts.Count + 1} {Words.Plural(city.Districts.Count + 1, "клетка", "клетки", "клеток")}", $"{city.Districts.Count + 1} {En(city.Districts.Count + 1, "tile", "tiles")}");
            Text(townBody, "screen-note", T($"Город — {cells}: {Towns.CentrePlaces} мест в центре и по {Towns.DistrictPlaces} в каждом квартале. ", $"The town covers {cells}: {Towns.CentrePlaces} places in the centre and {Towns.DistrictPlaces} in each quarter. ") +
                T("Дом или постройка занимает одно место.", "A house or a building takes one place."), regular);
            VisualElement grid = Child(townBody, "town-slots");
            bool castle = city.Buildings.Contains(TownBuilding.Castle);
            if (city.Capital || castle)
                Slot(grid, castle ? BuildingIcon(TownBuilding.Castle) : "icon_hall", castle ? T("Замок", "Castle") : T("Ратуша", "Town hall")).AddToClassList("town-slot-hall");
            Slot(grid, "icon_houses", T($"{city.Houses} {Words.Plural(city.Houses, "дом", "дома", "домов")}", $"{city.Houses} {En(city.Houses, "house", "houses")}")).AddToClassList("town-slot-houses");
            foreach (TownBuilding building in slotted)
                Slot(grid, BuildingIcon(building), Words.Town(building)).tooltip = Words.TownHint(building);
            for (int k = 0; k < free; k++)
            {
                VisualElement slot = Child(grid, "town-slot");
                slot.AddToClassList("town-slot-free");
                Text(slot, "town-slot-name", T("свободно", "free"), regular);
            }
            if (free == 0)
                ExpandFromScreen(city);

            bool any = false;
            foreach (TownBuilding building in Economy.TownBuildings)
            {
                if (city.Buildings.Contains(building))
                    continue;
                if (!any)
                {
                    Text(townBody, "screen-section", T("Построить", "Build"), black);
                    // A full town says why once over the list: in every entry the same red line
                    // ran down the screen (review of 2026-11-11). Those entries are only pale.
                    if (free == 0)
                        Text(townBody, "screen-why", Words.Refusal(Refusal.NoSlot), regular);
                }
                any = true;
                Refusal refusal = Economy.CanBuild(city, state.Treasury[city.Country], building);
                TownBuilding chosen = building;
                ActionButton(Words.Town(building), Words.TownHint(building), Economy.Cost(building), refusal,
                    () => BuildTown?.Invoke(city.Index, chosen), BuildingIcon(building), into: townBody,
                    saidAbove: refusal == Refusal.NoSlot && free == 0);
            }
            if (!any)
                Text(townBody, "screen-note", T("Всё, что можно, уже построено.", "Everything possible is already built."), regular);

            Text(townBody, "screen-section", T("Укрепления", "Fortifications"), black);
            foreach (TownBuilding building in Economy.Fortifications)
            {
                if (city.Buildings.Contains(building))
                {
                    string stands = building == TownBuilding.Castle ? T("Замок стоит", "The castle stands") : T("Стены стоят", "The walls stand");
                    Text(townBody, "screen-note", $"{stands} — {Words.TownHint(building)}.", regular);
                    continue;
                }
                Refusal refusal = Economy.CanBuild(city, state.Treasury[city.Country], building);
                TownBuilding chosen = building;
                ActionButton(Words.Town(building), Words.TownHint(building), Economy.Cost(building), refusal,
                    () => BuildTown?.Invoke(city.Index, chosen), BuildingIcon(building), into: townBody);
            }
            Text(townBody, "screen-note", T("Стены и замок не занимают мест.", "Walls and the castle take no places."), regular);
        }

        /// <summary>
        /// A full town grows from its own screen too (the players found it odd that it could not,
        /// 2026-11-11): the button takes the best cell <see cref="Towns.ExpansionCells"/> finds and
        /// rings it on the map while the pointer is over it; another cell can still be picked on the map.
        /// </summary>
        void ExpandFromScreen(City city)
        {
            World world = state.World;
            List<int> cells = Towns.ExpansionCells(world, city);
            if (cells.Count == 0)
            {
                Text(townBody, "screen-note", T("Мест нет: город не растёт и не строит. ", "No places: the town neither grows nor builds. ") +
                    T("А расти ему некуда: рядом нет своей пустой клетки.", "And it has nowhere to grow: there is no empty tile of yours next to it."), regular).AddToClassList("warn");
                return;
            }
            Text(townBody, "screen-note", T("Мест нет: город не растёт и не строит. Расширь его на соседнюю клетку.", "No places: the town neither grows nor builds. Expand it onto a neighbouring tile."), regular)
                .AddToClassList("warn");
            int cell = cells[0];
            Refusal refusal = Towns.CanExpand(world, state.Treasury[city.Country], city.Country, cell, out _);
            Button button = ActionButton(T("Расширить город", "Expand town"), T($"Новый квартал: {Towns.DistrictPlaces} мест", $"New quarter: {Towns.DistrictPlaces} places"), Towns.DistrictCost,
                refusal, () => ExpandTown?.Invoke(cell), "icon_town", into: townBody);
            button.RegisterCallback<PointerEnterEvent>(_ => view.SetHover(cell));
            button.RegisterCallback<PointerLeaveEvent>(_ => view.SetHover(-1));
            Text(townBody, "screen-note", T("Пока мышь на кнопке, клетка нового квартала подсвечена на карте. ", "While the mouse is on the button, the new quarter's tile is lit on the map. ") +
                T("Другую клетку можно выбрать на карте.", "You can pick another tile on the map."), regular);
        }

        /// <summary>A place in the town's grid: the picture of what stands there (none until it is painted) and its name.</summary>
        VisualElement Slot(VisualElement grid, string icon, string name)
        {
            VisualElement slot = Child(grid, "town-slot");
            SetIcon(Child(slot, "town-slot-icon"), icon);
            Text(slot, "town-slot-name", name, bold);
            return slot;
        }

        /// <summary>A town building's icon, the hammer until its own is painted.</summary>
        string BuildingIcon(TownBuilding building)
        {
            string name;
            switch (building)
            {
                case TownBuilding.Market: name = "icon_market"; break;
                case TownBuilding.Granary: name = "icon_granary"; break;
                case TownBuilding.Tavern: name = "icon_tavern"; break;
                case TownBuilding.Barracks: name = "icon_barracks"; break;
                case TownBuilding.ArcheryRange: name = "icon_range"; break;
                case TownBuilding.Stables: name = "icon_stables"; break;
                case TownBuilding.Walls: name = "icon_town"; break;
                default: name = "icon_castle"; break;
            }
            return IconTexture(name) != null ? name : "hammer";
        }

        /// <summary>What the town's barracks, range, stables and tavern offer, where the hired go, and the settlers.</summary>
        void TownHiring(City city)
        {
            Text(townBody, "screen-section", T("Нанять", "Hire"), black);
            int before = townBody.childCount;
            RecruitActions(city, townBody);
            Hero standing = state.HeroAt(city.Cell);
            bool led = standing != null && standing.Country == city.Country;
            if (townBody.childCount == before)
                Text(townBody, "screen-note", T("Здесь пока некого нанять: построй казарму, стрельбище или конюшню.", "No one to hire here yet: build barracks, an archery range or stables."), regular);
            else
            {
                foreach (Unit unit in Units.Hired)
                {
                    if (city.Buildings.Contains(Units.Building(unit)))
                    {
                        Text(townBody, "screen-note",
                            T($"{Words.Unit(unit)}: +{Units.PerTurn(unit)} за ход, ждут не больше {Units.PoolLimit(unit)}", $"{Words.Unit(unit)}: +{Units.PerTurn(unit)} a turn, at most {Units.PoolLimit(unit)} waiting"), regular);
                    }
                }
                Text(townBody, "screen-note", led
                    ? T($"Нанятые встанут в войско героя {standing.Label}.", $"The hired troops will join {standing.Label}'s army.")
                    : T("В городе нет героя: нанятые подождут его здесь.", "No hero in town: the hired troops will wait for one here."), regular);
            }

            Text(townBody, "screen-section", T("Поселенцы", "Settlers"), black);
            ActionButton(T("Обоз поселенцев", "Settlers' wagon"), T("Новый город: веди обоз героем на свободную землю", "A new town: lead the wagon with a hero to free land"),
                Settling.Cost(state.World, city.Country), state.CanGatherSettlers(city.Country, city.Index),
                () => GatherSettlers?.Invoke(city.Index), "icon_cart", into: townBody);
            string where = city.Settlers ? T("Обоз ждёт в городе: приведи сюда героя, и он поведёт его.", "The wagon waits in town: bring a hero here, and they will lead it.")
                : led && standing.Settlers ? T($"Герой {standing.Label} уже ведёт обоз; новый подождёт здесь другого героя.", $"{standing.Label} already leads a wagon; the new one will wait here for another hero.")
                : led ? T($"Обоз сразу пойдёт с героем {standing.Label}.", $"The wagon will set off with {standing.Label} at once.")
                : T("В городе нет героя: обоз подождёт его здесь.", "No hero in town: the wagon will wait for one here.");
            Text(townBody, "screen-note", where, regular);
            Text(townBody, "screen-note",
                T($"Город ставят не ближе {Settling.Spacing} шагов от других, не в горах и не на месторождении. Каждый новый город делает обоз дороже.", $"A town must be at least {Settling.Spacing} steps from the others, not in the mountains and not on a deposit. Every new town makes the wagon dearer."),
                regular);
        }

        /// <summary>The garrison; with a hero in the town, the exchange between them.</summary>
        void TownArmy(City city, Hero standing)
        {
            if (standing != null)
            {
                Text(townBody, "screen-section", T("Обмен с героем", "Trade with the hero"), black);
                VisualElement exchange = Child(townBody, "screen-exchange");
                FillExchange(exchange, standing.Label, standing.Army, city.Label, city.Garrison);
                return;
            }
            Text(townBody, "screen-section", T("Ждут героя в городе", "Waiting for a hero in town"), black);
            if (city.Garrison.Count == 0)
                Text(townBody, "screen-note", T("Никого. Нанятые отряды ждут здесь, пока не придёт герой.", "No one. Hired troops wait here until a hero comes."), regular);
            else
            {
                BodyArmy(townBody, city.Garrison);
                Text(townBody, "screen-note", T("Приведи сюда героя, и он заберёт их с собой.", "Bring a hero here, and they will take them along."), regular);
            }
        }

        // ----- The hero screen -----

        void FillHeroScreen()
        {
            Hero h = screenHero;
            World world = state.World;
            if (portraits?.Hero != null)
                heroScreenPortrait.style.backgroundImage = new StyleBackground(Background.FromTexture2D(portraits.Hero));
            Color banner = Palette.Countries[h.Country % Palette.Countries.Length];
            heroScreenPortrait.style.borderBottomColor = banner;
            heroScreenTitle.text = h.Label;
            heroScreenSubtitle.text = T($"Герой · {world.Countries[h.Country].Label}", $"Hero · {world.Countries[h.Country].Label}");

            VisualElement body = heroScreenBody;
            body.Clear();
            int from = HeroRules.NextLevel(h.Level - 1), to = HeroRules.NextLevel(h.Level);
            VisualElement levelRow = Child(body, "info-line");
            Text(levelRow, "info-key", T($"Уровень {h.Level}", $"Level {h.Level}"), black).AddToClassList("screen-level");
            VisualElement track = Child(levelRow, "growth-bar");
            Child(track, "screen-xp-fill").style.width = Length.Percent(100f * Mathf.Clamp01((float)(h.Experience - from) / Mathf.Max(1, to - from)));
            Text(levelRow, "growth-text", T($"опыт {h.Experience} из {to}", $"experience {h.Experience} of {to}"), bold);
            if (h.LevelUps > 0)
            {
                Button up = Btn(body, h.LevelUps > 1 ? T($"Новые уровни: {h.LevelUps}! Выбрать", $"New levels: {h.LevelUps}! Choose") : T("Новый уровень! Выбрать", "New level! Choose"), "button-primary",
                    () => LevelUpPicked?.Invoke());
                up.AddToClassList("screen-levelup");
            }

            Text(body, "screen-section", T("Сила героя", "Hero's strength"), black);
            Stat(body, "icon_attack", T("Атака", "Attack"), h.Attack.ToString(), T("каждый его отряд бьёт сильнее", "each of their troops strikes harder"));
            Stat(body, "icon_defence", T("Защита", "Defend"), h.Defence.ToString(), T("каждый его отряд получает меньше урона", "each of their troops takes less damage"));
            VisualElement moves = Child(body, "info-line");
            Text(moves, "info-key", T("Ходы", "Moves"), bold);
            VisualElement movesTrack = Child(moves, "growth-bar");
            Child(movesTrack, "screen-moves-fill").style.width = Length.Percent(100f * Mathf.Clamp01((float)h.Moves / HeroRules.MovesPerTurn));
            Text(moves, "growth-text", T($"{h.Moves} из {HeroRules.MovesPerTurn}", $"{h.Moves} of {HeroRules.MovesPerTurn}"), bold);
            City town = state.TownOf(h);
            if (town != null)
                Line(T("Город", "Town"), town.Label, null, body);
            else
                Line(T("Местность", "Terrain"), Words.Biome(world.Cells[h.Cell]), null, body);

            Text(body, "screen-section", T($"Войско · {h.Army.Count} из {Armies.MaxStacks}", $"Army · {h.Army.Count} of {Armies.MaxStacks}"), black);
            foreach (Stack stack in h.Army)
                ArmyRow(body, h, stack);
            int free = Armies.MaxStacks - h.Army.Count;
            if (free > 0)
            {
                VisualElement row = Child(body, "screen-unit");
                row.AddToClassList("screen-unit-free");
                for (int k = 0; k < free; k++)
                    Child(row, "screen-free-slot");
                Text(row, "screen-unit-free-text", free == 1 ? T("свободно одно место", "one place free") : free < 5 ? T($"свободно {free} места", $"{free} places free") : T($"свободно {free} мест", $"{free} places free"), regular);
            }
            if (h.Army.Count == 0)
                Text(body, "screen-warn", T("Без войска в бой не пойти: найми отряды в городе.", "No army, no battle: recruit troops in a town."), regular);

            if (h.Settlers)
            {
                Text(body, "screen-section", T("Обоз поселенцев", "Settlers' wagon"), black);
                Refusal can = state.CanFound(h);
                Text(body, "screen-note", can == Refusal.None ? T("Здесь можно основать город.", "A town can be founded here.")
                    : T($"Здесь город не основать: {Words.Refusal(can).ToLowerInvariant()}.", $"No town can be founded here: {Words.Refusal(can).ToLowerInvariant()}."), regular);
            }

            if (h.Country == world.Player)
            {
                VisualElement swaps = Child(body, "screen-swaps");
                if (h.Settlers)
                    FoundButton(swaps, h);
                foreach (Hero other in state.Neighbours(h))
                {
                    int with = other.Index;
                    Btn(swaps, T($"Обмен: {other.Label}", $"Trade: {other.Label}"), "swap-button", () => SwapPicked?.Invoke(with)).tooltip = T("Передать отряды друг другу", "Pass troops to each other");
                }
                if (town != null)
                {
                    Btn(swaps, T("Обмен: город", "Trade: town"), "swap-button", () => SwapPicked?.Invoke(-1)).tooltip =
                        T("Оставить отряды в городе другому герою или забрать оставленные", "Leave troops in town for another hero or take the ones left there");
                }
            }
        }

        void Stat(VisualElement parent, string icon, string name, string value, string what)
        {
            VisualElement row = Child(parent, "screen-stat");
            SetIcon(Child(row, "screen-stat-icon"), icon);
            Text(row, "screen-stat-name", name, bold);
            Text(row, "screen-stat-value", value, black);
            Text(row, "screen-stat-what", what, regular);
        }

        /// <summary>A stack of the army: its picture and count, its name and numbers with the hero's bonus.</summary>
        void ArmyRow(VisualElement parent, Hero h, Stack s)
        {
            VisualElement row = Child(parent, "screen-unit");
            VisualElement slot = Child(row, "army-slot");
            Texture2D picture = portraits?.Of(s.Unit);
            if (picture != null)
                slot.style.backgroundImage = new StyleBackground(Background.FromTexture2D(picture));
            Text(slot, "army-count", s.Count.ToString(), black);
            VisualElement words = Child(row, "screen-unit-words");
            Text(words, "screen-unit-name", Words.Count(s.Unit, s.Count), black);
            UnitStats u = Units.Of(s.Unit);
            Text(words, "screen-unit-numbers", T($"здоровье {u.Health} · атака {u.Attack}+{h.Attack} · защита {u.Defence}+{h.Defence}", $"health {u.Health} · attack {u.Attack}+{h.Attack} · defence {u.Defence}+{h.Defence}"), regular);
            Text(words, "screen-unit-numbers", T($"урон {u.MinDamage}–{u.MaxDamage} · скорость {u.Speed}", $"damage {u.MinDamage}–{u.MaxDamage} · speed {u.Speed}"), regular);
            string ability = Words.Abilities(u);
            if (ability != null)
                Text(words, "screen-unit-ability", ability, regular);
        }
    }
}
