using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Game loop: title -> load a level, tap/hold to strike with the selected weapon, drag to rotate, win at 100%.
    // The campaign has LevelList.Length levels; models repeat until every model is imported.
    public sealed class GameController : MonoBehaviour
    {
        const string FinishedKey = "voxel-smash.finished", LegacyLevelKey = "voxel-smash.level-number",
            SelectedKey = "voxel-smash.weapon", QualityKey = "voxel-smash.quality", TutorialKey = "voxel-smash.tutorial";
        const float HoldDelay = .22f, DragThresholdDp = 13f, YawPerScreenWidth = 3.7f;
        // Reference: milestone power hits strike 1.35x; six slow (>160 ms) frames drop to low detail,
        // which caps physics bodies at 110 instead of 260 and turns off shadows.
        const float BoostScale = 1.35f, SlowFrame = .16f, LowRenderScale = .75f;
        const int SlowFramesToDowngrade = 6, BodiesHigh = 260, BodiesLow = 110;
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
        [SerializeField] Material voxelMaterial;
        [SerializeField] Material fragmentMaterial;
        [SerializeField] Material fxAlphaMaterial;
        [SerializeField] Material fxAdditiveMaterial;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] HudView hud;

        MenuView menu;
        FeedbackView feedback;
        Progression campaign;
        bool quality, tutorialDismissed;
        int boostHits, slowFrames;
        float baseRenderScale = -1f;

        Sculpture sculpture;
        SculptureView view;
        DebrisField debris;
        HitEffects effects;
        WeaponAnimator weaponAnimator;
        Sfx sfx;
        Texture[] icons;
        OccupancyGrid occupancy;
        PhysicsMaterial voxelPhysics;
        int sculptureLayer, sculptureMask;
        int levelNumber, selected, weaponPage;
        bool won;
        float yaw = Framing.StartYaw, clock, lastHit = -5f;
        bool held, dragged, pressStartedOnUi;
        Vector2 downPos;
        float downAt, startYaw;
        int loadedFrame = -1;
        Vector2Int fittedScreen;
        readonly List<RaycastResult> uiHits = new();
        readonly List<QueuedHit> queuedHits = new();
        readonly List<Color> hitPalette = new();
        readonly List<HudView.WeaponButton> buttons = new();

        VoxelModel Model => levels.ModelFor(levelNumber);
        bool Paused => menu.TitleOpen || menu.MenuOpen;
        static bool LowPowerDevice => SystemInfo.processorCount <= 4 || SystemInfo.systemMemorySize <= 2048;

        void Start()
        {
            Application.targetFrameRate = 60;
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

            debris = new GameObject("Debris").AddComponent<DebrisField>();
            debris.Init(fragmentMaterial, voxelPhysics, Mathf.Max(0, LayerMask.NameToLayer(DebrisLayer)));
            effects = new GameObject("Effects").AddComponent<HitEffects>();
            effects.Init(fxAlphaMaterial, fxAdditiveMaterial, fragmentMaterial);
            weaponAnimator = new GameObject("Weapons").AddComponent<WeaponAnimator>();
            weaponAnimator.Init(weapons, cameraRig.Camera);
            sfx = new GameObject("Sfx").AddComponent<Sfx>();
            sfx.Init();
            icons = WeaponIcons.Render(weapons, previewLayer);

            hud.Build();
            hud.Restart += () => LoadLevel(levelNumber);
            hud.Replay += () => LoadLevel(levelNumber);
            hud.Next += () => LoadLevel(levelNumber + 1);
            hud.LayoutChanged += Fit;
            hud.WeaponSelected += SelectWeapon;
            hud.PageChanged += delta => { weaponPage += delta; RefreshWeapons(); };
            hud.SettingsRequested += OpenSettings;
            hud.LevelsRequested += OpenLevels;
            feedback = new GameObject("Feedback").AddComponent<FeedbackView>();
            feedback.Build(hud.transform, hud.WinIndex);
            menu = UiKit.Stretch("Menus", hud.transform).gameObject.AddComponent<MenuView>();
            menu.Build(icons[Mathf.Max(0, System.Array.FindIndex(weapons, w => w.type == WeaponType.Hammer))]);
            menu.Play += PlayFromTitle;
            menu.Resume += CloseMenus;
            menu.OpenLevels += OpenLevels;
            menu.MainMenu += OpenTitle;
            menu.ToggleQuality += () => SetQuality(!quality);
            menu.LevelChosen += n => LoadLevel(n);

            campaign = Progression.Parse(levels.Length, PlayerPrefs.GetString(FinishedKey, ""), PlayerPrefs.GetInt(LegacyLevelKey, 0));
            quality = PlayerPrefs.GetInt(QualityKey, LowPowerDevice ? 0 : 1) == 1;
            tutorialDismissed = PlayerPrefs.GetInt(TutorialKey, 0) == 1;
            ApplyQuality();
            selected = Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, 0), 0, weapons.Length - 1);
            LoadLevel(campaign.ResumeLevel);
            OpenTitle();
        }

        void OpenTitle()
        {
            held = false;
            int done = campaign.Completed;
            menu.ShowTitle(done > 0 ? $"CONTINUE · LEVEL {campaign.ResumeLevel}" : "PLAY",
                done > 0 ? $"{done} / {campaign.Total} levels cleared" : $"{campaign.Total} voxel creations. {weapons.Length} smash weapons.");
            SyncPause();
        }

        void PlayFromTitle()
        {
            menu.HideTitle();
            LoadLevel(campaign.ResumeLevel);
        }

        void OpenSettings()
        {
            if (menu.TitleOpen || won) return;
            held = false;
            menu.ShowSettings(quality);
            SyncPause();
        }

        void OpenLevels()
        {
            if (menu.TitleOpen) return;
            held = false;
            menu.ShowLevels(campaign.Total, n => campaign.IsFinished(n) ? MenuView.LevelState.Finished
                : campaign.CanPlay(n) ? MenuView.LevelState.Open : MenuView.LevelState.Locked, levelNumber);
            SyncPause();
        }

        void CloseMenus()
        {
            menu.CloseMenus();
            SyncPause();
        }

        // Menus freeze the world (physics, debris, swings, effects) and the sound, like the reference.
        void SyncPause()
        {
            bool paused = Paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        void SetQuality(bool high)
        {
            quality = high;
            ApplyQuality();
            menu.SetQuality(high);
            PlayerPrefs.SetInt(QualityKey, high ? 1 : 0);
            PlayerPrefs.Save();
        }

        void ApplyQuality()
        {
            debris.MaxBodies = quality ? BodiesHigh : BodiesLow;
            effects.Detail = quality;
            cameraRig.Camera.GetUniversalAdditionalCameraData().renderShadows = quality;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (baseRenderScale < 0f) baseRenderScale = urp.renderScale;
                urp.renderScale = baseRenderScale * (quality ? 1f : LowRenderScale);
            }
        }

        bool LoadLevel(int number)
        {
            if (!campaign.CanPlay(number))
            {
                feedback.Toast("Complete the previous level first.");
                return false;
            }
            levelNumber = number;
            loadedFrame = Time.frameCount;
            won = false;
            held = false;
            yaw = Framing.StartYaw;
            lastHit = -5f;
            queuedHits.Clear();
            weaponAnimator.Cancel();
            effects.Clear();
            sfx.StopAll();
            if (view) Destroy(view.gameObject);
            debris.Clear();
            var model = Model;
            sculpture = new Sculpture(model);
            view = SculptureView.Create(sculpture, model, voxelMaterial, voxelPhysics, sculptureLayer, transform);
            view.SetYaw(yaw);
            occupancy?.Dispose();
            occupancy = new OccupancyGrid(sculpture);
            debris.ClearRadius = Framing.DebrisClearance(model);
            debris.Occupancy = occupancy;
            debris.Sculpture = view.transform;
            if (!weapons[selected].AvailableAt(levelNumber)) selected = 0;
            hud.SetLevel(number, model.displayName);
            hud.SetProgress(0f);
            RefreshWeapons();
            Fit();
            feedback.ClearScores();
            feedback.SetTutorial(number == 1 && !tutorialDismissed);
            menu.CloseMenus();
            SyncPause();
            return true;
        }

        void SelectWeapon(int index)
        {
            if (won || !weapons[index].AvailableAt(levelNumber)) return;
            selected = index;
            PlayerPrefs.SetInt(SelectedKey, index);
            RefreshWeapons();
        }

        void RefreshWeapons()
        {
            buttons.Clear();
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i].AvailableAt(levelNumber))
                    buttons.Add(new HudView.WeaponButton { weapon = i, name = weapons[i].displayName, icon = icons[i] });
            hud.SetWeapons(buttons, selected, weaponPage, out weaponPage);
        }

        void Fit()
        {
            fittedScreen = new Vector2Int(Screen.width, Screen.height);
            cameraRig.Fit(Framing.For(Model), hud.TopPx, hud.BottomPx, hud.SidePx);
        }

        void Update()
        {
            clock += Time.deltaTime;
#if DEVELOPMENT_BUILD
            LogFrameRate();
#endif
            if (fittedScreen.x != Screen.width || fittedScreen.y != Screen.height) Fit();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
            WatchFrameTime();
            HandlePointer();
            UpdateQueuedHits(Time.deltaTime);
        }

        // Android back: leave from the title, close an open menu, otherwise open settings.
        void Back()
        {
            if (menu.TitleOpen) Application.Quit();
            else if (menu.MenuOpen) CloseMenus();
            else if (!won) OpenSettings();
        }

        void WatchFrameTime()
        {
            if (Paused || !quality) { slowFrames = 0; return; }
            slowFrames = Time.unscaledDeltaTime > SlowFrame ? slowFrames + 1 : Mathf.Max(0, slowFrames - 1);
            if (slowFrames < SlowFramesToDowngrade) return;
            slowFrames = 0;
            SetQuality(false);
            feedback.Toast("Debris detail set to LOW");
        }

        void LateUpdate()
        {
            if (!feedback.TutorialVisible) return;
            // Point at a cell 60% of the way through the model, as the reference does.
            int cell = Mathf.Min(sculpture.Total - 1, Mathf.FloorToInt(sculpture.Total * .6f));
            var screen = cameraRig.Camera.WorldToScreenPoint(view.WorldPosition(cell));
            feedback.PlaceTutorial(screen, hud.TopPx, hud.BottomPx);
        }

        void HandlePointer()
        {
            var pointer = Pointer.current;
            // A UI button that loads a level consumes the same press; don't let it strike the new sculpture.
            if (pointer == null || won || Paused || Time.frameCount == loadedFrame) { held = false; return; }
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
                yaw = startYaw + (pos.x - downPos.x) / Screen.width * YawPerScreenWidth;
                view.SetYaw(yaw);
            }
            else if (clock - downAt > HoldDelay) StrikeAt(pos);
        }

        bool OverUi(Vector2 pos)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pos }, uiHits);
            return uiHits.Count > 0;
        }

        // Start a swing at the tapped cell; damage lands when the weapon reaches it.
        void StrikeAt(Vector2 screenPos)
        {
            var weapon = weapons[selected];
            if (weaponAnimator.Busy || clock - lastHit < weapon.cooldown) return;
            var ray = cameraRig.Camera.ScreenPointToRay(screenPos);
            // Only the sculpture's colliders are tap targets; debris bodies and the table are ignored.
            if (!Physics.Raycast(ray, out var hit, 200f, sculptureMask)) return;
            int cell = view.CellFromHit(hit);
            if (cell < 0) return;
            lastHit = clock;
            int index = selected;
            var local = sculpture.Position(cell);
            weaponAnimator.Play(index, view.WorldPosition(cell), () =>
            {
                foreach (var step in weapon.pattern)
                {
                    var point = local + new Vector3(step.dx, step.dy, 0f);
                    if (step.delay <= 0f) Damage(point, index, step.scale, true);
                    else queuedHits.Add(new QueuedHit { delay = step.delay, local = point, weapon = index, scale = step.scale });
                }
            });
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
            // Milestone power hits widen the first hit of a strike.
            bool boosted = first && boostHits > 0;
            float boost = boosted ? BoostScale : 1f;
            var removed = sculpture.Strike(local, weapon.radius * scale * boost, weapon.mode);
            if (removed.Count == 0) return;
            if (boosted) boostHits--;
            if (!tutorialDismissed)
            {
                tutorialDismissed = true;
                PlayerPrefs.SetInt(TutorialKey, 1);
                feedback.SetTutorial(false);
            }
            var impact = view.transform.TransformPoint(local);
            view.OnRemoved(removed);
            occupancy.Remove(sculpture, removed);
            hitPalette.Clear();
            for (int i = 0; i < removed.Count && hitPalette.Count < 8; i++)
            {
                var c = view.CellColor(removed[i]);
                if (!hitPalette.Contains(c)) hitPalette.Add(c);
            }
            foreach (int i in removed) debris.Spawn(view.WorldPosition(i), view.CellColor(i), impact, weapon.power * boost);
            var toCamera = (cameraRig.transform.position - impact).normalized;
            if (weapon.type == WeaponType.Poison)
            {
                // Poison sparks are toxic green, not the sculpture's colours (palette is linear).
                hitPalette.Clear();
                hitPalette.Add(((Color)new Color32(0x86, 0xe5, 0x2b, 255)).linear);
                hitPalette.Add(((Color)new Color32(0xc3, 0xff, 0x69, 255)).linear);
            }
            effects.Impact(impact, weapon.Explosive, hitPalette, toCamera);
            if (first) effects.Special(weapon.type, impact, toCamera);
            sfx.Play(weapon.sound);
            cameraRig.Shake(weapon.shake);
            feedback.ShowScore(removed.Count, cameraRig.Camera.WorldToScreenPoint(impact));
            hud.SetProgress(sculpture.Progress);
            if (sculpture.Progress >= 1f) Win();
        }

        void Win()
        {
            won = true;
            held = false;
            queuedHits.Clear();
            feedback.SetTutorial(false);
            bool firstFinish = campaign.Complete(levelNumber);
            if (campaign.IsMilestone(firstFinish))
            {
                boostHits += Progression.MilestoneHits;
                feedback.Toast($"Milestone bonus: {Progression.MilestoneHits} power hits ready!");
            }
            PlayerPrefs.SetString(FinishedKey, campaign.Serialize());
            PlayerPrefs.Save();
            bool last = levelNumber >= campaign.Total;
            cameraRig.StartVictoryOrbit();
            effects.Confetti(new Vector3(0f, Framing.For(Model).targetY, 0f));
            sfx.PlayVictory();
            int unlock = System.Array.FindIndex(weapons, w => w.unlockLevel == levelNumber);
            hud.ShowWin(levelNumber % 2 == 0 ? "SMASHED!" : "DESTROYED!",
                unlock >= 0 ? "New Item Unlocked!" : "Sculpture Destroyed!",
                unlock >= 0 ? icons[unlock] : null,
                unlock >= 0 ? $"{weapons[unlock].displayName} unlocks next level" : last ? "All levels complete!" : $"Level {levelNumber + 1} unlocked",
                $"{sculpture.Total:N0} cubes · {sculpture.Hits} hits", !last);
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
