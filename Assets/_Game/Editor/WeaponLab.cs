using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VoxelSmash.Core;
using VoxelSmash.Game;

namespace VoxelSmash.EditorTools
{
    // Play-testing bench for the weapons: starts the game with every weapon available, switches weapon
    // and level from the window, slows time down, tunes each impact effect's scale and gold price on the
    // asset (strikes are free unless "Free strikes" is off), and keeps a verdict and a note per weapon
    // in Editor/Content/WeaponFeedback.json (read back by Claude).
    public sealed class WeaponLab : EditorWindow
    {
        const string FeedbackPath = "Assets/_Game/Editor/Content/WeaponFeedback.json";
        const string LevelKey = "VoxelSmash.WeaponLab.Level", StartKey = "VoxelSmash.WeaponLab.Start";
        static readonly string[] Verdicts = { "?", "OK", "Fix" };
        static readonly float[] Speeds = { 1f, .5f, .25f, .1f };

        [Serializable] sealed class Entry { public string weapon, verdict = "?", note = ""; }
        [Serializable] sealed class Feedback { public Entry[] weapons = Array.Empty<Entry>(); }

        Feedback feedback;
        WeaponDef[] weapons;
        Vector2 scroll;

        [MenuItem("Voxel Smash/Weapon Lab")]
        static void Open() => GetWindow<WeaponLab>("Weapon Lab");

        void OnEnable()
        {
            weapons = AssetDatabase.FindAssets("t:WeaponDef", new[] { "Assets/_Game/Data/Weapons" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<WeaponDef>).ToArray();
            LoadFeedback();
            EditorApplication.update += Tick;
        }

        void OnDisable() => EditorApplication.update -= Tick;

        // Waits for the game to boot after entering play mode, then opens the lab level.
        void Tick()
        {
            if (Application.isPlaying) Repaint();
            // Session state survives the domain reload that entering play mode can trigger.
            if (!SessionState.GetBool(StartKey, false) || !Application.isPlaying) return;
            var game = Game;
            if (!game || !game.Ready) return;
            SessionState.SetBool(StartKey, false);
            game.BeginLab(Level);
        }

        static GameController Game => Application.isPlaying ? FindAnyObjectByType<GameController>() : null;

        static int Level
        {
            get => EditorPrefs.GetInt(LevelKey, 18);
            set => EditorPrefs.SetInt(LevelKey, Mathf.Max(1, value));
        }

        void OnGUI()
        {
            var game = Game;
            DrawSession(game);
            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < weapons.Length; i++) DrawWeapon(game, i);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                int done = feedback.weapons.Count(e => e.verdict != "?");
                GUILayout.Label($"Feedback: {done}/{weapons.Length} reviewed — saved to {FeedbackPath}", EditorStyles.miniLabel);
                if (GUILayout.Button("Open file", GUILayout.Width(80)))
                    EditorUtility.RevealInFinder(FeedbackPath);
            }
        }

