using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The saves (the interface plan's 3.1, 2026-11-11).</summary>
    public class SaveTests
    {
        static readonly int[] Seeds = { 1, 7, 42 };

        /// <summary>The players' turn as the measurer's bot plays it, then the turn's end.</summary>
        static void BotTurn(GameState state)
        {
            int player = state.World.Player;
            Rival.PlayATurn(state, player);
            Rival.Arm(state, player);
            Rival.Hire(state, player);
            foreach (Hero hero in state.HeroesOf(player))
                Rival.HeroTurn(state, hero);
            state.EndTurn();
        }

        static GameState Played(int seed, int turns)
        {
            var state = new GameState(WorldGen.Generate(seed));
            for (int t = 0; t < turns; t++)
                BotTurn(state);
            return state;
        }

        static SaveGame Saved(GameState state) => new SaveGame
        {
            State = state,
            Header = { GameId = 123456789012L, Kind = SaveKind.Manual, Written = new DateTime(2026, 11, 11, 15, 30, 0, DateTimeKind.Utc), Played = 3725, Version = "test" },
            View = new SaveView { Hero = 0, X = 3.5f, Z = -2.25f, Distance = 26f, Yaw = 90f },
        };

        [Test]
        public void ALoadedGameIsTheSavedOneAndPlaysOnAlike()
        {
            foreach (int seed in Seeds)
            {
                GameState state = Played(seed, 20);
                GameState loaded = Save.FromBytes(Save.ToBytes(Saved(state))).State;
                Same(state, loaded, $"seed {seed}");
                for (int t = 0; t < 10; t++)
                {
                    BotTurn(state);
                    BotTurn(loaded);
                }
                Same(state, loaded, $"seed {seed}, ten turns later");
                Assert.AreEqual(31, loaded.Turn);
            }
        }

        [Test]
        public void ASaveWrittenAgainAfterReadingIsTheSameBytes()
        {
            GameState state = Played(3, 25);
            TurnReport report = state.EndTurn();
            SaveGame save = Saved(state);
            save.Report = report;
            byte[] first = Save.ToBytes(save);
            SaveGame read = Save.FromBytes(first);
            CollectionAssert.AreEqual(first, Save.ToBytes(read));

            SaveHeader header = read.Header;
            Country player = state.World.Countries[state.World.Player];
            Assert.AreEqual(Save.Format, header.Format);
            Assert.AreEqual(state.Turn, header.Turn);
            Assert.AreEqual(player.Name, header.Kingdom);
            Assert.AreEqual(player.Flag.Code, header.Flag);
            Assert.AreEqual(3, header.Seed);
            Assert.AreEqual(123456789012L, header.GameId);
            Assert.AreEqual(SaveKind.Manual, header.Kind);
            Assert.AreEqual(new DateTime(2026, 11, 11, 15, 30, 0, DateTimeKind.Utc), header.Written);
            Assert.AreEqual(3725, header.Played);
            Assert.AreEqual(26f, read.View.Distance);
            Assert.AreEqual(report.Events.Count, read.Report.Events.Count);
            Same(report.Events, read.Report.Events, "the report");

            using (var stream = new System.IO.MemoryStream(first))
                Assert.AreEqual(state.Turn, Save.ReadHeader(stream).Turn);
        }

        [Test]
        public void AFreshGameSavesWithoutAReport()
        {
            var state = new GameState(WorldGen.Generate(5));
            SaveGame read = Save.FromBytes(Save.ToBytes(new SaveGame { State = state }));
            Assert.IsNull(read.Report);
            Assert.AreEqual(-1, read.View.Hero);
            Same(state, read.State, "a fresh game");
        }

        /// <summary>
        /// The census: every field of the saved classes has its line in <see cref="Save.Fields"/>,
        /// and every class the state holds is among them. A field added without a place in the save
        /// fails here.
        /// </summary>
        [Test]
        public void TheSaveHoldsEveryFieldOfTheGame()
        {
            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (KeyValuePair<Type, string> kind in Save.Fields)
            {
                var listed = kind.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).OrderBy(f => f).ToArray();
                var fields = kind.Key.GetFields(Instance).Select(f => f.Name).OrderBy(f => f).ToArray();
                CollectionAssert.AreEqual(fields, listed, $"{kind.Key.Name}: the fields and the save's list differ");
            }

            var seen = new HashSet<Type>();
            var queue = new Queue<Type>();
            queue.Enqueue(typeof(GameState));
            while (queue.Count > 0)
            {
                Type type = queue.Dequeue();
                if (!seen.Add(type))
                    continue;
                foreach (FieldInfo field in type.GetFields(Instance))
                {
                    foreach (Type held in Held(field.FieldType))
                    {
                        if (held.Namespace != typeof(GameState).Namespace || held.IsEnum || held == typeof(Hex))
                            continue;
                        Assert.IsTrue(Save.Fields.ContainsKey(held), $"{type.Name}.{field.Name} holds a {held.Name}, which the save does not know");
                        queue.Enqueue(held);
                    }
                }
            }
        }

        /// <summary>The type and, for arrays and generic lists and dictionaries, the types they hold.</summary>
        static IEnumerable<Type> Held(Type type)
        {
            if (type.IsArray)
            {
                foreach (Type t in Held(type.GetElementType()))
                    yield return t;
                yield break;
            }
            if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                {
                    foreach (Type t in Held(argument))
                        yield return t;
                }
                yield break;
            }
            yield return type;
        }

        [Test]
        public void ACutOrForeignFileSaysWhyInsteadOfFailing()
        {
            byte[] bytes = Save.ToBytes(Saved(Played(2, 5)));

            Problem(SaveProblem.Foreign, new byte[0]);
            Problem(SaveProblem.Foreign, System.Text.Encoding.UTF8.GetBytes("Это не сохранение, а просто текст, довольно длинный."));
            foreach (int length in new[] { 10, 20, 40, bytes.Length / 2, bytes.Length - 3 })
                Problem(SaveProblem.Damaged, bytes.Take(length).ToArray());

            byte[] flipped = (byte[])bytes.Clone();
            flipped[bytes.Length / 2] ^= 0x10;
            Problem(SaveProblem.Damaged, flipped);

            byte[] newer = (byte[])bytes.Clone();
            BitConverter.GetBytes(Save.Format + 1).CopyTo(newer, 8);
            Problem(SaveProblem.Newer, newer);
            byte[] older = (byte[])bytes.Clone();
            BitConverter.GetBytes(Save.OldestFormat - 1).CopyTo(older, 8);
            Problem(SaveProblem.Old, older);
        }

        static void Problem(SaveProblem expected, byte[] bytes)
        {
            var e = Assert.Throws<SaveException>(() => Save.FromBytes(bytes), $"{bytes.Length} bytes");
            Assert.AreEqual(expected, e.Problem, $"{bytes.Length} bytes: {e.Message}");
        }

        /// <summary>Two values are equal field by field, down through lists, arrays and dictionaries.</summary>
        static void Same(object a, object b, string path)
        {
            if (a == null || b == null)
            {
                Assert.AreEqual(a == null, b == null, $"{path}: one is null");
                return;
            }
            Type type = a.GetType();
            Assert.AreEqual(type, b.GetType(), $"{path}: the types differ");
            if (type.IsPrimitive || type.IsEnum || a is string || a is DateTime)
            {
                Assert.AreEqual(a, b, path);
                return;
            }
            if (a is IDictionary da)
            {
                var db = (IDictionary)b;
                Assert.AreEqual(da.Count, db.Count, $"{path}: count");
                foreach (object key in da.Keys)
                {
                    Assert.IsTrue(db.Contains(key), $"{path}[{key}] is missing");
                    Same(da[key], db[key], $"{path}[{key}]");
                }
                return;
            }
            if (a is IList la)
            {
                var lb = (IList)b;
                Assert.AreEqual(la.Count, lb.Count, $"{path}: count");
                for (int i = 0; i < la.Count; i++)
                    Same(la[i], lb[i], $"{path}[{i}]");
                return;
            }
            for (Type t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    Same(field.GetValue(a), field.GetValue(b), $"{path}.{field.Name}");
            }
        }
    }
}
