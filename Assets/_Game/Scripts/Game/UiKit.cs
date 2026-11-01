using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelSmash.Game
{
    // Shared builders for the code-made uGUI screens, plus procedurally drawn sprites (rounded panels,
    // the settings gear, the tutorial hand) so the UI needs no imported art.
    public static class UiKit
    {
        const int Corner = 32;
        static Font font;
        static Sprite rounded;

        public static Font Font => font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Desktop web is landscape-first; the UI switches layouts on the window's shape.
        public static bool Landscape => Screen.width > Screen.height * 1.05f;

        // Remembers each element's portrait placement (as built) and its landscape placement.
        public sealed class Layout
        {
            readonly List<(RectTransform rt, Vector2 portrait, Vector2 landscape, Vector2 portraitSize, Vector2 landscapeSize,
                Vector2 portraitAnchor, Vector2 landscapeAnchor)> items = new();

            // `landscapeAnchor` (a point of the parent, 0..1) lets landscape columns follow the screen
            // width instead of sitting at fixed offsets from the centre.
            public RectTransform Add(RectTransform rt, Vector2 landscape, Vector2? landscapeSize = null, Vector2? landscapeAnchor = null)
            {
                items.Add((rt, rt.anchoredPosition, landscape, rt.sizeDelta, landscapeSize ?? rt.sizeDelta, rt.anchorMin, landscapeAnchor ?? rt.anchorMin));
                return rt;
            }

            public void Apply(bool landscape)
            {
                foreach (var (rt, portrait, land, portraitSize, landSize, portraitAnchor, landAnchor) in items)
                {
                    rt.anchorMin = rt.anchorMax = landscape ? landAnchor : portraitAnchor;
                    rt.anchoredPosition = landscape ? land : portrait;
                    rt.sizeDelta = landscape ? landSize : portraitSize;
                }
            }
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent) => Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Box(string name, Transform parent, Color color, Vector2 anchor, Vector2 position, Vector2 size, float radius = 0f)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var image = rt.GetComponent<Image>();
            image.color = color;
            if (radius > 0f) Round(image, radius);
            return Place(rt, anchor, position, size);
        }

        // Full-screen tinted backdrop that also swallows taps meant for the scene below.
        public static RectTransform Backdrop(string name, Transform parent, Color color)
        {
            var rt = Stretch(name, parent);
            rt.gameObject.AddComponent<Image>().color = color;
            return rt;
        }

        public static void Round(Image image, float radius)
        {
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Corner / radius;
        }

        public static void Border(Component target, Color color, float width)
        {
            var outline = target.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
        }

        public static Text Label(string name, Transform parent, int fontSize, FontStyle style, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Place(rt, anchor, position, size);
            var text = rt.GetComponent<Text>();
            text.font = Font; text.fontSize = fontSize; text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
            text.raycastTarget = false;
            var outline = rt.GetComponent<Outline>();
            outline.effectColor = new Color(.08f, .06f, .25f, .9f);
            outline.effectDistance = new Vector2(3, -3);
            return text;
        }

        // Dark text with no outline, for light card backgrounds.
        public static Text Plain(Text text, Color color)
        {
            text.color = color;
            UnityEngine.Object.Destroy(text.GetComponent<Outline>());
            return text;
        }

        public static RectTransform Button(string name, Transform parent, string caption, Vector2 anchor, Vector2 position, Vector2 size,
            Color color, Action onClick, float radius = 24f, int fontSize = 48)
        {
            var rt = Box(name, parent, color, anchor, position, size, radius);
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(() => onClick());
            // Disabled buttons go solid grey instead of the default half-transparent tint.
            var colors = button.colors;
            colors.disabledColor = new Color(.55f, .55f, .58f, 1f);
            button.colors = colors;
            var lip = rt.gameObject.AddComponent<Shadow>();
            lip.effectColor = Color.Lerp(color, Color.black, .45f);
            lip.effectDistance = new Vector2(0, -Mathf.Clamp(size.y * .07f, 4f, 10f));
            // Stretches with the button, so layouts that resize it (landscape) keep the shine inside.
            var shine = Box("Shine", rt, new Color(1f, 1f, 1f, .18f), Vector2.zero, Vector2.zero, Vector2.zero, radius * .7f);
            shine.anchorMin = new Vector2(0f, .52f);
            shine.anchorMax = new Vector2(1f, .94f);
            shine.offsetMin = new Vector2(radius * .45f, 0f);
            shine.offsetMax = new Vector2(-radius * .45f, 0f);
            shine.GetComponent<Image>().raycastTarget = false;
            if (caption != null) Label("Label", rt, fontSize, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, size).text = caption;
            return rt;
        }

        // Underlined-link style button used on cards ("Levels", "Main menu", "How to play").
        public static Text TextButton(string name, Transform parent, string caption, Vector2 anchor, Vector2 position, Color color, Action onClick)
        {
            var rt = Box(name, parent, Color.clear, anchor, position, new Vector2(600, 100));
            rt.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => onClick());
            var label = Plain(Label("Label", rt, 40, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(600, 100)), color);
            label.text = caption;
            return label;
        }

        // Display text: vertical colour gradient, a thick dark outline and a drop shadow.
        public static Text Headline(Text text, Color top, Color bottom, float stroke = 6f)
        {
            text.color = Color.white;
            text.gameObject.AddComponent<VerticalGradient>().Set(top, bottom);
            var outline = text.GetComponent<Outline>();
            if (!outline) outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.07f, .05f, .2f, 1f);
            outline.effectDistance = new Vector2(stroke, -stroke);
            var second = text.gameObject.AddComponent<Outline>();
            second.effectColor = new Color(.07f, .05f, .2f, 1f);
            second.effectDistance = new Vector2(-stroke * .6f, stroke * .6f);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .45f);
            shadow.effectDistance = new Vector2(0, -stroke * 1.8f);
            return text;
        }

        // Tints a graphic's vertices from top to bottom (used on headlines).
        public sealed class VerticalGradient : BaseMeshEffect
        {
            Color top = Color.white, bottom = Color.white;

            public void Set(Color topColor, Color bottomColor)
            {
                top = topColor;
                bottom = bottomColor;
                graphic.SetVerticesDirty();
            }

            public override void ModifyMesh(VertexHelper vh)
            {
                if (!IsActive() || vh.currentVertCount == 0) return;
                var v = new UIVertex();
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref v, i);
                    min = Mathf.Min(min, v.position.y);
                    max = Mathf.Max(max, v.position.y);
                }
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref v, i);
                    v.color *= Color.Lerp(bottom, top, Mathf.InverseLerp(min, max, v.position.y));
                    vh.SetUIVertex(v, i);
                }
            }
        }

        // Rotates a UI element on unscaled time (sunbursts keep turning while the game is paused).
        public sealed class Spin : MonoBehaviour
        {
            public float degreesPerSecond = 18f;

            void Update() => transform.Rotate(0f, 0f, -degreesPerSecond * Time.unscaledDeltaTime);
        }

        public static RawImage Picture(string name, Transform parent, Texture texture, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Place(rt, anchor, position, size);
            var raw = rt.GetComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            return raw;
        }

        static Sprite RoundedSprite
        {
            get
            {
                if (rounded) return rounded;
                int size = Corner * 2 + 2;
                var tex = Paint(size, (x, y) =>
                {
                    float cx = Mathf.Clamp(x, Corner, size - Corner), cy = Mathf.Clamp(y, Corner, size - Corner);
                    float d = new Vector2(x - cx, y - cy).magnitude - Corner;
                    return new Color(1, 1, 1, Mathf.Clamp01(.5f - d));
                }, 1);
                rounded = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(Corner, Corner, Corner, Corner));
                return rounded;
            }
        }

        // Eight-tooth gear with a hole, white on transparent.
        public static Texture2D Gear(int size) => Paint(size, (x, y) =>
        {
            var p = new Vector2(x / size - .5f, y / size - .5f);
            float r = p.magnitude, a = Mathf.Atan2(p.y, p.x);
            bool tooth = Mathf.Cos(a * 8f) > .2f;
            bool inside = r < (tooth ? .47f : .36f) && r > .15f;
            return inside ? Color.white : Color.clear;
        }, 4);

        // Five-pointed star, white on transparent (tinted per use): a point-in-polygon test against the
        // ten corners, so the arms have straight edges.
        public static Texture2D Star(int size)
        {
            var corners = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? .48f : .2f;
                corners[i] = new Vector2(.5f + Mathf.Cos(a) * r, .47f + Mathf.Sin(a) * r);
            }
            return Paint(size, (x, y) => Inside(corners, new Vector2(x / size, y / size)) ? Color.white : Color.clear, 4);
        }

        static bool Inside(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                    p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            return inside;
        }

        // Circular "restart" arrow: a 290-degree ring ending in an arrowhead.
        public static Texture2D Restart(int size) => Paint(size, (x, y) =>
        {
            var p = new Vector2(x / size - .5f, y / size - .5f);
            float r = p.magnitude, a = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 360f);
            bool ring = r > .24f && r < .36f && (a < 40f || a > 110f);
            // Arrowhead at the ring's end (40 degrees), pointing along the circle.
            var tip = new Vector2(Mathf.Cos(40f * Mathf.Deg2Rad), Mathf.Sin(40f * Mathf.Deg2Rad)) * .3f;
            var along = new Vector2(-Mathf.Sin(40f * Mathf.Deg2Rad), Mathf.Cos(40f * Mathf.Deg2Rad));
            var across = new Vector2(along.y, -along.x);
            var head = new[] { tip + along * .17f, tip + across * .15f, tip - across * .15f };
            return ring || Inside(head, p) ? Color.white : Color.clear;
        }, 4);

        // Soft sunburst: alternating rays that fade toward the edge.
        public static Texture2D Rays(int size) => Paint(size, (x, y) =>
        {
            var p = new Vector2(x / size - .5f, y / size - .5f);
            float r = p.magnitude * 2f, a = Mathf.Atan2(p.y, p.x);
            if (r > 1f) return Color.clear;
            float ray = Mathf.Clamp01((Mathf.Cos(a * 14f) - .35f) * 3f);
            return new Color(1f, 1f, 1f, ray * (1f - r) * (1f - r) * .9f + (1f - r) * .12f);
        }, 2);

        // Gold coin with a darker rim and a raised inner ring.
        public static Texture2D Coin(int size) => Paint(size, (x, y) =>
        {
            float d = new Vector2(x / size - .5f, y / size - .5f).magnitude;
            if (d > .47f) return Color.clear;
            if (d > .4f) return new Color(.80f, .52f, .08f);
            if (d > .33f) return new Color(1f, .82f, .2f);
            if (d > .28f) return new Color(.88f, .62f, .1f);
            return new Color(1f, .78f, .16f);
        }, 3);

        // Cartoon pointing hand (index finger up), white with a navy outline and a blue cuff.
        // The fingertip sits at (HandTip.x, HandTip.y) in normalized texture coordinates.
        public static readonly Vector2 HandTip = new(.3f, .97f);

        public static Texture2D Hand(int size)
        {
            Color fill = Color.white, line = new(.10f, .16f, .33f), cuff = new(.16f, .55f, .93f);
            const float stroke = .028f;
            return Paint(size, (x, y) =>
            {
                var p = new Vector2(x / size, y / size);
                float hand = Mathf.Min(Mathf.Min(
                        Capsule(p, new Vector2(.3f, .54f), new Vector2(.3f, .885f), .085f),
                        RoundBox(p, new Vector2(.48f, .38f), new Vector2(.2f, .17f), .1f)),
                    Mathf.Min(Mathf.Min(
                            Circle(p, new Vector2(.46f, .55f), .075f),
                            Circle(p, new Vector2(.58f, .53f), .072f)),
                        Mathf.Min(
                            Circle(p, new Vector2(.68f, .48f), .066f),
                            Capsule(p, new Vector2(.25f, .36f), new Vector2(.15f, .5f), .065f))));
                float sleeve = RoundBox(p, new Vector2(.5f, .1f), new Vector2(.2f, .085f), .03f);
                float d = Mathf.Min(hand, sleeve);
                if (d > 0f) return new Color(line.r, line.g, line.b, Mathf.Clamp01(1f - d * size));
                if (d > -stroke || (sleeve < 0f && hand > -stroke && hand < stroke)) return line;
                return sleeve < 0f ? cuff : fill;
            }, 3);
        }

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude - r;
        }

        static float RoundBox(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        // Rasterizes a shader-like function with samples x samples supersampling.
        static Texture2D Paint(int size, Func<float, float, Color> shade, int samples)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var sum = Color.clear;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    var c = shade(x + (sx + .5f) / samples, y + (sy + .5f) / samples);
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
                sum /= samples * samples;
                pixels[y * size + x] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
            }
            tex.SetPixels(pixels);
            tex.Apply(false);
            return tex;
        }
    }
}
