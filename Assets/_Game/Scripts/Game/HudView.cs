using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static VoxelSmash.Game.UiKit;

namespace VoxelSmash.Game
{
    // Code-built uGUI HUD so the scene needs no hand-authored UI. Portrait screens use a 1080-unit-wide
    // canvas with the weapon panel along the bottom; landscape screens (desktop web) use a 1080-unit-tall
    // canvas with the panel down the right side. The panels define the camera's clear rectangle.
    public sealed class HudView : MonoBehaviour
    {
        const float Ref = 1080f, TopReservePortrait = 330f, TopReserveLandscape = 250f, SideMargin = 24f;
        // Inventory metrics from the reference layout, in reference units (~2.6 per CSS px).
        const float PanelMaxWidth = 880f, PanelPadding = 30f, PanelGap = 20f, PanelBorder = 10f,
            ButtonMin = 115f, ButtonMax = 190f, PageControls = 126f, PanelBottom = 31f, PanelClearance = 52f,
            SideButton = 150f, SideRows = 3, SidePanelMargin = 24f;

        static readonly Color Navy = new(.11f, .14f, .30f), Blue = new(.13f, .55f, .93f), Gold = new(1f, .80f, .13f),
            PanelColor = new(.34f, .48f, .78f, .95f), WeaponIdle = new(.13f, .74f, 1f), WeaponActive = new(.96f, .98f, .73f),
            ActiveRim = new(.22f, .94f, .32f), WeaponLabel = new(.05f, .22f, .40f), Green = new(.45f, .75f, .13f),
            Purple = new(.44f, .32f, .71f), BossRed = new(1f, .42f, .32f), StarOff = new(.25f, .27f, .36f, .85f);

        CanvasScaler scaler;
        RectTransform safeRoot, weaponPanel, weaponGrid, pageBar, progressFill, starRow;
        Text levelText, percentText, modelText, coinText, winTitle, winScore, winRibbon, winUnlock, winCoins, pageLabel, powerLabel;
        RawImage winIcon;
        readonly RawImage[] stars = new RawImage[3];
        GameObject winPanel, nextButton, powerAd, powerBuy;
        Rect appliedSafeArea;
        float bottomReserve = 330f, sideReserve = 420f;
        bool landscape;
        readonly Layout layout = new();
        readonly Dictionary<int, RectTransform> weaponButtons = new();
        readonly List<WeaponButton> lastWeapons = new();
        int lastSelected, lastPage;

        public event Action Restart, Next, Replay, LayoutChanged, SettingsRequested, LevelsRequested, ShopRequested,
            PowerAdRequested, PowerBuyRequested;
        public event Action<int> WeaponSelected, PageChanged;

        // Sibling index of the win overlay: feedback layers go just beneath it.
        public int WinIndex => winPanel.transform.GetSiblingIndex();
        public bool IsLandscape => landscape;

        // Screen-pixel edges of the rectangle the sculpture must fit into (top/bottom from the top).
        public float TopPx => (Screen.height - Screen.safeArea.yMax) + (landscape ? TopReserveLandscape : TopReservePortrait) * Scale;
        public float BottomPx => Screen.height - Screen.safeArea.yMin - (landscape ? 30f : bottomReserve) * Scale;
        public float LeftPx => Screen.safeArea.xMin + (landscape ? 40f : SideMargin) * Scale;
        public float RightPx => Screen.safeArea.xMax - (landscape ? sideReserve : SideMargin) * Scale;
        float Scale => (landscape ? Screen.height : Screen.width) / Ref;

        public void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            gameObject.AddComponent<GraphicRaycaster>();
            landscape = Landscape;
            ApplyScaler();

            safeRoot = Stretch("Safe Area", transform);
            ApplySafeArea();

