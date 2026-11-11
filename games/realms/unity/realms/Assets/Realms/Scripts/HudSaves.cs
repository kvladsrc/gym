using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// The saves in the menu's column (the interface plan's 3.1, 2026-11-11): «Сохранить» and
    /// «Загрузить» open their lists in place of the menu's buttons, Esc or «Назад» goes back.
    /// The lists look different so as not to be mixed up: loading takes a row at once, as the game
    /// keeps itself and nothing is lost; saving into an occupied slot asks first, and the first
    /// empty slot is gold. «Сохранено» shows beside the turn counter for two seconds.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The players asked for the save or the load list.</summary>
        public event Action SavePicked, LoadPicked;

        VisualElement menuButtons, menuList, menuListRows;
        Label menuListTitle, savedLabel;
        Button menuSave, menuLoad;
        Label menuQuit;
        float savedUntil;

        /// <summary>The menu's buttons for the saves, after «Вернуться в игру».</summary>
        void BuildSaveButtons(VisualElement buttons)
        {
            menuButtons = buttons;
            menuSave = MenuButton(buttons, Words.MenuSave, "button", null, () => SavePicked?.Invoke());
            menuLoad = MenuButton(buttons, Words.MenuLoad, "button", null, () => LoadPicked?.Invoke());
        }

        /// <summary>The column's list of saves, in place of the buttons while open.</summary>
        void BuildSaveList()
        {
            menuList = Child(menu, "menu-list");
            menuListTitle = Text(menuList, "menu-list-title", "", black);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen-scroll");
            scroll.AddToClassList("menu-list-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            menuList.Add(scroll);
            menuListRows = scroll.contentContainer;
            MenuButton(menuList, Words.SavesBack, "button", "Esc", () => CloseSaves());
            menuList.style.display = DisplayStyle.None;
        }

        /// <summary>«Сохранено» in the top bar's turn counter, shown by <see cref="ShowSaved"/>.</summary>
        void BuildSavedLabel(VisualElement seasonRow)
        {
            savedLabel = Text(seasonRow, "turn-saved", Words.Saved, bold);
            savedLabel.pickingMode = PickingMode.Ignore;
            savedLabel.style.opacity = 0f;
        }

        /// <summary>The game kept itself: «Сохранено» beside the turn counter for two seconds.</summary>
        public void ShowSaved() => savedUntil = ViewClock.Now + 2f;

        void FadeSaved()
        {
            if (savedLabel != null)
                savedLabel.style.opacity = Mathf.Clamp01((savedUntil - ViewClock.Now) / 0.4f);
        }

        public bool SavesOpen => menuList != null && menuList.style.display == DisplayStyle.Flex;

        /// <summary>Closes the list of saves, back to the menu's buttons; false when none was open.</summary>
        public bool CloseSaves()
        {
            if (!SavesOpen)
                return false;
            menuList.style.display = DisplayStyle.None;
            MenuGroup.style.display = DisplayStyle.Flex;
            return true;
        }

        void OpenSaves(string title)
        {
            menuListTitle.text = title;
            menuListRows.Clear();
            MenuGroup.style.display = DisplayStyle.None;
            menuList.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// The save list: the players' slots (index 1 to 6, null where empty). An occupied one asks
        /// before it is replaced; the first empty one is gold, «Новое сохранение»; the other empty
        /// ones are not shown.
        /// </summary>
        public void ShowSaveList(SaveFile[] slots, Action<int> save)
        {
            OpenSaves(Words.SaveListTitle);
            bool offered = false;
            for (int slot = 1; slot < slots.Length; slot++)
            {
                int picked = slot;
                SaveFile file = slots[slot];
                if (file == null)
                {
                    if (offered)
                        continue;
                    offered = true;
                    Button fresh = SaveRow(menuListRows, null, Words.SaveNew, Words.SaveSlot(slot), null, () => save(picked));
                    fresh.RemoveFromClassList("button");
                    fresh.AddToClassList("button-primary");
                    continue;
                }
                SaveRow(menuListRows, file, null, null, Words.SaveSlot(slot), () =>
                {
                    if (file.Header == null)
                    {
                        save(picked);
                        return;
                    }
                    Ask(Words.ReplaceTitle, null, Words.ReplaceText(Names.Shown(file.Header.Kingdom), file.Header.Turn), Words.ReplaceAnswers, choice =>
                    {
                        if (choice == 0)
                            save(picked);
                    });
                });
            }
        }

        /// <summary>
        /// The load list in three parts: this game's autosaves, the newest of the games before it,
        /// and the players' own; a row loads at once. A file that does not open says why and takes no click.
        /// </summary>
        public void ShowLoadList(List<SaveFile> thisGame, List<SaveFile> earlier, List<SaveFile> own, Action<SaveFile> load)
        {
            OpenSaves(Words.LoadListTitle);
            void Part(string title, List<SaveFile> files, bool slots)
            {
                Text(menuListRows, "save-part", title, black);
                foreach (SaveFile file in files)
                {
                    SaveFile picked = file;
                    SaveRow(menuListRows, file, null, null, slots ? Words.SaveSlot(file.Slot) : null, () => load(picked));
                }
            }
            if (thisGame.Count > 0)
                Part(Words.LoadThisGame, thisGame, false);
            if (earlier.Count > 0)
                Part(Words.LoadEarlier, earlier, false);
            Part(Words.LoadOwn, own, true);
            if (own.Count == 0)
                Text(menuListRows, "save-none", Words.LoadOwnNone, regular);
        }

        /// <summary>
        /// A row: the save's shield, its kingdom, "ход 34 · лето" and when it was written and how
        /// long played; or <paramref name="empty"/> for a free slot; the slot's number at the right.
        /// </summary>
        Button SaveRow(VisualElement parent, SaveFile file, string empty, string emptyNote, string slot, Action clicked)
        {
            Button row = PressButton(clicked);
            row.AddToClassList("button");
            row.AddToClassList("save-row");
            row.focusable = false;
            parent.Add(row);
            VisualElement shield = Child(row, "save-shield");
            shield.pickingMode = PickingMode.Ignore;
            VisualElement lines = Child(row, "save-lines");
            lines.pickingMode = PickingMode.Ignore;
            if (file == null)
            {
                Text(lines, "save-name", empty, black).pickingMode = PickingMode.Ignore;
                if (emptyNote != null)
                    Text(lines, "save-when", emptyNote, regular).pickingMode = PickingMode.Ignore;
                shield.AddToClassList("save-shield-new");
            }
            else if (file.Header == null)
            {
                Text(lines, "save-name", Words.ProblemOf(file.Problem), bold).pickingMode = PickingMode.Ignore;
                row.SetEnabled(false);
                row.AddToClassList("save-row-bad");
            }
            else
            {
                SaveHeader h = file.Header;
                if (Flag.TryParse(h.Flag, out Flag flag))
                    shield.style.backgroundImage = new StyleBackground(FlagArt.Image(flag, FlagArt.Shape.Shield, 92, 104));
                Text(lines, "save-name", Names.Shown(h.Kingdom), black).pickingMode = PickingMode.Ignore;
                Text(lines, "save-turn", Words.SaveTurn(h.Turn), bold).pickingMode = PickingMode.Ignore;
                Text(lines, "save-when", Words.SaveWhen(h.Written, h.Played), regular).pickingMode = PickingMode.Ignore;
            }
            if (slot != null)
                Text(row, "save-slot", slot, bold).pickingMode = PickingMode.Ignore;
            return row;
        }
    }
}
