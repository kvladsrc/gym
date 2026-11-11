using System;
using System.Collections.Generic;
using System.IO;
using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>A save on disk as the menu's lists show it.</summary>
    public sealed class SaveFile
    {
        public string Path;

        /// <summary>The save's header, or null when the file does not open (<see cref="Problem"/>).</summary>
        public SaveHeader Header;

        public SaveProblem Problem;

        /// <summary>The players' slot, 1 to <see cref="RealmsGame.ManualSlots"/>, or 0 for an autosave.</summary>
        public int Slot;

        /// <summary>When the file was last written, for the order of the autosaves.</summary>
        public DateTime Time;

        /// <summary>The game the autosave belongs to, from its name.</summary>
        public long GameId;
    }

    /// <summary>
    /// The saves in the game (the interface plan's 3.1, 2026-11-11; the format is Core/Save.cs).
    /// The game writes itself after each turn's end, before each battle, before a new game and on
    /// quitting, into three autosaves a game in turn; the autosaves of the last three games are
    /// kept. The players have six slots of their own in the menu. A battle is never saved: quitting
    /// in one goes back to the save before it, with the same chance, so the same battle comes again.
    /// The last start's game opens again when the game starts.
    /// </summary>
    public sealed partial class RealmsGame
    {
        /// <summary>Autosaves a game keeps in turn.</summary>
        const int AutoSlots = 3;

        /// <summary>The players' own slots.</summary>
        public const int ManualSlots = 6;

        /// <summary>Games whose autosaves are kept.</summary>
        const int KeptGames = 3;

        /// <summary>The game's number, new for each new game; its autosaves carry it.</summary>
        long gameId;

        /// <summary>Seconds the game has been played, with the time of the saves it came from.</summary>
        double played;

        /// <summary>The last turn's report, shown again when the game is loaded.</summary>
        TurnReport lastReport;

        /// <summary>A fight is on (from its start to its result): its state is half way and is not saved.</summary>
        bool fighting;

        /// <summary>The folder of the saves; the captures keep theirs beside their pictures, away from the players'.</summary>
        string SaveDir => Path.Combine(captureDir ?? Application.persistentDataPath, "saves");

        string AutoPath(long game, int slot) => Path.Combine(SaveDir, $"auto-{game:x16}-{slot}.sav");

        string SlotPath(int slot) => Path.Combine(SaveDir, $"slot-{slot}.sav");

        static long NewGameId()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            return BitConverter.ToInt64(bytes, 0) & long.MaxValue;
        }

        /// <summary>The state may be written now: not in a fight, whose state is half way, nor a new game's preview.</summary>
        bool CanSave => state != null && !battleView.Active && !fighting && !previewing;

        SaveGame Snapshot(SaveKind kind)
        {
            mapCamera.MapPose(out Vector2 point, out float distance, out float facing);
            var save = new SaveGame
            {
                State = state,
                View = new SaveView { Hero = hero?.Index ?? -1, X = point.x, Z = point.y, Distance = distance, Yaw = facing },
                Report = lastReport,
            };
            save.Header.Version = Application.version;
            save.Header.GameId = gameId;
            save.Header.Kind = kind;
            save.Header.Written = DateTime.UtcNow;
            save.Header.Played = (int)Math.Min(played, int.MaxValue);
            return save;
        }

        /// <summary>
        /// The game writes itself into its next autosave: the missing one, else the oldest. Skipped
        /// in a fight; false when skipped or the disk refused, which a line in the log tells.
        /// </summary>
        bool AutoSave(bool tell = true)
        {
            if (!CanSave)
                return false;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string path = null;
            for (int slot = 0; slot < AutoSlots; slot++)
            {
                if (!File.Exists(AutoPath(gameId, slot)))
                {
                    path = AutoPath(gameId, slot);
                    break;
                }
            }
            path ??= OldestAuto();
            if (!WriteSafely(path, Snapshot(SaveKind.Auto)))
                return false;
            Prune();
            if (tell)
                hud.ShowSaved();
            Debug.Log($"Autosaved turn {state.Turn} in {watch.ElapsedMilliseconds} ms: {Path.GetFileName(path)}");
            return true;
        }

        /// <summary>The oldest of the game's autosaves, all of which exist.</summary>
        string OldestAuto()
        {
            string path = AutoPath(gameId, 0);
            DateTime oldest = File.GetLastWriteTimeUtc(path);
            for (int slot = 1; slot < AutoSlots; slot++)
            {
                string candidate = AutoPath(gameId, slot);
                DateTime time = File.GetLastWriteTimeUtc(candidate);
                if (time < oldest)
                {
                    oldest = time;
                    path = candidate;
                }
            }
            return path;
        }

        /// <summary>The players save into their slot (1 to <see cref="ManualSlots"/>).</summary>
        void SaveInto(int slot)
        {
            if (!CanSave || busy)
                return;
            if (!WriteSafely(SlotPath(slot), Snapshot(SaveKind.Manual)))
                return;
            hud.CloseSaves();
            hud.ShowSaved();
            hud.Toast(Words.SavedInto(state.World.Countries[state.World.Player].Label, state.Turn), 3f);
        }

        /// <summary>
        /// Writes the save into a file next to its place, then puts it in its place, so a game cut
        /// off half way leaves the old save whole. False, with a line, when the disk refused.
        /// </summary>
        bool WriteSafely(string path, SaveGame save)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                byte[] bytes = Save.ToBytes(save);
                string temp = path + ".tmp";
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                Debug.LogWarning($"The save {path} was not written: {e.Message}");
                hud.Toast(Words.SaveFailed, 4f);
                return false;
            }
        }

        /// <summary>The autosaves of all but the last <see cref="KeptGames"/> games go.</summary>
        void Prune()
        {
            var newest = new Dictionary<long, DateTime>();
            List<SaveFile> autos = AutoFiles(false);
            foreach (SaveFile file in autos)
            {
                if (!newest.TryGetValue(file.GameId, out DateTime time) || file.Time > time)
                    newest[file.GameId] = file.Time;
            }
            var games = new List<long>(newest.Keys);
            // This game first, then the others newest first.
            games.Sort((a, b) => a == b ? 0 : a == gameId ? -1 : b == gameId ? 1 : newest[b].CompareTo(newest[a]));
            for (int g = KeptGames; g < games.Count; g++)
            {
                foreach (SaveFile file in autos)
                {
                    if (file.GameId != games[g])
                        continue;
                    try
                    {
                        File.Delete(file.Path);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        Debug.LogWarning($"The old autosave {file.Path} stays: {e.Message}");
                    }
                }
            }
        }

        /// <summary>The autosaves on disk, the newest first; with their headers when <paramref name="headers"/>.</summary>
        List<SaveFile> AutoFiles(bool headers)
        {
            var files = new List<SaveFile>();
            if (!Directory.Exists(SaveDir))
                return files;
            foreach (string path in Directory.GetFiles(SaveDir, "auto-*.sav"))
            {
                string[] parts = Path.GetFileNameWithoutExtension(path).Split('-');
                if (parts.Length != 3 || !long.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out long game))
                    continue;
                SaveFile file = headers ? Described(path) : new SaveFile { Path = path };
                file.GameId = game;
                file.Time = File.GetLastWriteTimeUtc(path);
                files.Add(file);
            }
            files.Sort((a, b) => b.Time.CompareTo(a.Time));
            return files;
        }

        /// <summary>The players' slots, 1 to <see cref="ManualSlots"/>; null where a slot is empty.</summary>
        SaveFile[] SlotFiles()
        {
            var slots = new SaveFile[ManualSlots + 1];
            for (int slot = 1; slot <= ManualSlots; slot++)
            {
                string path = SlotPath(slot);
                if (!File.Exists(path))
                    continue;
                SaveFile file = Described(path);
                file.Slot = slot;
                file.Time = File.GetLastWriteTimeUtc(path);
                slots[slot] = file;
            }
            return slots;
        }

        /// <summary>The file with its header, or with the reason it does not open.</summary>
        static SaveFile Described(string path)
        {
            var file = new SaveFile { Path = path };
            try
            {
                using (var stream = File.OpenRead(path))
                    file.Header = Save.ReadHeader(stream);
            }
            catch (SaveException e)
            {
                file.Problem = e.Problem;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                file.Problem = SaveProblem.Damaged;
            }
            return file;
        }

        /// <summary>
        /// The «Загрузить» list: up to three autosaves of this game, the newest of each of the two
        /// games before it, then the players' slots that hold a save.
        /// </summary>
        void ShowLoadList()
        {
            var thisGame = new List<SaveFile>();
            var earlier = new List<SaveFile>();
            var games = new HashSet<long>();
            foreach (SaveFile file in AutoFiles(true))
            {
                if (file.GameId == gameId)
                {
                    if (thisGame.Count < AutoSlots)
                        thisGame.Add(file);
                }
                else if (earlier.Count < KeptGames - 1 && games.Add(file.GameId))
                    earlier.Add(file);
            }
            var own = new List<SaveFile>();
            foreach (SaveFile file in SlotFiles())
            {
                if (file != null)
                    own.Add(file);
            }
            hud.ShowLoadList(thisGame, earlier, own, LoadFrom);
        }

        /// <summary>The «Сохранить» list: the six slots, the first empty one gold.</summary>
        void ShowSaveList()
        {
            if (!CanSave || busy)
                return;
            hud.ShowSaveList(SlotFiles(), SaveInto);
        }

        /// <summary>
        /// Opens the save. A save of another game first puts this one into its autosave, so
        /// nothing is lost; one of this game is a step back and leaves the autosaves as they are.
        /// </summary>
        void LoadFrom(SaveFile file)
        {
            if (busy || loading)
                return;
            SaveGame game = ReadSave(file.Path, out SaveProblem? problem);
            if (game == null)
            {
                hud.Toast(Words.ProblemOf(problem ?? SaveProblem.Damaged), 5f);
                return;
            }
            // Behind the main menu the game is as its save left it.
            if (game.Header.GameId != gameId && !behindTitle)
                AutoSave(false);
            Open(game);
        }

        /// <summary>The save read whole, or null with the reason.</summary>
        static SaveGame ReadSave(string path, out SaveProblem? problem)
        {
            problem = null;
            try
            {
                using (var stream = File.OpenRead(path))
                    return Save.Read(stream);
            }
            catch (SaveException e)
            {
                Debug.LogWarning($"The save {path} does not open: {e.Message}");
                problem = e.Problem;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"The save {path} does not open: {e.Message}");
                problem = SaveProblem.Damaged;
            }
            return null;
        }

        /// <summary>The game is closed (the menu, the window or the system): it keeps itself, unless in a fight.</summary>
        void OnApplicationQuit()
        {
            if (!capturing && !behindTitle)
                AutoSave(false);
        }

        /// <summary>Seconds of play, while the game's window has the focus; a long stall counts as one second.</summary>
        void CountPlayed()
        {
            if (Application.isFocused)
                played += Mathf.Min(Time.unscaledDeltaTime, 1f);
        }

        /// <summary>A loaded game shows where it was left: the hero, the camera, the last report and a pending chest.</summary>
        void Resume(SaveGame game)
        {
            World world = state.World;
            Hero chosen = null;
            int index = game.View.Hero;
            if (index >= 0 && index < state.Heroes.Count && state.Heroes[index].Country == world.Player)
                chosen = state.Heroes[index];
            if (chosen == null)
            {
                List<Hero> mine = state.HeroesOf(world.Player);
                chosen = mine.Count > 0 ? mine[0] : null;
            }
            ChooseHero(chosen, false);
            if (game.View.Distance > 0f)
            {
                mapCamera.Face(game.View.Yaw, true);
                mapCamera.Focus(new Vector2(game.View.X, game.View.Z), game.View.Distance, true);
            }
            else
                FocusCapital(26f, true);
            if (restoring)
                return;
            // Back in the game: its return shows now, or once the players leave the main menu.
            returnWritten = game.Header.Written;
            if (behindTitle)
                returnPending = true;
            else
                ShowReturn();
        }
    }
}