        void DrawSession(GameController game)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Level = EditorGUILayout.IntField("Level", Level);
                if (!Application.isPlaying)
                {
                    if (GUILayout.Button("Play all weapons", GUILayout.Width(140)))
                    {
                        SessionState.SetBool(StartKey, true);
                        EditorApplication.isPlaying = true;
                    }
                }
                else
                {
                    GUI.enabled = game && game.Ready;
                    if (GUILayout.Button("Load", GUILayout.Width(50))) game.BeginLab(Level);
                    if (GUILayout.Button("Next", GUILayout.Width(50))) game.BeginLab(Level = game.LevelNumber + 1);
                    if (GUILayout.Button("Restart", GUILayout.Width(60))) game.BeginLab(game.LevelNumber);
                    GUI.enabled = true;
                    if (GUILayout.Button("Stop", GUILayout.Width(50))) EditorApplication.isPlaying = false;
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = game && game.Ready;
                GUILayout.Label("Speed", GUILayout.Width(EditorGUIUtility.labelWidth - 4));
                foreach (float s in Speeds)
                {
                    bool on = game && Mathf.Approximately(game.LabSpeed, s);
                    if (GUILayout.Toggle(on, $"×{s}", EditorStyles.miniButton) && !on) game.LabSpeed = s;
                }
                if (game && game.Ready) game.LabFreeStrikes = GUILayout.Toggle(game.LabFreeStrikes, "Free strikes", EditorStyles.miniButton, GUILayout.Width(90));
                GUI.enabled = !Application.isPlaying;
                if (GUILayout.Button(new GUIContent("Re-import art", "Rebuilds the weapon/effect prefabs from WeaponsExport 1 (edit mode only)"), GUILayout.Width(100)))
                    WeaponArtImporter.Run();
                GUI.enabled = true;
            }
            if (Application.isPlaying && game && game.Ready)
                EditorGUILayout.HelpBox("Pick a weapon below, then tap/hold the sculpture in the Game view. Drag to rotate.", MessageType.None);
        }

        void DrawWeapon(GameController game, int i)
        {
            var w = weapons[i];
            var entry = EntryFor(w);
            int index = game && game.Ready ? Array.IndexOf(game.Weapons, w) : -1;
            bool selected = index >= 0 && game.SelectedWeapon == index;
            using (new EditorGUILayout.VerticalScope(selected ? "SelectionRect" : EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = index >= 0;
                    if (GUILayout.Button($"{i + 1}. {w.displayName}", selected ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(130)))
                        game.LabSelect(index);
                    if (GUILayout.Button("Use", EditorStyles.miniButton, GUILayout.Width(40))) game.LabSelect(index);
                    GUI.enabled = true;
                    GUILayout.Label($"{(w.model ? w.model.name : "procedural")} · {(w.impactEffect ? w.impactEffect.name : "no FX")}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUI.BeginChangeCheck();
                    int verdict = GUILayout.Toolbar(Mathf.Max(0, Array.IndexOf(Verdicts, entry.verdict)), Verdicts, EditorStyles.miniButton, GUILayout.Width(110));
                    if (EditorGUI.EndChangeCheck()) { entry.verdict = Verdicts[verdict]; SaveFeedback(); }
                }
                if (w.impactEffect)
                {
                    EditorGUI.BeginChangeCheck();
                    float scale = EditorGUILayout.Slider("FX scale", w.impactEffectScale, .1f, 2f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(w, "FX scale");
                        w.impactEffectScale = scale;
                        EditorUtility.SetDirty(w);
                    }
                }
                EditorGUI.BeginChangeCheck();
                int cost = EditorGUILayout.IntSlider("Gold / strike", w.strikeCost, 0, 200);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(w, "Strike cost");
                    w.strikeCost = cost;
                    EditorUtility.SetDirty(w);
                }
                EditorGUI.BeginChangeCheck();
                string note = EditorGUILayout.TextField("Note", entry.note);
                if (EditorGUI.EndChangeCheck()) { entry.note = note; SaveFeedback(); }
            }
        }

        Entry EntryFor(WeaponDef w)
        {
            var entry = feedback.weapons.FirstOrDefault(e => e.weapon == w.displayName);
            if (entry != null) return entry;
            entry = new Entry { weapon = w.displayName };
            feedback.weapons = feedback.weapons.Append(entry).ToArray();
            return entry;
        }

        void LoadFeedback()
        {
            feedback = File.Exists(FeedbackPath) ? JsonUtility.FromJson<Feedback>(File.ReadAllText(FeedbackPath)) : new Feedback();
            feedback ??= new Feedback();
        }

        void SaveFeedback()
        {
            // Keep the weapon order of the inventory so the file reads top to bottom.
            feedback.weapons = weapons.Select(EntryFor).ToArray();
            File.WriteAllText(FeedbackPath, JsonUtility.ToJson(feedback, true));
        }
    }
}
