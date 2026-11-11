using System.Collections;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// Settlers and new towns: a town gathers a settlers' cart (the town screen's hiring tab), a
    /// hero leads it, and the button on his card founds a village where he stands under the name
    /// the players give it. The map is then drawn again round it, as the ground levels under the
    /// new town, and the new town's screen opens.
    /// </summary>
    public sealed partial class RealmsGame
    {
        void GatherSettlers(int city)
        {
            Refusal refusal = state.GatherSettlers(state.World.Player, city);
            if (refusal != Refusal.None)
            {
                hud.Toast(Words.Refusal(refusal));
                return;
            }
            Hero inTown = state.HeroAt(state.World.Cities[city].Cell);
            hud.Toast(inTown != null && inTown.Settlers && !state.World.Cities[city].Settlers
                ? T($"Обоз поселенцев пойдёт с героем {inTown.Label}", $"The settlers' wagon will go with {inTown.Label}") : T("Обоз поселенцев ждёт героя в городе", "The settlers' wagon waits for a hero in town"), 4f);
            heroes.Refresh();
            hud.Refresh();
            hud.RefreshInfo();
            hud.ShowHero(hero, path);
        }

        void FoundPicked()
        {
            if (!busy && hero != null)
                StartCoroutine(Found(hero));
        }

        /// <summary>The players name the new town (a free name is offered), then it stands.</summary>
        IEnumerator Found(Hero founder)
        {
            Refusal can = state.CanFound(founder);
            if (can != Refusal.None)
            {
                hud.Toast(Words.Refusal(can), 4f);
                yield break;
            }
            busy = true;
            // The offered name is shown in the game's language; kept as offered, the town takes the
            // name's key, so it reads in either language (in English the field offered the Russian
            // key, 2026-11-11).
            string suggestion = Settling.NewName(state.World), shown = NameWords.Shown(suggestion);
            TextField field = null;
            int choice = 1;
            yield return AskAndWait(T("Новый город", "New town"), "icon_cart", T("Поселенцы готовы строить. Как назовём город?", "The settlers are ready to build. What shall we call the town?"),
                new[] { T("Основать", "Found"), T("Отмена", "Cancel") }, k => choice = k, body => field = hud.NameField(body, shown));
            int[] before = Marks();
            string typed = field != null ? field.value : shown;
            if (choice != 0 || state.Found(founder, typed.Trim() == shown ? suggestion : typed) != Refusal.None)
            {
                busy = false;
                yield break;
            }
            City town = state.World.Cities[state.World.Cities.Count - 1];
            var watch = System.Diagnostics.Stopwatch.StartNew();
            RedrawMap(ChangedSince(before));
            hud.Toast(T($"Основан город {town.Label}!", $"{town.Label} is founded!"), 5f);
            AfterMove(true);
            busy = false;
            EnterTown(town.Index);
            Debug.Log($"Town founded and shown in {watch.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// A town grows onto the empty cell of the kingdom's beside it: a new quarter with six
        /// places. The map is drawn again round it, as the ground levels under it and its trees go.
        /// </summary>
        void ExpandTown(int cell)
        {
            if (busy)
                return;
            World world = state.World;
            int[] before = Marks();
            Refusal refusal = state.Expand(world.Player, cell);
            if (refusal != Refusal.None)
            {
                hud.Toast(Words.Refusal(refusal, state.Treasury[world.Player].Missing(Towns.DistrictCost)));
                return;
            }
            City town = world.Cities[world.Cells[cell].District];
            var watch = System.Diagnostics.Stopwatch.StartNew();
            RedrawMap(ChangedSince(before));
            if (mode == Mode.Town)
            {
                // Grown from the town's screen: the screen stays, framing the town with its new
                // quarter (the map's rings stay off); back on the map the quarter is chosen.
                select = cell;
                EnterTown(town.Index);
            }
            else
                Select(cell);
            hud.Refresh();
            hud.RefreshInfo();
            hud.Toast(T($"Город {town.Label} вырос на новую клетку: в квартале {Towns.DistrictPlaces} мест", $"{town.Label} has grown onto a new tile: {Towns.DistrictPlaces} places in the quarter"), 5f);
            Debug.Log($"Town grown and shown in {watch.ElapsedMilliseconds} ms");
            CrownAfterDeed();
        }

        /// <summary>
        /// The map drawn again for the world as it is now, keeping the fog, the heroes and the
        /// chosen one: a new town levels the ground and clears the trees around it. Given the
        /// <paramref name="changed"/> cells, only the land round them is drawn again
        /// (<see cref="MapView.Reshape"/>) and the fog clears slowly off the cells seen anew; the
        /// whole map drawn again made the game hang for seconds as a town was founded or grew
        /// (the players found it, 2026-11-11).
        /// </summary>
        void RedrawMap(System.Collections.Generic.List<int> changed = null)
        {
            // Timed into the log, with the frames after it.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (changed == null)
            {
                heroes.Clear();
                view.Show(state.World, art);
            }
            else
                view.Reshape(changed);
            long map = watch.ElapsedMilliseconds;
            view.SetSeen(state.Seen, changed == null);
            if (changed == null)
                heroes.Show(state, view, art, view.propMaterial);
            else
                heroes.Refresh();
            heroes.Choose(hero);
            long shown = watch.ElapsedMilliseconds;
            hud.Bind(state, view, art, mapCamera);
            hud.ShowHero(hero, path);
            Debug.Log($"Map redrawn in {watch.ElapsedMilliseconds} ms: the map {map}, the fog and heroes {shown - map}, the interface {watch.ElapsedMilliseconds - shown}");
            StartCoroutine(LogFrames("after the redraw", 3));
        }

        /// <summary>
        /// What the map shows of each cell that an action can change, eight numbers a cell: its
        /// owner, town, quarter, roads, resource, monsters, place and building.
        /// </summary>
        int[] Marks()
        {
            Cell[] cells = state.World.Cells;
            var marks = new int[cells.Length * 8];
            for (int i = 0; i < cells.Length; i++)
            {
                Cell c = cells[i];
                int k = i * 8;
                marks[k] = c.Owner;
                marks[k + 1] = c.City;
                marks[k + 2] = c.District;
                marks[k + 3] = c.Roads;
                marks[k + 4] = (int)c.Resource;
                marks[k + 5] = (int)c.Monster;
                marks[k + 6] = (int)c.Place;
                marks[k + 7] = (int)c.Building;
            }
            return marks;
        }

        /// <summary>The cells whose <see cref="Marks"/> differ from <paramref name="before"/>.</summary>
        System.Collections.Generic.List<int> ChangedSince(int[] before)
        {
            int[] now = Marks();
            var changed = new System.Collections.Generic.List<int>();
            for (int i = 0; i < now.Length; i += 8)
            {
                for (int k = i; k < i + 8; k++)
                {
                    if (now[k] != before[k])
                    {
                        changed.Add(i / 8);
                        break;
                    }
                }
            }
            return changed;
        }

        /// <summary>Logs how long the next few frames take, as the new meshes and textures go to the graphics card.</summary>
        static IEnumerator LogFrames(string what, int count)
        {
            var times = new System.Text.StringBuilder($"Frames {what}, ms:");
            for (int k = 0; k < count; k++)
            {
                yield return null;
                times.Append(' ').Append(Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f));
            }
            Debug.Log(times.ToString());
        }
    }
}
