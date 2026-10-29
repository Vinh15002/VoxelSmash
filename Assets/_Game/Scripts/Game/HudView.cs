using System;
using UnityEngine;
using UnityEngine.UI;
using static VoxelSmash.Game.UiKit;

namespace VoxelSmash.Game
{
    // Code-built uGUI HUD so the scene needs no hand-authored UI. Reference resolution 1080x1920,
    // scaled by width; the top bar and bottom panel heights define the camera's clear band.
    public sealed class HudView : MonoBehaviour
    {
        const float RefWidth = 1080f, TopReserve = 330f, SideMargin = 24f;
        // Inventory metrics from the reference layout, in reference units (~2.6 per CSS px).
        const float PanelMaxWidth = 880f, PanelPadding = 30f, PanelGap = 20f, PanelBorder = 10f,
            ButtonMin = 115f, ButtonMax = 190f, PageControls = 126f, PanelBottom = 31f, PanelClearance = 52f;

        static readonly Color Navy = new(.11f, .14f, .30f), Blue = new(.13f, .55f, .93f), Gold = new(1f, .80f, .13f),
            PanelColor = new(.34f, .48f, .78f, .95f), WeaponIdle = new(.13f, .74f, 1f), WeaponActive = new(.96f, .98f, .73f),
            ActiveRim = new(.22f, .94f, .32f), WeaponLabel = new(.05f, .22f, .40f), Green = new(.45f, .75f, .13f);

        RectTransform safeRoot, weaponPanel, weaponGrid, pageBar;
        Text levelText, percentText, modelText, winTitle, winScore, winRibbon, winUnlock, pageLabel;
        RawImage winIcon;
        RectTransform progressFill;
        GameObject winPanel, nextButton;
        Rect appliedSafeArea;
        float bottomReserve = 330f;

        public event Action Restart, Next, Replay, LayoutChanged, SettingsRequested, LevelsRequested;
        public event Action<int> WeaponSelected, PageChanged;

        // Sibling index of the win overlay: feedback layers go just beneath it.
        public int WinIndex => winPanel.transform.GetSiblingIndex();

        // Screen-pixel extents of the band the sculpture must fit into.
        public float TopPx => (Screen.height - Screen.safeArea.yMax) + TopReserve * Scale;
        public float BottomPx => Screen.height - (Screen.safeArea.yMin + bottomReserve * Scale);
        public float SidePx => SideMargin * Scale;
        static float Scale => Screen.width / RefWidth;

        public void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, 1920f);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            safeRoot = Rect("Safe Area", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ApplySafeArea();

            var top = Rect("Top Bar", safeRoot, new Vector2(0, 1), Vector2.one, new Vector2(0, -TopReserve), Vector2.zero);
            // Tapping the level title opens the level list, as in the reference.
            var title = Box("Level Button", top, Color.clear, new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(560, 110));
            title.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => LevelsRequested?.Invoke());
            levelText = Label("Level", title, 84, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(560, 110));
            var bar = Box("Progress", top, Navy, new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(660, 66), 33);
            Border(bar, Color.white, 4);
            var fillRoot = Rect("Fill Area", bar, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            progressFill = Box("Fill", fillRoot, Gold, Vector2.zero, Vector2.zero, Vector2.zero, 27);
            progressFill.anchorMin = Vector2.zero; progressFill.anchorMax = new Vector2(0, 1);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
            percentText = Label("Percent", bar, 44, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 66));
            modelText = Label("Model", top, 44, FontStyle.Bold, new Vector2(.5f, 1), new Vector2(0, -230), new Vector2(900, 60));
            Button("Restart", top, "RETRY", new Vector2(1, 1), new Vector2(-30, -30), new Vector2(170, 130), () => Restart?.Invoke());
            var settings = UiKit.Button("Settings", top, null, new Vector2(0, 1), new Vector2(30, -30), new Vector2(130, 130), Blue, () => SettingsRequested?.Invoke(), 30);
            Border(settings, new Color(.89f, .96f, 1f), 5);
            Picture("Gear", settings, Gear(128), new Vector2(.5f, .5f), Vector2.zero, new Vector2(96, 96));

