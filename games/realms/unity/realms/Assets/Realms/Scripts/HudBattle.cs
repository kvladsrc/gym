using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The battle's part of the interface: the title, the round and the turn queue at the top,
    /// whose turn it is and the Defend, Auto and Retreat buttons at the bottom, and a card for
    /// the stack under the pointer. The map's panels are hidden meanwhile.
    /// </summary>
    public sealed partial class Hud
    {
        const int QueueShown = 10;

        VisualElement battleTop, battleQueue, battleBar, battleInfo, battleInfoFace, battleInfoLines;
        Label battleTitle, battleRound, battleWho, battleHint, battleInfoName, battleInfoLine;
        Button autoButton;
        Action onDefend, onAuto, onRetreat;

        public bool InBattle => root != null && root.ClassListContains("in-battle");

        /// <summary>
        /// The player's kingdom colour, which the player's stacks wear in a battle: the monsters
        /// wear charcoal and bone, so red and blue keep meaning kingdoms.
        /// </summary>
        public Color Mine => state != null ? Palette.Countries[state.World.Player % Palette.Countries.Length] : Palette.Gold;

        /// <summary>Gives the element a rim of the colour on all four sides.</summary>
        public static void Rim(VisualElement element, Color colour)
        {
            element.style.borderTopColor = colour;
            element.style.borderRightColor = colour;
            element.style.borderBottomColor = colour;
            element.style.borderLeftColor = colour;
        }

        void BuildBattle()
        {
            battleTop = Child(root, "battle-top");
            VisualElement head = Child(battleTop, "battle-head");
            battleTitle = Text(head, "battle-title", "", black);
            battleRound = Text(head, "battle-round", "", bold);
            battleQueue = Child(battleTop, "battle-queue");

            battleBar = Child(root, "battle-bar");
            VisualElement words = Child(battleBar, "battle-words");
            battleWho = Text(words, "battle-who", "", black);
            battleHint = Text(words, "battle-hint", "", regular);
            Button defend = Btn(battleBar, T("Защита", "Defend"), "button", () => onDefend?.Invoke());
            defend.tooltip = T("Отряд пропускает удар и получает меньше урона до своего хода", "The troop skips its strike and takes less damage until its next turn");
            Text(defend, "key-hint-dark", "D", bold).pickingMode = PickingMode.Ignore;
            autoButton = Btn(battleBar, T("Авто", "Auto"), "button", () => onAuto?.Invoke());
            autoButton.tooltip = T("Отряды бьются сами, до конца боя", "The troops fight on their own until the battle ends");
            Text(autoButton, "key-hint-dark", "A", bold).pickingMode = PickingMode.Ignore;
            Btn(battleBar, T("Отступить", "Retreat"), "button", () => onRetreat?.Invoke());

            battleInfo = Child(root, "battle-info");
            VisualElement infoHead = Child(battleInfo, "battle-info-head");
            battleInfoFace = Child(infoHead, "battle-info-face");
            battleInfoName = Text(infoHead, "battle-info-name", "", black);
            battleInfoLines = Child(battleInfo, "battle-info-lines");
            battleInfoLine = Text(battleInfo, "battle-info-expect", "", black);

            battleTop.style.display = battleBar.style.display = battleInfo.style.display = DisplayStyle.None;
        }

        /// <summary>Hides the map's panels and shows the battle's.</summary>
        public void ShowBattle(string title, Portraits pictures, Action defend, Action auto, Action retreat)
        {
            if (pictures != null)
                portraits = pictures;
            onDefend = defend;
            onAuto = auto;
            onRetreat = retreat;
            root.AddToClassList("in-battle");
            battleTitle.text = title;
            battleRound.text = "";
            battleQueue.Clear();
            BattleAuto(false);
            BattleTurn(null, false);
            BattleInfo(null, null);
            battleTop.style.display = battleBar.style.display = DisplayStyle.Flex;
        }

        public void HideBattle()
        {
            if (root == null)
                return;
            root.RemoveFromClassList("in-battle");
            battleTop.style.display = battleBar.style.display = battleInfo.style.display = DisplayStyle.None;
            onDefend = onAuto = onRetreat = null;
            ClearTags();
        }

        /// <summary>
        /// The round and the stacks still to act in it, the current one first and bigger; then,
        /// after a thin bar, smaller and dimmer, the order of the round after it, so the end of a
        /// round does not look as if the other stacks were gone.
        /// </summary>
        public void BattleQueue(List<Fighter> queue, List<Fighter> next, int[] shown, int round)
        {
            battleRound.text = round > 0 ? T($"раунд {round}", $"round {round}") : "";
            battleQueue.Clear();
            int n = 0;
            foreach (Fighter f in queue)
            {
                if (n >= QueueShown)
                    return;
                if (QueueSlot(f, shown, n == 0 ? "queue-now" : null))
                    n++;
            }
            bool gap = false;
            foreach (Fighter f in next ?? new List<Fighter>())
            {
                if (n >= QueueShown)
                    return;
                if (ShownCount(f, shown) <= 0)
                    continue;
                if (!gap)
                {
                    Child(battleQueue, "queue-gap").tooltip = T("Дальше — следующий раунд", "Next — the next round");
                    gap = true;
                }
                QueueSlot(f, shown, "queue-next");
                n++;
            }
        }

        static int ShownCount(Fighter f, int[] shown) => shown != null && f.Id < shown.Length ? shown[f.Id] : f.Count;

        /// <summary>A stack's portrait in the queue, with its count; false when the stack is gone.</summary>
        bool QueueSlot(Fighter f, int[] shown, string extra)
        {
            int count = ShownCount(f, shown);
            if (count <= 0)
                return false;
            VisualElement slot = Child(battleQueue, "queue-slot");
            slot.AddToClassList(f.Side == Side.Players ? "queue-mine" : "queue-enemy");
            if (f.Side == Side.Players)
                Rim(slot, Mine);
            if (extra != null)
                slot.AddToClassList(extra);
            slot.tooltip = Words.Count(f.Unit, count);
            Texture2D picture = portraits?.Of(f.Unit);
            if (picture != null)
                slot.style.backgroundImage = new StyleBackground(Background.FromTexture2D(picture));
            Text(slot, "army-count", count.ToString(), black).pickingMode = PickingMode.Ignore;
            return true;
        }

        /// <summary>
        /// How many pixels the battle's top panel and bottom bar cover and half a count plate's
        /// height, known before the panels are laid out: the stylesheet's heights (.battle-top
        /// round the current stack's 72-point portrait, .battle-bar, .world-tag) in points.
        /// </summary>
        public void BattlePanels(out float top, out float bottom, out float plateHalf)
        {
            float height = root?.worldBound.height ?? 0f;
            float k = height > 1f ? Screen.height / height : 1f;
            top = 93f * k;
            bottom = 84f * k;
            plateHalf = 14f * k;
        }

        /// <summary>Whose turn it is (null while the monsters act) and what the players can do.</summary>
        public void BattleTurn(string who, bool shoots)
        {
            battleWho.text = who != null ? T($"Ходят: {who}", $"Moving: {who}") : T("Ходят враги…", "The enemies are moving…");
            battleHint.text = who == null ? "" : shoots
                ? T("Щёлкни по врагу — выстрел, по клетке — идти туда", "Click an enemy — shoot, a tile — go there")
                : T("Щёлкни по клетке — идти, по врагу — подойти и ударить", "Click a tile — go, an enemy — walk up and strike");
            battleHint.style.display = who != null ? DisplayStyle.Flex : DisplayStyle.None;
            battleBar.EnableInClassList("battle-bar-waiting", who == null);
            if (who == null && autoButton.ClassListContains("button-primary"))
                battleWho.text = T("Отряды бьются сами", "The troops fight on their own");
        }

        public void BattleAuto(bool on)
        {
            autoButton.EnableInClassList("button-primary", on);
            autoButton.EnableInClassList("button", !on);
            if (on)
                battleWho.text = T("Отряды бьются сами", "The troops fight on their own");
        }

        /// <summary>The card of the stack under the pointer, with what the current stack's blow would do to it.</summary>
        public void BattleInfo(Fighter f, string line)
        {
            if (f == null)
            {
                battleInfo.style.display = DisplayStyle.None;
                return;
            }
            battleInfo.style.display = DisplayStyle.Flex;
            battleInfo.EnableInClassList("battle-info-enemy", f.Side == Side.Monsters);
            battleInfo.style.borderTopColor = f.Side == Side.Players ? new StyleColor(Mine) : new StyleColor(StyleKeyword.Null);
            Texture2D picture = portraits?.Of(f.Unit);
            battleInfoFace.style.backgroundImage = picture != null
                ? new StyleBackground(Background.FromTexture2D(picture))
                : new StyleBackground(StyleKeyword.None);
            battleInfoName.text = Words.Count(f.Unit, f.Count);
            battleInfoLines.Clear();
            UnitStats s = f.Stats;
            BattleLine(T("Атака", "Attack"), s.Attack.ToString(), T("Защита", "Defend"), s.Defence.ToString());
            BattleLine(T("Урон", "Damage"), $"{s.MinDamage}–{s.MaxDamage}", T("Скорость", "Speed"), s.Speed.ToString());
            BattleLine(T("Здоровье", "Health"), f.Top < s.Health ? T($"{f.Top} из {s.Health}", $"{f.Top} of {s.Health}") : s.Health.ToString(), null, null);
            // Whom a monster stack goes for, so the players can plan against it (the battle plan of 2026-11-10).
            string temper = Words.Temper(f.Unit, f.Count);
            if (temper != null)
                Text(battleInfoLines, "battle-info-ability", temper, bold);
            string ability = Words.Abilities(s);
            if (ability != null)
                Text(battleInfoLines, "battle-info-ability", ability, regular);
            if (f.Defending)
                Text(battleInfoLines, "battle-info-ability", T("Держит защиту до своего хода.", "Holds its defence until its next turn."), regular);
            if (f.Webbed || f.Stuck)
                Text(battleInfoLines, "battle-info-ability", T("Запутался в паутине: не может идти.", "Caught in a web: cannot move."), regular);
            battleInfoLine.text = line ?? "";
            battleInfoLine.style.display = string.IsNullOrEmpty(line) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void BattleLine(string key, string value, string key2, string value2)
        {
            VisualElement row = Child(battleInfoLines, "battle-info-row");
            VisualElement a = Child(row, "battle-info-cell");
            Text(a, "info-key", key, bold);
            Text(a, "info-value", value, black);
            if (key2 == null)
                return;
            VisualElement b = Child(row, "battle-info-cell");
            Text(b, "info-key", key2, bold);
            Text(b, "info-value", value2, black);
        }
    }
}
