using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Realms
{
    /// <summary>
    /// The town and hero screens, the game's two modes besides the map: the camera comes down
    /// close to the town or the hero from a side where no mountain stands between and sways
    /// slowly round it, the land beyond goes out of focus, the map's
    /// panels give way to the screen's own. The town screen builds and hires, the hero screen
    /// shows the hero and his army. A double click, the button on the map's card, T or R opens
    /// one; Esc (or the same key) goes back to the map; Tab and F step to the next town or hero.
    /// </summary>
    public sealed partial class RealmsGame
    {
        enum Mode
        {
            Map,
            Town,
            Hero,
        }

        Mode mode;
        int screenCity = -1;
        ScreenLook look;

        /// <summary>The last left click, to tell a double click.</summary>
        int lastClickCell = -1;
        float lastClickTime = -10f;

        /// <summary>How far right of the middle the subject stands, a share of the half-width: the panel is on the other side.</summary>
        const float TownShare = 0.34f, HeroShare = -0.34f;

        bool OnScreen => mode != Mode.Map;

        void BindScreens()
        {
            look = mapCamera.gameObject.AddComponent<ScreenLook>();
            look.Bind(mapCamera);
            hud.TownScreenPicked += EnterTown;
            hud.HeroScreenPicked += () => EnterHero(hero);
            hud.ScreenClosed += LeaveScreen;
            hud.ScreenStep += StepScreen;
        }

        /// <summary>True when this left click on <paramref name="cell"/> is the second of a double click.</summary>
        bool DoubleClick(int cell)
        {
            bool twice = cell == lastClickCell && Time.unscaledTime - lastClickTime < 0.4f;
            lastClickCell = twice ? -1 : cell;
            lastClickTime = Time.unscaledTime;
            return twice;
        }

        /// <summary>A double click opens the screen of the players' town or hero on the cell.</summary>
        bool OpenScreenAt(int cell)
        {
            World world = state.World;
            Cell c = world.Cells[cell];
            if (c.Town >= 0 && world.Cities[c.Town].Country == world.Player)
            {
                EnterTown(c.Town);
                return true;
            }
            Hero there = state.HeroAt(cell);
            if (there != null && there.Country == world.Player)
            {
                EnterHero(there);
                return true;
            }
            return false;
        }

        /// <summary>T: the selected town, else the town the chosen hero stands in, else the capital.</summary>
        void TownKey()
        {
            World world = state.World;
            Country player = world.Countries[world.Player];
            if (player.Cities.Count == 0)
                return;
            int city = select >= 0 ? world.Cells[select].Town : -1;
            if (city < 0 || world.Cities[city].Country != world.Player)
                city = hero != null && state.TownOf(hero) != null ? state.TownOf(hero).Index : player.Capital;
            EnterTown(city);
        }

        void EnterTown(int index)
        {
            World world = state.World;
            if (busy || index < 0 || world.Cities[index].Country != world.Player)
                return;
            City city = world.Cities[index];
            mode = Mode.Town;
            screenCity = index;
            cityCursor = world.Countries[world.Player].Cities.IndexOf(index);
            QuietMap();
            // Looking at the castle or hall, the quarters pulling the point a little their way (a
            // review asked for the hall in the middle, 2026-11-10; round the centre cell alone an
            // earlier one found the quarters out of the view), at the middle of the houses, not
            // their feet; steeper than the hero's screen, so the land beyond the town fills less
            // of the view; as close as the town fits; the near cells' trees are cut away (from 30° and
            // 6 away they filled the lower 40 %).
            float x = 3f * world.CenterX(city.Cell), z = 3f * world.CenterZ(city.Cell);
            foreach (int district in city.Districts)
            {
                x += world.CenterX(district);
                z += world.CenterZ(district);
            }
            x /= city.Districts.Count + 3;
            z /= city.Districts.Count + 3;
            float reach = new Vector2(world.CenterX(city.Cell) - x, world.CenterZ(city.Cell) - z).magnitude;
            foreach (int district in city.Districts)
                reach = Mathf.Max(reach, new Vector2(world.CenterX(district) - x, world.CenterZ(district) - z).magnitude);
            float distance = Mathf.Clamp(2.4f * (reach + HexMetrics.Radius), 4.4f, 6f);
            HoldOn(new Vector3(x, Mathf.Max(view.Field.Height(x, z), 0f) + 0.4f, z), distance, 3.5f, 11f, 40f, TownShare,
                reach + HexMetrics.Radius, reach + 1.6f);
            look.Focus(new Vector2(0.5f + TownShare * 0.5f, 0.5f));
            hud.ShowTownScreen(index);
        }

        void EnterHero(Hero chosen)
        {
            if (busy || chosen == null || chosen.Country != state.World.Player)
                return;
            if (chosen != hero)
                ChooseHero(chosen, false);
            mode = Mode.Hero;
            screenCity = -1;
            QuietMap();
            // Close enough for the hero to fill about a third of the view's height (from 3.4 the
            // figure was small and the rocks round it filled the view, review of 2026-11-10), the
            // hero turned three quarters towards the camera.
            float facing = HoldOn(heroes.Spot(chosen.Cell) + new Vector3(0f, 0.35f, 0f), 2.5f, 1.8f, 6f, 22f, HeroShare, 0.75f, 1.1f);
            heroes.Face(chosen, facing);
            look.Focus(new Vector2(0.5f + HeroShare * 0.5f, 0.5f));
            hud.ShowHeroScreen(chosen);
        }

        /// <summary>
        /// Holds the camera on a screen's subject from one of the ways 15° apart, at
        /// <paramref name="pitch"/> or the first of a few steeper ones with a clear view (the
        /// mountains, hills and trees between under the line of sight): of the clear ways the
        /// one whose view shows the least sea and unseen land (a town screen came out half sea,
        /// review of 2026-11-10), of two alike the nearer to where the camera faces now. It sways
        /// as far either side as the view stays clear and about as good, up to 30°. The plants
        /// in front of the subject are cut away from <paramref name="clearFrom"/> on, in a band
        /// <paramref name="clearWidth"/> either side (MapCamera.Hold). Returns the way the camera faces.
        /// </summary>
        float HoldOn(Vector3 subject, float distance, float near, float far, float pitch, float share, float clearFrom, float clearWidth)
        {
            float from = mapCamera.Yaw, facing = from, bestPitch = pitch, best = float.MaxValue, leastBlocked = float.MaxValue;
            bool clear = false;
            for (float p = pitch; p <= pitch + 24.5f && !clear; p += 8f)
            {
                for (int k = 0; k < 24; k++)
                {
                    float yaw = from + (k + 1) / 2 * 15f * (k % 2 == 1 ? 1f : -1f);
                    float blocked = Blocked(subject, distance, p, yaw, share, clearFrom);
                    if (blocked > 0f)
                    {
                        if (!clear && blocked < leastBlocked)
                        {
                            leastBlocked = blocked;
                            facing = yaw;
                            bestPitch = p;
                        }
                        continue;
                    }
                    // Turning all the way round weighs as much as a twentieth of the view.
                    float score = Scenery(subject, distance, p, yaw, share) + 0.05f * Mathf.Abs(Mathf.DeltaAngle(from, yaw)) / 180f;
                    if (score < best)
                    {
                        best = score;
                        facing = yaw;
                        bestPitch = p;
                    }
                    clear = true;
                }
            }
            float sway = 0f;
            if (clear)
            {
                float worst = Mathf.Max(best + 0.08f, 0.2f);
                bool Fine(float a) => Blocked(subject, distance, bestPitch, facing + a, share, clearFrom) <= 0f
                    && Scenery(subject, distance, bestPitch, facing + a, share) <= worst;
                for (float a = 30f; a > 5f; a -= 10f)
                {
                    if (Fine(a) && Fine(-a) && Fine(a * 0.5f) && Fine(-a * 0.5f))
                    {
                        sway = a;
                        break;
                    }
                }
            }
            mapCamera.Hold(subject, distance, near, far, bestPitch, share, facing, sway, clearFrom, clearWidth);
            return facing;
        }

        /// <summary>
        /// How far the land and what stands on it rise above the lines of sight of a held camera
        /// (placed as MapCamera.Hold places it, <paramref name="distance"/> away facing
        /// <paramref name="yaw"/>, the subject <paramref name="share"/> of the half-width off the
        /// middle) to <paramref name="subject"/>, to the middle of the view and halfway between;
        /// zero or less is a clear view. The camera stands off to the side of the subject, so the
        /// line straight back from the subject missed a mountain's peak that then filled the hero
        /// screen (2026-11-10). Woods count only nearer than <paramref name="clearFrom"/>: past it
        /// their trees are cut away.
        /// </summary>
        float Blocked(Vector3 subject, float distance, float pitch, float yaw, float share, float clearFrom)
        {
            Camera cam = mapCamera.GetComponent<Camera>();
            float tanH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 middle = subject - rotation * Vector3.right * (share * distance * tanH);
            Vector3 eye = middle - rotation * Vector3.forward * distance;
            float worst = float.MinValue;
            for (int line = 0; line < 3; line++)
            {
                Vector3 to = Vector3.Lerp(subject, middle, line * 0.5f);
                for (int i = 2; i <= 12; i++)
                {
                    Vector3 p = Vector3.Lerp(to, eye, i / 12f);
                    float along = new Vector2(p.x - subject.x, p.z - subject.z).magnitude;
                    worst = Mathf.Max(worst, Standing(p.x, p.z, along < clearFrom + 0.6f) - p.y);
                }
            }
            return worst;
        }

        /// <summary>
        /// The share of a held camera's view (placed as MapCamera.Hold places it) that falls on the
        /// sea, on land the players have not seen or off the map; sampled on a grid, the panel's
        /// side too.
        /// </summary>
        float Scenery(Vector3 subject, float distance, float pitch, float yaw, float share)
        {
            const int columns = 16, rows = 9;
            Camera cam = mapCamera.GetComponent<Camera>();
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), tanH = tanV * cam.aspect;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 eye = subject - rotation * Vector3.right * (share * distance * tanH) - rotation * Vector3.forward * distance;
            World world = state.World;
            int unwanted = 0;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < columns; i++)
                {
                    Vector3 ray = rotation * new Vector3(((i + 0.5f) / columns * 2f - 1f) * tanH, ((j + 0.5f) / rows * 2f - 1f) * tanV, 1f);
                    int cell = ray.y < -0.001f ? view.CellAt(eye + ray * (-eye.y / ray.y)) : -1;
                    if (cell < 0 || !state.Seen[cell] || world.Cells[cell].Biome <= Biome.Shallows)
                        unwanted++;
                }
            }
            return unwanted / (float)(columns * rows);
        }

        /// <summary>
        /// The height of the ground at a point with what stands there: peaks round a mountain's
        /// pass, trees (fewer without the woods' trees, see <see cref="Blocked"/>), houses.
        /// </summary>
        float Standing(float x, float z, bool trees)
        {
            World world = state.World;
            float ground = Mathf.Max(view.Field.Height(x, z), 0f);
            int index = view.CellAt(new Vector3(x, 0f, z));
            if (index < 0)
                return ground;
            Cell cell = world.Cells[index];
            float fromMiddle = new Vector2(x - world.CenterX(index), z - world.CenterZ(index)).magnitude;
            return cell.Biome switch
            {
                Biome.Mountains => ground + (fromMiddle > 0.3f ? 1f : 0.2f),
                Biome.Forest => ground + (trees ? 0.55f : 0.3f),
                Biome.Hills => ground + 0.4f,
                _ => ground + 0.3f,
            };
        }

        /// <summary>On a screen the map shows no selection, pointer ring or way.</summary>
        void QuietMap()
        {
            hover = -1;
            view.SetHover(-1);
            view.SetSelect(-1);
            heroes.ShowPath(null, null);
        }

        void LeaveScreen()
        {
            if (!OnScreen)
                return;
            mode = Mode.Map;
            screenCity = -1;
            mapCamera.Release();
            look.Clear();
            hud.CloseScreen();
            heroes.ShowPath(hero, path);
            hud.ShowHero(hero, path);
            Select(select);
        }

        /// <summary>The next (+1) or previous (−1) town or hero on the same screen.</summary>
        void StepScreen(int step)
        {
            World world = state.World;
            if (mode == Mode.Town)
            {
                List<int> cities = world.Countries[world.Player].Cities;
                int k = cities.IndexOf(screenCity);
                EnterTown(cities[((k + step) % cities.Count + cities.Count) % cities.Count]);
            }
            else if (mode == Mode.Hero)
            {
                List<Hero> mine = state.HeroesOf(world.Player);
                if (mine.Count == 0)
                    return;
                int k = mine.IndexOf(hero);
                EnterHero(mine[((k + step) % mine.Count + mine.Count) % mine.Count]);
            }
        }

        /// <summary>The keys while a screen is open; the map's own keys wait.</summary>
        void ScreenKeys(Keyboard keyboard, bool help)
        {
            if (keyboard.f1Key.wasPressedThisFrame || keyboard.hKey.wasPressedThisFrame)
                hud.ToggleHelp(!help);
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (help)
                    hud.ToggleHelp(false);
                else
                    LeaveScreen();
            }
            else if (help || busy)
                return;
            else if ((mode == Mode.Town && keyboard.tKey.wasPressedThisFrame) || (mode == Mode.Hero && keyboard.rKey.wasPressedThisFrame))
                LeaveScreen();
            else if (mode == Mode.Hero && keyboard.tKey.wasPressedThisFrame)
                TownKey();
            else if (mode == Mode.Town && keyboard.rKey.wasPressedThisFrame && hero != null)
                EnterHero(hero);
            else if (mode == Mode.Town && keyboard.tabKey.wasPressedThisFrame)
                StepScreen(1);
            else if (keyboard.backspaceKey.wasPressedThisFrame)
                mapCamera.FaceDefault();
            else if (keyboard.fKey.wasPressedThisFrame)
            {
                if (mode == Mode.Hero)
                    StepScreen(1);
                else
                    NextHero();
            }
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                LeaveScreen();
                EndTurn();
            }
        }
    }
}
