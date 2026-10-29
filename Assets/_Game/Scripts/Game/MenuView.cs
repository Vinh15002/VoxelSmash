using System;
using UnityEngine;
using UnityEngine.UI;
using static VoxelSmash.Game.UiKit;

namespace VoxelSmash.Game
{
    // Title screen, settings card and level list, ported from the reference's overlays
    // (#title-screen, #settings-modal, #level-modal). Built in code on the HUD canvas, above it.
    public sealed class MenuView : MonoBehaviour
    {
        public enum LevelState { Locked, Open, Finished }

        const float CardWidth = 940f, TileWidth = 196f, TileHeight = 170f, TileGap = 24f;
        const int Columns = 4;

        static readonly Color Dim = new(.09f, .19f, .29f, .55f), TitleBack = new(.07f, .17f, .30f, .93f),
            CardColor = new(.90f, .95f, .99f), Ink = new(.13f, .24f, .38f), Muted = new(.28f, .39f, .49f),
            Gold = new(1f, .86f, .21f), Green = new(.45f, .75f, .13f), Purple = new(.44f, .32f, .71f),
            RowColor = new(1f, 1f, 1f, .7f), OnColor = new(.22f, .44f, .11f), Blue = new(.13f, .55f, .93f),
            TileOpen = new(.80f, .90f, .98f), TileOpenText = new(.13f, .31f, .48f),
            TileDone = new(.79f, .92f, .55f), TileDoneText = new(.24f, .40f, .11f),
            TileLocked = new(.78f, .82f, .85f), TileLockedText = new(.53f, .58f, .64f);

        GameObject title, settings, levels, howTo;
        Text playLabel, titleProgress, qualityValue;
        RectTransform levelContent;
        ScrollRect levelScroll;

        public event Action Play, Resume, OpenLevels, MainMenu, ToggleQuality;
        public event Action<int> LevelChosen;

        public bool TitleOpen => title.activeSelf;
        public bool MenuOpen => settings.activeSelf || levels.activeSelf;

        public void Build(Texture icon)
        {
            var root = (RectTransform)transform;
            BuildTitle(root, icon);
            BuildSettings(root);
            BuildLevels(root);
            CloseMenus();
            title.SetActive(false);
        }

        void BuildTitle(Transform root, Texture icon)
        {
            var back = Backdrop("Title Screen", root, TitleBack);
            title = back.gameObject;
            var c = new Vector2(.5f, .5f);
            var frame = Box("Icon Frame", back, Blue, c, new Vector2(0, 560), new Vector2(360, 360), 90);
            Border(frame, Color.white, 8);
            Picture("Icon", frame, icon, c, new Vector2(0, 6), new Vector2(300, 300));
            Label("Voxel", back, 150, FontStyle.Bold, c, new Vector2(0, 280), new Vector2(1000, 170)).text = "VOXEL";
            var smash = Label("Smash", back, 170, FontStyle.Bold, c, new Vector2(0, 130), new Vector2(1000, 190));
            smash.text = "SMASH";
            smash.color = Gold;
            Label("Tagline", back, 46, FontStyle.Bold, c, new Vector2(0, 10), new Vector2(1000, 70)).text = "One tap. A thousand pieces.";
            titleProgress = Label("Progress", back, 38, FontStyle.Bold, c, new Vector2(0, -70), new Vector2(1000, 60));
            titleProgress.color = new Color(.83f, .91f, 1f);
            var play = Button("Play", back, "PLAY", c, new Vector2(0, -230), new Vector2(740, 160), Green, () => Play?.Invoke(), 38, 58);
            Border(play, Color.white, 7);
            playLabel = play.GetComponentInChildren<Text>();
            TextButton("How To Play", back, "How to play", c, new Vector2(0, -390), Color.white, () => howTo.SetActive(!howTo.activeSelf));
            var help = Label("How To", back, 40, FontStyle.Bold, c, new Vector2(0, -540), new Vector2(1000, 200));
            help.text = "Tap the model to strike.\nChoose a weapon below.\nDrag to turn and clear every cube.";
            howTo = help.gameObject;
            howTo.SetActive(false);
        }

        void BuildSettings(Transform root)
        {
            var card = Card("Settings", root, 1060f, out settings);
            var top = new Vector2(.5f, 1);
            Plain(Label("Title", card, 68, FontStyle.Bold, top, new Vector2(0, -60), new Vector2(800, 100)), Ink).text = "Settings";
            var row = UiKit.Button("Quality", card, null, top, new Vector2(0, -190), new Vector2(840, 120), RowColor, () => ToggleQuality?.Invoke(), 22);
            Border(row, new Color(.72f, .81f, .9f), 3);
            var name = Plain(Label("Name", row, 42, FontStyle.Bold, new Vector2(0, .5f), new Vector2(30, 0), new Vector2(560, 100)), Ink);
            name.text = "Debris detail";
            name.alignment = TextAnchor.MiddleLeft;
            qualityValue = Plain(Label("Value", row, 42, FontStyle.Bold, new Vector2(1, .5f), new Vector2(-30, 0), new Vector2(240, 100)), OnColor);
            qualityValue.alignment = TextAnchor.MiddleRight;
            var help = Plain(Label("How To", card, 38, FontStyle.Normal, top, new Vector2(0, -385), new Vector2(840, 200)), Muted);
            help.alignment = TextAnchor.UpperLeft;
            help.lineSpacing = 1.2f;
            help.text = "Tap the model to strike.\nHold for repeated hits.\nDrag horizontally to turn the sculpture.";
            UiKit.Button("Resume", card, "RESUME", top, new Vector2(0, -560), new Vector2(840, 130), Purple, () => Resume?.Invoke(), 26, 46);
            TextButton("Levels", card, "Levels", top, new Vector2(0, -740), Ink, () => OpenLevels?.Invoke());
            TextButton("Main Menu", card, "Main menu", top, new Vector2(0, -860), Ink, () => MainMenu?.Invoke());
        }

