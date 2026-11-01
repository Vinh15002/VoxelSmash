using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static VoxelSmash.Game.UiKit;

namespace VoxelSmash.Game
{
    // Transient play feedback: floating "+N" cube counts (with combo and critical tags) at each hit,
    // the tutorial hand with its caption and skip button, and a status toast. Runs on unscaled time so
    // it works while paused.
    public sealed class FeedbackView : MonoBehaviour
    {
        // How the hand moves: bob onto a point, press and hold, swipe sideways, or point at a button.
        public enum HintMotion { Tap, Hold, Drag, Point }

        // Reference CSS: .score floats 65px up over .9s; #toast hides after 2.1s; the hand bobs every 1.1s.
        const float ScoreLife = .9f, ScoreRise = 170f, ToastLife = 2.1f, HandPeriod = 1.1f, HandBob = 13f, MaxHand = 350f,
            DragSwing = 150f;
        const int MaxScores = 24;

        struct Score
        {
            public Text text;
            public Vector2 start;
            public float age;
        }

        RectTransform layer, hand, handImage, caption, toast;
        Text comboText;
        float comboAge = 9f;
        Text toastText, captionText;
        HintMotion motion;
        CanvasGroup toastGroup;
        readonly List<Score> scores = new();
        readonly Stack<Text> spare = new();
        float toastAge = ToastLife, handClock;

        public bool TutorialVisible => hand.gameObject.activeSelf;
        public HintMotion Motion => motion;
        public event Action SkipRequested;

        // `layer` sits under the HUD's win overlay; the toast is added last so it stays on top of menus.
        public void Build(Transform canvas, int siblingIndex)
        {
            layer = Stretch("Feedback", canvas);
            layer.SetSiblingIndex(siblingIndex);

            hand = Rect("Tutorial", layer, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            handImage = Picture("Hand", hand, Hand(192), new Vector2(.5f, .5f), Vector2.zero, Vector2.one * MaxHand).rectTransform;
            handImage.pivot = HandTip;
            caption = Box("Caption", hand, new Color(.10f, .20f, .29f, .87f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(660, 90), 24);
            caption.GetComponent<Image>().raycastTarget = false;
            captionText = Label("Text", caption, 40, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(-62, 0), new Vector2(520, 90));
            // Onboarding must be skippable.
            var skip = UiKit.Button("Skip", caption, "Skip", new Vector2(1, .5f), new Vector2(-8, 0), new Vector2(100, 70),
                new Color(.3f, .4f, .52f), () => SkipRequested?.Invoke(), 18, 32);
            skip.pivot = new Vector2(1, .5f);
            hand.gameObject.SetActive(false);

            comboText = Headline(Label("Combo", layer, 96, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(0, 230), new Vector2(900, 130)),
                new Color(1f, .95f, .4f), new Color(1f, .5f, .15f), 6f);
            comboText.gameObject.SetActive(false);

            toast = Box("Toast", canvas, new Color(.09f, .21f, .34f, .92f), new Vector2(.5f, 0), Vector2.zero, new Vector2(820, 130), 32);
            toast.anchorMin = toast.anchorMax = new Vector2(.5f, .3f);
            toast.GetComponent<Image>().raycastTarget = false;
            Border(toast, new Color(.55f, .71f, .81f), 3);
            toastText = Label("Text", toast, 38, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(780, 120));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toast.gameObject.SetActive(false);
        }

        // Big "COMBO xN!" that pops in the upper middle of the screen and fades.
        public void ShowCombo(int combo)
        {
            comboText.text = $"COMBO x{combo}!";
            comboAge = 0f;
            comboText.gameObject.SetActive(true);
        }

        public void Toast(string message)
        {
            toastText.text = message;
            toastAge = 0f;
            toastGroup.alpha = 1f;
            toast.gameObject.SetActive(true);
            toast.SetAsLastSibling();
        }

        // "+N" rising from a screen point (the reference offsets it 12px left, 20px up of the hit), tagged
        // with the combo multiplier and a CRIT! label for critical hits.
        public void ShowScore(int count, Vector2 screenPoint, int combo = 1, bool critical = false)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPoint, null, out var local)) return;
            if (scores.Count >= MaxScores) Recycle(0);
            var text = spare.Count > 0 ? spare.Pop() : NewScore();
            text.text = (critical ? "<color=#ff5a3c>CRIT!</color> +" : "+") + count + (combo > 1 ? $" <color=#ffd23a>x{combo}</color>" : "");
            text.fontSize = critical ? 64 : 50;
            text.color = Color.white;
            text.rectTransform.anchoredPosition = local;
            text.gameObject.SetActive(true);
            scores.Add(new Score { text = text, start = local + new Vector2(-31f, 52f), age = 0f });
        }

        Text NewScore()
        {
            var text = Label("Score", layer, 50, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(480, 80));
            text.GetComponent<Outline>().effectColor = new Color(.32f, .19f, .51f, .9f);
            return text;
        }