            var top = Rect("Top Bar", safeRoot, new Vector2(0, 1), Vector2.one, new Vector2(0, -TopReservePortrait), Vector2.zero);
            // Tapping the level title opens the level list, as in the reference.
            var title = Box("Level Button", top, Color.clear, new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(620, 110));
            layout.Add(title, new Vector2(0, -16));
            title.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => LevelsRequested?.Invoke());
            levelText = Label("Level", title, 84, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 110));
            levelText.resizeTextForBestFit = true;
            levelText.resizeTextMinSize = 40;
            levelText.resizeTextMaxSize = 84;
            var bar = Box("Progress", top, Navy, new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(660, 66), 33);
            layout.Add(bar, new Vector2(0, -132), new Vector2(600, 60));
            Border(bar, Color.white, 4);
            var fillRoot = Rect("Fill Area", bar, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            progressFill = Box("Fill", fillRoot, Gold, Vector2.zero, Vector2.zero, Vector2.zero, 27);
            progressFill.anchorMin = Vector2.zero; progressFill.anchorMax = new Vector2(0, 1);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
            percentText = Label("Percent", bar, 44, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 66));
            modelText = Label("Model", top, 44, FontStyle.Bold, new Vector2(.5f, 1), new Vector2(0, -230), new Vector2(900, 60));
            layout.Add(modelText.rectTransform, new Vector2(0, -205));

            Button("Restart", top, "RETRY", new Vector2(1, 1), new Vector2(-30, -30), new Vector2(170, 130), () => Restart?.Invoke());
            var shop = UiKit.Button("Shop", top, "SHOP", new Vector2(1, 1), new Vector2(-30, -180), new Vector2(170, 86), Purple, () => ShopRequested?.Invoke(), 24, 40);
            Border(shop, new Color(.89f, .96f, 1f), 4);
            var settings = UiKit.Button("Settings", top, null, new Vector2(0, 1), new Vector2(30, -30), new Vector2(130, 130), Blue, () => SettingsRequested?.Invoke(), 30);
            Border(settings, new Color(.89f, .96f, 1f), 5);
            Picture("Gear", settings, Gear(128), new Vector2(.5f, .5f), Vector2.zero, new Vector2(96, 96));
            var coins = Box("Coins", top, new Color(.07f, .1f, .22f, .78f), new Vector2(0, 1), new Vector2(30, -180), new Vector2(230, 80), 40);
            Picture("Coin", coins, Coin(96), new Vector2(0, .5f), new Vector2(8, 0), new Vector2(64, 64));
            coinText = Label("Amount", coins, 42, FontStyle.Bold, new Vector2(1, .5f), new Vector2(-14, 0), new Vector2(150, 70));
            coinText.alignment = TextAnchor.MiddleRight;
            coinText.color = Gold;

            weaponPanel = Box("Weapon Panel", safeRoot, PanelColor, new Vector2(.5f, 0), new Vector2(0, PanelBottom), new Vector2(PanelMaxWidth, 300), 58);
            Border(weaponPanel, new Color(.94f, .95f, 1f), 5);
            weaponGrid = Stretch("Grid", weaponPanel);
            pageBar = Rect("Pages", weaponPanel, Vector2.zero, new Vector2(1, 0), new Vector2(0, PanelPadding * .5f), new Vector2(0, PageControls));
            Button("Prev Page", pageBar, "<", new Vector2(.5f, .5f), new Vector2(-130, 0), new Vector2(100, 70), () => PageChanged?.Invoke(-1));
            Button("Next Page", pageBar, ">", new Vector2(.5f, .5f), new Vector2(130, 0), new Vector2(100, 70), () => PageChanged?.Invoke(1));
            pageLabel = Label("Page", pageBar, 40, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(150, 70));

            BuildWin();
            ApplyLayout();
        }

        void BuildWin()
        {
            winPanel = Backdrop("Win", transform, new Color(0, 0, 0, .45f)).gameObject;
            var win = (RectTransform)winPanel.transform;
            var c = new Vector2(.5f, .5f);
            winTitle = Label("Title", win, 130, FontStyle.Bold, c, new Vector2(0, 600), new Vector2(1000, 170));
            layout.Add(winTitle.rectTransform, new Vector2(0, 405), new Vector2(1400, 170));
            winTitle.color = Gold;
            winRibbon = Label("Ribbon", win, 56, FontStyle.Bold, c, new Vector2(0, 470), new Vector2(1000, 90));
            layout.Add(winRibbon.rectTransform, new Vector2(0, 295));
            starRow = Rect("Stars", win, c, c, Vector2.zero, Vector2.zero);
            Place(starRow, c, new Vector2(0, 360), new Vector2(420, 130));
            layout.Add(starRow, new Vector2(-380, 175));
            var star = Star(128);
            for (int i = 0; i < 3; i++)
                stars[i] = Picture("Star", starRow, star, c, new Vector2((i - 1) * 135, i == 1 ? 12 : 0), Vector2.one * (i == 1 ? 130 : 110));
            winScore = Label("Score", win, 46, FontStyle.Bold, c, new Vector2(0, 265), new Vector2(1000, 76));
            layout.Add(winScore.rectTransform, new Vector2(-380, 70), new Vector2(760, 76));
            winCoins = Label("Coins", win, 50, FontStyle.Bold, c, new Vector2(0, 192), new Vector2(1000, 76));
            layout.Add(winCoins.rectTransform, new Vector2(-380, -10), new Vector2(760, 76));
            winCoins.color = Gold;
            winIcon = Picture("Unlock Icon", win, null, c, new Vector2(0, 75), new Vector2(140, 140));
            layout.Add(winIcon.rectTransform, new Vector2(-380, -135), new Vector2(120, 120));
            winUnlock = Label("Unlock", win, 42, FontStyle.Bold, c, new Vector2(0, -32), new Vector2(1000, 70));
            layout.Add(winUnlock.rectTransform, new Vector2(-380, -245), new Vector2(760, 70));

            // Optional power-hit offer for the next level: watch an ad, or pay coins instead.
            powerLabel = Label("Power Offer", win, 38, FontStyle.Bold, c, new Vector2(0, -125), new Vector2(1000, 60));
            layout.Add(powerLabel.rectTransform, new Vector2(380, 185), new Vector2(760, 60));
            powerLabel.text = "Start next level with 3 POWER HITS";
            var ad = UiKit.Button("Power Ad", win, "WATCH AD", c, new Vector2(-175, -205), new Vector2(330, 90), Purple, () => PowerAdRequested?.Invoke(), 24, 38);
            layout.Add(ad, new Vector2(240, 100), new Vector2(260, 84));
            powerAd = ad.gameObject;
            var buy = UiKit.Button("Power Buy", win, null, c, new Vector2(175, -205), new Vector2(330, 90), new Color(.85f, .6f, .1f), () => PowerBuyRequested?.Invoke(), 24);
            layout.Add(buy, new Vector2(520, 100), new Vector2(260, 84));
            Picture("Coin", buy, Coin(96), new Vector2(0, .5f), new Vector2(18, 0), new Vector2(56, 56));
            Label("Price", buy, 38, FontStyle.Bold, c, new Vector2(24, 0), new Vector2(240, 80)).text = Core.Economy.PowerHitsPrice.ToString();
            powerBuy = buy.gameObject;

            var next = UiKit.Button("Next", win, "NEXT", c, new Vector2(0, -340), new Vector2(560, 150), Green, () => Next?.Invoke(), 36, 60);
            layout.Add(next, new Vector2(380, -75), new Vector2(520, 140));
            nextButton = next.gameObject;
            Border(next.GetComponent<Image>(), Color.white, 6);
            var replay = UiKit.Button("Replay", win, "Replay level", c, new Vector2(0, -480), new Vector2(420, 100), Blue, () => Replay?.Invoke(), 24, 44);
            layout.Add(replay, new Vector2(380, -225), new Vector2(380, 96));
            winPanel.SetActive(false);
        }

        public void SetLevel(int number, string modelName, bool boss)
        {
            levelText.text = boss ? $"BOSS · Level {number}" : $"Level {number}";
            levelText.color = boss ? BossRed : Color.white;
            modelText.text = modelName;
            winPanel.SetActive(false);
        }

        public void SetProgress(float progress)
        {
            int pct = Mathf.FloorToInt(progress * 100f);
            progressFill.anchorMax = new Vector2(pct / 100f, 1);
            percentText.text = pct + "%";
        }

        public void SetCoins(int amount) => coinText.text = amount.ToString("N0");

        public void ShowWin(string title, string ribbon, Texture icon, string unlock, string score, string coins, int starCount, bool showNext)
        {
            nextButton.SetActive(showNext);
            winTitle.text = title;
            winRibbon.text = ribbon;
            winIcon.texture = icon;
            winIcon.enabled = icon != null;
            winUnlock.text = unlock;
            winScore.text = score;
            winCoins.text = coins;
            for (int i = 0; i < 3; i++) stars[i].color = i < starCount ? Gold : StarOff;
            winPanel.SetActive(true);
        }

        // Shows or hides each side of the power-hit offer; the label hides when neither applies.
        public void SetPowerOffer(bool adAvailable, bool canAfford, bool offered)
        {
            powerAd.SetActive(offered && adAvailable);
            powerBuy.SetActive(offered);
            powerBuy.GetComponent<UnityEngine.UI.Button>().interactable = canAfford;
            powerLabel.gameObject.SetActive(offered);
        }

        public struct WeaponButton
        {
            public int weapon;
            public string name;
            public Texture icon;
            public bool isNew;
        }

        // Lays out one page of the inventory: up to 4x2 along the bottom in portrait, 2x3 down the right
        // in landscape. Raises LayoutChanged when the reserved band changed size.
        public void SetWeapons(IReadOnlyList<WeaponButton> available, int selected, int page, out int clampedPage)
        {
            lastWeapons.Clear();
            lastWeapons.AddRange(available);
            lastSelected = selected;
            int count = Mathf.Max(1, available.Count);
            int maxColumns, pageSize;
            if (landscape)
            {
                maxColumns = Mathf.Min(2, count);
                pageSize = (int)SideRows * 2;
            }
            else
            {
                maxColumns = Mathf.Max(1, Mathf.Min(4, Mathf.Min(count, Mathf.FloorToInt((PanelMaxWidth - PanelPadding * 2 - PanelBorder + PanelGap) / (ButtonMin + PanelGap)))));
                pageSize = Mathf.Min(8, maxColumns * 2);
            }
            int pages = Mathf.CeilToInt(count / (float)pageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            clampedPage = lastPage = page;
            int visible = Mathf.Min(pageSize, available.Count - page * pageSize);
            int columns = Mathf.Max(1, Mathf.Min(maxColumns, visible)), rows = Mathf.CeilToInt(visible / (float)columns);
            if (landscape) columns = maxColumns;
            float width = landscape
                ? columns * SideButton + (columns - 1) * PanelGap + PanelPadding * 2 + PanelBorder
                : Mathf.Min(PanelMaxWidth, columns * ButtonMax + (columns - 1) * PanelGap + PanelPadding * 2 + PanelBorder);
            float button = (width - PanelPadding * 2 - PanelBorder - PanelGap * (columns - 1)) / columns;
            float controls = pages > 1 ? PageControls : 0f;
            float height = button * rows + PanelGap * (rows - 1) + PanelPadding * 2 + PanelBorder + controls;
            weaponPanel.sizeDelta = new Vector2(Mathf.Max(width, pages > 1 ? 380f : 0f), height);
            width = weaponPanel.sizeDelta.x;
            pageBar.gameObject.SetActive(pages > 1);
            pageLabel.text = $"{page + 1} / {pages}";

            weaponButtons.Clear();
            for (int i = weaponGrid.childCount - 1; i >= 0; i--) Destroy(weaponGrid.GetChild(i).gameObject);
            float gridWidth = columns * button + (columns - 1) * PanelGap;
            for (int k = 0; k < visible; k++)
            {
                var w = available[page * pageSize + k];
                int row = k / columns, col = k % columns;
                bool active = w.weapon == selected;
                var x = -gridWidth * .5f + col * (button + PanelGap) + button * .5f;
                var y = -(PanelPadding + PanelBorder * .5f + row * (button + PanelGap) + button * .5f);
                var rt = Box(w.name, weaponGrid, active ? WeaponActive : WeaponIdle, new Vector2(.5f, 1), new Vector2(x, y), new Vector2(button, button), button * .14f);
                rt.pivot = new Vector2(.5f, .5f);
                rt.anchoredPosition = new Vector2(x, y);
                weaponButtons[w.weapon] = rt;
                var outline = rt.gameObject.AddComponent<Outline>();
                outline.effectColor = active ? ActiveRim : new Color(.27f, .48f, .77f);
                outline.effectDistance = active ? new Vector2(8, -8) : new Vector2(4, -4);
                int weapon = w.weapon;
                rt.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => WeaponSelected?.Invoke(weapon));
                Picture("Icon", rt, w.icon, new Vector2(.5f, .5f), new Vector2(0, button * .08f), Vector2.one * button * .78f);
                var label = Label("Name", rt, Mathf.Max(24, Mathf.RoundToInt(button * .15f)), FontStyle.Bold, new Vector2(.5f, 0), new Vector2(0, button * .04f), new Vector2(button * 1.1f, button * .24f));
                label.text = w.name;
                label.color = WeaponLabel;
                label.GetComponent<Outline>().effectColor = new Color(1, 1, 1, .8f);
                label.GetComponent<Outline>().effectDistance = new Vector2(0, -2);
                if (w.isNew)
                {
                    var badge = Box("New", rt, BossRed, new Vector2(1, 1), new Vector2(10, 10), new Vector2(84, 40), 14);
                    badge.pivot = new Vector2(1, 1);
                    badge.GetComponent<Image>().raycastTarget = false;
                    Label("Text", badge, 28, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(84, 40)).text = "NEW";
                }
            }

            float bottom = landscape ? 0f : PanelBottom + height + PanelClearance;
            float side = landscape ? width + SidePanelMargin + 40f : 0f;
            if (!Mathf.Approximately(bottom, bottomReserve) || !Mathf.Approximately(side, sideReserve))
            {
                bottomReserve = bottom;
                sideReserve = side;
                LayoutChanged?.Invoke();
            }
        }

        // Where a weapon's button is on screen, when it is on the visible page.
        public bool WeaponButtonScreenPoint(int weapon, out Vector2 point)
        {
            point = default;
            if (!weaponButtons.TryGetValue(weapon, out var rt) || !rt) return false;
            point = RectTransformUtility.WorldToScreenPoint(null, rt.position);
            return true;
        }

        void Update()
        {
            // The HUD is built once the platform SDK is ready, which on the web is a few frames in.
            if (!scaler) return;
            if (Landscape != landscape)
            {
                landscape = Landscape;
                ApplyScaler();
                ApplyLayout();
                SetWeapons(lastWeapons, lastSelected, lastPage, out _);
                LayoutChanged?.Invoke();
            }
            if (Screen.safeArea != appliedSafeArea) ApplySafeArea();
        }

        void ApplyScaler()
        {
            scaler.referenceResolution = landscape ? new Vector2(1920f, Ref) : new Vector2(Ref, 1920f);
            scaler.matchWidthOrHeight = landscape ? 1f : 0f;
        }

        void ApplyLayout()
        {
            layout.Apply(landscape);
            weaponPanel.anchorMin = weaponPanel.anchorMax = weaponPanel.pivot = landscape ? new Vector2(1, .5f) : new Vector2(.5f, 0);
            weaponPanel.anchoredPosition = landscape ? new Vector2(-SidePanelMargin, -40f) : new Vector2(0, PanelBottom);
            ((RectTransform)safeRoot.Find("Top Bar")).offsetMin = new Vector2(0, -(landscape ? TopReserveLandscape : TopReservePortrait));
        }

        void ApplySafeArea()
        {
            appliedSafeArea = Screen.safeArea;
            var size = new Vector2(Screen.width, Screen.height);
            safeRoot.anchorMin = appliedSafeArea.position / size;
            safeRoot.anchorMax = (appliedSafeArea.position + appliedSafeArea.size) / size;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }

        void Button(string name, Transform parent, string caption, Vector2 anchor, Vector2 position, Vector2 size, Action onClick) =>
            UiKit.Button(name, parent, caption, anchor, position, size, Blue, onClick);
    }
}
