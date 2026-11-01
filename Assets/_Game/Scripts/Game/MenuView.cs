using System;
using UnityEngine;
using UnityEngine.UI;
using static VoxelSmash.Game.UiKit;

namespace VoxelSmash.Game
{
    // Title screen, settings card, level list and upgrade shop. Built in code on the HUD canvas, above
    // it; every screen has a portrait and a landscape arrangement.
    public sealed class MenuView : MonoBehaviour
    {
        public enum LevelState { Locked, Open, Finished }

        public struct LevelInfo
        {
            public LevelState state;
            public int stars;
            public bool boss;
        }

        public struct ShopState
        {
            public int coins, sizeLevel, sizeCost, magnetLevel, magnetCost, powerHits, maxLevel, powerPrice;
            public bool adAvailable;
        }

        const float CardWidth = 940f, TileWidth = 196f, TileHeight = 170f, TileGap = 24f,
            LevelViewPortrait = 1040f, LevelViewLandscape = 560f;
        const int Columns = 4;

        static readonly Color Dim = new(.09f, .19f, .29f, .55f), TitleBack = new(.07f, .17f, .30f, .93f),
            CardColor = new(.90f, .95f, .99f), Ink = new(.13f, .24f, .38f), Muted = new(.28f, .39f, .49f),
            Gold = new(1f, .86f, .21f), Green = new(.45f, .75f, .13f), Purple = new(.44f, .32f, .71f),
            RowColor = new(1f, 1f, 1f, .7f), OnColor = new(.22f, .44f, .11f), Blue = new(.13f, .55f, .93f),
            Orange = new(.85f, .6f, .1f), BossRed = new(.86f, .27f, .2f), StarOff = new(.6f, .64f, .7f, .7f),
            TileOpen = new(.80f, .90f, .98f), TileOpenText = new(.13f, .31f, .48f),
            TileDone = new(.79f, .92f, .55f), TileDoneText = new(.24f, .40f, .11f),
            TileLocked = new(.78f, .82f, .85f), TileLockedText = new(.53f, .58f, .64f);

        GameObject title, settings, levels, shop, howTo;
        Text playLabel, titleProgress, qualityValue, shopCoins, howToText, settingsHelp;
        RectTransform levelContent, levelCard, levelViewport, levelClose, settingsCard, shopCard;
        ScrollRect levelScroll;
        Texture2D starTexture;
        ShopRow sizeRow, magnetRow, powerRow;
        GameObject powerAdButton;
        readonly Layout layout = new();
        bool landscape;

        sealed class ShopRow
        {
            public Text level;
            public Text price;
            public UnityEngine.UI.Button buy;
        }

        public event Action Play, Resume, OpenLevels, MainMenu, ToggleQuality, BuySize, BuyMagnet, BuyPower, PowerAd;
        public event Action<int> LevelChosen;

        public bool TitleOpen => title.activeSelf;
        public bool MenuOpen => settings.activeSelf || levels.activeSelf || shop.activeSelf;

        public void Build(Texture icon, string helpText)
        {
            var root = (RectTransform)transform;
            starTexture = Star(64);
            BuildTitle(root, icon, helpText);
            BuildSettings(root, helpText);
            BuildLevels(root);
            BuildShop(root);
            CloseMenus();
            title.SetActive(false);
            landscape = Landscape;
            ApplyLayout();
        }