        public void ClearScores()
        {
            for (int i = scores.Count - 1; i >= 0; i--) Recycle(i);
        }

        void Recycle(int i)
        {
            scores[i].text.gameObject.SetActive(false);
            spare.Push(scores[i].text);
            scores.RemoveAt(i);
        }

        public void ShowHint(HintMotion hintMotion, string text)
        {
            motion = hintMotion;
            captionText.text = text;
            handClock = 0f;
            if (!hand.gameObject.activeSelf) hand.gameObject.SetActive(true);
        }

        // Called every frame by the hint logic: only restart the animation when the hint actually changes.
        public void ShowHintIfChanged(HintMotion hintMotion, string text)
        {
            if (hand.gameObject.activeSelf && motion == hintMotion && captionText.text == text) return;
            ShowHint(hintMotion, text);
        }

        public void HideHint()
        {
            if (hand.gameObject.activeSelf) hand.gameObject.SetActive(false);
        }

        // Puts the fingertip on a screen point, sized like the reference (min 135px, 25% width, 20% height).
        // Sculpture hints stay inside the band between the HUD panels; a Point hint may sit on a panel
        // button, so its caption goes above the hand instead of below.
        public void PlaceHint(Vector2 screenPoint, float topPx, float bottomPx)
        {
            var rect = layer.rect;
            float scale = Screen.width / Mathf.Max(1f, rect.width);
            float size = Mathf.Min(MaxHand, rect.width * .25f, rect.height * .2f);
            handImage.sizeDelta = Vector2.one * size;
            var tip = new Vector2(screenPoint.x / scale - rect.width * .5f, screenPoint.y / scale - rect.height * .5f);
            // Both band edges are measured in screen pixels from the top.
            float top = rect.height * .5f - topPx / scale, bottom = rect.height * .5f - bottomPx / scale;
            tip.x = Mathf.Clamp(tip.x, -rect.width * .5f + 8f + size * HandTip.x, rect.width * .5f - 8f - size * (1f - HandTip.x));
            if (motion != HintMotion.Point) tip.y = Mathf.Clamp(tip.y, bottom + size * HandTip.y + 100f, top);
            hand.anchoredPosition = tip;
            float captionX = Mathf.Clamp(size * (.5f - HandTip.x) - 20f, -rect.width * .5f + 290f - tip.x, rect.width * .5f - 290f - tip.x);
            caption.anchoredPosition = motion == HintMotion.Point
                ? new Vector2(captionX, 80f)
                : new Vector2(captionX, -size * HandTip.y - 55f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = scores.Count - 1; i >= 0; i--)
            {
                var s = scores[i];
                s.age += dt;
                if (s.age >= ScoreLife) { Recycle(i); continue; }
                float t = s.age / ScoreLife, ease = 1f - (1f - t) * (1f - t);
                var rt = s.text.rectTransform;
                rt.anchoredPosition = s.start + new Vector2(0, ScoreRise * ease);
                var c = s.text.color;
                c.a = 1f - ease;
                s.text.color = c;
                scores[i] = s;
            }

            if (hand.gameObject.activeSelf)
            {
                handClock += dt;
                float phase = handClock / HandPeriod * Mathf.PI * 2f;
                // CSS tutorial-tap: offset (5px, 5px) at the ends of the cycle, touching at the middle.
                float away = .5f + .5f * Mathf.Cos(phase);
                handImage.anchoredPosition = motion switch
                {
                    HintMotion.Hold => Vector2.zero,
                    HintMotion.Drag => new Vector2(Mathf.Sin(phase * .5f) * DragSwing, 0f),
                    _ => new Vector2(HandBob, -HandBob) * away
                };
                // A held press squeezes slightly and pulses so it reads as "keep pressing".
                handImage.localScale = Vector3.one * (motion == HintMotion.Hold ? .94f + .04f * Mathf.Sin(phase * 2f) : 1f);
            }

            if (comboText.gameObject.activeSelf)
            {
                comboAge += dt;
                const float life = 1.1f;
                float pop = comboAge < .12f ? Mathf.Lerp(1.7f, .92f, comboAge / .12f) : Mathf.Lerp(.92f, 1f, Mathf.Min(1f, (comboAge - .12f) / .1f));
                comboText.rectTransform.localScale = Vector3.one * pop;
                var c = comboText.color;
                c.a = Mathf.Clamp01((life - comboAge) / .3f);
                comboText.color = c;
                if (comboAge >= life) comboText.gameObject.SetActive(false);
            }

            if (toast.gameObject.activeSelf)
            {
                toastAge += dt;
                toastGroup.alpha = Mathf.Clamp01((ToastLife - toastAge) / .2f);
                if (toastAge >= ToastLife) toast.gameObject.SetActive(false);
            }
        }
    }
}
