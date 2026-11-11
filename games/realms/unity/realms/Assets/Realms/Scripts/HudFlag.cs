using System;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The flag editor in a dialog's body: the flag large and as a shield on the left, and on the
    /// right the patterns (drawn in the chosen colours), a row of colours each for the field, the
    /// second colour and the emblem, and the emblems on the field's colour. Every click shows the
    /// new flag at once; the dialog's buttons keep it or not.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The flag in the open editor; <see cref="Set"/> changes it as a click would.</summary>
        public sealed class FlagDraft
        {
            public Flag Flag;
            internal Action Changed;

            public void Set(Flag flag)
            {
                Flag = flag;
                Changed?.Invoke();
            }
        }

        /// <summary>Raised by a click on the kingdom's shield in the top bar.</summary>
        public event Action FlagPicked;

        static Texture2D flagPreview, flagShield;
        static Texture2D[] patternTiles;

        /// <summary>The editor for <paramref name="start"/> in the dialog's body, made wide for it.</summary>
        public FlagDraft FlagEditor(VisualElement body, Flag start)
        {
            modalCard.AddToClassList("modal-wide");
            var draft = new FlagDraft { Flag = start };
            flagPreview ??= FlagArt.NewTexture(330, 220, "preview");
            flagShield ??= FlagArt.NewTexture(92, 104, "shield");
            if (patternTiles == null)
            {
                patternTiles = new Texture2D[Flags.Patterns];
                for (int k = 0; k < patternTiles.Length; k++)
                    patternTiles[k] = FlagArt.NewTexture(66, 44, "pattern " + k);
            }

            VisualElement editor = Child(body, "flag-editor");
            VisualElement left = Child(editor, "flag-left");
            VisualElement preview = Child(left, "flag-preview");
            preview.style.backgroundImage = new StyleBackground(flagPreview);
            Label kingdom = Text(left, "flag-kingdom", "", bold);
            Label taken = Text(left, "flag-taken", "", regular);
            VisualElement shieldRow = Child(left, "flag-shield-row");
            VisualElement shieldView = Child(shieldRow, "flag-shield");
            shieldView.style.backgroundImage = new StyleBackground(flagShield);
            Text(shieldRow, "flag-note", T("Так флаг выглядит на щитах. На карте он развевается над городами и у героев.", "This is how the flag looks on shields. On the map it flies over towns and by heroes."), regular);
            Button random = Btn(left, T("Случайный флаг", "Random flag"), "button", () =>
                draft.Set(Flags.Generate(UnityEngine.Random.Range(0, 1 << 20), UnityEngine.Random.Range(0, Flags.KingdomTincture.Length))));
            random.AddToClassList("flag-random");

            VisualElement right = Child(editor, "flag-right");
            Text(right, "flag-head", T("Узор", "Pattern"), bold);
            VisualElement patterns = Child(right, "flag-tiles");
            var patternViews = new VisualElement[Flags.Patterns];
            for (int k = 0; k < Flags.Patterns; k++)
            {
                var pattern = (FlagPattern)k;
                VisualElement tile = Child(patterns, "flag-pattern");
                tile.tooltip = FlagArt.PatternNames[k];
                tile.style.backgroundImage = new StyleBackground(patternTiles[k]);
                OnPress(tile, () =>
                {
                    Flag f = draft.Flag;
                    f.Pattern = pattern;
                    draft.Set(f);
                });
                patternViews[k] = tile;
            }

            Text(right, "flag-head", T("Цвета", "Colours"), bold);
            string[] rows = { T("Поле", "Field"), T("Второй", "Second"), T("Знак", "Emblem") };
            var swatches = new VisualElement[rows.Length, Flags.Tinctures];
            for (int row = 0; row < rows.Length; row++)
            {
                VisualElement line = Child(right, "flag-colours");
                Text(line, "flag-colour-label", rows[row], bold);
                for (int t = 0; t < Flags.Tinctures; t++)
                {
                    int part = row, tincture = t;
                    VisualElement swatch = Child(line, "flag-swatch");
                    swatch.style.backgroundColor = FlagArt.Tinctures[t];
                    string owner = row == 0 ? FieldOwner(t) : null;
                    swatch.tooltip = owner == null ? FlagArt.TinctureNames[t] : T($"{FlagArt.TinctureNames[t]}: цвет соседей ({owner})", $"{FlagArt.TinctureNames[t]}: a neighbour's colour ({owner})");
                    OnPress(swatch, () =>
                    {
                        Flag f = draft.Flag;
                        if (part == 0)
                            f.Field = (byte)tincture;
                        else if (part == 1)
                            f.Second = (byte)tincture;
                        else
                            f.EmblemColour = (byte)tincture;
                        draft.Set(f);
                    });
                    swatches[row, t] = swatch;
                }
            }

            Text(right, "flag-head", T("Знак", "Emblem"), bold);
            VisualElement emblems = Child(right, "flag-tiles");
            var emblemViews = new VisualElement[Flags.Emblems];
            var emblemIcons = new VisualElement[Flags.Emblems];
            for (int k = 0; k < Flags.Emblems; k++)
            {
                var emblem = (Emblem)k;
                VisualElement tile = Child(emblems, "flag-emblem");
                tile.tooltip = FlagArt.EmblemNames[k];
                if (emblem == Emblem.None)
                    Text(tile, "flag-emblem-none", T("нет", "none"), bold).pickingMode = PickingMode.Ignore;
                else
                {
                    VisualElement icon = Child(tile, "flag-emblem-icon");
                    icon.pickingMode = PickingMode.Ignore;
                    Texture2D texture = IconTexture(FlagArt.EmblemIcon(emblem));
                    if (texture != null)
                        icon.style.backgroundImage = new StyleBackground(texture);
                    emblemIcons[k] = icon;
                }
                OnPress(tile, () =>
                {
                    Flag f = draft.Flag;
                    f.Emblem = emblem;
                    draft.Set(f);
                });
                emblemViews[k] = tile;
            }

            Flag shown = default;
            bool first = true;
            draft.Changed = () =>
            {
                Flag f = draft.Flag;
                FlagArt.PaintInto(flagPreview, f, FlagArt.Shape.Flag);
                FlagArt.PaintInto(flagShield, f, FlagArt.Shape.Shield);
                if (first || f.Field != shown.Field || f.Second != shown.Second)
                {
                    for (int k = 0; k < Flags.Patterns; k++)
                        FlagArt.PaintInto(patternTiles[k], new Flag((FlagPattern)k, f.Field, f.Second, Emblem.None, f.EmblemColour), FlagArt.Shape.Flag);
                }
                // The textures are painted over in place, so their elements are drawn again.
                for (int k = 0; k < Flags.Patterns; k++)
                {
                    patternViews[k].EnableInClassList("flag-picked", (int)f.Pattern == k);
                    patternViews[k].MarkDirtyRepaint();
                }
                for (int row = 0; row < rows.Length; row++)
                {
                    int picked = row == 0 ? f.Field : row == 1 ? f.Second : f.EmblemColour;
                    for (int t = 0; t < Flags.Tinctures; t++)
                        swatches[row, t].EnableInClassList("flag-picked", t == picked);
                }
                for (int k = 0; k < Flags.Emblems; k++)
                {
                    emblemViews[k].style.backgroundColor = FlagArt.Tinctures[f.Field];
                    emblemViews[k].EnableInClassList("flag-picked", (int)f.Emblem == k);
                    if (emblemIcons[k] != null)
                        emblemIcons[k].style.unityBackgroundImageTintColor = FlagArt.Tinctures[f.EmblemColour];
                }
                kingdom.text = Names.Shown(Names.Kingdoms[f.Field]);
                string owner = FieldOwner(f.Field);
                taken.text = owner == null ? "" : T($"Этот цвет у соседей ({owner}): они возьмут твой прежний.", $"The neighbours have this colour ({owner}): they will take your old one.");
                taken.style.display = owner == null ? DisplayStyle.None : DisplayStyle.Flex;
                preview.MarkDirtyRepaint();
                shieldView.MarkDirtyRepaint();
                shown = f;
                first = false;
            };
            draft.Changed();
            return draft;
        }

        /// <summary>The name of the other kingdom whose field is <paramref name="tincture"/>, or null.</summary>
        string FieldOwner(int tincture)
        {
            if (state == null)
                return null;
            foreach (Country country in state.World.Countries)
            {
                if (country.Index != state.World.Player && country.Flag.Field == tincture)
                    return country.Label;
            }
            return null;
        }

        /// <summary>Shows the kingdoms' new flags on the shields: the top bar, the kingdoms, the towns' plates and the card.</summary>
        public void RefreshFlags()
        {
            if (state == null)
                return;
            SetIcon(shield, FlagIcon(state.World.Player));
            BuildKingdoms();
            BuildLabels();
            RefreshInfo();
        }
    }
}
