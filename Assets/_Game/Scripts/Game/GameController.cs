using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Game loop: title -> load a level, tap/hold to strike with the selected weapon, drag to rotate, win at 100%.
    // Gold: every strike costs its weapon's price from the level's purse, smashed cubes pay into it, and
    // running out before the sculpture is gone loses the level. What is left on a win goes to the bank,
    // which buys upgrades and power hits. Around it: stars, combos and critical hits, boss levels, the
    // step-by-step onboarding hints, and the platform hooks (saves, gameplay start/stop).
    public sealed class GameController : MonoBehaviour
    {
        const string FinishedKey = "voxel-smash.finished", LegacyLevelKey = "voxel-smash.level-number",
            SelectedKey = "voxel-smash.weapon", QualityKey = "voxel-smash.quality", TutorialKey = "voxel-smash.tutorial",
            CoinsKey = "voxel-smash.coins", SizeKey = "voxel-smash.upgrade-size", MagnetKey = "voxel-smash.upgrade-magnet",
            PowerKey = "voxel-smash.power-hits", StarsKey = "voxel-smash.stars", SeenKey = "voxel-smash.weapons-seen";
        const float HoldDelay = .22f, DragThresholdDp = 13f, YawPerScreenWidth = 3.7f;
        // Six slow (>160 ms) frames drop to low detail, which caps physics bodies at 110 instead of 260
        // and turns off shadows. The last hit plays in slow motion.
        const float SlowFrame = .16f, LowRenderScale = .75f, SlowMotion = .3f, SlowMotionTime = .7f, HitStop = .06f;
        const int SlowFramesToDowngrade = 6, BodiesHigh = 260, BodiesLow = 110;
        // Onboarding steps, stored as bits; the first was a plain flag in earlier saves, which still reads right.
        const int TapStep = 1, HoldStep = 2, DragStep = 4, AllSteps = TapStep | HoldStep | DragStep;
        static readonly Color Toxic = ((Color)new Color32(0x86, 0xe5, 0x2b, 255)).linear;
        public const string SculptureLayer = "Sculpture", DebrisLayer = "Debris", PreviewLayer = "Preview";

        struct QueuedHit
        {
            public float delay;
            public Vector3 local;
            public int weapon;
            public float scale;
        }

        [SerializeField] LevelList levels;
        [SerializeField] WeaponDef[] weapons;
        // PixelLand's per-colour cube materials (TCP2 Hybrid) for the sculpture, debris and weapons.
        [SerializeField] Material[] voxelPalette;
        [SerializeField] Material fragmentMaterial;
        [SerializeField] Material fxAlphaMaterial;
        [SerializeField] Material fxAdditiveMaterial;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] HudView hud;

        MenuView menu;
        FeedbackView feedback;
        Progression campaign;
        bool ready, quality;
        int tutorial, weaponsSeen, slowFrames;
        float baseRenderScale = -1f, slowMotion, hitStop;
        Light sun;

        int coins, sizeLevel, magnetLevel, powerHits;
        readonly Dictionary<int, int> bestStars = new();
        int combo, hitsThisLevel;
        float lastComboAt = -5f;
        // Gold for this level (see Economy.StartPurse); `coins` is the bank.
        int purse;
        bool lost;

        Sculpture sculpture;
        SculptureView view;
        // The current model's framing; computing it walks every voxel, so it is cached per level.
        TargetFrame frame;
        DebrisField debris;
        HitEffects effects;
        WeaponAnimator weaponAnimator;
        Sfx sfx;
        Texture[] icons;
        OccupancyGrid occupancy;
        PhysicsMaterial voxelPhysics;
        VoxelMaterials voxelMaterials;
        int sculptureLayer, sculptureMask;
        int levelNumber, selected, weaponPage;
        bool won;
        float yaw = Framing.StartYaw, clock, lastHit = -5f;
        bool held, dragged, pressStartedOnUi;
        Vector2 downPos;
        float downAt, startYaw;
        int loadedFrame = -1;
        GameObject loadingCover;
        Vector2Int fittedScreen;
        readonly List<RaycastResult> uiHits = new();
        readonly List<QueuedHit> queuedHits = new();
        readonly List<Color> hitPalette = new();
        readonly List<HudView.WeaponButton> buttons = new();

        static IPlatformBackend Save => Platform.Backend;
        VoxelModel Model => levels.ModelFor(levelNumber);
        bool Boss => levels.IsBoss(levelNumber);
        bool Paused => menu.TitleOpen || menu.MenuOpen;
        static bool Touch => Touchscreen.current != null;

        // The SDK must finish initialising before saves can be read, so everything waits for it.
        void Start()
        {
            loadingCover = LoadingCover();
            Platform.Backend.Init(Boot);
        }

        // SDK start-up can take seconds; until the title is up, cover the bare scene (unfitted camera,
        // no HUD) with the title colour so the game never looks broken while it loads.
        static GameObject LoadingCover()
        {
            var go = new GameObject("Loading Cover", typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            UiKit.Backdrop("Back", go.transform, new Color(.05f, .12f, .24f));
            UiKit.Label("Text", go.transform, 36, FontStyle.Bold, new Vector2(.5f, .5f), Vector2.zero, new Vector2(600, 80)).text = "Loading...";
            return go;
        }

        void Boot()
        {
            // Browsers pace frames themselves; a target rate there only adds jitter.
            if (!Save.IsWeb) Application.targetFrameRate = 60;
            // Match the reference's cannon-es world: 60 Hz steps, 7 solver iterations, voxel-on-voxel contact.
            Time.fixedDeltaTime = 1f / 60f;
            Physics.defaultSolverIterations = 7;
            voxelPhysics = new PhysicsMaterial("Voxel")
            {
                dynamicFriction = .66f, staticFriction = .66f, bounciness = .23f,
                frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Average
            };
            sculptureLayer = Mathf.Max(0, LayerMask.NameToLayer(SculptureLayer));
            sculptureMask = 1 << sculptureLayer;
            int previewLayer = Mathf.Max(0, LayerMask.NameToLayer(PreviewLayer));
            cameraRig.Camera.cullingMask &= ~(1 << previewLayer);

            voxelMaterials = new VoxelMaterials(voxelPalette);
            WeaponModels.Materials = voxelMaterials;
            debris = new GameObject("Debris").AddComponent<DebrisField>();
            debris.Init(voxelMaterials, voxelPhysics, Mathf.Max(0, LayerMask.NameToLayer(DebrisLayer)));
            effects = new GameObject("Effects").AddComponent<HitEffects>();
            effects.Init(fxAlphaMaterial, fxAdditiveMaterial, fragmentMaterial);
            weaponAnimator = new GameObject("Weapons").AddComponent<WeaponAnimator>();
            weaponAnimator.Init(weapons, cameraRig.Camera, fxAlphaMaterial);
            sfx = new GameObject("Sfx").AddComponent<Sfx>();
            sfx.Init();
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) sun = light;
            icons = WeaponIcons.Render(weapons, previewLayer, sun);

            hud.Build();
            hud.Restart += () => LoadLevel(levelNumber);
            hud.Replay += () => LoadLevel(levelNumber);
            hud.Next += NextLevel;
            hud.LayoutChanged += Fit;
            hud.WeaponSelected += SelectWeapon;
            hud.PageChanged += delta => { weaponPage += delta; RefreshWeapons(); };
            hud.SettingsRequested += OpenSettings;
            hud.LevelsRequested += OpenLevels;
            hud.ShopRequested += OpenShop;
            hud.Continue += ContinueFromBank;
            feedback = new GameObject("Feedback").AddComponent<FeedbackView>();
            feedback.Build(hud.transform, hud.WinIndex);
            feedback.SkipRequested += SkipTutorial;
            menu = UiKit.Stretch("Menus", hud.transform).gameObject.AddComponent<MenuView>();
            menu.Build(icons[Mathf.Max(0, System.Array.FindIndex(weapons, w => w.type == WeaponType.Hammer))], HelpText());
            menu.Play += PlayFromTitle;
            menu.Resume += CloseMenus;
            menu.OpenLevels += OpenLevels;
            menu.MainMenu += OpenTitle;
            menu.ToggleQuality += () => SetQuality(!quality);
            menu.LevelChosen += n => LoadLevel(n);
            menu.BuySize += () => BuyUpgrade(ref sizeLevel, Economy.SizeCost(sizeLevel), SizeKey);
            menu.BuyMagnet += () => BuyUpgrade(ref magnetLevel, Economy.MagnetCost(magnetLevel), MagnetKey);
            menu.BuyPower += BuyPowerHits;

            LoadSave();
            ApplyQuality();
            ready = true;
            LoadLevel(campaign.ResumeLevel);
            OpenTitle();
            Destroy(loadingCover);
        }

        void LoadSave()
        {
            campaign = Progression.Parse(levels.Length, Save.GetString(FinishedKey, ""), Save.GetInt(LegacyLevelKey, 0));
            quality = Save.GetInt(QualityKey, Save.PreferLowQuality ? 0 : 1) == 1;
            tutorial = Save.GetInt(TutorialKey, 0);
            // Weapons available from the start never count as new.
            weaponsSeen = Save.GetInt(SeenKey, StartingWeapons());
            selected = Mathf.Clamp(Save.GetInt(SelectedKey, 0), 0, weapons.Length - 1);
            coins = Save.GetInt(CoinsKey, 0);
            sizeLevel = Mathf.Clamp(Save.GetInt(SizeKey, 0), 0, Economy.MaxUpgrade);
            magnetLevel = Mathf.Clamp(Save.GetInt(MagnetKey, 0), 0, Economy.MaxUpgrade);
            powerHits = Save.GetInt(PowerKey, 0);
            bestStars.Clear();
            foreach (var pair in Save.GetString(StarsKey, "").Split(','))
            {
                var parts = pair.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int level) && int.TryParse(parts[1], out int stars))
                    bestStars[level] = Mathf.Clamp(stars, 1, 3);
            }
        }

        int StartingWeapons()
        {
            int mask = 0;
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i].AvailableAt(1)) mask |= 1 << i;
            return mask;
        }

        void SaveEconomy()
        {
            Save.SetInt(CoinsKey, coins);
            Save.SetInt(SizeKey, sizeLevel);
            Save.SetInt(MagnetKey, magnetLevel);
            Save.SetInt(PowerKey, powerHits);
            Save.Flush();
        }

        string HelpText()
        {
            string verb = Touch ? "Tap" : "Click";
            return $"{verb} the model to strike.\nHold for repeated hits.\nDrag sideways to turn the sculpture." +
                   (Save.IsWeb && !Touch ? "\nPress P to pause." : "");
        }

        void OpenTitle()
        {
            held = false;
            int done = campaign.Completed;
            menu.ShowTitle(done > 0 ? $"CONTINUE · LEVEL {campaign.ResumeLevel}" : "PLAY",
                done > 0 ? $"{done} / {campaign.Total} levels cleared" : $"{campaign.Total} levels. {weapons.Length} weapons. A boss every {LevelList.BossEvery}.");
            SyncPause();
        }

        void PlayFromTitle()
        {
            menu.HideTitle();
            LoadLevel(campaign.ResumeLevel);
        }

        void OpenSettings()
        {
            if (menu.TitleOpen || won || Platform.AdRunning) return;
            held = false;
            menu.ShowSettings(quality);
            SyncPause();
        }

        void OpenLevels()
        {
            if (menu.TitleOpen || Platform.AdRunning) return;
            held = false;
            menu.ShowLevels(campaign.Total, n => new MenuView.LevelInfo
            {
                state = campaign.IsFinished(n) ? MenuView.LevelState.Finished : campaign.CanPlay(n) ? MenuView.LevelState.Open : MenuView.LevelState.Locked,
                stars = bestStars.TryGetValue(n, out int s) ? s : 1,
                boss = levels.IsBoss(n)
            }, levelNumber);
            SyncPause();
        }

        void OpenShop()
        {
            if (menu.TitleOpen || Platform.AdRunning) return;
            held = false;
            menu.ShowShop(ShopState());
            SyncPause();
        }

        MenuView.ShopState ShopState() => new()
        {
            coins = coins, sizeLevel = sizeLevel, sizeCost = Economy.SizeCost(sizeLevel),
            magnetLevel = magnetLevel, magnetCost = Economy.MagnetCost(magnetLevel), powerHits = powerHits,
            maxLevel = Economy.MaxUpgrade, powerPrice = Economy.PowerHitsPrice
        };

        void BuyUpgrade(ref int level, int cost, string key)
        {
            if (level >= Economy.MaxUpgrade || coins < cost) return;
            coins -= cost;
            level++;
            SaveEconomy();
            menu.RefreshShop(ShopState());
            sfx.Play(WeaponSound.Glove);
        }

        // Power hits are bought from the bank in the shop.
        void BuyPowerHits()
        {
            if (coins < Economy.PowerHitsPrice) return;
            coins -= Economy.PowerHitsPrice;
            powerHits += Economy.PowerHits;
            SaveEconomy();
            feedback.Toast($"{powerHits} power hits ready!");
            menu.RefreshShop(ShopState());
        }

        // ---- Gold -----------------------------------------------------------------------------

        // The weapon lab strikes for free unless asked to play the economy; so does media capture.
        int StrikeCost(WeaponDef weapon) => allWeapons && labFree || captureSize.x > 0 ? 0 : weapon.strikeCost;

        int CheapestStrike()
        {
            int cheapest = int.MaxValue;
            for (int i = 0; i < weapons.Length; i++)
                if (Available(i)) cheapest = Mathf.Min(cheapest, StrikeCost(weapons[i]));
            return cheapest;
        }

        void SetPurse(int amount)
        {
            purse = amount;
            hud.SetGold(purse);
        }

        // Out of gold once every strike in flight has landed and no weapon is affordable.
        void CheckBroke()
        {
            if (won || lost || weaponAnimator.Busy || queuedHits.Count > 0 || purse >= CheapestStrike()) return;
            lost = true;
            held = false;
            feedback.HideHint();
            int topUp = Economy.ContinueGold(sculpture.Total);
            hud.ShowLose($"{Mathf.FloorToInt(sculpture.Progress * 100f)}% smashed", topUp, coins >= topUp);
            sfx.Play(WeaponSound.Hammer);
            SyncPause();
        }

        // Keeps the sculpture as it is and moves gold from the bank into the purse, one for one.
        void ContinueFromBank()
        {
            int topUp = Economy.ContinueGold(sculpture.Total);
            if (!lost || coins < topUp) return;
            coins -= topUp;
            SaveEconomy();
            lost = false;
            hud.HideLose();
            SetPurse(purse + topUp);
            SyncPause();
        }

        void CloseMenus()
        {
            menu.CloseMenus();
            SyncPause();
        }

        // Menus freeze the world (physics, debris, swings, effects) and the sound. While an ad runs the SDK
        // owns the time scale and audio and restores them itself.
        void SyncPause()
        {
            if (Platform.AdRunning) return;
            bool paused = Paused;
            Time.timeScale = paused ? 0f : (hitStop > 0f ? .04f : slowMotion > 0f ? SlowMotion : 1f) * labSpeed;
            AudioListener.pause = paused;
            Platform.SetPlaying(ready && !paused && !won && !lost);
        }

        // `remember` is false for the automatic downgrade: a few slow frames (shader warm-up, a busy
        // tab) shouldn't lock a capable device to LOW on every later visit.
        void SetQuality(bool high, bool remember = true)
        {
            quality = high;
            ApplyQuality();
            menu.SetQuality(high);
            if (!remember) return;
            Save.SetInt(QualityKey, high ? 1 : 0);
            Save.Flush();
        }

        void ApplyQuality()
        {
            debris.MaxBodies = quality ? BodiesHigh : BodiesLow;
            effects.Detail = quality;
            // Switch shadows at the light, not per camera: a camera that skips shadows while the light
            // casts them still samples an unbound shadow map on WebGL (GL texture/sampler mismatch).
            if (sun) sun.shadows = quality ? LightShadows.Soft : LightShadows.None;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (baseRenderScale < 0f) baseRenderScale = urp.renderScale;
                urp.renderScale = baseRenderScale * (quality ? 1f : LowRenderScale);
            }
        }

        void NextLevel()
        {
            int next = levelNumber + 1;
            LoadLevel(next, allWeapons);
        }

        bool LoadLevel(int number) => LoadLevel(number, false);

        bool LoadLevel(int number, bool unlocked)
        {
            if (!unlocked && !campaign.CanPlay(number))
            {
                feedback.Toast("Complete the previous level first.");
                return false;
            }
            levelNumber = number;
            loadedFrame = Time.frameCount;
            won = false;
            held = false;
            slowMotion = 0f;
            yaw = Framing.StartYaw;
            lastHit = -5f;
            combo = 0;
            hitsThisLevel = 0;
            lost = false;
            queuedHits.Clear();
            weaponAnimator.Cancel();
            effects.Clear();
            sfx.StopAll();
            if (view) Destroy(view.gameObject);
            debris.Clear();
            var model = Model;
            frame = Framing.For(model);
            sculpture = new Sculpture(model);
            SetPurse(Economy.StartPurse(sculpture.Total));
            view = SculptureView.Create(sculpture, model, voxelMaterials, voxelPhysics, sculptureLayer, transform);
            view.SetYaw(yaw);
            occupancy?.Dispose();
            occupancy = new OccupancyGrid(sculpture);
            debris.ClearRadius = Framing.DebrisClearance(model);
            debris.Occupancy = occupancy;
            debris.Sculpture = view.transform;
            if (!Available(selected)) selected = 0;
            hud.SetLevel(number, model.displayName, Boss);
            hud.SetProgress(0f);
            RefreshWeapons();
            Fit();
            feedback.ClearScores();
            feedback.HideHint();
            menu.CloseMenus();
            if (Boss) feedback.Toast("BOSS LEVEL! Double coins.");
            SaveEconomy();
            SyncPause();
            return true;
        }

        void SelectWeapon(int index)
        {
            if (won || !Available(index)) return;
            selected = index;
            weaponsSeen |= 1 << index;
            Save.SetInt(SelectedKey, index);
            Save.SetInt(SeenKey, weaponsSeen);
            RefreshWeapons();
        }

        bool Available(int index) => allWeapons || weapons[index].AvailableAt(levelNumber);

        void RefreshWeapons()
        {
            buttons.Clear();
            for (int i = 0; i < weapons.Length; i++)
                if (Available(i))
                    buttons.Add(new HudView.WeaponButton { weapon = i, name = weapons[i].displayName, icon = icons[i], cost = StrikeCost(weapons[i]), isNew = (weaponsSeen & (1 << i)) == 0 });
            hud.SetWeapons(buttons, selected, weaponPage, out weaponPage);
            hud.SetGold(purse);
        }

        void Fit()
        {
            if (!ready) return;
            fittedScreen = new Vector2Int(Screen.width, Screen.height);
            if (captureSize.x > 0)
            {
                // Media capture hides the HUD: frame the sculpture in most of the picture.
                float w = captureSize.x, h = captureSize.y;
                cameraRig.Fit(frame, h * .1f, h * .95f, w * .08f, w * .92f, w, h);
            }
            else cameraRig.Fit(frame, hud.TopPx, hud.BottomPx, hud.LeftPx, hud.RightPx);
        }

        // ---- Weapon lab (editor tool: every weapon on any level, see WeaponLab) ------------------

        bool allWeapons, labFree = true;
        float labSpeed = 1f;

        public WeaponDef[] Weapons => weapons;
        public int SelectedWeapon => selected;
        public int LevelNumber => levelNumber;

        // Loads `level` with every weapon available and the onboarding skipped; Next then keeps going
        // without locks or ads.
        public void BeginLab(int level)
        {
            allWeapons = true;
            tutorial = AllSteps;
            // Silences the "new weapon" pointers (picking a weapon then saves this mask, editor save only).
            weaponsSeen |= (1 << weapons.Length) - 1;
            menu.HideTitle();
            LoadLevel(Mathf.Clamp(level, 1, levels.Count), true);
        }

        public void LabSelect(int index) => SelectWeapon(index);

        // Strikes cost nothing in the lab unless this is turned off to try the gold economy.
        public bool LabFreeStrikes
        {
            get => labFree;
            set { labFree = value; RefreshWeapons(); }
        }

        public float LabSpeed
        {
            get => labSpeed;
            set { labSpeed = value; SyncPause(); }
        }

        // ---- Media capture (store covers and preview videos; see MediaCapture) ------------------

        Vector2Int captureSize;

        public bool Ready => ready;
        public bool Won => won;
        public bool SwingBusy => weaponAnimator.Busy;

        // Plays `level` with every weapon unlocked, no HUD and no onboarding, framed for a `size` picture.
        public void BeginCapture(int level, Vector2Int size)
        {
            captureSize = size;
            tutorial = AllSteps;
            hud.GetComponent<Canvas>().enabled = false;
            menu.HideTitle();
            LoadLevel(level, true);
        }

        public void CaptureWeapon(WeaponType type) => SelectWeapon(System.Array.FindIndex(weapons, w => w.type == type));

        public void CaptureYaw(float delta)
        {
            yaw += delta;
            view.SetYaw(yaw);
        }

        // Strikes the live cell found at fraction `u` of the model, as if the player tapped it.
        public bool CaptureStrike(float u, Camera renderCamera)
        {
            int start = Mathf.Clamp(Mathf.FloorToInt(u * sculpture.Total), 0, sculpture.Total - 1);
            for (int i = 0; i < sculpture.Total; i++)
            {
                int cell = (start + i) % sculpture.Total;
                if (!sculpture.cells[cell].alive) continue;
                var screen = renderCamera.WorldToScreenPoint(view.WorldPosition(cell));
                return StrikeAt(screen);
            }
            return false;
        }

        void Update()
        {
            if (!ready) return;
            clock += Time.deltaTime;
#if DEVELOPMENT_BUILD
            LogFrameRate();
#endif
            if (fittedScreen.x != Screen.width || fittedScreen.y != Screen.height) Fit();
            if (BackPressed()) Back();
            if (slowMotion > 0f)
            {
                slowMotion -= Time.unscaledDeltaTime;
                if (slowMotion <= 0f) SyncPause();
            }
            if (hitStop > 0f)
            {
                hitStop -= Time.unscaledDeltaTime;
                if (hitStop <= 0f) SyncPause();
            }
            WatchFrameTime();
            HandlePointer();
            UpdateQueuedHits(Time.deltaTime);
            CheckBroke();
        }

        // Escape belongs to the browser (it leaves fullscreen), so the web build pauses on P instead.
        bool BackPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return Save.IsWeb ? keyboard.pKey.wasPressedThisFrame : keyboard.escapeKey.wasPressedThisFrame;
        }

        // Android back: leave from the title, close an open menu, otherwise open settings.
        void Back()
        {
            if (Platform.AdRunning) return;
            if (menu.TitleOpen) { if (!Save.IsWeb) Application.Quit(); }
            else if (menu.MenuOpen) CloseMenus();
            else if (!won) OpenSettings();
        }

        void WatchFrameTime()
        {
            // Hidden or unfocused tabs are throttled by the browser; those frames say nothing about the device.
            if (Paused || !quality || Platform.AdRunning || !Application.isFocused) { slowFrames = 0; return; }
            if (Time.unscaledDeltaTime > 1f) return;
            slowFrames = Time.unscaledDeltaTime > SlowFrame ? slowFrames + 1 : Mathf.Max(0, slowFrames - 1);
            if (slowFrames < SlowFramesToDowngrade) return;
            slowFrames = 0;
            SetQuality(false, remember: false);
            feedback.Toast("Debris detail set to LOW");
        }

        void LateUpdate()
        {
            if (!ready) return;
            UpdateHint();
        }

        // Onboarding, one step at a time and only during play: tap, then hold, then drag to turn, plus a
        // pointer to each newly unlocked weapon.
        void UpdateHint()
        {
            if (Paused || won || Platform.AdRunning) { feedback.HideHint(); return; }
            string verb = Touch ? "Tap" : "Click";
            if ((tutorial & TapStep) == 0 && levelNumber == 1)
                HintAtSculpture(FeedbackView.HintMotion.Tap, $"{verb} the model to smash");
            else if ((tutorial & TapStep) != 0 && (tutorial & HoldStep) == 0 && hitsThisLevel >= 3)
                HintAtSculpture(FeedbackView.HintMotion.Hold, Touch ? "Hold to keep smashing" : "Hold the mouse to keep smashing");
            else if ((tutorial & HoldStep) != 0 && (tutorial & DragStep) == 0 && (levelNumber >= 2 || sculpture.Progress >= .35f))
                HintAtSculpture(FeedbackView.HintMotion.Drag, "Drag sideways to turn it");
            else if ((tutorial & AllSteps) == AllSteps && NewWeaponOnPage(out var point))
            {
                feedback.ShowHintIfChanged(FeedbackView.HintMotion.Point, $"New weapon! {verb} to equip");
                feedback.PlaceHint(point, hud.TopPx, hud.BottomPx);
            }
            else feedback.HideHint();
        }

        void HintAtSculpture(FeedbackView.HintMotion motion, string text)
        {
            feedback.ShowHintIfChanged(motion, text);
            // Point at a cell 60% of the way through the model, as the reference does (or the first
            // live cell after it once that one is gone).
            int cell = Mathf.Min(sculpture.Total - 1, Mathf.FloorToInt(sculpture.Total * .6f));
            for (int i = 0; i < sculpture.Total && !sculpture.cells[cell].alive; i++) cell = (cell + 1) % sculpture.Total;
            var world = motion == FeedbackView.HintMotion.Drag ? view.transform.position + Vector3.up * frame.targetY : view.WorldPosition(cell);
            feedback.PlaceHint(cameraRig.Camera.WorldToScreenPoint(world), hud.TopPx, hud.BottomPx);
        }

        bool NewWeaponOnPage(out Vector2 point)
        {
            point = default;
            foreach (var b in buttons)
                if (b.isNew && hud.WeaponButtonScreenPoint(b.weapon, out point)) return true;
            return false;
        }

        void CompleteStep(int step)
        {
            if ((tutorial & step) != 0) return;
            tutorial |= step;
            Save.SetInt(TutorialKey, tutorial);
            feedback.HideHint();
        }

        void SkipTutorial()
        {
            tutorial |= AllSteps;
            foreach (var b in buttons) weaponsSeen |= 1 << b.weapon;
            Save.SetInt(TutorialKey, tutorial);
            Save.SetInt(SeenKey, weaponsSeen);
            feedback.HideHint();
            RefreshWeapons();
        }

        void HandlePointer()
        {
            var pointer = Pointer.current;
            // A UI button that loads a level consumes the same press; don't let it strike the new sculpture.
            if (pointer == null || won || Paused || Platform.AdRunning || Time.frameCount == loadedFrame) { held = false; return; }
            var pos = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                pressStartedOnUi = OverUi(pos);
                if (pressStartedOnUi) return;
                held = true; dragged = false;
                downPos = pos; downAt = clock; startYaw = yaw;
            }
            if (!held || pressStartedOnUi) return;
            if (pointer.press.wasReleasedThisFrame)
            {
                held = false;
                if (!dragged) StrikeAt(pos);
                return;
            }
            float threshold = DragThresholdDp * Mathf.Max(1f, Screen.dpi / 160f);
            if (!dragged && (pos - downPos).magnitude > threshold) dragged = true;
            if (dragged)
            {
                // The camera looks down -z, so a positive yaw turns the near face to screen-left; negate
                // so the model follows the finger.
                yaw = startYaw - (pos.x - downPos.x) / Screen.width * YawPerScreenWidth;
                view.SetYaw(yaw);
                if (Mathf.Abs(yaw - startYaw) > .6f) CompleteStep(DragStep);
            }
            else if (clock - downAt > HoldDelay && StrikeAt(pos) && (tutorial & TapStep) != 0) CompleteStep(HoldStep);
        }

        bool OverUi(Vector2 pos)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pos }, uiHits);
            return uiHits.Count > 0;
        }

        // Start a swing at the tapped cell; damage lands when the weapon reaches it. False when no swing started.
        bool StrikeAt(Vector2 screenPos)
        {
            var weapon = weapons[selected];
            if (lost || weaponAnimator.Busy || clock - lastHit < weapon.cooldown) return false;
            var ray = cameraRig.Camera.ScreenPointToRay(screenPos);
            // Only the sculpture's colliders are tap targets; debris bodies and the table are ignored.
            if (!Physics.Raycast(ray, out var hit, 200f, sculptureMask)) return false;
            int cell = view.CellFromHit(hit);
            if (cell < 0) return false;
            int cost = StrikeCost(weapon);
            if (purse < cost)
            {
                feedback.Toast($"Not enough gold for the {weapon.displayName}!");
                return false;
            }
            SetPurse(purse - cost);
            lastHit = clock;
            int index = selected;
            var local = sculpture.Position(cell);
            sfx.Play(weapon.swingSounds);
            weaponAnimator.Play(index, view.WorldPosition(cell), () =>
            {
                foreach (var step in weapon.pattern)
                {
                    var point = local + new Vector3(step.dx, step.dy, 0f);
                    if (step.delay <= 0f) Damage(point, index, step.scale, true);
                    else queuedHits.Add(new QueuedHit { delay = step.delay, local = point, weapon = index, scale = step.scale });
                }
            });
            return true;
        }

        void UpdateQueuedHits(float dt)
        {
            for (int i = queuedHits.Count - 1; i >= 0; i--)
            {
                var q = queuedHits[i];
                q.delay -= dt;
                if (q.delay > 0f) { queuedHits[i] = q; continue; }
                queuedHits.RemoveAt(i);
                Damage(q.local, q.weapon, q.scale, false);
            }
        }

        void Damage(Vector3 local, int index, float scale, bool first)
        {
            if (won) return;
            var weapon = weapons[index];
            // The first hit of each strike can be a power hit (bought in the shop) and can roll a critical.
            bool powered = first && powerHits > 0;
            bool critical = first && Random.value < Economy.CritChance;
            float boost = (powered ? Economy.PowerScale : 1f) * (critical ? Economy.CritScale : 1f);
            var removed = sculpture.Strike(local, weapon.radius * scale * boost * Economy.SizeMultiplier(sizeLevel), weapon.mode);
            if (removed.Count == 0) return;
            if (powered) powerHits--;
            if (first)
            {
                combo = clock - lastComboAt <= Economy.ComboWindow ? Mathf.Min(combo + 1, Economy.MaxCombo) : 1;
                lastComboAt = clock;
                hitsThisLevel++;
                if (combo is 3 or 5 or 8 or Economy.MaxCombo) feedback.ShowCombo(combo);
            }
            CompleteStep(TapStep);
            var impact = view.transform.TransformPoint(local);
            view.OnRemoved(removed);
            occupancy.Remove(sculpture, removed);
            debris.WakeBodies();
            hitPalette.Clear();
            for (int i = 0; i < removed.Count && hitPalette.Count < 8; i++)
            {
                var c = view.CellColor(removed[i]);
                if (!hitPalette.Contains(c)) hitPalette.Add(c);
            }
            // Poison melts what it hits: the voxels slump off tinted toxic green instead of flying.
            bool melt = weapon.type == WeaponType.Poison;
            foreach (int i in removed)
                debris.Spawn(view.WorldPosition(i), melt ? Color.Lerp(view.CellColor(i), Toxic, .65f) : view.CellColor(i), impact,
                    weapon.power * boost * (melt ? .25f : 1f));
            var toCamera = (cameraRig.transform.position - impact).normalized;
            if (melt)
            {
                // Poison sparks are toxic green, not the sculpture's colours (palette is linear).
                hitPalette.Clear();
                hitPalette.Add(Toxic);
                hitPalette.Add(((Color)new Color32(0xc3, 0xff, 0x69, 255)).linear);
            }
            effects.Impact(impact, weapon.Explosive || critical, hitPalette, toCamera);
            if (first) effects.Special(weapon.type, impact, toCamera);
            if (melt) effects.Melt(impact, hitPalette);
            if (weapon.impactEffect) effects.PlayBurst(weapon.impactEffect, impact, toCamera, weapon.impactEffectScale);
            if (!sfx.Play(weapon.impactSounds)) sfx.Play(weapon.sound);
            if (critical) sfx.Play(WeaponSound.Explosion);
            cameraRig.Shake(weapon.shake * (critical ? 2.2f : 1f));
            bool heavy = weapon.Explosive || critical;
            cameraRig.Punch(heavy ? 3.2f : 1f);
            if (critical && !weapon.Explosive) effects.Shockwave(impact, .7f);
            // A brief freeze on big blows sells the weight (skipped on the winning hit, which slows down instead).
            if (heavy && first && sculpture.Progress < 1f)
            {
                hitStop = HitStop;
                SyncPause();
            }

            int earned = Economy.CoinsForHit(removed.Count, combo, magnetLevel, Boss);
            SetPurse(purse + earned);
            feedback.ShowScore(removed.Count, cameraRig.Camera.WorldToScreenPoint(impact), combo, critical);
            hud.SetProgress(sculpture.Progress);
            if (sculpture.Progress >= 1f) Win();
        }

        void Win()
        {
            won = true;
            held = false;
            queuedHits.Clear();
            feedback.HideHint();
            int stars = Economy.Stars(hitsThisLevel, sculpture.Total);
            int bonus = Economy.StarBonusCoins(stars) * (Boss ? 2 : 1);
            int banked = purse + bonus;
            coins += banked;
            if (!bestStars.TryGetValue(levelNumber, out int best) || stars > best) bestStars[levelNumber] = stars;
            Save.SetString(StarsKey, string.Join(",", bestStars.Select(p => $"{p.Key}:{p.Value}")));
            campaign.Complete(levelNumber);
            Save.SetString(FinishedKey, campaign.Serialize());
            SaveEconomy();

            bool last = levelNumber >= campaign.Total;
            int unlock = System.Array.FindIndex(weapons, w => w.unlockLevel == levelNumber);
            if (Boss || unlock >= 0) Save.HappyTime();
            cameraRig.StartVictoryOrbit();
            effects.Confetti(new Vector3(0f, frame.targetY, 0f));
            effects.Fireworks(new Vector3(0f, frame.targetY + frame.height * .5f, -1.5f), Mathf.Max(1.6f, frame.width * .6f));
            sfx.PlayVictory();
            slowMotion = SlowMotionTime;
            hud.ShowWin(Boss ? "BOSS DOWN!" : levelNumber % 2 == 0 ? "SMASHED!" : "DESTROYED!",
                unlock >= 0 ? "New Item Unlocked!" : Boss ? "Boss Scrapped!" : "Sculpture Destroyed!",
                unlock >= 0 ? icons[unlock] : null,
                unlock >= 0 ? $"{weapons[unlock].displayName} unlocks next level" : last ? "All levels complete!" : $"Level {levelNumber + 1} unlocked",
                $"{sculpture.Total:N0} cubes · {hitsThisLevel} {(hitsThisLevel == 1 ? "hit" : "hits")}",
                $"+{banked:N0} coins to the bank", stars, !last);
            SyncPause();
        }

#if DEVELOPMENT_BUILD
        int fpsFrames;
        float fpsTime, fpsWorst;

        // Development builds log frame rate every two seconds (read with adb logcat -s Unity).
        void LogFrameRate()
        {
            fpsFrames++;
            fpsTime += Time.unscaledDeltaTime;
            fpsWorst = Mathf.Max(fpsWorst, Time.unscaledDeltaTime);
            if (fpsTime < 2f) return;
            Debug.Log($"[Perf] fps={fpsFrames / fpsTime:F1} worst={fpsWorst * 1000f:F0}ms debris={debris.Count} bodies={debris.ActiveBodies}");
            fpsFrames = 0; fpsTime = 0f; fpsWorst = 0f;
        }
#endif

        void OnDestroy()
        {
            occupancy?.Dispose();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            // renderScale lives on the shared pipeline asset; don't leave the editor copy changed.
            if (baseRenderScale >= 0f && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = baseRenderScale;
        }
    }
}
