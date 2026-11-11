using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The interface, built in code on the UI Toolkit panel. Over the map there are two strips
    /// along the screen's edges: the top bar (kingdom, treasury, season and year, buttons), with
    /// the kingdoms' list opening under it, and the bottom panel (the chosen hero, the chosen place
    /// with its build buttons, the minimap). Then the tasks as lines of text under the bar,
    /// the turn report, the help screen, short messages, and the labels that float over cities,
    /// resources and monsters. The hero's part of the bottom panel, the dialogs, the town and hero screens and
    /// the battle's panels are in the other parts of this class.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class Hud : MonoBehaviour
    {
        public StyleSheet style;
        public Font regular, bold, black;

        public event Action NextTurn, Help;
        public event Action<Vector2> MinimapPoint;
        public event Action<int> KingdomPicked;

        /// <summary>The players asked to build on a cell, to grow a town onto it, or to build in a town.</summary>
        public event Action<int> BuildField, ExpandTown;

        /// <summary>The players set their stake on the cell, or take it away with -1.</summary>
        public event Action<int> PlaceStake;
        public event Action<int, TownBuilding> BuildTown;

        /// <summary>A town in the turn report was clicked.</summary>
        public event Action<int> CityPicked;

        /// <summary>The players asked a town to hire units (town, unit, count) or a hero.</summary>
        public event Action<int, Unit, int> Recruit;
        public event Action<int> HireHero;

        static readonly Color[] SeasonColours =
        {
            Palette.Hex("#8DB062"), Palette.Hex("#D9AE55"), Palette.Hex("#C97A4A"), Palette.Hex("#9DBFD6"),
        };
        static readonly string[] GoodsIcons = { "coins", "wheat", "logs", "stone" };

        enum LabelKind { Capital, Town, Resource, Group, Place }

        struct MapLabel
        {
            public VisualElement Anchor;
            public Vector3 World;
            public LabelKind Kind;

            /// <summary>
            /// How far from <see cref="World"/> towards the far side, as the camera faces, the label
            /// stands: a town's name sits over the town's far edge, above its houses, not on them.
            /// </summary>
            public float Reach;

            /// <summary>The cell it belongs to: hidden while the cell is in the fog.</summary>
            public int Cell;
        }

        VisualElement root, labelLayer, shieldLayer, infoIcon, infoLines, minimap, minimapView, helpOverlay, topBar;
        VisualElement shield, infoDot, infoChips, infoTags, kingdoms, kingdomRows, seasonDot;
        VisualElement infoTown, infoActions, quests, questRows, report, reportRows;
        VisualElement dock, info, infoEmpty, infoMain, infoSide;
        Button kingdomsButton;
        VisualElement crownIcon;
        Label kingdomName, seasonLabel, yearLabel, infoTitle, infoSubtitle, infoHint, toast, reportTitle;
        readonly Label[] amounts = new Label[4], incomes = new Label[4];
        readonly VisualElement[] goodsItems = new VisualElement[4];
        readonly Dictionary<int, VisualElement> badges = new Dictionary<int, VisualElement>();
        readonly VisualElement[] pips = new VisualElement[4];
        readonly List<(int Country, Label Income)> rowIncomes = new List<(int, Label)>();
        readonly List<MapLabel> labels = new List<MapLabel>();
        readonly List<Rect> plateRects = new List<Rect>();

        /// <summary>The task lines' rectangle on screen for the map's shaders (RealmsHudShow).</summary>
        static readonly int HudMaskId = Shader.PropertyToID("_RealmsHudMask");
        readonly Vector3[] corners = new Vector3[4];
        readonly Vector2[] viewQuad = new Vector2[4];
        bool viewShown;
        VisualElement compass;
        Texture2D minimapTexture;
        Vector2 minimapSize;

        /// <summary>The room for the minimap at the bottom panel's right end, in panel pixels.</summary>
        static readonly Vector2 MinimapBox = new Vector2(330, 216);
        int[] minimapCells;
        World minimapWorld;

        /// <summary>The part of the world (x, z) the minimap shows, see <see cref="MinimapWindow"/>.</summary>
        Rect minimapWindow;
        float toastUntil, reportUntil;
        int shownCell = -1;
        bool shownActions;

        GameState state;
        MapView view;
        ArtLibrary art;
        MapCamera mapCamera;
        Camera cam;

        public bool HelpVisible => helpOverlay != null && helpOverlay.style.display == DisplayStyle.Flex;

        /// <summary>The whole interface, names over the map too, hidden for the promo's view of the land alone.</summary>
        public bool Hidden
        {
            set => root.style.visibility = value ? Visibility.Hidden : Visibility.Visible;
        }

        void Build()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            if (style != null)
                root.styleSheets.Add(style);
            root.AddToClassList("hud");
            root.pickingMode = PickingMode.Ignore;
            UseFont(root, regular);

            // The heroes' shields under the towns' plates and the badges.
            shieldLayer = Child(root, "labels");
            shieldLayer.pickingMode = PickingMode.Ignore;
            labelLayer = Child(root, "labels");
            labelLayer.pickingMode = PickingMode.Ignore;
            // The heroes' tags follow them over the map, under the panels.
            tagLayer = Child(root, "tags");
            tagLayer.pickingMode = PickingMode.Ignore;

            // Top bar along the edge: kingdom, treasury, turn and buttons.
            VisualElement bar = topBar = Child(root, "top-bar");
            VisualElement kingdom = Child(bar, "kingdom");
            shield = Child(kingdom, "kingdom-shield");
            shield.tooltip = T("Флаг королевства: нажми, чтобы нарисовать свой", "Kingdom flag: click to draw your own");
            OnPress(shield, () => FlagPicked?.Invoke());
            kingdomName = Text(kingdom, "kingdom-name", "", black);
            VisualElement treasury = Child(bar, "treasury");
            string[] tips = { T("Золото", "Gold"), T("Еда", "Food"), T("Дерево", "Wood"), T("Камень", "Stone") };
            for (int k = 0; k < 4; k++)
            {
                VisualElement item = Child(treasury, "goods");
                item.tooltip = tips[k];
                goodsItems[k] = item;
                VisualElement icon = Child(item, "goods-icon");
                SetIcon(icon, GoodsIcons[k]);
                amounts[k] = Text(item, "goods-amount", "0", black);
                incomes[k] = Text(item, "goods-income", "", bold);
            }
            Child(bar, "spacer");
            // Season and year: a coloured dot, the season with four pips for the year, the year and turn.
            VisualElement turn = Child(bar, "turn");
            seasonDot = Child(turn, "season-dot");
            VisualElement turnText = Child(turn, "turn-text");
            VisualElement seasonRow = Child(turnText, "turn-season-row");
            seasonLabel = Text(seasonRow, "turn-season", "", black);
            VisualElement pipRow = Child(seasonRow, "turn-pips");
            for (int k = 0; k < 4; k++)
                pips[k] = Child(pipRow, "turn-pip");
            BuildSavedLabel(seasonRow);
            yearLabel = Text(turnText, "turn-year", "", bold);
            Button next = nextTurnButton = Btn(bar, T("Следующий ход", "End turn"), "button-primary", () => NextTurn?.Invoke());
            Text(next, "key-hint", "Enter", bold).pickingMode = PickingMode.Ignore;
            Guided(next, null);
            // The menu, where a new game now is: a button here could end the game with one slip.
            Button menuButton = Btn(bar, Words.MenuButton, "button", () => MenuPicked?.Invoke());
            Text(menuButton, "key-hint-dark", "Esc", bold).pickingMode = PickingMode.Ignore;
            // The crown with its gems' sockets; it opens the crown's feats over the kingdoms' list.
            kingdomsButton = CrownButton(bar);
            Btn(bar, "?", "button-round", () => Help?.Invoke());

            // The turn report, top centre under the bar; a click closes it.
            report = Child(root, "report");
            reportTitle = Text(report, "report-title", "", black);
            reportRows = Child(report, "report-rows");
            OnPress(report, () => reportUntil = 0);
            report.style.display = DisplayStyle.None;

            // The crown's feats and the kingdoms, top right under the bar, opened by the crown or K.
            kingdoms = Child(root, "kingdoms");
            BuildCrownList(kingdoms);
            VisualElement kingdomsHead = Child(kingdoms, "kingdoms-head");
            Text(kingdomsHead, "kingdoms-title", T("Королевства", "Kingdoms"), black);
            // The bars had no caption: a reviewer could not tell what they measure.
            Text(kingdoms, "kingdoms-note", Words.KingdomsNote, regular);
            // Under the crown's feats only four of the six kingdoms fitted above the bottom panel
            // (2026-11-11): the rows scroll, and the list ends above the panel (LateUpdate).
            var rowsScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
            };
            rowsScroll.AddToClassList("kingdoms-rows");
            kingdoms.Add(rowsScroll);
            kingdomRows = rowsScroll.contentContainer;
            kingdoms.style.display = DisplayStyle.None;

            // The tasks: lines of text over the map under the bar, no window; the map takes the clicks.
            quests = Child(root, "quests");
            quests.pickingMode = PickingMode.Ignore;
            questRows = Child(quests, "quests-rows");
            questRows.pickingMode = PickingMode.Ignore;

            // The panel along the bottom: the chosen hero, the chosen place and the minimap.
            dock = Child(root, "dock");
            BuildHeroCard(dock);

            // The chosen place: what it is and whose on the left, the town, the buttons and the hint on the right.
            info = Child(dock, "info");
            infoEmpty = Text(info, "dock-empty", T("Щёлкни по клетке, городу или чудовищу, чтобы узнать о нём.", "Click a tile, a town or a monster to learn about it."), regular);
            infoMain = Child(info, "info-main");
            VisualElement head = Child(infoMain, "info-head");
            infoIcon = Child(head, "info-icon");
            VisualElement titles = Child(head, "info-titles");
            infoTitle = Text(titles, "info-title", "", black);
            VisualElement owner = Child(titles, "info-owner");
            infoDot = Child(owner, "info-dot");
            infoSubtitle = Text(owner, "info-subtitle", "", bold);
            infoLines = Child(infoMain, "info-lines");
            infoChips = Child(infoMain, "info-chips");
            infoSide = Child(info, "info-side");
            infoTown = Child(infoSide, "info-town");
            infoTags = Child(infoSide, "info-tags");
            infoActions = Child(infoSide, "info-actions");
            infoHint = Text(infoSide, "info-hint", "", regular);

            // Minimap, at the right end.
            VisualElement frame = Child(dock, "minimap-frame");
            minimap = Child(frame, "minimap");
            minimapView = Child(minimap, "minimap-view");
            minimapView.pickingMode = PickingMode.Ignore;
            minimapView.generateVisualContent += DrawMinimapView;
            // The compass: where north is on the screen; a click turns the map back to it.
            compass = Child(frame, "compass");
            Text(compass, "compass-north", T("С", "N"), black).pickingMode = PickingMode.Ignore;
            Child(compass, "compass-needle").pickingMode = PickingMode.Ignore;
            OnPress(compass, () => mapCamera?.FaceNorth());

            // Under the dialogs, so that its questions show over it.
            BuildMenu();
            BuildHeroes();
            BuildScreens();
            BuildBattle();

            toast = Text(root, "toast", "", black);
            toast.pickingMode = PickingMode.Ignore;
            toast.style.opacity = 0;

            helpOverlay = Child(root, "help-overlay");
            VisualElement card = Child(helpOverlay, "help-card");
            Frame(card);
            Text(card, "help-title", Words.HelpTitle, black);
            // Three pages instead of one table (the interface plan's 3.3): the few things that
            // matter first, the keys, and the hints the players have already seen.
            VisualElement tabs = Child(card, "screen-tabs");
            tabs.AddToClassList("help-tabs");
            string[] pages = { Words.HelpMain, Words.HelpKeys, Words.HelpHints };
            helpTabs = new Button[pages.Length];
            helpPages = new VisualElement[pages.Length];
            VisualElement body = Child(card, "help-body");
            for (int k = 0; k < pages.Length; k++)
            {
                int page = k;
                helpTabs[k] = Btn(tabs, pages[k], "screen-tab", () => ShowHelpPage(page));
                helpPages[k] = Child(body, "help-page");
            }
            HelpBasics(helpPages[0]);
            // Three groups in two columns: one long list was too much to read at once.
            VisualElement columns = Child(helpPages[1], "help-columns");
            VisualElement left = Child(columns, "help-column"), right = Child(columns, "help-column");
            HelpGroup(left, T("Карта", "Map"), new[,]
            {
                { T("W A S D, стрелки", "W A S D, arrows"), T("двигать карту (Shift — быстрее)", "move the map (Shift — faster)") },
                { T("Тащить правой", "Drag with the right button"), T("двигать карту", "move the map") },
                { T("Колесо мыши, + и −", "Mouse wheel, + and −"), T("приблизить и отдалить", "zoom in and out") },
                { T("Q и E, тащить средней", "Q and E, drag with the middle button"), T("повернуть карту; Backspace или компас у миникарты — снова на север", "turn the map; Backspace or the compass by the minimap — north again") },
                { T("Держать Alt", "Hold Alt"), T("значки ресурсов, мест и чудовищ на всей карте", "icons of resources, places and monsters over the whole map") },
                { T("Левая кнопка мыши", "Left mouse button"), T("выбрать клетку, город или героя", "select a tile, a town or a hero") },
                { T("Пробел, Home", "Space, Home"), T("к столице", "to the capital") },
                { T("K, корона", "K, the crown"), T("подвиги короны и список королевств; щелчок — к столице", "the crown's deeds and the kingdoms; click — to the capital") },
                { "Enter", T("следующий ход: казна пополнится, города растут", "next turn: the treasury fills up, towns grow") },
                { "F1, H", T("эта подсказка", "this help") },
                { "Esc", T("снять выбор, закрыть подсказку или экран; когда снимать нечего — меню", "clear the selection, close the help or a screen; when there is nothing to close — the menu") },
            });
            HelpGroup(right, T("Герой и город", "Hero and town"), new[,]
            {
                { T("Правая кнопка мыши", "Right mouse button"), T("проложить путь выбранному герою", "lay a path for the selected hero") },
                { T("Правой ещё раз, G", "Right again, G"), T("герой идёт по пути; на чудовищ — в бой", "the hero walks the path; onto monsters — into battle") },
                { T("Правой по своему герою", "Right on your own hero"), T("подойти и обменяться войском", "walk up and trade troops") },
                { "F", T("следующий герой", "next hero") },
                { "Tab", T("следующий свой город", "next town of yours") },
                { T("T, дважды по городу", "T, double-click a town"), T("экран города: постройки, наём, войско", "town screen: buildings, recruits, army") },
                { T("R, дважды по герою", "R, double-click a hero"), T("экран героя: сила, опыт, войско", "hero screen: strength, experience, army") },
            });
            HelpGroup(right, T("Бой", "Battle"), new[,]
            {
                { T("Щелчок по клетке", "Click a tile"), T("отряд идёт туда", "the troop goes there") },
                { T("Щелчок по врагу", "Click an enemy"), T("отряд бьёт или стреляет", "the troop strikes or shoots") },
                { "D", T("защита: отряд стоит и держит оборону до своего хода", "defend: the troop stands and holds until its next turn") },
                { "A", T("бьются сами; A ещё раз — снова ты", "they fight on their own; A again — you are in charge again") },
            });
            Btn(card, T("Понятно", "Got it"), "button-primary", () => Help?.Invoke());
            ShowHelpPage(0);
            helpOverlay.style.display = DisplayStyle.None;
        }

        public void Bind(GameState gameState, MapView mapView, ArtLibrary library, MapCamera camera)
        {
            state = gameState;
            view = mapView;
            art = library;
            mapCamera = camera;
            cam = camera.GetComponent<Camera>();
            if (root == null)
                Build();
            SetIcon(shield, FlagIcon(state.World.Player));
            SetIcon(crownIcon, "crown");
            kingdomName.text = state.World.Countries[state.World.Player].Label;
            BuildKingdoms();
            BuildLabels();
            BuildMinimap();
            ShowInfo(-1);
            Refresh();
        }

        public void Refresh()
        {
            int player = state.World.Player;
            Stock stock = state.Treasury[player], income = Economy.Income(state.World, player);
            // The towns take food for their growth from the turn's harvest and the stores.
            int growth = Math.Min(Towns.Demand(state.World, player), stock.Food + income.Food);
            int[] have = { stock.Gold, stock.Food, stock.Wood, stock.Stone };
            int[] gain = { income.Gold, income.Food - growth, income.Wood, income.Stone };
            for (int k = 0; k < 4; k++)
            {
                amounts[k].text = have[k].ToString();
                incomes[k].text = gain[k] > 0 ? $"+{gain[k]}" : gain[k] < 0 ? $"−{-gain[k]}" : "";
                incomes[k].EnableInClassList("goods-loss", gain[k] < 0);
            }
            goodsItems[0].tooltip = T($"Золото: +{income.Gold} за ход", $"Gold: +{income.Gold} a turn");
            goodsItems[1].tooltip = growth > 0 ? T($"Еда: +{income.Food} за ход, на рост городов уйдёт {growth}", $"Food: +{income.Food} a turn, {growth} goes to growing towns") : T($"Еда: +{income.Food} за ход", $"Food: +{income.Food} a turn");
            goodsItems[2].tooltip = T($"Дерево: +{income.Wood} за ход", $"Wood: +{income.Wood} a turn");
            goodsItems[3].tooltip = T($"Камень: +{income.Stone} за ход", $"Stone: +{income.Stone} a turn");
            BuildQuests();
            foreach (KeyValuePair<int, VisualElement> badge in badges)
            {
                Cell cell = state.World.Cells[badge.Key];
                badge.Value.EnableInClassList("resource-wild", cell.Owner < 0);
                badge.Value.EnableInClassList("resource-built", cell.Building != FieldBuilding.None);
            }
            int turn = state.Turn, season = (turn - 1) % 4;
            seasonLabel.text = Words.Season(turn);
            yearLabel.text = T($"Год {Words.Year(turn)} · ход {turn}", $"Year {Words.Year(turn)} · turn {turn}");
            seasonDot.style.backgroundColor = SeasonColours[season];
            for (int k = 0; k < 4; k++)
            {
                pips[k].EnableInClassList("turn-pip-on", k <= season);
                pips[k].style.backgroundColor = k <= season ? SeasonColours[k] : new StyleColor(StyleKeyword.Null);
            }
            RefreshIncomes();
            RefreshCrown();
        }

        /// <summary>Each kingdom's gold a turn in its row of the kingdoms' list.</summary>
        void RefreshIncomes()
        {
            foreach ((int country, Label label) in rowIncomes)
                label.text = "+" + Economy.Income(state.World, country).Gold;
        }

        /// <summary>After land changed hands or more was seen: the minimap and the camera's reach, the kingdoms' land and the rest.</summary>
        public void WorldChanged()
        {
            BuildMinimap();
            BuildKingdoms();
            Refresh();
        }

        // ----- Tasks and the turn report -----

        /// <summary>The tasks on offer, a line each: icon, task and reward.</summary>
        void BuildQuests()
        {
            questRows.Clear();
            List<Quest> offered = state.Offered();
            int raids = 0;
            // The bands going for the players' towns come first, in the warning colour (the fun plan's Ф3).
            foreach (Raid raid in state.Bands)
            {
                City town = state.World.Cities[raid.Town];
                if (town.Country != state.World.Player)
                    continue;
                raids++;
                VisualElement row = Child(questRows, "quest");
                SetIcon(Child(row, "quest-icon"), "swords");
                bool standing = raid.Stay > 0;
                Label line = Halo(row, "quest-text", Words.RaidLine(state.World.Cells[raid.Cell].Monster, town.Label,
                    standing ? raid.Stay : Raids.TurnsLeft(raid), standing), bold);
                line.AddToClassList("quest-raid");
                foreach (VisualElement child in row.Query<VisualElement>().ToList())
                    child.pickingMode = PickingMode.Ignore;
            }
            // Then the neighbours' troops at lairs the players know: a race the hero may still win (the economy plan's step 4).
            foreach (Troop troop in state.LairTroops)
            {
                if (!state.Seen[troop.Lair])
                    continue;
                raids++;
                VisualElement row = Child(questRows, "quest");
                SetIcon(Child(row, "quest-icon"), FlagIcon(troop.Country));
                Halo(row, "quest-text", Words.TroopRow(state.World.Countries[troop.Country].Label, troop.Turns), bold);
                foreach (VisualElement child in row.Query<VisualElement>().ToList())
                    child.pickingMode = PickingMode.Ignore;
            }
            quests.style.display = offered.Count + raids > 0 || guideLine != null ? DisplayStyle.Flex : DisplayStyle.None;
            bool guided = false;
            foreach (Quest quest in offered)
            {
                VisualElement row = Child(questRows, "quest");
                SetIcon(Child(row, "quest-icon"), quest == Quest.Stake ? FlagIcon(state.World.Player) : Words.QuestIcon(quest));
                Halo(row, "quest-text", Words.Quest(quest), bold);
                Reward(row, Quests.Reward(quest), halo: true);
                foreach (VisualElement child in row.Query<VisualElement>().ToList())
                    child.pickingMode = PickingMode.Ignore;
                // The guide's «как» line stands under the task it helps with (HudGuide).
                if (guideLine != null && guideQuest == quest)
                {
                    GuideRow();
                    guided = true;
                }
            }
            if (guideLine != null && !guided)
                GuideRow();
        }

        /// <summary>A chip a good of a reward or a cost; with <paramref name="halo"/> its figure has a dark halo, for text over the map.</summary>
        void Reward(VisualElement parent, Stock stock, string cls = "reward", bool plus = true, bool halo = false)
        {
            int[] values = { stock.Gold, stock.Food, stock.Wood, stock.Stone };
            for (int k = 0; k < 4; k++)
            {
                if (values[k] <= 0)
                    continue;
                VisualElement chip = Child(parent, cls);
                SetIcon(Child(chip, "chip-icon"), GoodsIcons[k]);
                string figure = (plus ? "+" : "") + values[k];
                if (halo)
                    Halo(chip, cls + "-text", figure, black);
                else
                    Text(chip, cls + "-text", figure, black);
            }
        }

        /// <summary>
        /// Text over the map with a dark halo: under the text lies a copy of it in dark with a wide
        /// outline and a soft shadow, so the halo grows outwards and the letters keep their strokes
        /// (an outline on the text itself eats into letters this small). The copy has the text's
        /// box, so it wraps the same way.
        /// </summary>
        static Label Halo(VisualElement parent, string cls, string text, Font font)
        {
            VisualElement holder = Child(parent, "halo");
            holder.pickingMode = PickingMode.Ignore;
            Label under = Text(holder, cls, text, font);
            under.AddToClassList("halo-under");
            under.pickingMode = PickingMode.Ignore;
            Label over = Text(holder, cls, text, font);
            over.AddToClassList("halo-over");
            over.pickingMode = PickingMode.Ignore;
            return over;
        }

        /// <summary>The season, the harvest, the food the towns took, and what happened to the towns and tasks.</summary>
        /// <param name="title">The title instead of the season's: «Ход N. В прошлый ход:» on returning to a game.</param>
        public void ShowReport(TurnReport turn, string title = null)
        {
            World world = state.World;
            reportRows.Clear();
            reportTitle.text = title ?? T($"{Words.Season(state.Turn)}, год {Words.Year(state.Turn)}", $"{Words.Season(state.Turn)}, year {Words.Year(state.Turn)}");
            VisualElement harvest = Child(reportRows, "report-row");
            Text(harvest, "report-text", T("Казна пополнилась", "The treasury filled up"), bold);
            Reward(harvest, turn.Income);
            if (turn.GrowthFood > 0)
            {
                VisualElement eaten = Child(reportRows, "report-row");
                Text(eaten, "report-text", T("На рост городов ушло", "Spent on growing towns"), bold);
                Reward(eaten, new Stock(0, turn.GrowthFood, 0, 0), "cost", false);
            }
            BorderRows(turn);
            foreach (TurnEvent e in turn.Events)
            {
                if (e.Kind == TurnEventKind.Border)
                    continue;
                VisualElement row = Child(reportRows, "report-row");
                string icon = e.Kind == TurnEventKind.QuestDone ? "coins" : e.Kind == TurnEventKind.LevelUp ? "crown"
                    : e.Kind == TurnEventKind.GemSet ? Words.GemIcon(e.Gem)
                    : e.Kind == TurnEventKind.StakeReached || e.Kind == TurnEventKind.StakeLost ? FlagIcon(world.Player)
                    : e.Kind == TurnEventKind.RaidSent || e.Kind == TurnEventKind.RaidArrived || e.Kind == TurnEventKind.RaidLeft ? "swords"
                    : e.Kind == TurnEventKind.RaidRepelled ? "coins"
                    : e.Kind == TurnEventKind.TroopSent || e.Kind == TurnEventKind.TroopCleared || e.Kind == TurnEventKind.TroopRecalled ? FlagIcon(e.Country)
                    : e.Kind == TurnEventKind.Grew ? "hammer" : e.Stop == GrowthStop.Food ? "wheat" : "hammer";
                if (IconTexture(icon) == null)
                    icon = "crown";
                SetIcon(Child(row, "report-icon"), icon);
                bool big = e.Kind == TurnEventKind.LevelUp || (e.Kind == TurnEventKind.GemSet && e.Country == world.Player);
                Label text = Text(row, "report-text", Words.Event(e, world), big ? black : regular);
                if (e.Kind == TurnEventKind.Stopped || e.Kind == TurnEventKind.LandFull || e.Kind == TurnEventKind.StakeLost
                    || e.Kind == TurnEventKind.RaidSent || e.Kind == TurnEventKind.RaidArrived || e.Kind == TurnEventKind.TroopSent)
                    text.AddToClassList("report-warn");
                if (e.City >= 0)
                {
                    int city = e.City;
                    row.AddToClassList("report-link");
                    OnPress(row, () => CityPicked?.Invoke(city));
                }
                foreach (VisualElement child in row.Query<VisualElement>().ToList())
                {
                    if (child != row)
                        child.pickingMode = PickingMode.Ignore;
                }
            }
            report.style.display = DisplayStyle.Flex;
            // On returning it stays longer: the players look round their kingdom first.
            reportUntil = ViewClock.Now + (title != null ? 20f : turn.Events.Count > 0 ? 10f : 5f);
        }

        /// <summary>
        /// The cells the players' towns took this turn, one line for all, and a line for each
        /// resource that came inside the border (the territory plan of 2026-11-11).
        /// </summary>
        void BorderRows(TurnReport turn)
        {
            World world = state.World;
            var towns = new List<string>();
            var resources = new List<int>();
            int cells = 0, first = -1;
            foreach (TurnEvent e in turn.Events)
            {
                if (e.Kind != TurnEventKind.Border)
                    continue;
                cells++;
                if (first < 0)
                    first = e.City;
                if (!towns.Contains(world.Cities[e.City].Label))
                    towns.Add(world.Cities[e.City].Label);
                if (world.Cells[e.Cell].Resource != Resource.None)
                    resources.Add(e.Cell);
            }
            if (cells == 0)
                return;
            ReportLine(FlagIcon(world.Player), Words.BorderGrew(cells, towns), first);
            foreach (int cell in resources)
                ReportLine(Words.ResourceIcon(world.Cells[cell].Resource), Words.ResourceInside(world.Cells[cell].Resource), -1);
        }

        void ReportLine(string icon, string words, int city)
        {
            VisualElement row = Child(reportRows, "report-row");
            SetIcon(Child(row, "report-icon"), IconTexture(icon) != null ? icon : "crown");
            Text(row, "report-text", words, regular);
            if (city >= 0)
            {
                row.AddToClassList("report-link");
                OnPress(row, () => CityPicked?.Invoke(city));
            }
            foreach (VisualElement child in row.Query<VisualElement>().ToList())
            {
                if (child != row)
                    child.pickingMode = PickingMode.Ignore;
            }
        }

        public void ToggleKingdoms()
        {
            if (kingdoms == null)
                return;
            bool show = kingdoms.style.display == DisplayStyle.None;
            // The list hides under the menu, so the crown closes the menu first.
            if (show && MenuOpen)
                ShowMenu(false);
            kingdoms.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            kingdomsButton.EnableInClassList("button-on", show);
        }

        /// <summary>Closes the kingdoms' list opened from the bar; false when it was not open.</summary>
        public bool CloseLists()
        {
            bool open = kingdoms.style.display == DisplayStyle.Flex;
            if (open)
                ToggleKingdoms();
            return open;
        }

        // ----- Kingdoms -----

        /// <summary>One row a kingdom, the players' first, then by land: shield, name, cities, gold, land bar.</summary>
        void BuildKingdoms()
        {
            kingdomRows.Clear();
            rowIncomes.Clear();
            rowGems.Clear();
            rowAims.Clear();
            World world = state.World;
            int count = world.Countries.Count;
            var land = new int[count];
            foreach (Cell cell in world.Cells)
            {
                if (cell.Owner >= 0 && cell.IsLand)
                    land[cell.Owner]++;
            }
            int most = 1;
            foreach (int n in land)
                most = Mathf.Max(most, n);
            var order = new List<int>();
            for (int c = 0; c < count; c++)
                order.Add(c);
            order.Sort((a, b) => a == b ? 0 : a == world.Player ? -1 : b == world.Player ? 1
                : land[b] != land[a] ? land[b].CompareTo(land[a]) : a.CompareTo(b));
            foreach (int c in order)
            {
                Country country = world.Countries[c];
                Color colour = Palette.Countries[c % Palette.Countries.Length];
                VisualElement row = Child(kingdomRows, "kingdom-row");
                if (c == world.Player)
                    row.AddToClassList("kingdom-row-player");
                row.style.borderLeftColor = colour;
                int picked = c;
                OnPress(row, () => KingdomPicked?.Invoke(picked));
                VisualElement emblem = Child(row, "kingdom-row-shield");
                SetIcon(emblem, FlagIcon(c));
                VisualElement body = Child(row, "kingdom-row-body");
                Text(body, "kingdom-row-name", country.Label + (c == world.Player ? T(" (ты)", " (you)") : ""), black);
                VisualElement stats = Child(body, "kingdom-row-stats");
                int cities = country.Cities.Count;
                Text(stats, "kingdom-row-stat", T($"{cities} {Words.Plural(cities, "город", "города", "городов")}", $"{cities} {En(cities, "town", "towns")}"), bold);
                Text(stats, "kingdom-row-dot", "·", bold);
                SetIcon(Child(stats, "kingdom-row-coin"), "coins");
                rowIncomes.Add((c, Text(stats, "kingdom-row-stat", "", bold)));
                KingdomCrown(stats, c);
                // The land bar moves now that the land grows by itself; beside it the cells the
                // kingdom's towns took last turn (the territory plan of 2026-11-11).
                VisualElement landRow = Child(body, "kingdom-row-land");
                VisualElement track = Child(landRow, "kingdom-row-bar");
                VisualElement fill = Child(track, "kingdom-row-fill");
                fill.style.width = Length.Percent(100f * land[c] / most);
                fill.style.backgroundColor = colour;
                int grew = state.GrownLastTurn[c];
                Text(landRow, "kingdom-row-gain", grew > 0 ? $"+{grew}" : "", bold).tooltip = Words.LandGain(grew);
                foreach (VisualElement child in row.Query<VisualElement>().ToList())
                {
                    if (child != row)
                        child.pickingMode = PickingMode.Ignore;
                }
            }
            // A new flag rebuilds the rows without the rest of Refresh, so the incomes and gems are filled here.
            RefreshIncomes();
            RefreshCrown();
        }

        /// <summary>The top of the bottom panel in screen pixels from the bottom; 0 before the first layout.</summary>
        public float DockTop
        {
            get
            {
                float top = dock?.worldBound.yMin ?? float.NaN, height = root?.worldBound.height ?? 0f;
                return float.IsNaN(top) || !(height > 0f) ? 0f : Screen.height * (1f - top / height);
            }
        }

        public void ToggleHelp(bool show)
        {
            if (show)
                FillHelpHints();
            helpOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        Button[] helpTabs;
        VisualElement[] helpPages;
        List<string> seenHints = new List<string>();

        /// <summary>The hints the players have seen (kept in the settings), for the help's third page.</summary>
        public void SetSeenHints(List<string> hints) => seenHints = hints ?? new List<string>();

        /// <summary>Shows one of the help's pages; the help opens on the page last shown.</summary>
        public void ShowHelpPage(int page)
        {
            for (int k = 0; k < helpPages.Length; k++)
            {
                helpPages[k].style.display = k == page ? DisplayStyle.Flex : DisplayStyle.None;
                helpTabs[k].EnableInClassList("screen-tab-on", k == page);
            }
        }

        /// <summary>The first page: choose, order, build, the turn and the goal, each with its picture.</summary>
        void HelpBasics(VisualElement page)
        {
            foreach ((string icon, string head, string line) in Words.HelpBasics)
            {
                VisualElement row = Child(page, "help-basic");
                SetIcon(Child(row, "help-basic-icon"), IconTexture(icon) != null ? icon : "crown");
                VisualElement words = Child(row, "help-basic-words");
                Text(words, "help-basic-head", head, black);
                Text(words, "help-text", line, regular);
            }
            Text(page, "help-note", Words.HelpNote, regular);
        }

        /// <summary>The third page: every hint the players have seen, so a forgotten one can be read again.</summary>
        void FillHelpHints()
        {
            VisualElement page = helpPages[2];
            page.Clear();
            if (seenHints.Count == 0)
            {
                Text(page, "help-note", Words.HelpHintsNone, regular);
                return;
            }
            foreach (string hint in seenHints)
            {
                VisualElement row = Child(page, "help-row");
                SetIcon(Child(row, "help-hint-dot"), "crown");
                Text(row, "help-text", hint, regular);
            }
        }

        public void Toast(string text, float seconds = 3.5f)
        {
            toast.text = text;
            toastUntil = ViewClock.Now + seconds;
        }

        /// <summary>The message line gone at once.</summary>
        public void ClearToast() => toastUntil = 0f;

        /// <summary>True when the pointer (screen pixels, origin bottom left) is over a panel.</summary>
        public bool PointerOverUi(Vector2 screen)
        {
            if (root?.panel == null)
                return false;
            // A click that has just closed a panel does not fall through to the map behind it.
            if (Time.frameCount - pressFrame <= 1)
                return true;
            VisualElement picked = Picked(screen);
            return picked != null && picked != root;
        }

        // ----- Info card -----

        /// <summary>
        /// Shows a cell on the card; with <paramref name="actions"/> (a selected cell) also the
        /// buttons to build on it or in its town, or when its land comes.
        /// </summary>
        public void ShowInfo(int index, bool actions = false)
        {
            shownCell = index;
            shownActions = actions;
            bool shown = index >= 0 && state != null;
            infoEmpty.style.display = shown ? DisplayStyle.None : DisplayStyle.Flex;
            infoMain.style.display = infoSide.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown)
                return;
            World world = state.World;
            Cell cell = world.Cells[index];
            int player = world.Player;
            infoLines.Clear();
            infoChips.Clear();
            infoTown.Clear();
            infoTags.Clear();
            infoActions.Clear();
            if (!state.Seen[index])
            {
                Fogged();
                return;
            }
            string icon = null;
            string title = Words.Biome(cell);
            bool owned = cell.Owner >= 0;
            string hint = Words.BiomeHint(cell);
            Hero standing = state.HeroAt(index);
            if (cell.City >= 0)
            {
                City city = world.Cities[cell.City];
                title = city.Label;
                icon = city.Capital ? "crown" : FlagIcon(city.Country);
                string level = Words.Level(Towns.Level(city));
                Line(city.Capital ? T("Столица", "Capital") : T("Поселение", "Settlement"), city.Capital ? level.ToLowerInvariant() : level);
                if (city.Country == player)
                    Line(Words.BorderCaption, Words.TownBorder(city));
                if (standing != null)
                    Line(T("В городе", "In town"), T($"герой {standing.Label}", $"hero {standing.Label}"));
                Chips(Economy.CityYield(city), T("Даёт за ход", "Gives each turn"), false);
                if (city.Country == player)
                    TownPanel(city, actions);
                else
                {
                    Line(T("Дома", "Houses"), city.Houses.ToString());
                    foreach (TownBuilding building in city.Buildings)
                        Text(infoTags, "tag", Words.Town(building), bold);
                }
                hint = null;
            }
            else if (cell.District >= 0)
            {
                City city = world.Cities[cell.District];
                title = T("Квартал", "Quarter");
                icon = city.Capital ? "crown" : FlagIcon(city.Country);
                Line(T("Город", "Town"), city.Label);
                QuarterTags(city, index);
                hint = T("Квартал — часть города: в нём шесть мест для домов и построек.", "A quarter is part of a town: it has six places for houses and buildings.");
                if (city.Country == player && actions)
                {
                    Button enter = Btn(infoActions, Words.EnterTown, "button-primary", () => TownScreenPicked?.Invoke(city.Index));
                    enter.AddToClassList("info-enter");
                    Text(enter, "key-hint", "T", bold).pickingMode = PickingMode.Ignore;
                    Guided(enter, Words.EnterTown);
                }
            }
            else if (cell.Monster != Monster.None)
            {
                title = Words.Monster(cell.Monster, cell.Lair);
                icon = "lair";
                Line(T("Отряд", "Troop"), Words.Count(Units.Of(cell.Monster), cell.Count));
                if (hero != null && hero.Army.Count > 0 && actions)
                    OddsLines(index);
                else
                    DangerLine(Words.Danger(cell.Monster, cell.Count));
                Raid raid = cell.Band ? Raids.At(state, index) : null;
                if (raid != null)
                {
                    bool atTown = raid.Stay > 0;
                    Line(Words.BandCaption, Words.BandLine(world.Cities[raid.Town].Label, atTown ? raid.Stay : Raids.TurnsLeft(raid), atTown));
                    Line("", Words.BandHint, "monster-hint");
                }
                else if (Troops.At(state, index) is Troop troop)
                {
                    Line(Words.TroopCaption, Words.TroopLine(world.Countries[troop.Country].Label, troop.Turns));
                    Line("", Words.TroopHint, "monster-hint");
                }
                else
                    Line("", Words.MonsterHint(cell.Monster), "monster-hint");
                if (cell.Place != Core.Place.None)
                    Line(T("Сторожит", "Guards"), Words.Place(cell.Place));
                hint = hero == null ? T("Выбери героя, чтобы узнать, по силам ли ему этот бой.", "Select a hero to see whether they can win this fight.")
                    : hero.Army.Count == 0 ? T("У героя нет войска: найми его в городе с казармами.", "The hero has no army: recruit one in a town with barracks.") : null;
            }
            else if (cell.Place != Core.Place.None)
            {
                title = Words.Place(cell.Place);
                icon = Words.PlaceIcon(cell.Place);
                hint = Words.PlaceHint(cell.Place);
                bool shrine = cell.Place == Core.Place.ShrineAttack || cell.Place == Core.Place.ShrineDefence;
                if (shrine && hero != null && hero.Shrines.Contains(index))
                    Line("", T($"{hero.Label} уже молился здесь", $"{hero.Label} has already prayed here"), "muted");
                Line(T("Местность", "Terrain"), Words.Biome(cell));
            }
            else if (cell.Resource != Resource.None)
            {
                icon = Words.ResourceIcon(cell.Resource);
                title = Words.Resource(cell.Resource);
                Line(T("Местность", "Terrain"), Words.Biome(cell));
                FieldBuilding needed = Economy.BuildingFor(cell.Resource);
                if (cell.Building != FieldBuilding.None)
                {
                    Line(T("Постройка", "Building"), Words.Field(cell.Building));
                    Chips(Economy.Yield(cell), T("Даёт за ход", "Gives each turn"), false);
                }
                else
                {
                    Line(T("Нужна постройка", "Needs a building"), Words.Field(needed));
                    Chips(Economy.Potential(cell), T("С постройкой даст", "With a building it gives"), true);
                    if (actions && cell.Owner == player)
                    {
                        Refusal refusal = Economy.CanBuild(world, state.Treasury[player], player, index);
                        Stock cost = Economy.Cost(needed);
                        ActionButton(Words.Field(needed), null, cost, refusal, () => BuildField?.Invoke(index));
                    }
                    else if (cell.Owner == player)
                        hint = T("Щёлкни по клетке, чтобы строить.", "Click the tile to build.");
                }
            }
            if (standing != null && cell.City < 0)
                Line(T("Здесь", "Here"), T($"герой {standing.Label}", $"hero {standing.Label}"));
            if (icon == null && cell.Resource == Resource.None && cell.Place == Core.Place.None && !cell.IsWater)
                Line(T("Ресурсов нет", "No resources"), "");
            if (cell.Owner < 0 && cell.City < 0 && cell.Monster == Monster.None)
                BorderHint(index, actions, ref hint);
            if (cell.Owner == player && cell.Town < 0)
                ExpandAction(index, actions, ref hint);
            string way = WayHint(index, actions);
            if (way != null)
                hint = way;

            Color colour;
            if (owned)
            {
                infoSubtitle.text = world.Countries[cell.Owner].Label + (cell.Owner == player ? T(" (ты)", " (you)") : "");
                colour = Palette.Countries[cell.Owner % Palette.Countries.Length];
            }
            else
            {
                infoSubtitle.text = cell.IsWater ? T("Ничьи воды", "Unclaimed waters") : T("Ничья земля", "Unclaimed land");
                colour = new Color(0.66f, 0.64f, 0.6f);
            }
            Finish(title, hint, icon, colour);
        }

        /// <summary>A cell in the fog: nothing is known about it.</summary>
        void Fogged()
        {
            infoSubtitle.text = T("Туман", "Fog");
            Finish(T("Неизведанная земля", "Unexplored land"), T("Пошли сюда героя: он разведает всё вокруг.", "Send a hero here: they will scout everything around."), null, new Color(0.45f, 0.47f, 0.5f));
        }

        void Finish(string title, string hint, string icon, Color colour)
        {
            infoDot.style.backgroundColor = colour;
            infoTitle.text = title;
            infoHint.text = hint ?? "";
            infoHint.style.display = string.IsNullOrEmpty(infoHint.text) ? DisplayStyle.None : DisplayStyle.Flex;
            infoTags.style.display = infoTags.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            infoChips.style.display = infoChips.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            infoTown.style.display = infoTown.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            infoActions.style.display = infoActions.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            infoLines.style.display = infoLines.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            infoIcon.style.display = icon != null && IconTexture(icon) != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (icon != null)
                SetIcon(infoIcon, icon);
            infoSide.style.display = infoTown.childCount + infoTags.childCount + infoActions.childCount > 0
                || !string.IsNullOrEmpty(infoHint.text) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>How the chosen hero's fight with the group on the cell would go: the odds, the reward and the losses.</summary>
        void OddsLines(int index)
        {
            Odds odds = OddsFor(index);
            string cls = odds.Win >= Odds.Easy ? "good" : odds.Win >= Odds.Even ? "warn" : "danger";
            Line(T("Бой", "Battle"), Words.Odds(odds), cls);
            int factor = Odds.RewardFactor(odds.Win);
            Chips(state.RewardOf(index, odds), factor > 1 ? $"{Words.FightReward} ×{factor}" : Words.FightReward, false);
            float lost = 0f, total = 0f;
            for (int k = 0; k < hero.Army.Count && k < odds.Losses.Length; k++)
            {
                lost += odds.Losses[k];
                total += hero.Army[k].Count;
            }
            Line(T("Потери", "Losses"), Words.Losses(total > 0 ? lost / total : 0f), cls == "good" ? null : cls);
        }

        /// <summary>The odds, worked out once a cell, turn and army (hundreds of fights are played for them).</summary>
        Odds OddsFor(int index)
        {
            int key = index;
            key = key * 31 + hero.Index;
            key = key * 31 + state.Turn;
            key = key * 31 + hero.Attack * 7 + hero.Defence;
            key = key * 31 + state.World.Cells[index].Count;
            foreach (Stack s in hero.Army)
                key = key * 17 + s.Count * 11 + (int)s.Unit;
            if (oddsKey != key || oddsCache.Losses == null)
            {
                oddsCache = state.OddsOf(hero, index);
                oddsKey = key;
            }
            return oddsCache;
        }

        /// <summary>What the chosen hero's way to the cell takes, or why there is none; null when it does not apply.</summary>
        string WayHint(int index, bool actions)
        {
            if (hero == null || !actions || index == hero.Cell)
                return null;
            if (path == null || path.Target != index)
                return null;
            int turns = path.Turns[path.Turns.Count - 1];
            bool fight = state.World.Cells[index].Monster != Monster.None;
            Hero other = state.HeroAt(index);
            string go = fight ? T("Щёлкни правой ещё раз (или G) — и в бой!", "Right-click again (or G) — and into battle!")
                : other != null ? T($"Щёлкни правой ещё раз (или G) — {hero.Label} подойдёт к герою {other.Label}, и они обменяются войском.", $"Right-click again (or G) — {hero.Label} will walk up to {other.Label}, and they will trade troops.")
                : T("Щёлкни правой ещё раз (или G) — герой пойдёт.", "Right-click again (or G) — the hero will go.");
            return turns == 0 ? go : T($"Дойдёт за {turns + 1} {Words.Plural(turns + 1, "ход", "хода", "ходов")}. {go}", $"Gets there in {turns + 1} {En(turns + 1, "turn", "turns")}. {go}");
        }

        /// <summary>Shows the card again after the treasury or the world changed.</summary>
        public void RefreshInfo()
        {
            ShowInfo(shownCell, shownActions);
            RefreshScreen();
        }

        /// <summary>
        /// When one of the players' towns takes the wild cell, or why none will: the land grows by
        /// itself from the towns (the territory plan of 2026-11-11).
        /// </summary>
        void BorderHint(int index, bool actions, ref string hint)
        {
            World world = state.World;
            if (!world.Cells[index].IsLand)
                return;
            int player = world.Player;
            int stake = world.Countries[player].Stake;
            int staked = Influence.StakeTurns(world, player, index, out int leader);
            if (stake == index && staked > 0)
            {
                // The stake stands here: the border comes by its way.
                Line(Words.BorderCaption, Words.BorderTurns(world.Cities[leader].Label, staked));
                Line(Words.StakeLine, Words.StakeHere(world.Cities[leader].Label), "good");
                hint = Words.StakeHint;
                if (actions)
                    ActionButton(Words.StakeRemove, Words.StakeRemoveWhat, default, Refusal.None, () => PlaceStake?.Invoke(-1), FlagIcon(player));
                return;
            }
            int turns = Influence.TurnsTo(world, player, index, out int town);
            if (turns > 0)
            {
                Line(Words.BorderCaption, Words.BorderTurns(world.Cities[town].Label, turns));
                hint = Words.BorderHint;
            }
            else
                hint = Words.BorderNever(Influence.Open(world, index), Influence.NearOtherTown(world, player, index));
            if (staked <= 0)
                return;
            Line(Words.StakeCaption, Words.StakeTurns(staked), staked < turns ? "good" : null);
            if (actions)
            {
                hint = Words.StakeHint;
                ActionButton(stake >= 0 ? Words.StakeMove : Words.StakeSet, stake >= 0 ? Words.StakeMoveWhat : Words.StakeSetWhat,
                    default, Refusal.None, () => PlaceStake?.Invoke(index), FlagIcon(player));
            }
            else
                hint = Words.StakeClick;
        }

        /// <summary>
        /// The button that grows the town beside an empty cell of the players' onto it, once the
        /// town has no free place; the hint says so while the pointer is over the cell.
        /// </summary>
        void ExpandAction(int index, bool actions, ref string hint)
        {
            World world = state.World;
            int player = world.Player;
            Refusal refusal = Towns.CanExpand(world, state.Treasury[player], player, index, out int town);
            if (refusal != Refusal.None && refusal != Refusal.NotEnough)
                return;
            string name = world.Cities[town].Label;
            if (actions)
                ActionButton(T("Расширить город", "Expand town"), T($"Новый квартал: {Towns.DistrictPlaces} мест", $"New quarter: {Towns.DistrictPlaces} places"), Towns.DistrictCost,
                    refusal, () => ExpandTown?.Invoke(index), "icon_town");
            else
                hint = T($"Сюда может вырасти город {name}: в нём кончились места. Щёлкни по клетке.", $"{name} can grow here: it has run out of places. Click the tile.");
        }

        /// <summary>What stands in a quarter: its houses, its buildings and the free places.</summary>
        void QuarterTags(City city, int cell)
        {
            int[] lots = Towns.Layout(city);
            int first = Towns.FirstPlace(city, cell), houses = 0, free = 0;
            var buildings = new List<string>();
            for (int p = first; p >= 0 && p < first + Towns.DistrictPlaces && p < lots.Length; p++)
            {
                if (lots[p] == Towns.HouseLot)
                    houses++;
                else if (lots[p] == Towns.FreeLot)
                    free++;
                else
                    buildings.Add(Words.Town((TownBuilding)lots[p]));
            }
            if (houses > 0)
                Line(T("Дома", "Houses"), houses.ToString());
            foreach (string building in buildings)
                Text(infoTags, "tag", building, bold);
            Line(T("Свободно", "Free"), free == 0 ? T("мест нет", "no places") : T($"{free} {Words.Plural(free, "место", "места", "мест")}", $"{free} {En(free, "place", "places")}"));
        }

        /// <summary>The players' town: its houses and growth, its places, and the way into its screen.</summary>
        void TownPanel(City city, bool actions)
        {
            // A short account: the buildings themselves are on the town's screen.
            VisualElement houses = Child(infoTown, "info-line");
            Text(houses, "info-key", T("Дома", "Houses"), bold);
            Text(houses, "info-value", city.Houses.ToString(), black);
            Text(houses, "info-key", T(" · места", " · places"), bold);
            Text(houses, "info-value", T($"{Towns.Taken(city)} из {Towns.Places(city)}", $"{Towns.Taken(city)} of {Towns.Places(city)}"), regular);
            Growth(city, infoTown);
            if (city.Garrison.Count > 0)
            {
                var units = new List<string>();
                foreach (Stack s in city.Garrison)
                    units.Add(Words.Count(s.Unit, s.Count));
                Line(T("Ждут героя", "Waiting for a hero"), string.Join(", ", units), null, infoTown);
            }
            if (!actions)
            {
                Line("", T("Щёлкни дважды или нажми T, чтобы войти в город", "Double-click or press T to enter the town"), "muted", infoTown);
                return;
            }
            // Building and hiring are on the town's screen; the card says what waits there.
            int canBuild = 0;
            foreach (TownBuilding[] list in new[] { Economy.TownBuildings, Economy.Fortifications })
            {
                foreach (TownBuilding building in list)
                {
                    if (Economy.CanBuild(city, state.Treasury[city.Country], building) == Refusal.None)
                        canBuild++;
                }
            }
            if (canBuild > 0)
                Line(T("Можно построить", "Can build"), canBuild.ToString(), null, infoTown);
            Button enter = Btn(infoActions, Words.EnterTown, "button-primary", () => TownScreenPicked?.Invoke(city.Index));
            enter.AddToClassList("info-enter");
            Text(enter, "key-hint", "T", bold).pickingMode = PickingMode.Ignore;
            Guided(enter, Words.EnterTown);
        }

        /// <summary>The town's growth: a bar towards the next house, or why it stopped.</summary>
        void Growth(City city, VisualElement into)
        {
            GrowthStop limit = Towns.Limit(city);
            VisualElement growth = Child(into, "info-line");
            Text(growth, "info-key", T("Рост", "Growth"), bold);
            if (limit != GrowthStop.None)
            {
                bool warn = limit == GrowthStop.Tavern || limit == GrowthStop.Castle || limit == GrowthStop.Room;
                Text(growth, "info-value", Words.Stop(limit), regular).AddToClassList(warn ? "warn" : "info-value");
                if (limit == GrowthStop.Room)
                    Line("", into == townStats ? T("Кнопка «Расширить город» — на вкладке «Постройки».", "The “Expand town” button is on the “Buildings” tab.")
                        : T("Щёлкни на карте по своей пустой клетке рядом с городом: там будет кнопка «Расширить город».", "Click an empty tile of yours next to the town on the map: it will have an “Expand town” button."), "muted", into);
                return;
            }
            int next = Towns.NextHouse(city);
            VisualElement track = Child(growth, "growth-bar");
            VisualElement fill = Child(track, "growth-fill");
            fill.style.width = Length.Percent(100f * Mathf.Clamp01((float)city.Growth / next));
            Text(growth, "growth-text", T($"{city.Growth} / {next} еды", $"{city.Growth} / {next} food"), bold);
            if (city.Stop == GrowthStop.Food)
                Line("", T("Не хватает еды: построй ферму или причал", "Not enough food: build a farm or a pier"), "warn", into);
            else
                Line("", T($"берёт {Towns.IntakeOf(city)} еды за ход", $"takes {Towns.IntakeOf(city)} food a turn"), "muted", into);
        }

        /// <summary>
        /// The town's hire buttons: every recruit waiting in its barracks, range and stables (as many
        /// as the treasury pays), and a new hero in a town with a tavern.
        /// </summary>
        void RecruitActions(City city, VisualElement into = null)
        {
            int country = city.Country;
            foreach (Unit unit in Units.Hired)
            {
                if (!city.Buildings.Contains(Units.Building(unit)))
                    continue;
                int waiting = city.Recruits[(int)unit];
                int count = Math.Max(1, Math.Min(waiting, state.Affordable(country, city.Index, unit)));
                Refusal refusal = state.CanRecruit(country, city.Index, unit, count);
                Unit hired = unit;
                string what = waiting > 0 ? T($"ждут найма: {waiting}", $"waiting to be hired: {waiting}") : null;
                ActionButton(T($"Нанять: {Words.Count(unit, count)}", $"Hire: {Words.Count(unit, count)}"), what, Units.Cost(unit) * count, refusal,
                    () => Recruit?.Invoke(city.Index, hired, count), null, portraits?.Of(unit), into);
            }
            if (city.Buildings.Contains(TownBuilding.Tavern))
            {
                Refusal refusal = state.CanHireHero(country, city.Index);
                if (refusal != Refusal.TooManyHeroes)
                {
                    ActionButton(T("Нанять героя", "Hire a hero"), T($"с ним {HeroRules.HireSpearmen} копейщиков", $"comes with {HeroRules.HireSpearmen} spearmen"), new Stock(HeroRules.HireGold, 0, 0, 0),
                        refusal, () => HireHero?.Invoke(city.Index), null, portraits?.Hero, into);
                }
            }
        }

        /// <summary>
        /// A build or hire button: the name and what it does, the cost as chips; when it cannot
        /// be done it is pale and says why, unless that is <paramref name="saidAbove"/> the list.
        /// The picture is an icon or a portrait (a hammer by default).
        /// </summary>
        Button ActionButton(string name, string what, Stock cost, Refusal refusal, Action clicked, string icon = "hammer",
            Texture2D picture = null, VisualElement into = null, bool saidAbove = false)
        {
            // A refused button still runs the action: the handler checks again and says why it
            // cannot be done, and the card may be older than the treasury.
            Button button = PressButton(clicked);
            button.AddToClassList("action");
            button.focusable = false;
            if (refusal != Refusal.None)
                button.AddToClassList("action-off");
            (into ?? infoActions).Add(button);
            VisualElement image = Child(button, "action-icon");
            if (picture != null)
            {
                image.style.backgroundImage = new StyleBackground(Background.FromTexture2D(picture));
                image.AddToClassList("action-portrait");
            }
            else
                SetIcon(image, icon ?? "hammer");
            VisualElement words = Child(button, "action-words");
            Text(words, "action-name", name, black);
            bool why = refusal != Refusal.None && !saidAbove;
            string line = why ? Words.Refusal(refusal, state.Treasury[state.World.Player].Missing(cost)) : what;
            if (!string.IsNullOrEmpty(line))
                Text(words, why ? "action-why" : "action-what", line, regular);
            VisualElement price = Child(button, "action-cost");
            Reward(price, cost, "cost", false);
            foreach (VisualElement child in button.Query<VisualElement>().ToList())
            {
                if (child != button)
                    child.pickingMode = PickingMode.Ignore;
            }
            Guided(button, name);
            return button;
        }

        void Line(string key, string value, string extra = null, VisualElement parent = null)
        {
            VisualElement row = Child(parent ?? infoLines, "info-line");
            if (!string.IsNullOrEmpty(key))
                Text(row, "info-key", key, bold);
            if (!string.IsNullOrEmpty(value))
            {
                Label v = Text(row, "info-value", value, regular);
                if (extra != null)
                    v.AddToClassList(extra);
            }
        }

        /// <summary>Danger as one to three crossed swords; the rest stay pale.</summary>
        void DangerLine(int level)
        {
            VisualElement row = Child(infoLines, "info-line");
            Text(row, "info-key", T("Опасность", "Danger"), bold);
            for (int k = 0; k < 3; k++)
            {
                VisualElement swords = Child(row, "danger-icon");
                SetIcon(swords, "swords");
                if (k >= level)
                    swords.AddToClassList("danger-off");
            }
        }

        /// <summary>What the cell gives a turn, one chip a good; pale while it is only a promise.</summary>
        void Chips(Stock stock, string caption, bool pale, VisualElement into = null)
        {
            into ??= infoChips;
            int[] values = { stock.Gold, stock.Food, stock.Wood, stock.Stone };
            bool any = false;
            for (int k = 0; k < 4; k++)
            {
                if (values[k] <= 0)
                    continue;
                if (!any)
                    Text(into, "info-key", caption, bold);
                any = true;
                VisualElement chip = Child(into, "chip");
                if (pale)
                    chip.AddToClassList("chip-pale");
                SetIcon(Child(chip, "chip-icon"), GoodsIcons[k]);
                Text(chip, "chip-text", "+" + values[k], black);
            }
        }

        // ----- Labels over the map -----

        /// <summary>After fights, visits and growth: the monsters' and places' labels anew.</summary>
        public void RebuildLabels() => BuildLabels();

        /// <summary>The map's badges show as while Alt is held: for the captures.</summary>
        public bool BadgesShown { get; set; }

        void BuildLabels()
        {
            labelLayer.Clear();
            labels.Clear();
            badges.Clear();
            // The heroes' shields come back with the kingdoms' flags as they are now.
            shieldLayer.Clear();
            heroShields.Clear();
            World world = state.World;
            foreach (City city in world.Cities)
            {
                VisualElement anchor = Child(labelLayer, "anchor");
                VisualElement plate = Child(anchor, city.Capital ? "plate-capital" : "plate");
                plate.AddToClassList("plate-base");
                Color color = Palette.Countries[city.Country % Palette.Countries.Length];
                plate.style.borderTopColor = plate.style.borderBottomColor = plate.style.borderLeftColor = plate.style.borderRightColor = color;
                if (city.Capital)
                {
                    VisualElement crown = Child(plate, "plate-crown");
                    SetIcon(crown, "crown");
                }
                VisualElement badge = Child(plate, "plate-shield");
                SetIcon(badge, FlagIcon(city.Country));
                Text(plate, "plate-name", city.Label, black);
                // Over the far edge of the town (a capital fills its cell up to the walls, a
                // village's houses stand nearer its middle), a little above its wall towers.
                float x = world.CenterX(city.Cell), z = world.CenterZ(city.Cell);
                float y = view.Field.Height(x, z) + (city.Capital ? 0.42f : 0.3f);
                Add(anchor, new Vector3(x, y, z), city.Capital ? LabelKind.Capital : LabelKind.Town, city.Cell, city.Capital ? 0.9f : 0.62f);
            }
            foreach (Cell cell in world.Cells)
            {
                float x = world.CenterX(cell.Index), z = world.CenterZ(cell.Index);
                if (cell.Monster != Monster.None)
                {
                    // A pill: the den's sign for a lair, crossed swords for a roaming group, and the count.
                    VisualElement anchor = Child(labelLayer, "anchor");
                    VisualElement badge = Child(anchor, "group-badge");
                    if (cell.Lair)
                        badge.AddToClassList("group-lair");
                    SetIcon(Child(badge, "group-icon"), cell.Lair ? "lair" : "swords");
                    Text(badge, "group-count", cell.Count.ToString(), black);
                    // A neighbour's troop at the lair: its flag and the turns until the lair is its (the economy plan's step 4).
                    if (cell.Lair && Troops.At(state, cell.Index) is Troop troop)
                    {
                        SetIcon(Child(badge, "group-icon"), FlagIcon(troop.Country));
                        Text(badge, "group-count", troop.Turns.ToString(), black);
                    }
                    Add(anchor, CellMiddle(x, z), LabelKind.Group, cell.Index);
                }
                else if (cell.Place != Core.Place.None)
                {
                    string icon = Words.PlaceIcon(cell.Place);
                    if (IconTexture(icon) == null)
                        continue;
                    VisualElement anchor = Child(labelLayer, "anchor");
                    VisualElement badge = Child(anchor, "place-badge");
                    SetIcon(Child(badge, "badge-icon"), icon);
                    Add(anchor, CellMiddle(x, z), LabelKind.Place, cell.Index);
                }
                else if (cell.Resource != Resource.None && cell.City < 0)
                {
                    string icon = Words.ResourceIcon(cell.Resource);
                    if (IconTexture(icon) == null)
                        continue;
                    VisualElement anchor = Child(labelLayer, "anchor");
                    VisualElement badge = Child(anchor, "resource-badge");
                    badges[cell.Index] = badge;
                    if (cell.Owner < 0)
                        badge.AddToClassList("resource-wild");
                    SetIcon(Child(badge, "badge-icon"), icon);
                    Add(anchor, CellMiddle(x, z), LabelKind.Resource, cell.Index);
                }
            }
        }

        /// <summary>
        /// Where a badge stands: in the middle of its cell, at the mean height of the cell's rim
        /// (its corners and the middles of its sides), where the grid draws the hex's outline, so
        /// that on the screen the badge sits in the middle of that outline, as the cell's own mark.
        /// The players asked for the badges bigger and in the middle of the hex (2026-11-10); they
        /// stood a little above the things and off the middle, and a badge lifted above its hex
        /// slid over the neighbouring cell behind it, as the camera looks down at a slant. So did
        /// one on the ground at a mountain's or a hill's top, which stands well above its rim.
        /// </summary>
        Vector3 CellMiddle(float x, float z)
        {
            float sum = 0f;
            for (int i = 0; i < 6; i++)
            {
                int k = (i + 1) % 6;
                sum += view.Field.Height(x + HexMetrics.CornerX[i], z + HexMetrics.CornerZ[i]);
                sum += view.Field.Height(x + 0.5f * (HexMetrics.CornerX[i] + HexMetrics.CornerX[k]),
                    z + 0.5f * (HexMetrics.CornerZ[i] + HexMetrics.CornerZ[k]));
            }
            return new Vector3(x, Mathf.Max(sum / 12f, 0f), z);
        }

        /// <summary>Where a town's name plate stands on the screen this frame, with the crown over a capital's.</summary>
        void AddPlate(VisualElement plate, Vector2 p, bool capital)
        {
            float w = plate.resolvedStyle.width, h = plate.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h))
                return;
            float crown = capital ? CrownHeight : 0f;
            plateRects.Add(new Rect(p.x - w / 2, p.y - h - crown, w, h + crown));
        }

        /// <summary>
        /// A hero's shield that would cover a town's name stands beside the plate instead, at
        /// least <see cref="PlateGap"/> from it. The towns' labels come first
        /// (<see cref="BuildLabels"/>), so their plates are known. One that may go
        /// <paramref name="below"/> the plate, half as far from it, does so where that is
        /// nearer: a hero's shield in its town, whose plate stands right over the hero (beside
        /// the capital's long plate it hung a hundred pixels off, over the roads). The map's
        /// badges no longer move aside (<see cref="LateUpdate"/>).
        /// </summary>
        Vector2 BesidePlates(VisualElement badge, Vector2 p, bool below = false)
        {
            float w = badge.resolvedStyle.width, h = badge.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h))
                return p;
            foreach (Rect plate in plateRects)
            {
                if (Mathf.Abs(p.x - plate.center.x) * 2f < plate.width + w + 2f * PlateGap
                    && Mathf.Abs(p.y - plate.center.y) * 2f < plate.height + h + 2f * PlateGap)
                {
                    float side = p.x >= plate.center.x ? plate.xMax + w / 2 + PlateGap : plate.xMin - w / 2 - PlateGap;
                    float under = plate.yMax + h / 2 + PlateGap / 2;
                    if (below && under - p.y < Mathf.Abs(side - p.x))
                        p.y = under;
                    else
                        p.x = side;
                }
            }
            return p;
        }

        /// <summary>
        /// How much a badge is scaled to sit in its cell as the land does: about seven tenths of
        /// the cell's width across (a round badge of <see cref="BadgeSize"/> points; the monsters'
        /// wider pill stays within the cell), however near or far the camera is, but never under
        /// <see cref="BadgeLeast"/> or over <see cref="BadgeMost"/> points. At a fixed size, bigger
        /// badges would have covered the neighbours from afar and looked small up close.
        /// </summary>
        float BadgeScale(VisualElement badge, Vector3 at, Vector2 p)
        {
            Vector2 side = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, at + cam.transform.right * HexMetrics.Sqrt3, cam);
            float cell = Vector2.Distance(p, side);
            float scale = Mathf.Clamp(cell * BadgeShare, BadgeLeast, BadgeMost) / BadgeSize;
            float w = badge.resolvedStyle.width;
            return w > BadgeSize ? Mathf.Min(scale, cell * 0.92f / w) : scale;
        }

        /// <summary>The badges' share of their cell's width, and their least and greatest size in points.</summary>
        const float BadgeShare = 0.7f, BadgeLeast = 30f, BadgeMost = 120f;

        /// <summary>The round badges' size in the style sheet, in points.</summary>
        const float BadgeSize = 50f;

        /// <summary>The least gap between a badge and a town's plate, in points (12 pixels of a 900-pixel screen).</summary>
        const float PlateGap = 14f;

        /// <summary>The crown over a capital's plate, in points.</summary>
        const float CrownHeight = 27f;

        /// <summary>
        /// The top of a label on the panel: a town's plate hangs over its point (with the crown
        /// over a capital's), a badge is centred on it. NaN until the label has been laid out.
        /// </summary>
        static float Top(MapLabel label, Vector2 p, bool town, float scale = 1f)
        {
            float h = label.Anchor.childCount > 0 ? label.Anchor[0].resolvedStyle.height * scale : float.NaN;
            if (!(h > 0f))
                return float.NaN;
            return town ? p.y - h - (label.Kind == LabelKind.Capital ? CrownHeight : 0f) : p.y - h / 2f;
        }

        void Add(VisualElement anchor, Vector3 world, LabelKind kind, int cell, float reach = 0f)
        {
            anchor.pickingMode = PickingMode.Ignore;
            foreach (VisualElement child in anchor.Query<VisualElement>().ToList())
                child.pickingMode = PickingMode.Ignore;
            labels.Add(new MapLabel { Anchor = anchor, World = world, Kind = kind, Cell = cell, Reach = reach });
        }

        void LateUpdate()
        {
            // Under the loading screen the state and the map are half made.
            if (state == null || root?.panel == null || LoadingShown)
                return;
            float distance = mapCamera.Distance;
            float yaw = mapCamera.Yaw * Mathf.Deg2Rad;
            var far = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            // The tasks are lines without a window: a label under them would mix with their text,
            // so it fades there (a plate stands above and to both sides of its point).
            Rect tasks = quests.style.display == DisplayStyle.Flex ? questRows.worldBound : Rect.zero;
            bool fade = tasks.width > 0f && !float.IsNaN(tasks.width);
            // Under them the map's borders and grid dim too, over the lines' box and a little round it.
            Rect panel = root.worldBound;
            // The kingdoms' list ends above the bottom panel; its rows scroll.
            float dockTop = dock.worldBound.yMin, listTop = kingdoms.resolvedStyle.top;
            if (!float.IsNaN(dockTop) && !float.IsNaN(listTop) && dockTop > listTop)
                kingdoms.style.maxHeight = Mathf.Max(160f, dockTop - listTop - 10f);
            Vector4 mask = Vector4.zero;
            if (fade && panel.width > 0f && panel.height > 0f)
                mask = new Vector4((tasks.xMin - 6f) / panel.width, 1f - (tasks.yMax + 6f) / panel.height,
                    (tasks.xMax + 6f) / panel.width, 1f - (tasks.yMin - 6f) / panel.height);
            Shader.SetGlobalVector(HudMaskId, mask);
            if (fade)
                tasks = Rect.MinMaxRect(tasks.xMin - 100f, tasks.yMin - 10f, tasks.xMax + 100f, tasks.yMax + 60f);
            plateRects.Clear();
            float barBottom = topBar != null && !float.IsNaN(topBar.worldBound.yMax) ? topBar.worldBound.yMax : 0f;
            // The badges of resources, places and monster groups hide until Alt is held, and then
            // show over all the seen land, not only near the camera: the players asked for them
            // so (2026-11-10). The towns' names keep showing.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            bool badgesShown = BadgesShown || keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            // With them, the cells the other kingdoms' towns take next (the territory plan of 2026-11-11).
            view?.ShowOtherMarks(badgesShown);
            foreach (MapLabel label in labels)
            {
                bool wanted = state.Seen[label.Cell] && label.Kind switch
                {
                    LabelKind.Capital => true,
                    LabelKind.Town => distance < 60f,
                    _ => badgesShown,
                };
                Vector3 at = label.World + far * label.Reach;
                Vector3 screen = cam.WorldToScreenPoint(at);
                bool onScreen = screen.z > 0 && screen.x > -200 && screen.x < Screen.width + 200 && screen.y > -100 && screen.y < Screen.height + 100;
                if (!wanted || !onScreen)
                {
                    label.Anchor.style.display = DisplayStyle.None;
                    continue;
                }
                label.Anchor.style.display = DisplayStyle.Flex;
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, at, cam);
                bool town = label.Kind == LabelKind.Capital || label.Kind == LabelKind.Town;
                float scale = town ? 1f : BadgeScale(label.Anchor[0], at, p);
                if (!town)
                    label.Anchor.style.scale = new Scale(new Vector3(scale, scale, 1f));
                // A label reaching under the top bar would show only its lower part, and one not
                // laid out yet has no known size: they hide by opacity, which keeps their layout.
                float top = Top(label, p, town, scale);
                bool hidden = float.IsNaN(top) || top < barBottom;
                // A badge keeps to the middle of its cell even where a town's name hangs over that
                // cell: moved aside, it stood over the neighbouring cell. It is drawn over the name
                // (the towns' labels come first in the layer), and only while Alt is held.
                if (town && !hidden)
                    AddPlate(label.Anchor[0], p, label.Kind == LabelKind.Capital);
                label.Anchor.style.translate = new Translate(p.x, p.y);
                label.Anchor.style.opacity = hidden ? 0f : fade && tasks.Contains(p) ? 0.12f : 1f;
            }

            PlaceHeroShields(distance, barBottom, fade ? tasks : Rect.zero);
            UpdateMinimapView();
            PlaceTags();

            float left = toastUntil - ViewClock.Now;
            toast.style.opacity = Mathf.Clamp01(left / 0.4f);
            FadeSaved();
            PulseGuide();
            // Under the top bar, or under the turn report while it shows.
            toast.style.top = report.style.display == DisplayStyle.Flex && !float.IsNaN(report.layout.yMax) ? report.layout.yMax + 8f : 72f;
            if (report.style.display == DisplayStyle.Flex)
            {
                float shown = reportUntil - ViewClock.Now;
                if (shown <= 0)
                    report.style.display = DisplayStyle.None;
                else
                    report.style.opacity = Mathf.Clamp01(shown / 0.5f);
            }
        }

        // ----- Minimap -----

        /// <summary>
        /// The minimap of the seen land (see <see cref="MinimapWindow"/>); the camera is told the
        /// seen land too, so it keeps over it.
        /// </summary>
        void BuildMinimap()
        {
            World world = state.World;
            Rect seenArea = SeenArea();
            mapCamera.SetSeen(seenArea, SeenAt);
            Rect window = MinimapWindow(seenArea);
            const int width = 320;
            int height = Mathf.Max(1, Mathf.RoundToInt(width * window.height / window.width));
            if (minimapTexture == null || minimapTexture.width != width || minimapTexture.height != height)
            {
                if (minimapTexture != null)
                    Destroy(minimapTexture);
                minimapTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Minimap", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
            }
            if (minimapWorld != world || minimapWindow != window || minimapCells == null || minimapCells.Length != width * height)
            {
                minimapWorld = world;
                minimapWindow = window;
                minimapCells = new int[width * height];
                for (int j = 0; j < height; j++)
                for (int i = 0; i < width; i++)
                {
                    float x = Mathf.Lerp(window.xMin, window.xMax, (i + 0.5f) / width);
                    float z = Mathf.Lerp(window.yMin, window.yMax, (j + 0.5f) / height);
                    // Past the world's ragged edges (every other row of cells ends half a cell
                    // further out) the nearest edge cell: drawn as sea, the edge column read as
                    // a dotted line.
                    var hex = HexMetrics.FromWorld(x, z);
                    minimapCells[j * width + i] = world.IndexOf(Mathf.Clamp(hex.Col, 0, world.Width - 1), Mathf.Clamp(hex.Row, 0, world.Height - 1));
                }
            }
            int[] cellOf = minimapCells;
            bool[] seen = state.Seen;
            var owner = new int[width * height];
            for (int k = 0; k < owner.Length; k++)
                owner[k] = cellOf[k] < 0 ? -1 : world.Cells[cellOf[k]].Owner;
            var pixels = new Color32[width * height];
            Color deep = Palette.Hex("#3F6694"), shallow = Palette.Hex("#679EA9"), fog = Palette.Hex("#4A4E55");
            for (int j = 0; j < height; j++)
            for (int i = 0; i < width; i++)
            {
                int k = j * width + i, index = cellOf[k];
                Color c;
                if (index < 0)
                    c = deep;
                else if (!seen[index])
                    c = fog;
                else
                {
                    Cell cell = world.Cells[index];
                    c = cell.Biome == Biome.Ocean ? deep : cell.IsWater ? shallow : Palette.Ground(cell.Biome);
                    int o = owner[k];
                    if (o >= 0)
                    {
                        Color tint = Palette.Countries[o % Palette.Countries.Length];
                        c = Color.Lerp(c, tint, cell.IsWater ? 0.3f : 0.5f);
                        bool edge = false;
                        if (i + 1 < width && owner[k + 1] != o) edge = true;
                        if (i > 0 && owner[k - 1] != o) edge = true;
                        if (j + 1 < height && owner[k + width] != o) edge = true;
                        if (j > 0 && owner[k - width] != o) edge = true;
                        if (edge)
                            c = Color.Lerp(tint, Color.white, 0.25f);
                    }
                }
                pixels[k] = c;
            }
            void Dot(float x, float z, int r, Color color)
            {
                int ci = Mathf.RoundToInt((x - window.xMin) / window.width * width);
                int cj = Mathf.RoundToInt((z - window.yMin) / window.height * height);
                for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                {
                    int i = ci + di, j = cj + dj;
                    if (i >= 0 && j >= 0 && i < width && j < height && di * di + dj * dj <= r * r + 1)
                        pixels[j * width + i] = color;
                }
            }
            foreach (Cell cell in world.Cells)
            {
                if (cell.Monster != Monster.None && seen[cell.Index])
                    Dot(world.CenterX(cell.Index), world.CenterZ(cell.Index), 2, Palette.Hex("#2A2522"));
            }
            foreach (City city in world.Cities)
            {
                if (seen[city.Cell])
                    Dot(world.CenterX(city.Cell), world.CenterZ(city.Cell), city.Capital ? 3 : 2, Color.white);
            }
            foreach (Hero h in state.Heroes)
            {
                Dot(world.CenterX(h.Cell), world.CenterZ(h.Cell), 4, Palette.Hex("#1C1A18"));
                Dot(world.CenterX(h.Cell), world.CenterZ(h.Cell), 3, Palette.Hex("#E7C86A"));
            }
            minimapTexture.SetPixels32(pixels);
            minimapTexture.Apply(false);
            minimap.style.backgroundImage = new StyleBackground(minimapTexture);
            // As large as the panel's corner holds.
            float fit = Mathf.Min(MinimapBox.x / width, MinimapBox.y / height);
            minimapSize = new Vector2(Mathf.Round(width * fit), Mathf.Round(height * fit));
            minimap.style.width = minimapSize.x;
            minimap.style.height = minimapSize.y;
        }

        /// <summary>Whether the players have seen the cell under a point of the map (x, z).</summary>
        bool SeenAt(Vector2 p)
        {
            int i = view != null ? view.CellAt(new Vector3(p.x, 0f, p.y)) : -1;
            return i >= 0 && i < state.Seen.Length && state.Seen[i];
        }

        /// <summary>The box (x, z) round the cells the players have seen, or an empty one.</summary>
        Rect SeenArea()
        {
            World world = state.World;
            bool[] seen = state.Seen;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int i = 0; i < seen.Length; i++)
            {
                if (!seen[i])
                    continue;
                float x = world.CenterX(i), z = world.CenterZ(i);
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minZ = Mathf.Min(minZ, z);
                maxZ = Mathf.Max(maxZ, z);
            }
            if (minX > maxX)
                return default;
            return Rect.MinMaxRect(minX - HexMetrics.InnerRadius, minZ - HexMetrics.Radius, maxX + HexMetrics.InnerRadius, maxZ + HexMetrics.Radius);
        }

        /// <summary>
        /// The part of the world the minimap shows: the seen land with a tenth of its size round
        /// it, widened to the minimap's shape and to at least a fifth of the world's width, inside
        /// the world; all of it before anything is seen. It grows as the players explore. (With a
        /// margin of a few cells and at least 0.4 of the world's width, the land at the start
        /// filled only a third of the minimap.)
        /// </summary>
        Rect MinimapWindow(Rect seenArea)
        {
            World world = state.World;
            Rect all = Rect.MinMaxRect(world.MinX, world.MinZ, world.MaxX, world.MaxZ);
            if (seenArea.width <= 0f)
                return all;
            float margin = 0.1f * Mathf.Max(seenArea.width, seenArea.height);
            float aspect = MinimapBox.x / MinimapBox.y;
            float w = Mathf.Max(seenArea.width + 2f * margin, all.width * 0.2f), h = seenArea.height + 2f * margin;
            if (w / h < aspect)
                w = h * aspect;
            else
                h = w / aspect;
            w = Mathf.Min(w, all.width);
            h = Mathf.Min(h, all.height);
            Vector2 middle = seenArea.center;
            float x = Mathf.Clamp(middle.x - w * 0.5f, all.xMin, all.xMax - w);
            float z = Mathf.Clamp(middle.y - h * 0.5f, all.yMin, all.yMax - h);
            return new Rect(x, z, w, h);
        }

        /// <summary>The part of the map the camera sees, as a four-cornered outline on the minimap.</summary>
        void UpdateMinimapView()
        {
            Rect window = minimapWindow;
            float yaw = mapCamera.Yaw;
            compass.style.rotate = new Rotate(new Angle(-yaw, AngleUnit.Degree));
            compass.EnableInClassList("compass-off", Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) < 1f);
            viewShown = mapCamera.ViewCorners(corners);
            if (viewShown)
            {
                float w = minimapSize.x, h = minimapSize.y;
                for (int i = 0; i < 4; i++)
                    viewQuad[i] = new Vector2((corners[i].x - window.xMin) / window.width * w,
                        (1f - (corners[i].z - window.yMin) / window.height) * h);
            }
            minimapView.MarkDirtyRepaint();
        }

        void DrawMinimapView(MeshGenerationContext context)
        {
            if (!viewShown)
                return;
            Painter2D painter = context.painter2D;
            painter.BeginPath();
            painter.MoveTo(viewQuad[0]);
            for (int i = 1; i < 4; i++)
                painter.LineTo(viewQuad[i]);
            painter.ClosePath();
            painter.fillColor = new Color(0.91f, 0.88f, 0.81f, 0.1f);
            painter.Fill();
            painter.strokeColor = new Color(0.91f, 0.88f, 0.81f);
            painter.lineWidth = 2f;
            painter.lineJoin = LineJoin.Round;
            painter.Stroke();
        }

        void MinimapAt(Vector2 local)
        {
            if (state == null || minimapTexture == null)
                return;
            Rect window = minimapWindow;
            float x = Mathf.Lerp(window.xMin, window.xMax, local.x / minimapSize.x);
            float z = Mathf.Lerp(window.yMax, window.yMin, local.y / minimapSize.y);
            MinimapPoint?.Invoke(new Vector2(x, z));
        }

        // ----- Helpers -----

        /// <summary>An icon of the art by name, or a kingdom's shield with its flag (<see cref="FlagIcon"/>).</summary>
        Texture2D IconTexture(string name)
        {
            if (name == null)
                return null;
            if (name.StartsWith(FlagPrefix, StringComparison.Ordinal) && int.TryParse(name.Substring(FlagPrefix.Length), out int country))
            {
                Texture2D shield = FlagArt.ShieldOf(country);
                if (shield != null)
                    return shield;
                name = "shield_" + Palette.CountryKeys[country % Palette.CountryKeys.Length];
            }
            return art != null ? art.IconOf(name) : null;
        }

        const string FlagPrefix = "flag:";

        /// <summary>The name of a kingdom's shield bearing its flag, for <see cref="SetIcon"/>.</summary>
        static string FlagIcon(int country) => FlagPrefix + country;

        void SetIcon(VisualElement element, string name)
        {
            Texture2D texture = IconTexture(name);
            element.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
            element.style.display = texture != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>The gilded frame over a portrait, its painted corners drawn at 0.4 of their size.</summary>
        static void PortraitFrame(VisualElement portrait)
        {
            VisualElement frame = Child(portrait, "portrait-frame");
            frame.pickingMode = PickingMode.Ignore;
            frame.style.unitySliceScale = 0.4f;
        }

        /// <summary>The card's painted frame, behind everything else in it.</summary>
        static void Frame(VisualElement card)
        {
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("card-frame");
            card.Insert(0, frame);
        }

        static VisualElement Child(VisualElement parent, string cls)
        {
            var element = new VisualElement();
            element.AddToClassList(cls);
            parent.Add(element);
            return element;
        }

        /// <summary>A heading and its "key — what it does" rows in the help window.</summary>
        void HelpGroup(VisualElement column, string title, string[,] rows)
        {
            Text(column, "help-group", title, black);
            for (int r = 0; r < rows.GetLength(0); r++)
            {
                VisualElement row = Child(column, "help-row");
                Text(row, "help-key", rows[r, 0], black);
                Text(row, "help-text", rows[r, 1], regular);
            }
        }

        static Label Text(VisualElement parent, string cls, string text, Font font)
        {
            var label = new Label(text);
            label.AddToClassList(cls);
            UseFont(label, font);
            parent.Add(label);
            return label;
        }

        Button Btn(VisualElement parent, string text, string cls, Action clicked)
        {
            Button button = PressButton(clicked);
            button.AddToClassList(cls);
            button.focusable = false;
            Text(button, "button-caption", text, black).pickingMode = PickingMode.Ignore;
            parent.Add(button);
            return button;
        }

        static void UseFont(VisualElement element, Font font)
        {
            if (font != null)
                element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
        }

        void OnDestroy()
        {
            if (minimapTexture != null)
                Destroy(minimapTexture);
        }
    }
}
