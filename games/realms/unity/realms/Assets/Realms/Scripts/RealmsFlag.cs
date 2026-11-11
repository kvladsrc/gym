using System.Collections;
using Realms.Core;
using UnityEngine;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The players' own flag: drawn in the editor when a game starts and on a click on the
    /// kingdom's shield, flown at once by every banner and shield of the kingdom, and kept for the
    /// next games. The flag's field is the kingdom's colour on the map.
    /// </summary>
    public sealed partial class RealmsGame
    {
        const string FlagKey = "Realms.Flag";

        /// <summary>The open editor's flag (for the captures).</summary>
        Hud.FlagDraft flagDraft;

        /// <summary>
        /// The players' kingdom takes the flag they drew in an earlier game. The captures keep the
        /// generated flags, so their pictures do not depend on the players' games.
        /// </summary>
        void LoadFlag(World world)
        {
            if (captureDir == null && Flag.TryParse(PlayerPrefs.GetString(FlagKey, ""), out Flag flag))
                Flags.Claim(world, world.Player, flag);
        }

        void FlagPicked()
        {
            if (!busy && !hud.Modal)
            {
                hud.ShowMenu(false);
                StartCoroutine(EditFlag(false));
            }
        }

        /// <summary>
        /// The editor with the kingdom's flag. At the start of a game (<paramref name="start"/>)
        /// it has only the button that raises the flag.
        /// </summary>
        IEnumerator EditFlag(bool start)
        {
            busy = true;
            Country country = state.World.Countries[state.World.Player];
            Hud.FlagDraft draft = null;
            int choice = 1;
            string text = start
                ? T("Собери флаг своего королевства: узор, цвета и знак. Цвет поля станет цветом королевства на карте и даст ему имя.", "Make your kingdom's flag: a pattern, colours and an emblem. The field's colour becomes the kingdom's colour on the map and gives it its name.")
                : T("Собери свой флаг: узор, цвета и знак. Цвет поля — цвет королевства на карте и его имя.", "Make your flag: a pattern, colours and an emblem. The field's colour is the kingdom's colour on the map and its name.");
            string[] buttons = start ? new[] { T("Поднять флаг", "Raise the flag") } : new[] { T("Поднять флаг", "Raise the flag"), T("Отмена", "Cancel") };
            yield return AskAndWait(T("Флаг королевства", "Kingdom flag"), null, text, buttons, k => choice = k,
                body => flagDraft = draft = hud.FlagEditor(body, country.Flag));
            busy = false;
            if (choice == 0 && draft != null && !draft.Flag.Equals(country.Flag))
                SetFlag(draft.Flag, !start);
        }

        /// <summary>
        /// The players' kingdom flies the flag: the map's cloth, the hero's portrait and the shields
        /// follow. A new field recolours the kingdom (and the one whose colour it took), so the
        /// map is drawn again. <paramref name="tell"/> says so in a toast.
        /// </summary>
        void SetFlag(Flag flag, bool tell = true)
        {
            World world = state.World;
            int other = -1;
            foreach (Country country in world.Countries)
            {
                if (country.Index != world.Player && country.Flag.Field == flag.Field)
                    other = country.Index;
            }
            bool recoloured = Flags.Claim(world, world.Player, flag);
            if (captureDir == null)
            {
                PlayerPrefs.SetString(FlagKey, flag.Code);
                PlayerPrefs.Save();
            }
            FlagArt.Refresh();
            RenderPortraits();
            if (recoloured)
                RedrawMap();
            hud.RefreshFlags();
            hud.ShowHero(hero, path);
            Country player = world.Countries[world.Player];
            if (!tell)
                return;
            if (other >= 0)
                hud.Toast(T($"Теперь ты правишь: {player.Label}. Соседи взяли твой прежний цвет: {world.Countries[other].Label}", $"Now you rule: {player.Label}. The neighbours took your old colour: {world.Countries[other].Label}"), 5f);
            else if (recoloured)
                hud.Toast(T($"Теперь ты правишь: {player.Label}", $"Now you rule: {player.Label}"), 4f);
            else
                hud.Toast(T($"Новый флаг поднят над городами: {player.Label}", $"A new flag flies over the towns: {player.Label}"), 4f);
        }

        /// <summary>The units' and the hero's pictures; the hero's carries the kingdom's flag.</summary>
        void RenderPortraits()
        {
            portraits?.Dispose();
            portraits = Portraits.Render(art, view.propMaterial, state.World.Player);
            hud.portraits = portraits;
            battleView.portraits = portraits;
        }
    }
}
