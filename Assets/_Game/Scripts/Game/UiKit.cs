using System;
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
            rt.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => onClick());
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