        void BuildTitle(Transform root, Texture icon, string helpText)
        {
            var back = Backdrop("Title Screen", root, TitleBack);
            title = back.gameObject;
            var c = new Vector2(.5f, .5f);
            var frame = Box("Icon Frame", back, Blue, c, new Vector2(0, 560), new Vector2(360, 360), 90);
            layout.Add(frame, new Vector2(-450, 170), new Vector2(320, 320));
            Border(frame, Color.white, 8);
            var picture = Picture("Icon", frame, icon, c, Vector2.zero, Vector2.zero);
            picture.rectTransform.anchorMin = Vector2.one * .08f;
            picture.rectTransform.anchorMax = Vector2.one * .92f;
            var top = Label("Name Top", back, 140, FontStyle.Bold, c, new Vector2(0, 280), new Vector2(1060, 170));
            layout.Add(top.rectTransform, new Vector2(-450, -90), new Vector2(900, 170));
            top.text = GameInfo.TitleTop;
            top.resizeTextForBestFit = true;
            top.resizeTextMaxSize = 140;
            var bottom = Label("Name Bottom", back, 170, FontStyle.Bold, c, new Vector2(0, 130), new Vector2(1000, 190));
            layout.Add(bottom.rectTransform, new Vector2(-450, -240), new Vector2(900, 190));
            bottom.text = GameInfo.TitleBottom;
            bottom.color = Gold;
            var tagline = Label("Tagline", back, 46, FontStyle.Bold, c, new Vector2(0, 10), new Vector2(1000, 70));
            layout.Add(tagline.rectTransform, new Vector2(420, 280), new Vector2(860, 70));
            tagline.text = GameInfo.Tagline;
            titleProgress = Label("Progress", back, 38, FontStyle.Bold, c, new Vector2(0, -70), new Vector2(1000, 60));
            layout.Add(titleProgress.rectTransform, new Vector2(420, 195), new Vector2(860, 60));
            titleProgress.color = new Color(.83f, .91f, 1f);
            var play = Button("Play", back, "PLAY", c, new Vector2(0, -230), new Vector2(740, 160), Green, () => Play?.Invoke(), 38, 58);
            layout.Add(play, new Vector2(420, 40), new Vector2(680, 150));
            Border(play, Color.white, 7);
            playLabel = play.GetComponentInChildren<Text>();
            var how = TextButton("How To Play", back, "How to play", c, new Vector2(0, -390), Color.white, () => howTo.SetActive(!howTo.activeSelf));
            layout.Add((RectTransform)how.transform.parent, new Vector2(420, -110));
            howToText = Label("How To", back, 40, FontStyle.Bold, c, new Vector2(0, -560), new Vector2(1000, 240));
            layout.Add(howToText.rectTransform, new Vector2(420, -270), new Vector2(860, 240));
            howToText.text = helpText;
            howTo = howToText.gameObject;
            howTo.SetActive(false);
        }

        void BuildSettings(Transform root, string helpText)
        {
            settingsCard = Card("Settings", root, 1060f, out settings);
            var card = settingsCard;
            var top = new Vector2(.5f, 1);
            Plain(Label("Title", card, 68, FontStyle.Bold, top, new Vector2(0, -60), new Vector2(800, 100)), Ink).text = "Settings";
            var row = UiKit.Button("Quality", card, null, top, new Vector2(0, -190), new Vector2(840, 120), RowColor, () => ToggleQuality?.Invoke(), 22);
            Border(row, new Color(.72f, .81f, .9f), 3);
            var name = Plain(Label("Name", row, 42, FontStyle.Bold, new Vector2(0, .5f), new Vector2(30, 0), new Vector2(560, 100)), Ink);
            name.text = "Debris detail";
            name.alignment = TextAnchor.MiddleLeft;
            qualityValue = Plain(Label("Value", row, 42, FontStyle.Bold, new Vector2(1, .5f), new Vector2(-30, 0), new Vector2(240, 100)), OnColor);
            qualityValue.alignment = TextAnchor.MiddleRight;
            settingsHelp = Plain(Label("How To", card, 38, FontStyle.Normal, top, new Vector2(0, -345), new Vector2(840, 220)), Muted);
            settingsHelp.alignment = TextAnchor.UpperLeft;
            settingsHelp.lineSpacing = 1.15f;
            settingsHelp.text = helpText;
            UiKit.Button("Resume", card, "RESUME", top, new Vector2(0, -600), new Vector2(840, 130), Purple, () => Resume?.Invoke(), 26, 46);
            TextButton("Levels", card, "Levels", top, new Vector2(0, -770), Ink, () => OpenLevels?.Invoke());
            TextButton("Main Menu", card, "Main menu", top, new Vector2(0, -880), Ink, () => MainMenu?.Invoke());
        }

