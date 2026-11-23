using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// «Об игре» on the main menu (the interface plan's 3.7, 2026-11-12), in the column in place of
    /// its buttons like the settings: the name and the version, what the game is, how its art and
    /// sound were made, the font's and the code's licences, and where the saves and the log lie,
    /// for a player who writes about a fault: «Открыть папку» opens the saves' folder and
    /// «Скопировать путь» puts the log's path on the clipboard. The generating models' licences are
    /// not named here: the plan leaves their terms to be checked first.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The players asked about the game.</summary>
        public event Action AboutPicked;

        VisualElement menuAbout;
        Label aboutSaves, aboutLog;
        Button aboutCopy;
        string aboutSavesPath, aboutLogPath;

        public bool AboutOpen => menuAbout != null && menuAbout.style.display == DisplayStyle.Flex;

        /// <summary>«Об игре», in the column in place of its buttons while open.</summary>
        void BuildAbout()
        {
            menuAbout = Child(menu, "menu-list");
            menuAbout.AddToClassList("menu-settings");
            menuAbout.AddToClassList("menu-about");
            Text(menuAbout, "menu-list-title", Words.MenuAbout, black);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen-scroll");
            scroll.AddToClassList("menu-list-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            menuAbout.Add(scroll);
            VisualElement rows = scroll.contentContainer;

            Text(rows, "about-name", $"{Words.GameName} · {Words.Version(Application.version)}", black).pickingMode = PickingMode.Ignore;
            AboutText(rows, Words.AboutWhat);
            SettingsPart(rows, Words.AboutArtPart);
            AboutText(rows, Words.AboutArt);
            SettingsPart(rows, Words.AboutLicencesPart);
            AboutText(rows, Words.AboutFont);
            AboutText(rows, Words.AboutCode);
            SettingsPart(rows, Words.AboutFaultsPart);
            AboutText(rows, Words.AboutFaults);
            // Each place: its caption with the button at the right, the path under them.
            SettingsButton(SettingsRow(rows, Words.AboutSaves), Words.AboutOpenFolder, OpenSavesFolder);
            aboutSaves = AboutText(rows, "");
            aboutSaves.AddToClassList("about-path");
            aboutCopy = SettingsButton(SettingsRow(rows, Words.AboutLog), Words.AboutCopyPath, CopyLogPath);
            aboutLog = AboutText(rows, "");
            aboutLog.AddToClassList("about-path");

            MenuButton(menuAbout, Words.SavesBack, "button", "Esc", () => CloseMenuList());
            menuAbout.style.display = DisplayStyle.None;
        }

        Label AboutText(VisualElement parent, string text)
        {
            Label label = Text(parent, "about-text", text, regular);
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>
        /// Opens «Об игре» in place of the column's buttons, the logo stepping aside as for the
        /// settings; the saves' folder and the log's path are the game's own.
        /// </summary>
        public void ShowAbout(string savesFolder, string logPath)
        {
            if (menuAbout == null || !MenuOpen)
                return;
            CloseMenuList();
            MenuGroup.style.display = DisplayStyle.None;
            if (menuMode == MenuMode.Title)
                titleLogo.style.display = DisplayStyle.None;
            aboutSavesPath = savesFolder;
            aboutLogPath = logPath;
            aboutSaves.text = savesFolder;
            bool logged = !string.IsNullOrEmpty(logPath);
            aboutLog.text = logged ? logPath : "—";
            aboutCopy.SetEnabled(logged);
            aboutCopy.Q<Label>(className: "button-caption").text = Words.AboutCopyPath;
            menuAbout.style.display = DisplayStyle.Flex;
            root.AddToClassList("on-settings");
        }

        void CloseAbout()
        {
            menuAbout.style.display = DisplayStyle.None;
            root.RemoveFromClassList("on-settings");
            if (menuMode == MenuMode.Title)
                titleLogo.style.display = DisplayStyle.Flex;
        }

        /// <summary>The saves' folder in the system's file manager; made first when nothing was saved yet.</summary>
        void OpenSavesFolder()
        {
            if (string.IsNullOrEmpty(aboutSavesPath))
                return;
            try
            {
                Directory.CreateDirectory(aboutSavesPath);
                Application.OpenURL(new Uri(aboutSavesPath).AbsoluteUri);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Realms: the saves' folder did not open: {e.Message}");
            }
        }

        void CopyLogPath()
        {
            GUIUtility.systemCopyBuffer = aboutLogPath;
            aboutCopy.Q<Label>(className: "button-caption").text = Words.AboutCopied;
        }
    }
}
