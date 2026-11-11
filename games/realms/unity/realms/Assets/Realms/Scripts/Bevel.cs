using UnityEngine;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// The menu's buttons drawn as the players' reference page draws them (2026-11-11): an
    /// underlay with cut corners, on it a face a few pixels shorter, the face a rim with a darker
    /// plate inside it, and a soft shadow under the whole. The key hints, the languages' strip
    /// and the version's plate are the same shape without the underlay. It is drawn with vectors,
    /// so its corners stay sharp at any size of the interface, and the style sheet gives its
    /// colours and sizes:
    /// --bevel-face and --bevel-face-low, the plate's colour at its top and its bottom (one
    /// colour without the second); --bevel-rim and --bevel-rim-low, the same for the rim;
    /// --bevel-side, the underlay; --bevel-cut, the corners, which --bevel-cut-left and
    /// --bevel-cut-right change for one end; --bevel-depth, how much of the underlay shows under
    /// the face; --bevel-rim-width; --bevel-light, a thin line of light along the plate's top;
    /// --bevel-shade and --bevel-shade-size, the dark that gathers along its bottom, and how far up;
    /// --bevel-shadow, --bevel-shadow-drop and --bevel-shadow-blur, the shadow's colour, how far
    /// down it falls and how soft its edge is.
    /// The element's bottom padding should be its depth, so its content sits on the face.
    ///
    /// The reference moves its buttons with transitions, each with its own time and curve, and a
    /// style sheet does not ease custom properties, so these are eased here, on the view's clock:
    /// --bevel-grow, how far the plate widens to each side (its side margins go that far below
    /// zero, so the content follows: a caption stays in the middle, a key rides the end), in
    /// --bevel-time; --bevel-sink, how far the face goes down onto its underlay, the children
    /// with it, while the element itself keeps its place under the pointer, in --bevel-press-time;
    /// the shadow in --bevel-shadow-time; the rim's colour in --bevel-rim-time; and --bevel-ink,
    /// a colour for the children's text, in --bevel-ink-time. Without its time a change shows at once.
    /// </summary>
    public static class Bevel
    {
        static readonly CustomStyleProperty<Color> FaceProperty = new CustomStyleProperty<Color>("--bevel-face");
        static readonly CustomStyleProperty<Color> FaceLowProperty = new CustomStyleProperty<Color>("--bevel-face-low");
        static readonly CustomStyleProperty<Color> RimProperty = new CustomStyleProperty<Color>("--bevel-rim");
        static readonly CustomStyleProperty<Color> RimLowProperty = new CustomStyleProperty<Color>("--bevel-rim-low");
        static readonly CustomStyleProperty<Color> SideProperty = new CustomStyleProperty<Color>("--bevel-side");
        static readonly CustomStyleProperty<float> CutProperty = new CustomStyleProperty<float>("--bevel-cut");
        static readonly CustomStyleProperty<float> CutLeftProperty = new CustomStyleProperty<float>("--bevel-cut-left");
        static readonly CustomStyleProperty<float> CutRightProperty = new CustomStyleProperty<float>("--bevel-cut-right");
        static readonly CustomStyleProperty<float> DepthProperty = new CustomStyleProperty<float>("--bevel-depth");
        static readonly CustomStyleProperty<float> RimWidthProperty = new CustomStyleProperty<float>("--bevel-rim-width");
        static readonly CustomStyleProperty<Color> LightProperty = new CustomStyleProperty<Color>("--bevel-light");
        static readonly CustomStyleProperty<Color> ShadeProperty = new CustomStyleProperty<Color>("--bevel-shade");
        static readonly CustomStyleProperty<float> ShadeSizeProperty = new CustomStyleProperty<float>("--bevel-shade-size");
        static readonly CustomStyleProperty<Color> ShadowProperty = new CustomStyleProperty<Color>("--bevel-shadow");
        static readonly CustomStyleProperty<float> ShadowDropProperty = new CustomStyleProperty<float>("--bevel-shadow-drop");
        static readonly CustomStyleProperty<float> ShadowBlurProperty = new CustomStyleProperty<float>("--bevel-shadow-blur");
        static readonly CustomStyleProperty<float> GrowProperty = new CustomStyleProperty<float>("--bevel-grow");
        static readonly CustomStyleProperty<float> SinkProperty = new CustomStyleProperty<float>("--bevel-sink");
        static readonly CustomStyleProperty<Color> InkProperty = new CustomStyleProperty<Color>("--bevel-ink");
        static readonly CustomStyleProperty<float> TimeProperty = new CustomStyleProperty<float>("--bevel-time");
        static readonly CustomStyleProperty<float> PressTimeProperty = new CustomStyleProperty<float>("--bevel-press-time");
        static readonly CustomStyleProperty<float> ShadowTimeProperty = new CustomStyleProperty<float>("--bevel-shadow-time");
        static readonly CustomStyleProperty<float> RimTimeProperty = new CustomStyleProperty<float>("--bevel-rim-time");
        static readonly CustomStyleProperty<float> InkTimeProperty = new CustomStyleProperty<float>("--bevel-ink-time");

        /// <summary>The shadow is this many fills, each a step further out, so its edge is soft without a blur.</summary>
        const int ShadowLayers = 12;

        /// <summary>
        /// Where the shadow's soft edge begins and ends, in blurs from the outline it falls from:
        /// measured on the reference's own shadow, which is still dark at the plate's edge and gone
        /// a blur and a quarter further out.
        /// </summary>
        const float ShadowIn = -1.85f, ShadowOut = 1.25f;

        /// <summary>How much shorter a cut is on an outline one pixel further in, so the two run side by side.</summary>
        const float CutInset = 0.586f;

        /// <summary>The line of light along the plate's top: one of the reference's pixels at the game's size.</summary>
        const float LightWidth = 1.5f;

        /// <summary>
        /// The reference's inner shade is a blurred shadow that falls from the plate's bottom edge
        /// and, weaker, from its top: this much of it is left half-way up its reach and at the top edge.
        /// </summary>
        const float ShadeHalf = 0.34f;

        /// <summary>The widening's curve in the reference, quick at first and long in settling; the rest use a style sheet's "ease".</summary>
        static readonly Curve WidthCurve = new Curve(0.22f, 1f, 0.36f, 1f), Ease = new Curve(0.25f, 0.1f, 0.25f, 1f);

        /// <summary>A style sheet's cubic-bezier(x1, y1, x2, y2): how far a change has come at each part of its time.</summary>
        readonly struct Curve
        {
            readonly float x1, y1, x2, y2;

            public Curve(float x1, float y1, float x2, float y2)
            {
                this.x1 = x1;
                this.y1 = y1;
                this.x2 = x2;
                this.y2 = y2;
            }

            public float At(float time)
            {
                // The curve's x only grows along it, so the point where it is the time is found by halving.
                float low = 0f, high = 1f, u = time;
                for (int i = 0; i < 14; i++)
                {
                    u = (low + high) * 0.5f;
                    if (Along(x1, x2, u) < time)
                        low = u;
                    else
                        high = u;
                }
                return Along(y1, y2, u);
            }

            static float Along(float a, float b, float u)
            {
                float v = 1f - u;
                return 3f * v * v * u * a + 3f * v * u * u * b + u * u * u;
            }
        }

        /// <summary>
        /// What a plate is. A change of it shows at once, and everything else with it: «Новая игра»
        /// turns gold as the main menu opens without a game, and must not be seen fading into it.
        /// </summary>
        struct Look
        {
            public Color Face, FaceLow, Side, Light, Shade;
            public float CutLeft, CutRight, Depth, RimWidth, ShadeSize;

            public bool Same(in Look other) =>
                Face == other.Face && FaceLow == other.FaceLow && Side == other.Side && Light == other.Light && Shade == other.Shade
                && CutLeft == other.CutLeft && CutRight == other.CutRight && Depth == other.Depth && RimWidth == other.RimWidth
                && ShadeSize == other.ShadeSize;
        }

        /// <summary>What the pointer does to a plate. A change of it is eased, each part in its own time.</summary>
        struct Touch
        {
            public float Grow, Sink, Drop, Blur;
            public Color Shadow, RimTop, RimLow, Ink;
        }

        /// <summary>The parts of a <see cref="Touch"/> that the reference eases apart from one another.</summary>
        enum Part
        {
            Width,
            Press,
            Shadow,
            Rim,
            Ink,
        }

        const int Parts = 5;

        sealed class Plate
        {
            public VisualElement Element;
            public Look Look;
            public Touch From, To, Now;
            public readonly float[] Started = new float[Parts], Length = new float[Parts];
            /// <summary>The widening, the sinking and the text's colour the element and its children have now.</summary>
            public float Grown, Sunk;
            public Color Inked;
            public bool Styled, Inks;
            /// <summary>The plate's colours down its height with the shade in them, kept while its look and height last.</summary>
            public Gradient Shading;
            public float ShadedHeight;
            public IVisualElementScheduledItem Run;
        }

        /// <summary>Draws <paramref name="element"/> as a bevelled plate behind its children.</summary>
        public static void Attach(VisualElement element)
        {
            var plate = new Plate { Element = element };
            // Runs every frame, and only while a change is under way.
            plate.Run = element.schedule.Execute(() => Step(plate)).Every(1);
            plate.Run.Pause();
            element.RegisterCallback<CustomStyleResolvedEvent>(e => Restyle(plate, e.customStyle));
            element.generateVisualContent += context => Draw(context.painter2D, element.layout.size, plate);
        }

        static void Restyle(Plate plate, ICustomStyle style)
        {
            // A rule may leave any of these out, so each starts from nothing.
            var look = new Look();
            style.TryGetValue(FaceProperty, out look.Face);
            look.FaceLow = style.TryGetValue(FaceLowProperty, out Color faceLow) ? faceLow : look.Face;
            style.TryGetValue(SideProperty, out look.Side);
            style.TryGetValue(LightProperty, out look.Light);
            style.TryGetValue(ShadeProperty, out look.Shade);
            style.TryGetValue(ShadeSizeProperty, out look.ShadeSize);
            style.TryGetValue(DepthProperty, out look.Depth);
            style.TryGetValue(RimWidthProperty, out look.RimWidth);
            style.TryGetValue(CutProperty, out float cut);
            look.CutLeft = style.TryGetValue(CutLeftProperty, out float cutLeft) ? cutLeft : cut;
            look.CutRight = style.TryGetValue(CutRightProperty, out float cutRight) ? cutRight : cut;

            var to = new Touch();
            if (style.TryGetValue(GrowProperty, out float grow)) to.Grow = Mathf.Max(0f, grow);
            if (style.TryGetValue(SinkProperty, out float sink)) to.Sink = Mathf.Clamp(sink, 0f, look.Depth);
            style.TryGetValue(ShadowProperty, out to.Shadow);
            style.TryGetValue(ShadowDropProperty, out to.Drop);
            style.TryGetValue(ShadowBlurProperty, out to.Blur);
            style.TryGetValue(RimProperty, out to.RimTop);
            to.RimLow = style.TryGetValue(RimLowProperty, out Color rimLow) ? rimLow : to.RimTop;
            plate.Inks = style.TryGetValue(InkProperty, out to.Ink);

            style.TryGetValue(TimeProperty, out float time);
            style.TryGetValue(PressTimeProperty, out float pressTime);
            style.TryGetValue(ShadowTimeProperty, out float shadowTime);
            style.TryGetValue(RimTimeProperty, out float rimTime);
            style.TryGetValue(InkTimeProperty, out float inkTime);

            Touch was = plate.To, now = plate.Now;
            if (!plate.Styled || !look.Same(plate.Look))
            {
                plate.Styled = true;
                plate.Look = look;
                plate.Shading = null;
                plate.From = to;
                for (int part = 0; part < Parts; part++)
                    plate.Length[part] = 0f;
            }
            else
            {
                // Each from where it is now, so a change turned back half-way does not jump.
                if (to.Grow != was.Grow)
                {
                    plate.From.Grow = now.Grow;
                    Start(plate, Part.Width, time);
                }
                if (to.Sink != was.Sink)
                {
                    plate.From.Sink = now.Sink;
                    Start(plate, Part.Press, pressTime);
                }
                if (to.Shadow != was.Shadow || to.Drop != was.Drop || to.Blur != was.Blur)
                {
                    plate.From.Shadow = now.Shadow;
                    plate.From.Drop = now.Drop;
                    plate.From.Blur = now.Blur;
                    Start(plate, Part.Shadow, shadowTime);
                }
                if (to.RimTop != was.RimTop || to.RimLow != was.RimLow)
                {
                    plate.From.RimTop = now.RimTop;
                    plate.From.RimLow = now.RimLow;
                    // A rim of two colours changes at once, as it does in the reference: a browser
                    // does not ease from one gradient to another.
                    bool graded = to.RimTop != to.RimLow || was.RimTop != was.RimLow;
                    Start(plate, Part.Rim, graded ? 0f : rimTime);
                }
                if (to.Ink != was.Ink)
                {
                    plate.From.Ink = now.Ink;
                    Start(plate, Part.Ink, inkTime);
                }
            }
            plate.To = to;
            plate.Run.Resume();
            Step(plate);
        }

        static void Start(Plate plate, Part part, float length)
        {
            plate.Started[(int)part] = ViewClock.Now;
            plate.Length[(int)part] = length;
        }

        /// <summary>One frame of the changes under way; it stops itself when the last of them is done.</summary>
        static void Step(Plate plate)
        {
            bool moving = false;
            float width = Done(plate, Part.Width, WidthCurve, ref moving);
            float press = Done(plate, Part.Press, Ease, ref moving);
            float shadow = Done(plate, Part.Shadow, Ease, ref moving);
            float rim = Done(plate, Part.Rim, Ease, ref moving);
            float ink = Done(plate, Part.Ink, Ease, ref moving);
            Touch from = plate.From, to = plate.To;
            plate.Now = new Touch
            {
                Grow = Mathf.Lerp(from.Grow, to.Grow, width),
                Sink = Mathf.Lerp(from.Sink, to.Sink, press),
                Shadow = Color.Lerp(from.Shadow, to.Shadow, shadow),
                Drop = Mathf.Lerp(from.Drop, to.Drop, shadow),
                Blur = Mathf.Lerp(from.Blur, to.Blur, shadow),
                RimTop = Color.Lerp(from.RimTop, to.RimTop, rim),
                RimLow = Color.Lerp(from.RimLow, to.RimLow, rim),
                Ink = Color.Lerp(from.Ink, to.Ink, ink),
            };
            if (!moving)
                plate.Run.Pause();
            Place(plate);
            plate.Element.MarkDirtyRepaint();
        }

        /// <summary>How much of its change a part has made, from 0 to 1 along its curve.</summary>
        static float Done(Plate plate, Part part, in Curve curve, ref bool moving)
        {
            float length = plate.Length[(int)part];
            float time = length > 0f ? (ViewClock.Now - plate.Started[(int)part]) / length : 1f;
            if (time >= 1f)
                return 1f;
            moving = true;
            return curve.At(Mathf.Max(0f, time));
        }

        /// <summary>Gives the element the width and its children the place and the colour the plate has now.</summary>
        static void Place(Plate plate)
        {
            VisualElement element = plate.Element;
            float grow = Mathf.Max(0f, plate.Now.Grow), sink = plate.Now.Sink;
            if (grow != plate.Grown)
            {
                plate.Grown = grow;
                // At rest the style sheet's own margins are back.
                StyleLength margin = grow > 0f ? new StyleLength(-grow) : new StyleLength(StyleKeyword.Null);
                element.style.marginLeft = margin;
                element.style.marginRight = margin;
            }
            if (sink != plate.Sunk)
            {
                plate.Sunk = sink;
                StyleTranslate down = sink > 0f ? new StyleTranslate(new Translate(0f, sink)) : new StyleTranslate(StyleKeyword.Null);
                foreach (VisualElement child in element.Children())
                    child.style.translate = down;
            }
            if (plate.Inks && plate.Now.Ink != plate.Inked)
            {
                plate.Inked = plate.Now.Ink;
                foreach (VisualElement child in element.Children())
                {
                    child.style.color = plate.Inked;
                    child.MarkDirtyRepaint();
                }
            }
        }

        static void Draw(Painter2D painter, Vector2 size, Plate plate)
        {
            Look look = plate.Look;
            Touch touch = plate.Now;
            float w = size.x, h = size.y, face = h - look.Depth;
            if (w <= 2f || face <= 2f)
                return;
            float most = Mathf.Min(face * 0.45f, w * 0.25f);
            float left = Mathf.Min(look.CutLeft, most), right = Mathf.Min(look.CutRight, most);
            bool rimmed = look.RimWidth > 0f && touch.RimTop.a > 0f;
            if (!rimmed && look.Face.a <= 0f)
                return;

            // The shadow of the whole plate: thin layers one over another, each a step further out.
            if (touch.Shadow.a > 0f)
            {
                Color layer = touch.Shadow;
                layer.a = 1f - Mathf.Pow(1f - Mathf.Min(layer.a, 0.99f), 1f / ShadowLayers);
                painter.fillColor = layer;
                for (int i = 0; i < ShadowLayers; i++)
                {
                    float far = touch.Blur * Mathf.Lerp(ShadowIn, ShadowOut, i / (ShadowLayers - 1f));
                    // A layer that does not reach past the plate is not seen.
                    if (far <= 0f && touch.Drop + far <= 0f)
                        continue;
                    Path(painter, -far, touch.Drop - far, w + far, h + touch.Drop + far, left + far * CutInset, right + far * CutInset);
                    painter.Fill();
                }
            }
            // The underlay, as large as the element: it shows under the face, and over a held one.
            if (look.Side.a > 0f && look.Depth > 0f)
            {
                painter.fillColor = look.Side;
                Path(painter, 0f, 0f, w, h, left, right);
                painter.Fill();
            }

            // The face: the rim's colour first and the plate over it, a rim's width further in. A
            // held face stands lower on its underlay. Once a fill is graded the ones after it are
            // too, and the painter is given back without a gradient, as other drawing expects it.
            float top = touch.Sink, bottom = top + face, rim = rimmed ? look.RimWidth : 0f;
            bool shaded = look.Shade.a > 0f && look.ShadeSize > 0f;
            bool graded = shaded || look.Face != look.FaceLow || (rimmed && touch.RimTop != touch.RimLow);
            if (graded)
                painter.fillColor = Color.white;
            if (rimmed)
            {
                Path(painter, 0f, top, w, bottom, left, right);
                Fill(painter, touch.RimTop, touch.RimLow, top, bottom, graded);
            }
            if (look.Face.a > 0f)
            {
                float y0 = top + rim, y1 = bottom - rim;
                Path(painter, rim, y0, w - rim, y1, left - rim * CutInset, right - rim * CutInset);
                if (shaded)
                {
                    if (plate.Shading == null || plate.ShadedHeight != y1 - y0)
                    {
                        plate.Shading = Shading(look, y1 - y0);
                        plate.ShadedHeight = y1 - y0;
                    }
                    painter.fillGradient = FillGradient.MakeLinearGradient(plate.Shading, new Vector2(0f, y0), new Vector2(0f, y1), AddressMode.Clamp);
                    painter.Fill();
                }
                else
                {
                    Fill(painter, look.Face, look.FaceLow, y0, y1, graded);
                }
                if (look.Light.a > 0f)
                {
                    // Under the light is the plate's top with what the shade leaves of it.
                    Color lit = shaded ? Mixed(look.Face, look.Shade, ShadeHalf) : look.Face;
                    lit = Mixed(lit, look.Light, 1f);
                    float x0 = rim + Mathf.Max(0f, left - rim * CutInset), x1 = w - rim - Mathf.Max(0f, right - rim * CutInset);
                    bool leftCut = left > 0f, rightCut = right > 0f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x0, y0));
                    painter.LineTo(new Vector2(x1, y0));
                    painter.LineTo(new Vector2(rightCut ? x1 + LightWidth : x1, y0 + LightWidth));
                    painter.LineTo(new Vector2(leftCut ? x0 - LightWidth : x0, y0 + LightWidth));
                    painter.ClosePath();
                    Fill(painter, lit, lit, y0, y0 + LightWidth, graded);
                }
            }
            if (graded)
                painter.fillGradient = default;
        }

        /// <summary>
        /// The plate's colours from its top to its bottom with the reference's inner shade in
        /// them: dark along the bottom edge, fading out upwards, and a little of it at the top.
        /// </summary>
        static Gradient Shading(in Look look, float height)
        {
            float reach = Mathf.Min(look.ShadeSize, height * 0.5f) / height;
            var colours = new[]
            {
                new GradientColorKey(Shaded(look, 0f, ShadeHalf), 0f),
                new GradientColorKey(Shaded(look, reach * 0.5f, 0f), reach * 0.5f),
                new GradientColorKey(Shaded(look, 1f - reach, 0f), 1f - reach),
                new GradientColorKey(Shaded(look, 1f - reach * 0.5f, ShadeHalf), 1f - reach * 0.5f),
                new GradientColorKey(Shaded(look, 1f, 1f), 1f),
            };
            var alphas = new[] { new GradientAlphaKey(look.Face.a, 0f), new GradientAlphaKey(look.FaceLow.a, 1f) };
            var gradient = new Gradient();
            gradient.SetKeys(colours, alphas);
            return gradient;
        }

        /// <summary>The plate's own colour at <paramref name="height"/> of its height, under that <paramref name="part"/> of its shade.</summary>
        static Color Shaded(in Look look, float height, float part) =>
            Mixed(Color.Lerp(look.Face, look.FaceLow, height), look.Shade, part);

        /// <summary>A colour with a see-through one laid over it, <paramref name="part"/> of the upper one's own strength.</summary>
        static Color Mixed(Color under, Color over, float part)
        {
            float k = over.a * part;
            return new Color(Mathf.Lerp(under.r, over.r, k), Mathf.Lerp(under.g, over.g, k), Mathf.Lerp(under.b, over.b, k), under.a);
        }

        /// <summary>The outline with cut corners; a cut of zero or less leaves the corner square.</summary>
        static void Path(Painter2D painter, float x0, float y0, float x1, float y1, float left, float right)
        {
            left = Mathf.Max(0f, left);
            right = Mathf.Max(0f, right);
            painter.BeginPath();
            painter.MoveTo(new Vector2(x0 + left, y0));
            painter.LineTo(new Vector2(x1 - right, y0));
            if (right > 0f)
                painter.LineTo(new Vector2(x1, y0 + right));
            painter.LineTo(new Vector2(x1, y1 - right));
            if (right > 0f)
                painter.LineTo(new Vector2(x1 - right, y1));
            painter.LineTo(new Vector2(x0 + left, y1));
            if (left > 0f)
            {
                painter.LineTo(new Vector2(x0, y1 - left));
                painter.LineTo(new Vector2(x0, y0 + left));
            }
            painter.ClosePath();
        }

        /// <summary>Fills the path from one colour at <paramref name="y0"/> to the other at <paramref name="y1"/>.</summary>
        static void Fill(Painter2D painter, Color top, Color bottom, float y0, float y1, bool graded)
        {
            if (graded)
                painter.fillGradient = FillGradient.MakeLinearGradient(top, bottom, new Vector2(0f, y0), new Vector2(0f, y1), AddressMode.Clamp);
            else
                painter.fillColor = top;
            painter.Fill();
        }
    }
}
