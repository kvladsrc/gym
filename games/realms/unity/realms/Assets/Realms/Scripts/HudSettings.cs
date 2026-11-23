using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// The settings in the menu's column (the interface plan's 3.5 and the sound plan's П7,
    /// 2026-11-12), in place of its buttons like the lists of saves, without scrolling at the
    /// usual size, every change at once. The five volumes are bars of ten steps that a click or a
    /// drag sets, each playing its sample as it moves: the whole game the coins, the interface a
    /// click, the world a build, the music a phrase if none plays, the surroundings heard as they
    /// are. Then the screen and a window's size, the interface's size, how Alt shows the badges,
    /// the newcomer's hints and «Показать снова»; the camera's speeds and the screen's edge; and
    /// «Вернуть как было». A change of the screen goes back by itself in ten seconds unless kept.
    /// The languages stay at the column's foot. Kept by <see cref="GameSettings"/> and
    /// <see cref="SoundSettings"/>.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The players asked for the settings, or for the newcomer's hints again.</summary>
        public event Action SettingsPicked, HintsAgainPicked;

        sealed class LevelBar
        {
            public VisualElement Bar;
            public VisualElement[] Steps;
            public Label Number;
            public int Min;
            public Func<int> Get;
            public Action<int> Set;
            public Action Sample;
        }

        VisualElement menuSettings;
        Label windowLabel;
        Button hintsAgain;
        readonly List<Action> settingsRefresh = new List<Action>();
        readonly List<LevelBar> levelBars = new List<LevelBar>();
        LevelBar draggedBar;
        int draggedStep;
        bool screenAsking, screenAskShown;
        float screenAskUntil;

        /// <summary>The badges shown by Alt when Alt turns them on and off (<see cref="GameSettings.AltToggles"/>).</summary>
        bool badgesOn;

        public bool SettingsOpen => menuSettings != null && menuSettings.style.display == DisplayStyle.Flex;

        /// <summary>The settings, in the column in place of its buttons while open.</summary>
        void BuildSettings()
        {
            menuSettings = Child(menu, "menu-list");
            menuSettings.AddToClassList("menu-settings");
            Text(menuSettings, "menu-list-title", Words.MenuSettings, black);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen-scroll");
            scroll.AddToClassList("menu-list-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            menuSettings.Add(scroll);
            VisualElement rows = scroll.contentContainer;

            SettingsPart(rows, Words.SettingsSound);
            LevelRow(rows, Words.SettingsMaster, 0, () => SoundSettings.Master, v => SoundSettings.Master = v, () => Sounds.Play(Sfx.Coins));
            LevelRow(rows, Words.SettingsMusic, 0, () => SoundSettings.Level(SoundGroup.Music), v => SoundSettings.SetLevel(SoundGroup.Music, v), Music.Sample);
            LevelRow(rows, Words.SettingsAmbience, 0, () => SoundSettings.Level(SoundGroup.Ambience), v => SoundSettings.SetLevel(SoundGroup.Ambience, v), null);
            LevelRow(rows, Words.SettingsInterface, 0, () => SoundSettings.Level(SoundGroup.Interface), v => SoundSettings.SetLevel(SoundGroup.Interface, v), () => Sounds.Play(Sfx.Click));
            LevelRow(rows, Words.SettingsWorld, 0, () => SoundSettings.Level(SoundGroup.World), v => SoundSettings.SetLevel(SoundGroup.World, v), () => Sounds.Play(Sfx.Build));

            SettingsPart(rows, Words.SettingsScreenGroup);
            ChoiceRow(rows, Words.SettingsScreen, new[] { Words.SettingsFullscreen, Words.SettingsWindowed },
                () => GameSettings.Fullscreen ? 0 : 1, k => ChangeScreen(k == 0, GameSettings.Window));
            WindowRow(rows);
            ChoiceRow(rows, Words.SettingsBadges, new[] { Words.SettingsBadgesHold, Words.SettingsBadgesToggle },
                () => GameSettings.AltToggles ? 1 : 0, k =>
                {
                    GameSettings.AltToggles = k == 1;
                    badgesOn = false;
                    UpdateAltHelp();
                });
            VisualElement hints = ChoiceRow(rows, Words.SettingsHints, new[] { Words.SettingsYes, Words.SettingsNo },
                () => GameSettings.Hints ? 0 : 1, k => GameSettings.Hints = k == 0);
            // «Показать снова»: the newcomer's lines and hints from the start, once more.
            hintsAgain = SettingsButton(hints, Words.SettingsHintsAgain, () =>
            {
                GameSettings.Hints = true;
                HintsAgainPicked?.Invoke();
                hintsAgain.SetEnabled(false);
                hintsAgain.Q<Label>(className: "button-caption").text = Words.SettingsHintsAgainDone;
                RefreshSettings();
            });

            SettingsPart(rows, Words.SettingsCamera);
            LevelRow(rows, Words.SettingsMove, 1, () => GameSettings.CameraMove, v => GameSettings.CameraMove = v, null);
            LevelRow(rows, Words.SettingsTurn, 1, () => GameSettings.CameraTurn, v => GameSettings.CameraTurn = v, null);
            ChoiceRow(rows, Words.SettingsEdge, new[] { Words.SettingsEdgeMoves, Words.SettingsNo },
                () => GameSettings.EdgePan ? 0 : 1, k => GameSettings.EdgePan = k == 0);

            VisualElement last = Child(rows, "settings-row");
            last.AddToClassList("settings-last");
            SettingsButton(last, Words.SettingsReset, ResetSettings).AddToClassList("settings-reset");

            MenuButton(menuSettings, Words.SavesBack, "button", "Esc", () => CloseMenuList());
            menuSettings.style.display = DisplayStyle.None;
        }

        /// <summary>Opens the settings in place of the column's buttons; on the main menu the logo steps aside for them.</summary>
        public void ShowSettings()
        {
            if (menuSettings == null || !MenuOpen)
                return;
            CloseMenuList();
            MenuGroup.style.display = DisplayStyle.None;
            if (menuMode == MenuMode.Title)
                titleLogo.style.display = DisplayStyle.None;
            if (hintsAgain != null)
            {
                hintsAgain.SetEnabled(true);
                hintsAgain.Q<Label>(className: "button-caption").text = Words.SettingsHintsAgain;
            }
            RefreshSettings();
            menuSettings.style.display = DisplayStyle.Flex;
            root.AddToClassList("on-settings");
        }

        void CloseSettings()
        {
            menuSettings.style.display = DisplayStyle.None;
            draggedBar = null;
            root.RemoveFromClassList("on-settings");
            if (menuMode == MenuMode.Title)
                titleLogo.style.display = DisplayStyle.Flex;
        }

        void SettingsPart(VisualElement parent, string title) =>
            Text(parent, "settings-part", title, black).pickingMode = PickingMode.Ignore;

        /// <summary>A row with its caption at the left and its control at the right.</summary>
        VisualElement SettingsRow(VisualElement parent, string caption)
        {
            VisualElement row = Child(parent, "settings-row");
            Text(row, "settings-caption", caption, bold).pickingMode = PickingMode.Ignore;
            return row;
        }

        /// <summary>
        /// A level from <paramref name="min"/> to 10 as a bar of ten steps on a strip like the
        /// languages' one, its number beside it: a click on a step sets it, a click on the lit last
        /// step takes one off, a drag runs along; <paramref name="sample"/> plays at each change.
        /// </summary>
        void LevelRow(VisualElement parent, string caption, int min, Func<int> get, Action<int> set, Action sample)
        {
            VisualElement row = SettingsRow(parent, caption);
            VisualElement bar = Child(row, "settings-bar");
            Bevel.Attach(bar);
            var level = new LevelBar { Bar = bar, Steps = new VisualElement[10], Min = min, Get = get, Set = set, Sample = sample };
            for (int k = 0; k < 10; k++)
            {
                level.Steps[k] = Child(bar, "settings-step");
                level.Steps[k].pickingMode = PickingMode.Ignore;
            }
            level.Number = Text(row, "settings-level", "", black);
            level.Number.pickingMode = PickingMode.Ignore;
            levelBars.Add(level);
            settingsRefresh.Add(() =>
            {
                int now = get();
                for (int k = 0; k < 10; k++)
                    level.Steps[k].EnableInClassList("settings-step-on", k < now);
                level.Number.text = now.ToString();
            });
        }

        /// <summary>
        /// One of a few choices on a strip like the languages' one, the chosen one gold; the row
        /// is returned for a button beside it.
        /// </summary>
        VisualElement ChoiceRow(VisualElement parent, string caption, string[] options, Func<int> get, Action<int> set)
        {
            VisualElement row = SettingsRow(parent, caption);
            VisualElement strip = Child(row, "settings-choice");
            Bevel.Attach(strip);
            var buttons = new Button[options.Length];
            for (int k = 0; k < options.Length; k++)
            {
                int picked = k;
                Button choice = Btn(strip, options[k], "button", () =>
                {
                    if (get() == picked)
                        return;
                    set(picked);
                    RefreshSettings();
                });
                choice.AddToClassList("menu-lang");
                if (k == 0)
                    choice.AddToClassList("menu-lang-first");
                if (k == options.Length - 1)
                    choice.AddToClassList("menu-lang-last");
                Bevel.Attach(choice);
                buttons[k] = choice;
            }
            int shown = -1;
            settingsRefresh.Add(() =>
            {
                int now = get();
                if (now == shown)
                    return;
                shown = now;
                for (int k = 0; k < buttons.Length; k++)
                {
                    bool on = k == now;
                    buttons[k].EnableInClassList("button-primary", on);
                    buttons[k].EnableInClassList("button", !on);
                    UseFont(buttons[k].Q<Label>(className: "button-caption"), on ? black : bold);
                }
            });
            return row;
        }

        /// <summary>The window's size, ‹ and › through those smaller than the display; shown only in a window.</summary>
        void WindowRow(VisualElement parent)
        {
            VisualElement row = SettingsRow(parent, Words.SettingsWindow);
            VisualElement strip = Child(row, "settings-choice");
            Bevel.Attach(strip);
            Step("‹", -1, "menu-lang-first");
            windowLabel = Text(strip, "settings-window", "", black);
            windowLabel.pickingMode = PickingMode.Ignore;
            Step("›", 1, "menu-lang-last");
            settingsRefresh.Add(() =>
            {
                row.style.display = GameSettings.Fullscreen ? DisplayStyle.None : DisplayStyle.Flex;
                Vector2Int window = GameSettings.Window;
                windowLabel.text = $"{window.x}×{window.y}";
            });

            void Step(string sign, int by, string end)
            {
                Button step = Btn(strip, sign, "button", () =>
                {
                    Vector2Int[] windows = GameSettings.Windows();
                    int at = Array.IndexOf(windows, GameSettings.Window);
                    if (at < 0)
                        at = windows.Length - 1;
                    int next = Mathf.Clamp(at + by, 0, windows.Length - 1);
                    if (next != at)
                        ChangeScreen(false, windows[next]);
                });
                step.AddToClassList("menu-lang");
                step.AddToClassList("settings-arrow");
                step.AddToClassList(end);
                Bevel.Attach(step);
            }
        }

        /// <summary>A small button of the settings, on a strip of its own.</summary>
        Button SettingsButton(VisualElement parent, string text, Action clicked)
        {
            Button button = Btn(parent, text, "button", clicked);
            button.AddToClassList("menu-lang");
            button.AddToClassList("settings-button");
            Bevel.Attach(button);
            return button;
        }

        /// <summary>Every row as the settings stand now.</summary>
        void RefreshSettings()
        {
            foreach (Action refresh in settingsRefresh)
                refresh();
        }

        /// <summary>
        /// The screen filled or a window of a size; it goes back by itself in ten seconds unless
        /// the players keep it, so that a size the screen cannot show strands nobody (the
        /// interface plan's 3.5).
        /// </summary>
        void ChangeScreen(bool fullscreen, Vector2Int window)
        {
            bool wasFull = GameSettings.Fullscreen;
            Vector2Int was = GameSettings.Window;
            // One change at a time: the next waits until this one is kept or undone.
            if (screenAsking || (fullscreen == wasFull && (fullscreen || window == was)))
                return;
            GameSettings.SetScreen(fullscreen, window);
            screenAsking = true;
            Ask(Words.SettingsKeepTitle, null, Words.SettingsKeepText(10), Words.SettingsKeepAnswers, choice =>
            {
                screenAsking = screenAskShown = false;
                if (choice != 0)
                {
                    GameSettings.Window = was;
                    GameSettings.SetScreen(wasFull, was);
                }
            }, body =>
            {
                // Shown now: the ten seconds start.
                screenAskShown = true;
                screenAskUntil = Time.unscaledTime + 10f;
            }, back: 1);
        }

        /// <summary>Each frame: the screen's question counts down; a level bar follows the pointer; the rows keep up.</summary>
        void UpdateSettings()
        {
            if (screenAskShown && DialogOpen)
            {
                float left = screenAskUntil - Time.unscaledTime;
                if (left <= 0f)
                    Answer(1);
                else
                    modalText.text = Words.SettingsKeepText(Mathf.CeilToInt(left));
            }
            if (!SettingsOpen)
            {
                draggedBar = null;
                return;
            }
            // The screen's mode and size change at the frame's end: the rows look again now and then.
            if (Time.frameCount % 10 == 0)
                RefreshSettings();
            DragLevel();
        }

        /// <summary>A press on a level bar sets it, and the bar follows the pointer while the button is held.</summary>
        void DragLevel()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || root?.panel == null)
                return;
            Vector2 screen = mouse.position.ReadValue();
            bool pressed = mouse.leftButton.wasPressedThisFrame;
            if (pressed)
            {
                draggedBar = null;
                VisualElement top = Modal ? null : Picked(screen);
                foreach (LevelBar bar in levelBars)
                {
                    if (top != null && (top == bar.Bar || bar.Bar.Contains(top)))
                        draggedBar = bar;
                }
            }
            if (draggedBar == null)
                return;
            if (!mouse.leftButton.isPressed)
            {
                draggedBar = null;
                return;
            }
            LevelBar level = draggedBar;
            Rect first = level.Steps[0].worldBound, end = level.Steps[9].worldBound;
            float width = (end.xMax - first.xMin) / 10f;
            if (width <= 0f)
                return;
            float x = PanelPoint(screen).x;
            int step = x < first.xMin ? 0 : Mathf.Clamp(Mathf.FloorToInt((x - first.xMin) / width) + 1, 1, 10);
            int now = level.Get(), wanted;
            if (pressed)
            {
                // A click on the lit last step takes it off: the way down to nothing.
                wanted = step == now ? step - 1 : step;
                draggedStep = step;
            }
            else
            {
                if (step == draggedStep)
                    return;
                draggedStep = step;
                wanted = step;
            }
            wanted = Mathf.Clamp(wanted, level.Min, 10);
            if (wanted == now)
                return;
            level.Set(wanted);
            RefreshSettings();
            level.Sample?.Invoke();
        }

        /// <summary>«Вернуть как было»: every setting and volume at its default, the interface at its size, the screen filled.</summary>
        void ResetSettings()
        {
            GameSettings.Reset();
            badgesOn = false;
            UpdateAltHelp();
            if (!GameSettings.Fullscreen)
                ChangeScreen(true, GameSettings.Window);
            RefreshSettings();
        }
    }
}