        void BuildLevels(Transform root)
        {
            levelCard = Card("Levels", root, LevelViewPortrait + 400f, out levels);
            var top = new Vector2(.5f, 1);
            Plain(Label("Title", levelCard, 64, FontStyle.Bold, top, new Vector2(0, -60), new Vector2(800, 100)), Ink).text = "Choose a level";
            float width = Columns * TileWidth + (Columns - 1) * TileGap + 40f;
            levelViewport = Box("Viewport", levelCard, Color.clear, top, new Vector2(0, -160), new Vector2(width, LevelViewPortrait));
            levelViewport.gameObject.AddComponent<RectMask2D>();
            levelContent = Rect("Content", levelViewport, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            levelContent.pivot = new Vector2(.5f, 1);
            levelScroll = levelViewport.gameObject.AddComponent<ScrollRect>();
            levelScroll.viewport = levelViewport;
            levelScroll.content = levelContent;
            levelScroll.horizontal = false;
            levelScroll.movementType = ScrollRect.MovementType.Clamped;
            levelScroll.scrollSensitivity = 40f;
            levelClose = (RectTransform)TextButton("Close", levelCard, "Close", top, Vector2.zero, Ink, () => Resume?.Invoke()).transform.parent;
        }

        void BuildShop(Transform root)
        {
            shopCard = Card("Shop", root, 1040f, out shop);
            var card = shopCard;
            var top = new Vector2(.5f, 1);
            Plain(Label("Title", card, 68, FontStyle.Bold, top, new Vector2(0, -60), new Vector2(800, 100)), Ink).text = "Upgrades";
            var wallet = Box("Wallet", card, new Color(.07f, .1f, .22f, .85f), top, new Vector2(0, -175), new Vector2(330, 84), 42);
            Picture("Coin", wallet, Coin(96), new Vector2(0, .5f), new Vector2(10, 0), new Vector2(66, 66));
            shopCoins = Label("Amount", wallet, 44, FontStyle.Bold, new Vector2(1, .5f), new Vector2(-18, 0), new Vector2(230, 80));
            shopCoins.alignment = TextAnchor.MiddleRight;
            shopCoins.color = Gold;
            sizeRow = Row(card, -290, "Bigger hits", "+8% strike size per level", () => BuySize?.Invoke());
            magnetRow = Row(card, -475, "Coin magnet", "+10% coins per level", () => BuyMagnet?.Invoke());
            powerRow = Row(card, -660, "3 power hits", "+35% for 3 strikes", () => BuyPower?.Invoke());
            var ad = UiKit.Button("Watch Ad", powerRow.buy.transform.parent, "WATCH AD", new Vector2(1, .5f), new Vector2(-290, 0), new Vector2(220, 96), Purple, () => PowerAd?.Invoke(), 22, 32);
            powerAdButton = ad.gameObject;
            TextButton("Close", card, "Close", top, new Vector2(0, -900), Ink, () => Resume?.Invoke());
        }

        // A shop line: name and description on the left, a coin-price buy button on the right.
        ShopRow Row(Transform card, float y, string name, string description, Action buy)
        {
            var row = Box(name, card, RowColor, new Vector2(.5f, 1), new Vector2(0, y), new Vector2(860, 160), 26);
            Border(row, new Color(.72f, .81f, .9f), 3);
            var title = Plain(Label("Name", row, 42, FontStyle.Bold, new Vector2(0, .5f), new Vector2(30, 28), new Vector2(560, 60)), Ink);
            title.alignment = TextAnchor.MiddleLeft;
            title.text = name;
            var level = Plain(Label("Level", row, 32, FontStyle.Normal, new Vector2(0, .5f), new Vector2(30, -30), new Vector2(560, 50)), Muted);
            level.alignment = TextAnchor.MiddleLeft;
            level.text = description;
            var button = UiKit.Button("Buy", row, null, new Vector2(1, .5f), new Vector2(-24, 0), new Vector2(230, 96), Orange, buy, 22);
            Picture("Coin", button, Coin(96), new Vector2(0, .5f), new Vector2(14, 0), new Vector2(54, 54));
            var price = Label("Price", button, 38, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(22, 0), new Vector2(170, 90));
            return new ShopRow { level = level, price = price, buy = button.GetComponent<UnityEngine.UI.Button>() };
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
            CloseMenus();
            SetQuality(highQuality);
            settings.SetActive(true);
        }

        public void SetQuality(bool high) => qualityValue.text = high ? "HIGH" : "LOW";

        public void ShowShop(ShopState s)
        {
            CloseMenus();
            RefreshShop(s);
            shop.SetActive(true);
        }

        public void RefreshShop(ShopState s)
        {
            shopCoins.text = s.coins.ToString("N0");
            Fill(sizeRow, $"Level {s.sizeLevel}/{s.maxLevel}  ·  +8% strike size", s.sizeLevel >= s.maxLevel ? -1 : s.sizeCost, s.coins);
            Fill(magnetRow, $"Level {s.magnetLevel}/{s.maxLevel}  ·  +10% coins", s.magnetLevel >= s.maxLevel ? -1 : s.magnetCost, s.coins);
            Fill(powerRow, s.powerHits > 0 ? $"{s.powerHits} ready" : "+35% for 3 strikes", s.powerPrice, s.coins);
            powerAdButton.SetActive(s.adAvailable);
        }

        static void Fill(ShopRow row, string level, int cost, int coins)
        {
            row.level.text = level;
            row.price.text = cost < 0 ? "MAX" : cost.ToString("N0");
            row.buy.interactable = cost >= 0 && coins >= cost;
        }

        public void ShowLevels(int total, Func<int, LevelInfo> info, int current)
        {
            CloseMenus();
            for (int i = levelContent.childCount - 1; i >= 0; i--) Destroy(levelContent.GetChild(i).gameObject);
            int rows = Mathf.CeilToInt(total / (float)Columns);
            levelContent.sizeDelta = new Vector2(0, rows * (TileHeight + TileGap) + TileGap);
            float left = -(Columns * TileWidth + (Columns - 1) * TileGap) * .5f + TileWidth * .5f;
            for (int i = 0; i < total; i++)
            {
                int number = i + 1;
                var level = info(number);
                var (fill, ink, caption) = level.state switch
                {
                    LevelState.Finished => (TileDone, TileDoneText, "DONE"),
                    LevelState.Open => (TileOpen, TileOpenText, "PLAY"),
                    _ => (TileLocked, TileLockedText, "LOCKED")
                };
                if (level.boss && level.state != LevelState.Finished) caption = "BOSS";
                var pos = new Vector2(left + i % Columns * (TileWidth + TileGap), -TileGap - TileHeight * .5f - i / Columns * (TileHeight + TileGap));
                var tile = Box("Level " + number, levelContent, fill, new Vector2(.5f, 1), pos, new Vector2(TileWidth, TileHeight), 30);
                tile.pivot = new Vector2(.5f, .5f);
                tile.anchoredPosition = pos;
                Border(tile, number == current ? Gold : level.boss ? BossRed : Color.Lerp(fill, ink, .35f), number == current || level.boss ? 9 : 4);
                var button = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.interactable = level.state != LevelState.Locked;
                button.onClick.AddListener(() => LevelChosen?.Invoke(number));
                Plain(Label("Number", tile, 66, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(0, 26), new Vector2(TileWidth, 80)), ink).text = number.ToString();
                if (level.state == LevelState.Finished)
                    for (int k = 0; k < 3; k++)
                        Picture("Star", tile, starTexture, new Vector2(.5f, .5f), new Vector2((k - 1) * 46, -46), new Vector2(42, 42)).color = k < level.stars ? Gold : StarOff;
                else
                    Plain(Label("State", tile, 28, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(0, -45), new Vector2(TileWidth, 40)), level.boss ? BossRed : ink).text = caption;
            }
            levels.SetActive(true);
            // Scroll so the current level's row sits mid-view.
            float view = levelViewport.sizeDelta.y;
            float y = (current - 1) / Columns * (TileHeight + TileGap) - view * .5f + TileHeight;
            levelContent.anchoredPosition = new Vector2(0, Mathf.Clamp(y, 0f, Mathf.Max(0f, levelContent.sizeDelta.y - view)));
        }

        public void CloseMenus()
        {
            settings.SetActive(false);
            levels.SetActive(false);
            shop.SetActive(false);
        }

        void Update()
        {
            if (Landscape == landscape) return;
            landscape = Landscape;
            ApplyLayout();
        }

        void ApplyLayout()
        {
            layout.Apply(landscape);
            // A 1080-unit-tall landscape canvas can't fit the portrait cards: shrink the tall ones and
            // shorten the level list's scroll view.
            settingsCard.localScale = shopCard.localScale = Vector3.one * (landscape ? .92f : 1f);
            float view = landscape ? LevelViewLandscape : LevelViewPortrait;
            levelViewport.sizeDelta = new Vector2(levelViewport.sizeDelta.x, view);
            levelCard.sizeDelta = new Vector2(CardWidth, view + 400f);
            levelClose.anchoredPosition = new Vector2(0, -view - 250f);
        }
    }
}