            weaponPanel = Box("Weapon Panel", safeRoot, PanelColor, new Vector2(.5f, 0), new Vector2(0, PanelBottom), new Vector2(PanelMaxWidth, 300), 58);
            Border(weaponPanel, new Color(.94f, .95f, 1f), 5);
            weaponGrid = Rect("Grid", weaponPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pageBar = Rect("Pages", weaponPanel, Vector2.zero, new Vector2(1, 0), new Vector2(0, PanelPadding * .5f), new Vector2(0, PageControls));
            Button("Prev Page", pageBar, "<", new Vector2(.5f, .5f), new Vector2(-180, 0), new Vector2(110, 70), () => PageChanged?.Invoke(-1));
            Button("Next Page", pageBar, ">", new Vector2(.5f, .5f), new Vector2(180, 0), new Vector2(110, 70), () => PageChanged?.Invoke(1));
            pageLabel = Label("Page", pageBar, 40, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(200, 70));

            winPanel = Box("Win", transform, new Color(0, 0, 0, .45f), Vector2.zero, Vector2.zero, Vector2.zero).gameObject;
            var winRect = (RectTransform)winPanel.transform;
            winRect.anchorMin = Vector2.zero; winRect.anchorMax = Vector2.one; winRect.offsetMin = winRect.offsetMax = Vector2.zero;
            winTitle = Label("Title", winRect, 130, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(0, 600), new Vector2(1000, 170));
            winTitle.color = Gold;
            winScore = Label("Score", winRect, 48, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(0, 340), new Vector2(1000, 80));
            winRibbon = Label("Ribbon", winRect, 56, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(0, 450), new Vector2(1000, 90));
            var iconRt = new GameObject("Unlock Icon", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
            iconRt.SetParent(winRect, false);
            Place(iconRt, new Vector2(.5f, .5f), new Vector2(0, 190), new Vector2(170, 170));
            winIcon = iconRt.GetComponent<RawImage>();
            winIcon.raycastTarget = false;
            winUnlock = Label("Unlock", winRect, 44, FontStyle.Bold, new Vector2(.5f, .5f), new Vector2(0, 55), new Vector2(1000, 70));
            nextButton = UiKit.Button("Next", winRect, "NEXT", new Vector2(.5f, .5f), new Vector2(0, -120), new Vector2(560, 150), Green, () => Next?.Invoke(), 36, 60).gameObject;
            Border(nextButton.GetComponent<Image>(), Color.white, 6);
            Button("Replay", winRect, "Replay level", new Vector2(.5f, .5f), new Vector2(0, -300), new Vector2(420, 110), () => Replay?.Invoke());
            winPanel.SetActive(false);
        }

        public void SetLevel(int number, string modelName)
        {
            levelText.text = $"Level {number}";
            modelText.text = modelName;
            winPanel.SetActive(false);
        }

        public void SetProgress(float progress)
        {
            int pct = Mathf.FloorToInt(progress * 100f);
            progressFill.anchorMax = new Vector2(pct / 100f, 1);
            percentText.text = pct + "%";
        }

        public void ShowWin(string title, string ribbon, Texture icon, string unlock, string score, bool showNext)
        {
            nextButton.SetActive(showNext);
            winTitle.text = title;
            winRibbon.text = ribbon;
            winIcon.texture = icon;
            winIcon.enabled = icon != null;
            winUnlock.text = unlock;
            winScore.text = score;
            winPanel.SetActive(true);
        }

        public struct WeaponButton
        {
            public int weapon;
            public string name;
            public Texture icon;
        }

        // Lays out one page of the inventory as a grid of square buttons (up to 4 columns, 2 rows)
        // and resizes the reserved bottom band; raises LayoutChanged when the band height moved.
        public void SetWeapons(System.Collections.Generic.IReadOnlyList<WeaponButton> available, int selected, int page, out int clampedPage)
        {
            int count = Mathf.Max(1, available.Count);
            int maxColumns = Mathf.Max(1, Mathf.Min(4, Mathf.Min(count, Mathf.FloorToInt((PanelMaxWidth - PanelPadding * 2 - PanelBorder + PanelGap) / (ButtonMin + PanelGap)))));
            int pageSize = Mathf.Min(8, maxColumns * 2), pages = Mathf.CeilToInt(count / (float)pageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            clampedPage = page;
            int visible = Mathf.Min(pageSize, available.Count - page * pageSize);
            int columns = Mathf.Max(1, Mathf.Min(maxColumns, visible)), rows = Mathf.CeilToInt(visible / (float)columns);
            float width = Mathf.Min(PanelMaxWidth, columns * ButtonMax + (columns - 1) * PanelGap + PanelPadding * 2 + PanelBorder);
            float button = (width - PanelPadding * 2 - PanelBorder - PanelGap * (columns - 1)) / columns;
            float controls = pages > 1 ? PageControls : 0f;
            float height = button * rows + PanelGap * (rows - 1) + PanelPadding * 2 + PanelBorder + controls;
            weaponPanel.sizeDelta = new Vector2(width, height);
            pageBar.gameObject.SetActive(pages > 1);
            pageLabel.text = $"{page + 1} / {pages}";

            for (int i = weaponGrid.childCount - 1; i >= 0; i--) Destroy(weaponGrid.GetChild(i).gameObject);
            for (int k = 0; k < visible; k++)
            {
                var w = available[page * pageSize + k];
                int row = k / columns, col = k % columns;
                bool active = w.weapon == selected;
                var x = PanelPadding + PanelBorder * .5f + col * (button + PanelGap) + button * .5f - width * .5f;
                var y = -(PanelPadding + PanelBorder * .5f + row * (button + PanelGap) + button * .5f);
                var rt = Box(w.name, weaponGrid, active ? WeaponActive : WeaponIdle, new Vector2(.5f, 1), new Vector2(x, y), new Vector2(button, button), button * .14f);
                rt.pivot = new Vector2(.5f, .5f);
                rt.anchoredPosition = new Vector2(x, y);
                var outline = rt.gameObject.AddComponent<Outline>();
                outline.effectColor = active ? ActiveRim : new Color(.27f, .48f, .77f);
                outline.effectDistance = active ? new Vector2(8, -8) : new Vector2(4, -4);
                int weapon = w.weapon;
                rt.gameObject.AddComponent<Button>().onClick.AddListener(() => WeaponSelected?.Invoke(weapon));
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
                icon.SetParent(rt, false);
                Place(icon, new Vector2(.5f, .5f), new Vector2(0, button * .08f), Vector2.one * button * .78f);
                var raw = icon.GetComponent<RawImage>();
                raw.texture = w.icon;
                raw.raycastTarget = false;
                var label = Label("Name", rt, Mathf.RoundToInt(button * .15f), FontStyle.Bold, new Vector2(.5f, 0), new Vector2(0, button * .04f), new Vector2(button, button * .22f));
                label.text = w.name;
                label.color = WeaponLabel;
                label.GetComponent<Outline>().effectColor = new Color(1, 1, 1, .8f);
                label.GetComponent<Outline>().effectDistance = new Vector2(0, -2);
            }

            float reserve = PanelBottom + height + PanelClearance;
            if (!Mathf.Approximately(reserve, bottomReserve))
            {
                bottomReserve = reserve;
                LayoutChanged?.Invoke();
            }
        }

        void Update()
        {
            if (Screen.safeArea != appliedSafeArea) ApplySafeArea();
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
