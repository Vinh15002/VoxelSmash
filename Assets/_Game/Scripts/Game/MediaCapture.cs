using System.Collections;
using System.IO;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Records store media from a desktop build: run with
    //   -capture <outDir> -width 1920 -height 1080 -level 18 [-seconds 19]
    // It plays a scripted run (a tour of weapons, a slow turn, the win), renders the camera into a
    // render texture of exactly that size at a fixed 30 fps and writes numbered JPEG frames, then quits.
    // Frames become the preview video and the source for the cover art.
    public sealed class MediaCapture : MonoBehaviour
    {
        const int Fps = 30;
        static readonly WeaponType[] Tour =
        {
            WeaponType.Glove, WeaponType.Glove, WeaponType.Hammer, WeaponType.Mace, WeaponType.Lightning,
            WeaponType.Bomb, WeaponType.Hammer, WeaponType.Bullets, WeaponType.Chain, WeaponType.Rocket,
            WeaponType.Poison, WeaponType.Bomb, WeaponType.Rocket, WeaponType.Lightning, WeaponType.Bomb
        };

        string outDir;
        Vector2Int size;
        int level;
        float seconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            if (Application.isEditor || Arg("-capture") == null) return;
            var go = new GameObject("Media Capture");
            DontDestroyOnLoad(go);
            go.AddComponent<MediaCapture>();
        }

        static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        IEnumerator Start()
        {
            outDir = Arg("-capture");
            size = new Vector2Int(int.Parse(Arg("-width") ?? "1920"), int.Parse(Arg("-height") ?? "1080"));
            level = int.Parse(Arg("-level") ?? "18");
            seconds = float.Parse(Arg("-seconds") ?? "19", System.Globalization.CultureInfo.InvariantCulture);
            Directory.CreateDirectory(outDir);

            var game = FindAnyObjectByType<GameController>();
            while (!game.Ready) yield return null;
            var camera = Camera.main;
            var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            game.BeginCapture(level, size);
            Time.captureFramerate = Fps;

            var frame = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            int total = Mathf.RoundToInt(seconds * Fps), tour = 0, wonAt = -1;
            float nextStrike = .6f, t = 0f;
            for (int i = 0; i < total; i++)
            {
                t += 1f / Fps;
                game.CaptureYaw(.0045f);
                if (!game.Won && t >= nextStrike && !game.SwingBusy)
                {
                    game.CaptureWeapon(Tour[Mathf.Min(tour, Tour.Length - 1)]);
                    // Aim low first so the model sags and topples, then anywhere.
                    if (game.CaptureStrike(tour < 4 ? .15f + tour * .2f : Random.value, camera)) tour++;
                    nextStrike = t + (tour < 4 ? 1f : .8f);
                }
                if (game.Won && wonAt < 0) wonAt = i;
                // Stop a few seconds after the win; there is nothing new to show after the confetti.
                if (wonAt >= 0 && i - wonAt > Fps * 3) break;
                yield return new WaitForEndOfFrame();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                frame.Apply(false);
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, $"frame_{i:0000}.jpg"), frame.EncodeToJPG(93));
            }
            Application.Quit();
        }
    }
}
