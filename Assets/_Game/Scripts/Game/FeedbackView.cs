using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static VoxelSmash.Game.UiKit;

namespace VoxelSmash.Game
{
    // Transient play feedback ported from the reference: floating "+N" cube counts at each hit,
    // the tutorial hand on level 1, and a status toast. Runs on unscaled time so it works while paused.
    public sealed class FeedbackView : MonoBehaviour
    {
        // Reference CSS: .score floats 65px up over .9s; #toast hides after 2.1s; the hand bobs every 1.1s.
        const float ScoreLife = .9f, ScoreRise = 170f, ToastLife = 2.1f, HandPeriod = 1.1f, HandBob = 13f, MaxHand = 350f;
        const int MaxScores = 24;

        struct Score
        {
            public Text text;
            public Vector2 start;
            public float age;
        }

        RectTransform layer, hand, handImage, toast;
        Text toastText;
        CanvasGroup toastGroup;
        readonly List<Score> scores = new();
        readonly Stack<Text> spare = new();
        float toastAge = ToastLife, handClock;

        public bool TutorialVisible => hand.gameObject.activeSelf;

        // `layer` sits under the HUD's win overlay; the toast is added last so it stays on top of menus.
        public void Build(Transform canvas, int siblingIndex)
        {
            layer = Stretch("Feedback", canvas);
            layer.SetSiblingIndex(siblingIndex);

            hand = Rect("Tutorial", layer, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            handImage = Picture("Hand", hand, Hand(192), new Vector2(.5f, .5f), Vector2.zero, Vector2.one * MaxHand).rectTransform;
            handImage.pivot = HandTip;
            var tip = Box("Caption", hand, new Color(.10f, .20f, .29f, .87f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(470, 90), 24);
            tip.GetComponent<Image>().raycastTarget = false;
            Label("Text", tip, 40, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(470, 90)).text = "Tap the model to smash";
            hand.gameObject.SetActive(false);

            toast = Box("Toast", canvas, new Color(.09f, .21f, .34f, .92f), new Vector2(.5f, 0), Vector2.zero, new Vector2(820, 130), 32);
            toast.anchorMin = toast.anchorMax = new Vector2(.5f, .3f);
            toast.GetComponent<Image>().raycastTarget = false;
            Border(toast, new Color(.55f, .71f, .81f), 3);
            toastText = Label("Text", toast, 38, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(780, 120));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toast.gameObject.SetActive(false);
        }

        public void Toast(string message)
        {
            toastText.text = message;
            toastAge = 0f;
            toastGroup.alpha = 1f;
            toast.gameObject.SetActive(true);
            toast.SetAsLastSibling();
        }

        // "+N" rising from a screen point (the reference offsets it 12px left, 20px up of the hit).
        public void ShowScore(int count, Vector2 screenPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPoint, null, out var local)) return;
            if (scores.Count >= MaxScores) Recycle(0);
            var text = spare.Count > 0 ? spare.Pop() : NewScore();
            text.text = "+" + count;
            text.color = Color.white;
            text.rectTransform.anchoredPosition = local;
            text.gameObject.SetActive(true);
            scores.Add(new Score { text = text, start = local + new Vector2(-31f, 52f), age = 0f });
        }

        Text NewScore()
        {
            var text = Label("Score", layer, 50, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(240, 70));
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

        public void SetTutorial(bool visible)
        {
            if (hand.gameObject.activeSelf != visible) hand.gameObject.SetActive(visible);
        }

        // Puts the fingertip on a screen point, sized like the reference (min 135px, 25% width, 20% height),
        // and keeps the hand inside the band between the top bar and the weapon panel.
        public void PlaceTutorial(Vector2 screenPoint, float topPx, float bottomPx)
        {
            var rect = layer.rect;
            float scale = Screen.width / Mathf.Max(1f, rect.width);
            float size = Mathf.Min(MaxHand, rect.width * .25f, rect.height * .2f);
            handImage.sizeDelta = Vector2.one * size;
            var tip = new Vector2(screenPoint.x / scale - rect.width * .5f, screenPoint.y / scale - rect.height * .5f);
            // Both band edges are measured in screen pixels from the top.
            float top = rect.height * .5f - topPx / scale, bottom = rect.height * .5f - bottomPx / scale;
            tip.x = Mathf.Clamp(tip.x, -rect.width * .5f + 8f + size * HandTip.x, rect.width * .5f - 8f - size * (1f - HandTip.x));
            tip.y = Mathf.Clamp(tip.y, bottom + size * HandTip.y + 100f, top);
            hand.anchoredPosition = tip;
            var caption = (RectTransform)hand.Find("Caption");
            caption.anchoredPosition = new Vector2(size * (.5f - HandTip.x) - 20f, -size * HandTip.y - 55f);
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
                // CSS tutorial-tap: offset (5px, 5px) at the ends of the cycle, touching at the middle.
                float away = .5f + .5f * Mathf.Cos(handClock / HandPeriod * Mathf.PI * 2f);
                handImage.anchoredPosition = new Vector2(HandBob, -HandBob) * away;
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
