using System.Collections.Generic;
using System.Linq;
using Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// The new day's events: lines over the map along the right edge, without a window, as the tasks
    /// are along the left, under the «События» button, which opens the last turns' events in a
    /// dialog (also C). The players asked for them so on 2026-11-12, instead of the report window in
    /// the middle of the screen, which covered the map and faded after a few seconds. The lines stay
    /// until the next turn; the most important come first, and those that do not fit wait in the
    /// dialog.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>How many turns the dialog keeps (the save keeps the last one).</summary>
        const int LoggedTurns = 12;

        /// <summary>The most lines along the right edge.</summary>
        const int EdgeLines = 6;

        /// <summary>One turn's events as the players saw them: the turn they were shown on and the report.</summary>
        sealed class Day
        {
            public int Turn;
            public TurnReport Report;
        }

        /// <summary>A line of the events: along the edge and in the dialog alike.</summary>
        struct EventLine
        {
            public string Icon, Text;
            public bool Big, Warn;
            public int City;
            public Stock Reward, Cost;

            /// <summary>The order along the edge: warnings first, then news, the treasury, the land and the rest.</summary>
            public int Rank;
        }

        VisualElement events, eventRows;
        readonly List<Day> days = new List<Day>();

        void BuildEvents()
        {
            events = Child(root, "events");
            events.pickingMode = PickingMode.Ignore;
            Button button = Btn(events, Words.EventsButton, "button", ShowEvents);
            button.AddToClassList("events-button");
            Text(button, "key-hint-dark", "C", bold).pickingMode = PickingMode.Ignore;
            eventRows = Child(events, "events-rows");
            eventRows.pickingMode = PickingMode.Ignore;
        }

        /// <summary>A new game or a loaded one: the last game's events go.</summary>
        void ForgetEvents()
        {
            days.Clear();
            eventRows?.Clear();
        }

        /// <summary>
        /// The turn's events along the right edge, kept for the dialog. The season, the harvest,
        /// the food the towns took, the land, and what happened to the towns, the tasks, the
        /// crown and the monsters.
        /// </summary>
        /// <param name="title">A line over them: «Ход N. В прошлый ход:» on returning to a game.</param>
        public void ShowReport(TurnReport turn, string title = null)
        {
            if (turn == null)
                return;
            days.RemoveAll(d => d.Turn == state.Turn);
            days.Add(new Day { Turn = state.Turn, Report = turn });
            if (days.Count > LoggedTurns)
                days.RemoveAt(0);

            eventRows.Clear();
            if (title != null)
            {
                VisualElement head = Child(eventRows, "event");
                Halo(head, "event-title", title, black);
                Still(head);
            }
            List<EventLine> lines = Lines(turn);
            int shown = lines.Count > EdgeLines ? EdgeLines - 1 : lines.Count;
            for (int k = 0; k < shown; k++)
                EdgeRow(lines[k]);
            if (shown < lines.Count)
            {
                VisualElement more = Child(eventRows, "event");
                Halo(more, "event-more", Words.EventsMore(lines.Count - shown), bold);
                Link(more, ShowEvents);
            }
        }

        /// <summary>The last turns' events in a dialog, the newest first; a town's line takes the camera there.</summary>
        public void ShowEvents()
        {
            if (state == null || DialogOpen)
                return;
            Ask(Words.EventsTitle, null, days.Count == 0 ? Words.EventsNone : null, new[] { Words.Close }, _ => { }, body =>
            {
                if (days.Count == 0)
                    return;
                var scroll = new ScrollView(ScrollViewMode.Vertical)
                {
                    horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                    verticalScrollerVisibility = ScrollerVisibility.Auto,
                };
                scroll.AddToClassList("events-log");
                body.Add(scroll);
                for (int i = days.Count - 1; i >= 0; i--)
                {
                    Text(scroll.contentContainer, "events-day", Words.EventsDay(days[i].Turn), black);
                    foreach (EventLine line in Lines(days[i].Report))
                        LogRow(scroll.contentContainer, line);
                }
            }, also: Key.C);
        }

        /// <summary>The report's lines, the most important first (a stable order: the core's within a rank).</summary>
        List<EventLine> Lines(TurnReport turn)
        {
            World world = state.World;
            var lines = new List<EventLine>
            {
                new EventLine { Icon = "coins", Text = Words.Treasury, Reward = turn.Income, City = -1, Rank = 2 },
            };
            if (turn.ArmyFood > 0)
                lines.Add(new EventLine { Icon = "swords", Text = Words.ArmyFood, Cost = new Stock(0, turn.ArmyFood, 0, 0), City = -1, Rank = 5 });
            if (turn.GrowthFood > 0)
                lines.Add(new EventLine { Icon = "wheat", Text = Words.GrowthFood, Cost = new Stock(0, turn.GrowthFood, 0, 0), City = -1, Rank = 5 });
            BorderLines(turn, lines);
            foreach (TurnEvent e in turn.Events)
            {
                if (e.Kind == TurnEventKind.Border)
                    continue;
                string icon = e.Kind == TurnEventKind.QuestDone ? "coins" : e.Kind == TurnEventKind.LevelUp ? "crown"
                    : e.Kind == TurnEventKind.GemSet ? Words.GemIcon(e.Gem)
                    : e.Kind == TurnEventKind.StakeReached || e.Kind == TurnEventKind.StakeLost ? FlagIcon(world.Player)
                    : e.Kind == TurnEventKind.RaidSent || e.Kind == TurnEventKind.RaidArrived || e.Kind == TurnEventKind.RaidLeft ? "swords"
                    : e.Kind == TurnEventKind.RaidRepelled ? "coins"
                    : e.Kind == TurnEventKind.TroopSent || e.Kind == TurnEventKind.TroopCleared || e.Kind == TurnEventKind.TroopRecalled ? FlagIcon(e.Country)
                    : e.Kind == TurnEventKind.ArmyHungry ? "wheat"
                    : e.Kind == TurnEventKind.Grew ? "hammer" : e.Stop == GrowthStop.Food ? "wheat" : "hammer";
                bool mine = e.Kind != TurnEventKind.GemSet || e.Country == world.Player;
                bool warn = e.Kind == TurnEventKind.Stopped || e.Kind == TurnEventKind.LandFull || e.Kind == TurnEventKind.StakeLost
                    || e.Kind == TurnEventKind.RaidSent || e.Kind == TurnEventKind.RaidArrived || e.Kind == TurnEventKind.TroopSent
                    || e.Kind == TurnEventKind.ArmyHungry;
                bool news = e.Kind == TurnEventKind.LevelUp || e.Kind == TurnEventKind.QuestDone || e.Kind == TurnEventKind.StakeReached
                    || e.Kind == TurnEventKind.RaidRepelled || (e.Kind == TurnEventKind.GemSet && mine);
                lines.Add(new EventLine
                {
                    Icon = icon,
                    Text = Words.Event(e, world),
                    Big = e.Kind == TurnEventKind.LevelUp || (e.Kind == TurnEventKind.GemSet && mine),
                    Warn = warn,
                    City = e.City,
                    Rank = warn ? 0 : news ? 1 : 4,
                });
            }
            return lines.Select((line, at) => (line, at)).OrderBy(p => p.line.Rank).ThenBy(p => p.at).Select(p => p.line).ToList();
        }

        /// <summary>
        /// The cells the players' towns took this turn, one line for all, and a line for each
        /// resource that came inside the border (the territory plan of 2026-11-11).
        /// </summary>
        void BorderLines(TurnReport turn, List<EventLine> lines)
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
            lines.Add(new EventLine { Icon = FlagIcon(world.Player), Text = Words.BorderGrew(cells, towns), City = first, Rank = 3 });
            foreach (int cell in resources)
            {
                Resource resource = world.Cells[cell].Resource;
                lines.Add(new EventLine { Icon = Words.ResourceIcon(resource), Text = Words.ResourceInside(resource), City = -1, Rank = 3 });
            }
        }

        /// <summary>A line along the edge: the words and their chips, then the icon at the screen's edge.</summary>
        void EdgeRow(EventLine line)
        {
            VisualElement row = Child(eventRows, "event");
            VisualElement body = Child(row, "event-body");
            Label text = Halo(body, "event-text", line.Text, line.Big ? black : bold);
            if (line.Warn)
                text.AddToClassList("event-warn");
            Reward(body, line.Reward, halo: true);
            Reward(body, line.Cost, "cost", false, halo: true);
            SetIcon(Child(row, "event-icon"), IconTexture(line.Icon) != null ? line.Icon : "crown");
            if (line.City >= 0)
            {
                int city = line.City;
                Link(row, () => CityPicked?.Invoke(city));
            }
            else
                Still(row);
        }

        /// <summary>A line in the dialog: the icon, the words and their chips, as the report window had them.</summary>
        void LogRow(VisualElement parent, EventLine line)
        {
            VisualElement row = Child(parent, "log-row");
            SetIcon(Child(row, "log-icon"), IconTexture(line.Icon) != null ? line.Icon : "crown");
            Label text = Text(row, "log-text", line.Text, line.Big ? black : regular);
            if (line.Warn)
                text.AddToClassList("log-warn");
            Reward(row, line.Reward);
            Reward(row, line.Cost, "cost", false);
            foreach (VisualElement child in row.Query<VisualElement>().ToList())
            {
                if (child != row)
                    child.pickingMode = PickingMode.Ignore;
            }
            if (line.City >= 0)
            {
                int city = line.City;
                row.AddToClassList("log-link");
                OnPress(row, () =>
                {
                    AnswerModal(0);
                    CityPicked?.Invoke(city);
                });
            }
        }

        /// <summary>A line that answers a click: it alone takes the pointer, the map keeps the rest.</summary>
        void Link(VisualElement row, System.Action pressed)
        {
            row.AddToClassList("event-link");
            foreach (VisualElement child in row.Query<VisualElement>().ToList())
            {
                if (child != row)
                    child.pickingMode = PickingMode.Ignore;
            }
            OnPress(row, pressed);
        }

        /// <summary>A line the map's clicks go through.</summary>
        static void Still(VisualElement row)
        {
            foreach (VisualElement child in row.Query<VisualElement>().ToList())
                child.pickingMode = PickingMode.Ignore;
        }

        /// <summary>The lines' box while they show over the map (none while the kingdoms' list stands there), else empty.</summary>
        Rect EventsBox()
        {
            if (events == null || events.style.display == DisplayStyle.None || kingdoms.style.display == DisplayStyle.Flex)
                return Rect.zero;
            Rect box = events.worldBound;
            return float.IsNaN(box.width) ? Rect.zero : box;
        }
    }
}