        void BuildLevels(Transform root)
        {
            const float viewHeight = 1040f;
            var card = Card("Levels", root, viewHeight + 400f, out levels);
            var top = new Vector2(.5f, 1);
            Plain(Label("Title", card, 64, FontStyle.Bold, top, new Vector2(0, -60), new Vector2(800, 100)), Ink).text = "Choose a level";
            float width = Columns * TileWidth + (Columns - 1) * TileGap + 40f;
            var viewport = Box("Viewport", card, Color.clear, top, new Vector2(0, -160), new Vector2(width, viewHeight));
            viewport.gameObject.AddComponent<RectMask2D>();
            levelContent = Rect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            levelContent.pivot = new Vector2(.5f, 1);
            levelScroll = viewport.gameObject.AddComponent<ScrollRect>();
            levelScroll.viewport = viewport;
            levelScroll.content = levelContent;
            levelScroll.horizontal = false;
            levelScroll.movementType = ScrollRect.MovementType.Clamped;
            levelScroll.scrollSensitivity = 40f;
            TextButton("Close", card, "Close", top, new Vector2(0, -viewHeight - 250f), Ink, () => Resume?.Invoke());
        }

        // Light rounded card centred on a dimmed backdrop; returns the card, outputs the backdrop.
        static RectTransform Card(string name, Transform root, float height, out GameObject overlay)
        {
            var back = Backdrop(name, root, Dim);
            overlay = back.gameObject;
            var card = Box("Card", back, CardColor, new Vector2(.5f, .5f), Vector2.zero, new Vector2(CardWidth, height), 56);
            Border(card, Color.white, 8);
            return card;
        }

        public void ShowTitle(string playCaption, string progress)
        {
            CloseMenus();
            playLabel.text = playCaption;
            titleProgress.text = progress;
            howTo.SetActive(false);
            title.SetActive(true);
            title.transform.SetAsLastSibling();
        }

        public void HideTitle() => title.SetActive(false);

        public void ShowSettings(bool highQuality)
        {
            levels.SetActive(false);
            SetQuality(highQuality);
            settings.SetActive(true);
        }

        public void SetQuality(bool high) => qualityValue.text = high ? "HIGH" : "LOW";

        public void ShowLevels(int total, Func<int, LevelState> state, int current)
        {
            settings.SetActive(false);
            for (int i = levelContent.childCount - 1; i >= 0; i--) Destroy(levelContent.GetChild(i).gameObject);
            int rows = Mathf.CeilToInt(total / (float)Columns);
            levelContent.sizeDelta = new Vector2(0, rows * (TileHeight + TileGap) + TileGap);
            float left = -(Columns * TileWidth + (Columns - 1) * TileGap) * .5f + TileWidth * .5f;
            for (int i = 0; i < total; i++)
            {
                int number = i + 1;
                var s = state(number);
                var (fill, ink, caption) = s switch
                {
                    LevelState.Finished => (TileDone, TileDoneText, "DONE"),
                    LevelState.Open => (TileOpen, TileOpenText, "PLAY"),
                    _ => (TileLocked, TileLockedText, "LOCKED")
                };
                var pos = new Vector2(left + i % Columns * (TileWidth + TileGap), -TileGap - TileHeight * .5f - i / Columns * (TileHeight + TileGap));
                var tile = Box("Level " + number, levelContent, fill, new Vector2(.5f, 1), pos, new Vector2(TileWidth, TileHeight), 30);
                tile.pivot = new Vector2(.5f, .5f);
                tile.anchoredPosition = pos;
                Border(tile, number == current ? Gold : Color.Lerp(fill, ink, .35f), number == current ? 9 : 4);
                var button = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.interactable = s != LevelState.Locked;
                button.onClick.AddListener(() => LevelChosen?.Invoke(number));
                Plain(Label("Number", tile, 66, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(0, 20), new Vector2(TileWidth, 80)), ink).text = number.ToString();
                Plain(Label("State", tile, 26, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(0, -45), new Vector2(TileWidth, 40)), ink).text = caption;
            }
            levels.SetActive(true);
            // Scroll so the current level's row sits mid-view.
            float view = ((RectTransform)levelScroll.transform).rect.height;
            float y = (current - 1) / Columns * (TileHeight + TileGap) - view * .5f + TileHeight;
            levelContent.anchoredPosition = new Vector2(0, Mathf.Clamp(y, 0f, Mathf.Max(0f, levelContent.sizeDelta.y - view)));
        }

        public void CloseMenus()
        {
            settings.SetActive(false);
            levels.SetActive(false);
        }
    }
}
