using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The heroes' part of the interface: the kingdom's heroes and the chosen one's card in the
    /// bottom panel (portrait, level, attack and defence, moves, army), the dialogs, and the tags
    /// and floating numbers that follow points in the world.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>A hero in the list was clicked; the level-up button on the card was clicked.</summary>
        public event Action<int> HeroPicked;
        public event Action LevelUpPicked;

        /// <summary>An exchange button on the card: with the fellow hero of that index, or -1 for the town.</summary>
        public event Action<int> SwapPicked;

        /// <summary>The chosen hero's settlers are to found a town where he stands.</summary>
        public event Action FoundPicked;

        /// <summary>Unit and hero pictures, rendered once by the game.</summary>
        public Portraits portraits;

        struct WorldTag
        {
            public VisualElement Anchor;
            public Label Label;
            public Func<Vector3> Where;
        }

        struct FloatText
        {
            public VisualElement Anchor;
            public Label Label;
            public Vector3 World;
            public float Start, Seconds;
        }

        struct Question
        {
            public string Title, Icon, Text;
            public string[] Buttons;
            public Action<int> Chosen;
            public Action<VisualElement> Body;
            public int Back;
        }

        VisualElement heroRows, heroCard, heroPortrait, heroXpFill, heroArmy, heroSwaps, heroLevelUp, heroMovesFill, heroDetails;
        Label heroEmpty, heroLevel, heroAttack, heroDefence, heroMoves, heroNote;
        VisualElement tagLayer, modalOverlay, modalCard, modalIcon, modalBody, modalButtons;
        Label modalTitle, modalText;
        readonly List<WorldTag> tags = new List<WorldTag>();
        readonly List<FloatText> floats = new List<FloatText>();
        readonly Queue<Question> questions = new Queue<Question>();
        Action<int> answer;
        int back, modalOpenedFrame = -10, modalClosedFrame = -10;

        Hero hero;
        HeroPath path;
        int oddsKey;
        Odds oddsCache;

        /// <summary>A dialog is open, or closed this frame or the last (so its click or key does not reach the map).</summary>
        public bool Modal => modalOverlay != null && (modalOverlay.style.display == DisplayStyle.Flex || Time.frameCount - modalClosedFrame <= 1);

        /// <summary>A dialog is on the screen now.</summary>
        public bool DialogOpen => modalOverlay != null && modalOverlay.style.display == DisplayStyle.Flex;

        /// <summary>
        /// The hero's part of the bottom panel: the kingdom's heroes as tabs with their names and
        /// moves left, and under them the chosen one's portrait, level, attack, defence, moves and
        /// army with the buttons for his screen, exchanges and settlers.
        /// </summary>
        void BuildHeroCard(VisualElement dock)
        {
            heroCard = Child(dock, "hero-card");
            heroPortrait = Child(heroCard, "hero-portrait");
            PortraitFrame(heroPortrait);
            heroPortrait.tooltip = T("Экран героя (R)", "Hero screen (R)");
            OnPress(heroPortrait, () => HeroScreenPicked?.Invoke());
            VisualElement body = Child(heroCard, "hero-body");
            heroRows = Child(body, "hero-tabs");
            heroEmpty = Text(body, "dock-empty", "", regular);
            heroDetails = Child(body, "hero-details");
            body = Child(heroDetails, "hero-main");
            heroSwaps = Child(heroDetails, "hero-swaps");
            VisualElement top = Child(body, "hero-top");
            heroLevel = Text(top, "hero-level", "", bold);
            heroLevelUp = Child(top, "hero-levelup");
            Text(heroLevelUp, "hero-levelup-text", T("Новый уровень!", "New level!"), black).pickingMode = PickingMode.Ignore;
            OnPress(heroLevelUp, () => LevelUpPicked?.Invoke());
            VisualElement xp = Child(body, "hero-xp");
            heroXpFill = Child(xp, "hero-xp-fill");
            VisualElement stats = Child(body, "hero-stats");
            VisualElement attack = Child(stats, "hero-stat");
            attack.tooltip = T("Атака героя: его войско бьёт сильнее", "Hero's attack: their army strikes harder");
            SetIcon(Child(attack, "hero-stat-icon"), "swords");
            heroAttack = Text(attack, "hero-stat-value", "", black);
            VisualElement defence = Child(stats, "hero-stat");
            defence.tooltip = T("Защита героя: его войско получает меньше урона", "Hero's defence: their army takes less damage");
            SetIcon(Child(defence, "hero-stat-icon"), "shield_blue");
            heroDefence = Text(defence, "hero-stat-value", "", black);
            VisualElement moves = Child(stats, "hero-moves");
            moves.tooltip = T("Сколько герой ещё пройдёт за этот ход: дорога 1, поле 2, лес и холмы 3, горы 4", "How far the hero can still go this turn: road 1, field 2, forest and hills 3, mountains 4");
            Text(moves, "hero-moves-key", T("Ходы", "Moves"), bold);
            VisualElement track = Child(moves, "hero-moves-bar");
            heroMovesFill = Child(track, "hero-moves-fill");
            heroMoves = Text(moves, "hero-moves-value", "", bold);
            VisualElement armyRow = Child(body, "hero-army-row");
            heroArmy = Child(armyRow, "hero-army");
            heroNote = Text(body, "hero-note", "", regular);
        }

        void BuildHeroes()
        {
            modalOverlay = Child(root, "modal-overlay");
            VisualElement card = Child(modalOverlay, "modal-card");
            modalCard = card;
            Frame(card);
            VisualElement modalHead = Child(card, "modal-head");
            modalIcon = Child(modalHead, "modal-icon");
            modalTitle = Text(modalHead, "modal-title", "", black);
            modalText = Text(card, "modal-text", "", regular);
            modalBody = Child(card, "modal-body");
            modalButtons = Child(card, "modal-buttons");
            modalOverlay.style.display = DisplayStyle.None;
        }

        /// <summary>The chosen hero (or none) and the way it is to take; the list and the card follow.</summary>
        public void ShowHero(Hero chosen, HeroPath way)
        {
            hero = chosen;
            path = way;
            BuildHeroRows();
            FillHeroCard();
            if (screenHero != null && chosen != null)
            {
                screenHero = chosen;
                FillHeroScreen();
            }
        }

        /// <summary>A tab a hero of the kingdom: the name, a star for a new level, and a bar of the moves left.</summary>
        void BuildHeroRows()
        {
            heroRows.Clear();
            List<Hero> mine = state.HeroesOf(state.World.Player);
            heroRows.style.display = mine.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (Hero h in mine)
            {
                VisualElement tab = Child(heroRows, "hero-tab");
                if (h == hero)
                    tab.AddToClassList("hero-tab-on");
                tab.tooltip = T($"{h.Label}, уровень {h.Level}: ходов {h.Moves} из {HeroRules.MovesPerTurn} (F — следующий)", $"{h.Label}, level {h.Level}: {h.Moves} of {HeroRules.MovesPerTurn} moves (F — next)");
                int picked = h.Index;
                OnPress(tab, () => HeroPicked?.Invoke(picked));
                VisualElement line = Child(tab, "hero-tab-line");
                Text(line, "hero-tab-name", h.Label, black);
                if (h.LevelUps > 0)
                    Child(line, "hero-row-star");
                VisualElement track = Child(tab, "hero-tab-bar");
                VisualElement fill = Child(track, "hero-tab-fill");
                fill.style.width = Length.Percent(100f * Mathf.Clamp01((float)h.Moves / HeroRules.MovesPerTurn));
                foreach (VisualElement child in tab.Query<VisualElement>().ToList())
                {
                    if (child != tab)
                        child.pickingMode = PickingMode.Ignore;
                }
            }
        }

        void FillHeroCard()
        {
            bool shown = hero != null;
            heroDetails.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            heroEmpty.style.display = shown ? DisplayStyle.None : DisplayStyle.Flex;
            // An empty frame only took room from the hint (review 2026-11-10).
            heroPortrait.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown)
            {
                heroPortrait.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                heroPortrait.style.borderBottomColor = new StyleColor(StyleKeyword.Null);
                heroEmpty.text = state != null && state.HeroesOf(state.World.Player).Count > 0
                    ? T("Выбери героя: щёлкни по имени или по нему на карте, или нажми F.", "Select a hero: click their name or them on the map, or press F.")
                    : T("Героев нет: найми героя в городе с таверной.", "No heroes: hire a hero in a town with a tavern.");
                return;
            }
            if (portraits?.Hero != null)
                heroPortrait.style.backgroundImage = new StyleBackground(Background.FromTexture2D(portraits.Hero));
            heroPortrait.style.borderBottomColor = Palette.Countries[hero.Country % Palette.Countries.Length];
            int from = HeroRules.NextLevel(hero.Level - 1), to = HeroRules.NextLevel(hero.Level);
            // The tabs name the kingdom's own heroes; another kingdom's is named here.
            heroLevel.text = hero.Country == state.World.Player
                ? T($"Уровень {hero.Level} · опыт {hero.Experience} из {to}", $"Level {hero.Level} · experience {hero.Experience} of {to}")
                : T($"{hero.Label}, {state.World.Countries[hero.Country].Label} · уровень {hero.Level}", $"{hero.Label}, {state.World.Countries[hero.Country].Label} · level {hero.Level}");
            heroXpFill.style.width = Length.Percent(100f * Mathf.Clamp01((float)(hero.Experience - from) / Mathf.Max(1, to - from)));
            heroLevelUp.style.display = hero.LevelUps > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            heroAttack.text = hero.Attack.ToString();
            heroDefence.text = hero.Defence.ToString();
            heroMoves.text = T($"{hero.Moves} из {HeroRules.MovesPerTurn}", $"{hero.Moves} of {HeroRules.MovesPerTurn}");
            heroMovesFill.style.width = Length.Percent(100f * Mathf.Clamp01((float)hero.Moves / HeroRules.MovesPerTurn));
            heroArmy.Clear();
            for (int k = 0; k < Armies.MaxStacks; k++)
            {
                VisualElement slot = Child(heroArmy, "army-slot");
                if (k >= hero.Army.Count)
                {
                    slot.AddToClassList("army-slot-empty");
                    continue;
                }
                Stack stack = hero.Army[k];
                slot.tooltip = Words.Count(stack.Unit, stack.Count);
                Texture2D picture = portraits?.Of(stack.Unit);
                if (picture != null)
                    slot.style.backgroundImage = new StyleBackground(Background.FromTexture2D(picture));
                Text(slot, "army-count", stack.Count.ToString(), black).pickingMode = PickingMode.Ignore;
            }
            heroSwaps.Clear();
            if (hero.Country == state.World.Player)
            {
                Button open = Btn(heroSwaps, T("Герой", "Hero"), "swap-button", () => HeroScreenPicked?.Invoke());
                open.tooltip = T("Экран героя: сила, войско, новый уровень", "Hero screen: strength, army, new level");
                Text(open, "key-hint-dark", "R", bold).pickingMode = PickingMode.Ignore;
                if (hero.Settlers)
                    FoundButton(heroSwaps, hero);
                foreach (Hero other in state.Neighbours(hero))
                {
                    int with = other.Index;
                    Btn(heroSwaps, T($"Обмен: {other.Label}", $"Trade: {other.Label}"), "swap-button", () => SwapPicked?.Invoke(with)).tooltip =
                        T("Передать отряды друг другу", "Pass troops to each other");
                }
                if (state.TownOf(hero) != null)
                {
                    Btn(heroSwaps, T("Обмен: город", "Trade: town"), "swap-button", () => SwapPicked?.Invoke(-1)).tooltip =
                        T("Оставить отряды в городе другому герою или забрать оставленные", "Leave troops in town for another hero or take the ones left there");
                }
            }
            // A warning in red; else what the right button does now.
            string note = hero.Army.Count == 0 ? T("Без войска в бой не пойти: найми отряды в городе.", "No army, no battle: recruit troops in a town.")
                : hero.Moves == 0 ? T("Ходы кончились: герой пойдёт дальше в следующий ход.", "Out of moves: the hero will go on next turn.") : null;
            string tip = path != null ? T("Ещё раз правой кнопкой (или G) — идти", "Right-click again (or G) — go")
                : hero.Settlers ? T($"С героем обоз поселенцев: город ставят не ближе {Settling.Spacing} шагов от других", $"The hero leads settlers: a town must be at least {Settling.Spacing} steps from the others")
                : T("Куда идти — щёлкни правой кнопкой по карте", "Where to go — right-click on the map");
            heroNote.text = note ?? tip;
            heroNote.EnableInClassList("hero-tip", note == null);
        }

        /// <summary>The found-a-town button: bright where a town may stand, else dim, its click saying why not.</summary>
        void FoundButton(VisualElement parent, Hero h)
        {
            Refusal can = state.CanFound(h);
            Button found = Btn(parent, T("Основать город", "Found a town"), "swap-button", () => FoundPicked?.Invoke());
            found.AddToClassList(can == Refusal.None ? "found-button" : "found-button-off");
            found.tooltip = can == Refusal.None ? T("Поселенцы построят здесь деревню", "The settlers will build a village here") : Words.Refusal(can);
        }

        // ----- Exchanging units -----

        /// <summary>
        /// Two armies side by side, heroes meeting or a hero and his town's garrison: a row a kind
        /// of unit with its count on either side and buttons that pass all, one or half of them
        /// across. The armies change at once; <paramref name="done"/> runs when the dialog closes.
        /// </summary>
        public void Exchange(string leftName, List<Stack> left, string rightName, List<Stack> right, Action done)
        {
            Ask(T("Обмен войсками", "Trading troops"), "swords", T("Стрелки передают отряды: всех, по одному или поровну.", "The arrows pass troops: all, one at a time or evenly."), new[] { T("Готово", "Done") },
                _ => done?.Invoke(), body => FillExchange(body, leftName, left, rightName, right));
        }

        void FillExchange(VisualElement body, string leftName, List<Stack> left, string rightName, List<Stack> right)
        {
            body.Clear();
            VisualElement head = Child(body, "swap-row");
            Text(head, "swap-name", leftName, black);
            Child(head, "swap-arrows");
            Text(head, "swap-name", rightName, black).AddToClassList("swap-name-right");
            var kinds = new List<Unit>();
            foreach (List<Stack> army in new[] { left, right })
            {
                foreach (Stack s in army)
                {
                    if (!kinds.Contains(s.Unit))
                        kinds.Add(s.Unit);
                }
            }
            kinds.Sort();
            void Again() => FillExchange(body, leftName, left, rightName, right);
            foreach (Unit unit in kinds)
            {
                int l = Armies.Count(left, unit), r = Armies.Count(right, unit), even = (l + r + 1) / 2;
                Unit kind = unit;
                VisualElement row = Child(body, "swap-row");
                SwapSide(row, kind, l);
                VisualElement arrows = Child(row, "swap-arrows");
                SwapArrow(arrows, "«", T("все налево", "all to the left"), r > 0 && Armies.CanAdd(left, kind), () => Armies.Move(right, left, kind, r), Again);
                SwapArrow(arrows, "‹", T("один налево", "one to the left"), r > 0 && Armies.CanAdd(left, kind), () => Armies.Move(right, left, kind, 1), Again);
                SwapArrow(arrows, "½", T("поровну", "evenly"), l != even, () =>
                {
                    if (l > even)
                        Armies.Move(left, right, kind, l - even);
                    else
                        Armies.Move(right, left, kind, even - l);
                }, Again);
                SwapArrow(arrows, "›", T("один направо", "one to the right"), l > 0 && Armies.CanAdd(right, kind), () => Armies.Move(left, right, kind, 1), Again);
                SwapArrow(arrows, "»", T("все направо", "all to the right"), l > 0 && Armies.CanAdd(right, kind), () => Armies.Move(left, right, kind, l), Again);
                SwapSide(row, kind, r).AddToClassList("swap-side-right");
            }
            if (left.Count == 0 || right.Count == 0)
            {
                string empty = left.Count == 0 ? leftName : rightName;
                Text(body, "swap-warn", T($"{empty}: без войска. Героя без войска чудовища не пропустят.", $"{empty}: no army. Monsters won't let a hero without an army pass."), regular);
            }
        }

        VisualElement SwapSide(VisualElement row, Unit unit, int count)
        {
            VisualElement side = Child(row, "swap-side");
            VisualElement slot = Child(side, "army-slot");
            if (count == 0)
                slot.AddToClassList("army-slot-empty");
            Texture2D picture = portraits?.Of(unit);
            if (picture != null)
                slot.style.backgroundImage = new StyleBackground(Background.FromTexture2D(picture));
            slot.style.opacity = count == 0 ? 0.35f : 1f;
            Text(slot, "army-count", count.ToString(), black);
            Text(side, "swap-unit", Words.Unit(unit), bold);
            return side;
        }

        void SwapArrow(VisualElement parent, string text, string tip, bool enabled, Action move, Action again)
        {
            Button button = Btn(parent, text, "swap-arrow", () =>
            {
                move();
                again();
            });
            button.tooltip = tip;
            button.SetEnabled(enabled);
        }

        // ----- Dialogs -----

        /// <summary>
        /// A dialog in the middle of the screen: a title, a line of text, an optional body and
        /// buttons; the first button is the main one (Enter), the last one, or the
        /// <paramref name="back"/> one, the way back (Esc). Dialogs asked while one is open wait
        /// their turn.
        /// </summary>
        public void Ask(string title, string icon, string text, string[] buttons, Action<int> chosen, Action<VisualElement> body = null, int back = -1)
        {
            questions.Enqueue(new Question { Title = title, Icon = icon, Text = text, Buttons = buttons, Chosen = chosen, Body = body, Back = back });
            if (modalOverlay.style.display != DisplayStyle.Flex)
                NextQuestion();
        }

        void NextQuestion()
        {
            if (questions.Count == 0)
            {
                modalOverlay.style.display = DisplayStyle.None;
                return;
            }
            Question q = questions.Dequeue();
            modalTitle.text = q.Title;
            modalIcon.style.display = DisplayStyle.None;
            if (q.Icon != null)
                SetIcon(modalIcon, q.Icon);
            modalText.text = q.Text ?? "";
            modalText.style.display = string.IsNullOrEmpty(q.Text) ? DisplayStyle.None : DisplayStyle.Flex;
            modalBody.Clear();
            modalCard.RemoveFromClassList("modal-wide");
            q.Body?.Invoke(modalBody);
            modalBody.style.display = modalBody.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            modalButtons.Clear();
            answer = q.Chosen;
            back = q.Back >= 0 ? q.Back : q.Buttons.Length - 1;
            // The key that opened it (Enter for the next turn) must not answer it in the same frame.
            modalOpenedFrame = Time.frameCount;
            for (int k = 0; k < q.Buttons.Length; k++)
            {
                int choice = k;
                Button button = Btn(modalButtons, q.Buttons[k], k == 0 ? "button-primary" : "button", () => Answer(choice));
                if (k == 0 && q.Buttons.Length > 1)
                    Text(button, "key-hint", "Enter", bold).pickingMode = PickingMode.Ignore;
            }
            modalOverlay.style.display = DisplayStyle.Flex;
        }

        /// <summary>Answers the open dialog as if its button was clicked (for the captures).</summary>
        public void AnswerModal(int choice) => Answer(choice);

        void Answer(int choice)
        {
            if (modalOverlay.style.display != DisplayStyle.Flex)
                return;
            Action<int> chosen = answer;
            answer = null;
            modalClosedFrame = Time.frameCount;
            modalOverlay.style.display = DisplayStyle.None;
            chosen?.Invoke(choice);
            if (modalOverlay.style.display != DisplayStyle.Flex)
                NextQuestion();
        }

        void Update()
        {
            Presses();
            if (modalOverlay == null || modalOverlay.style.display != DisplayStyle.Flex)
                return;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (Time.frameCount == modalOpenedFrame)
                return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                Answer(0);
            else if (keyboard.escapeKey.wasPressedThisFrame)
                Answer(back);
        }

        /// <summary>Goods as reward chips inside a dialog's body.</summary>
        public void RewardChips(VisualElement parent, Stock stock)
        {
            VisualElement row = Child(parent, "modal-chips");
            Reward(row, stock);
        }

        /// <summary>A text field for a name in a dialog's body, its text chosen so that typing replaces it.</summary>
        public TextField NameField(VisualElement parent, string value)
        {
            var field = new TextField { value = value, maxLength = Settling.NameLength, isDelayed = false };
            field.AddToClassList("name-field");
            UseFont(field, black);
            parent.Add(field);
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
            return field;
        }

        /// <summary>A line of the dialog's body: a key and a value.</summary>
        public void BodyLine(VisualElement parent, string key, string value, string cls = null)
        {
            VisualElement row = Child(parent, "modal-line");
            if (!string.IsNullOrEmpty(key))
                Text(row, "modal-key", key, bold);
            Label v = Text(row, "modal-value", value, black);
            if (cls != null)
                v.AddToClassList(cls);
        }

        /// <summary>The hero's army in a dialog's body: a portrait a stack and its count, with the losses under it.</summary>
        public void BodyArmy(VisualElement parent, IReadOnlyList<Stack> army, IReadOnlyList<float> losses = null, IReadOnlyList<int> lost = null)
        {
            VisualElement row = Child(parent, "modal-army");
            for (int k = 0; k < army.Count; k++)
            {
                Stack stack = army[k];
                VisualElement cell = Child(row, "modal-stack");
                VisualElement slot = Child(cell, "army-slot");
                slot.tooltip = Words.Count(stack.Unit, stack.Count);
                Texture2D picture = portraits?.Of(stack.Unit);
                if (picture != null)
                    slot.style.backgroundImage = new StyleBackground(Background.FromTexture2D(picture));
                Text(slot, "army-count", stack.Count.ToString(), black);
                if (losses != null && k < losses.Count)
                {
                    int expected = Mathf.RoundToInt(losses[k]);
                    Text(cell, expected > 0 ? "modal-loss" : "modal-safe", expected > 0 ? $"−{expected}" : T("целы", "safe"), black);
                }
                else if (lost != null && k < lost.Count)
                    Text(cell, lost[k] > 0 ? "modal-loss" : "modal-safe", lost[k] > 0 ? $"−{lost[k]}" : T("целы", "safe"), black);
            }
        }

        // ----- The heroes' shields -----

        /// <summary>Where a hero's figure stands now (HeroView.FigureAt), or null while it has none.</summary>
        public Func<Hero, Vector3?> HeroFigure;

        /// <summary>The camera's distance from which the heroes' shields show: nearer, a figure stands out by itself.</summary>
        const float ShieldsFrom = 11f;

        /// <summary>How far over a hero's feet its shield's lower tip stands, in world units: clear of a rider's head.</summary>
        const float ShieldLift = 0.75f;

        readonly Dictionary<Hero, VisualElement> heroShields = new Dictionary<Hero, VisualElement>();
        readonly List<Hero> shieldsGone = new List<Hero>();

        /// <summary>
        /// From afar a hero's figure is a few pixels among the trees and houses, where a review lost
        /// the heroes: then a small shield in the kingdom's colours stands over each seen hero, like
        /// the monsters' badges, clear of the towns' plates, hidden under the top bar and faint
        /// under the task lines (<paramref name="tasks"/>).
        /// </summary>
        void PlaceHeroShields(float distance, float barBottom, Rect tasks)
        {
            bool far = distance >= ShieldsFrom && HeroFigure != null;
            foreach (Hero hero in state.Heroes)
            {
                if (!heroShields.TryGetValue(hero, out VisualElement anchor))
                {
                    anchor = Child(shieldLayer, "anchor");
                    anchor.pickingMode = PickingMode.Ignore;
                    VisualElement picture = Child(anchor, "hero-shield");
                    picture.pickingMode = PickingMode.Ignore;
                    SetIcon(picture, FlagIcon(hero.Country));
                    heroShields[hero] = anchor;
                }
                VisualElement shield = anchor[0];
                Vector3? feet = far && hero.Cell >= 0 && hero.Cell < state.Seen.Length && state.Seen[hero.Cell] ? HeroFigure(hero) : null;
                Vector3 at = (feet ?? default) + Vector3.up * ShieldLift;
                if (feet == null || shield.style.display == DisplayStyle.None || cam.WorldToScreenPoint(at).z <= 0f)
                {
                    anchor.style.display = DisplayStyle.None;
                    continue;
                }
                anchor.style.display = DisplayStyle.Flex;
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, at, cam);
                // The shield is centred on its anchor: its lower tip at the point.
                float h = shield.resolvedStyle.height;
                Vector2 middle = BesidePlates(shield, new Vector2(p.x, p.y - (float.IsNaN(h) ? 0f : h / 2f)), below: true);
                anchor.style.translate = new Translate(middle.x, middle.y);
                bool under = float.IsNaN(h) || middle.y - h / 2f < barBottom;
                anchor.style.opacity = under ? 0f : tasks.Contains(middle) ? 0.12f : 1f;
            }
            // The fallen heroes' shields go with them.
            shieldsGone.Clear();
            foreach (Hero hero in heroShields.Keys)
                if (!state.Heroes.Contains(hero))
                    shieldsGone.Add(hero);
            foreach (Hero hero in shieldsGone)
            {
                heroShields[hero].RemoveFromHierarchy();
                heroShields.Remove(hero);
            }
        }

        // ----- Tags and floating text over the world -----

        /// <summary>A label that follows a point in the world (for example a stack's count on the battlefield).</summary>
        public Label AddTag(string cls, string text, Func<Vector3> where)
        {
            VisualElement anchor = Child(tagLayer, "anchor");
            anchor.pickingMode = PickingMode.Ignore;
            Label label = Text(anchor, "world-tag", text, black);
            label.AddToClassList(cls);
            label.pickingMode = PickingMode.Ignore;
            tags.Add(new WorldTag { Anchor = anchor, Label = label, Where = where });
            Follow(anchor, where(), 0f);
            return label;
        }

        public void RemoveTag(Label label)
        {
            if (label == null)
                return;
            tags.RemoveAll(t => t.Label == label);
            (label.parent ?? label).RemoveFromHierarchy();
        }

        public void ClearTags()
        {
            foreach (WorldTag t in tags)
                t.Anchor.RemoveFromHierarchy();
            tags.Clear();
            foreach (FloatText f in floats)
                f.Anchor.RemoveFromHierarchy();
            floats.Clear();
        }

        /// <summary>A word or number that rises from a point in the world and fades.</summary>
        public void Float(string text, string cls, Vector3 world, float seconds = 1.1f)
        {
            VisualElement anchor = Child(tagLayer, "anchor");
            anchor.pickingMode = PickingMode.Ignore;
            Label label = Text(anchor, "float", text, black);
            label.AddToClassList(cls);
            label.pickingMode = PickingMode.Ignore;
            floats.Add(new FloatText { Anchor = anchor, Label = label, World = world, Start = Time.time, Seconds = seconds });
            Follow(anchor, world, 0f);
        }

        void PlaceTags()
        {
            foreach (WorldTag t in tags)
                Follow(t.Anchor, t.Where(), 0f);
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                FloatText f = floats[i];
                float k = (Time.time - f.Start) / f.Seconds;
                if (k >= 1f)
                {
                    f.Anchor.RemoveFromHierarchy();
                    floats.RemoveAt(i);
                    continue;
                }
                Follow(f.Anchor, f.World, -60f * Mathf.Sqrt(k));
                f.Label.style.opacity = Mathf.Clamp01((1f - k) / 0.35f);
            }
        }

        void Follow(VisualElement element, Vector3 world, float lift)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0)
            {
                element.style.display = DisplayStyle.None;
                return;
            }
            element.style.display = DisplayStyle.Flex;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, cam);
            element.style.translate = new Translate(p.x, p.y + lift);
        }
    }
}
