using System.Collections.Generic;
using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// What the game's own deeds sound like (the sound plan of 2026-11-11, П3 and П4, with its
    /// fal.ai changes of 2026-11-12): the turn's report as a short queue, the builds by their
    /// material, the gems and the crown.
    /// </summary>
    public sealed partial class RealmsGame
    {
        /// <summary>
        /// The turn's end, the income 0.35 s later, then at most two of the turn's events 0.5 s
        /// apart, in the order gem, town level, task, neighbours, growth; a raid's warning always
        /// sounds. The rest of the report stays silent.
        /// </summary>
        void TurnSounds(TurnReport report)
        {
            Sounds.Play(Sfx.TurnEnd);
            float at = 0.35f;
            if (report.Income.Gold > 0)
            {
                Sounds.Later(Sfx.Coins, at);
                at += 0.5f;
            }
            int player = state.World.Player;
            int gems = 0;
            bool level = false, quest = false, rival = false, grew = false, warn = false, deeper = false, defended = false;
            bool reached = false, lost = false;
            foreach (TurnEvent e in report.Events)
            {
                switch (e.Kind)
                {
                    case TurnEventKind.GemSet:
                        if (e.Country == player)
                            gems++;
                        else
                            rival = true;
                        break;
                    case TurnEventKind.LevelUp:
                        level = true;
                        break;
                    case TurnEventKind.QuestDone:
                        quest = true;
                        break;
                    case TurnEventKind.TroopSent:
                    case TurnEventKind.TroopCleared:
                        rival = true;
                        break;
                    case TurnEventKind.Grew:
                        grew = true;
                        break;
                    case TurnEventKind.RaidSent:
                        warn = true;
                        break;
                    case TurnEventKind.RaidArrived:
                        warn = deeper = true;
                        break;
                    case TurnEventKind.RaidRepelled:
                        defended = true;
                        break;
                    case TurnEventKind.StakeReached:
                        reached = true;
                        break;
                    case TurnEventKind.StakeLost:
                        lost = true;
                        break;
                }
            }
            if (warn)
            {
                Sounds.Later(Sfx.Warn, at, 1f, deeper ? -3f : 0f);
                at += 0.9f;
            }
            if (defended)
            {
                Sounds.Later(Sfx.Defend, at);
                Sounds.Later(Sfx.Coins, at + 0.25f);
                at += 0.6f;
            }
            var queue = new List<(Sfx sfx, float semitones)>();
            if (gems > 0)
                queue.Add((Sfx.Gem, Mathf.Min(6f, 2f * (state.Gems[player].Count - 1))));
            if (level)
                queue.Add((Sfx.TownLevel, 0f));
            if (quest)
                queue.Add((Sfx.QuestDone, 0f));
            if (rival)
                queue.Add((Sfx.Rival, 0f));
            if (reached)
                queue.Add((Sfx.Reward, 0f));
            if (grew)
                queue.Add((Sfx.Grew, 0f));
            if (lost)
                queue.Add((Sfx.Back, 0f));
            for (int i = 0; i < queue.Count && i < 2; i++)
            {
                Sounds.Later(queue[i].sfx, at, 1f, queue[i].semitones);
                at += 0.5f;
            }
            Music.TurnEnded();
        }

        /// <summary>Wood knocks for most builds; stone for walls, the castle, quarries and mines.</summary>
        static Sfx BuildSound(World world, int cell, TownBuilding? building)
        {
            if (building is TownBuilding.Walls or TownBuilding.Castle)
                return Sfx.BuildStone;
            if (building == null && world.Cells[cell].Building is FieldBuilding.Quarry or FieldBuilding.GoldMine)
                return Sfx.BuildStone;
            return Sfx.Build;
        }

        /// <summary>A finished task: its short tune and its line.</summary>
        /// <summary>
        /// The tasks a deed finished: the reward's tune once, and each task's line in the message
        /// queue, one after another (only the first showed before, 2026-11-12).
        /// </summary>
        void QuestToasts(List<Quest> finished)
        {
            if (finished.Count == 0)
                return;
            Sounds.Play(Sfx.QuestDone);
            foreach (Quest quest in finished)
                hud.News(Words.QuestDone(quest), 5f);
        }

        /// <summary>The players' new gem: a chime, a little higher for every gem they hold.</summary>
        void GemSound(int count) => Sounds.Play(Sfx.Gem, 1f, Mathf.Min(6f, 2f * (count - 1)));

        /// <summary>The end of the game: the players' crown is a celebration and the theme after it; a neighbour's first crown is a defeat.</summary>
        void CrownSound(int winner)
        {
            if (winner == state.World.Player)
            {
                Sounds.Play(Sfx.GameWon);
                Music.Crowned();
            }
            else
                Sounds.Play(Sfx.Defeat);
        }
    }
}
