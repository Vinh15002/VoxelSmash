using System;
using UnityEngine;

namespace VoxelSmash.Game
{
    // Services the host platform provides: save storage, gameplay start/stop reporting and video ads.
    // The default backend keeps saves in PlayerPrefs and has no ads (Android, tests); on the web the
    // CrazyGames bridge (Assets/_Game/Platform, outside this assembly) registers itself instead.
    public interface IPlatformBackend
    {
        bool IsWeb { get; }
        bool PreferLowQuality { get; }
        // False once ads are known not to work (ad blocker, ads disabled) so reward buttons can hide.
        bool RewardedAvailable { get; }

        void Init(Action ready);
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void Flush();

        void GameplayStart();
        void GameplayStop();
        void HappyTime();
        // `done` always runs, after the ad or straight away when there is none.
        void ShowMidgame(Action done);
        // `done(true)` only when the ad played to the end; never reward on failure.
        void ShowRewarded(Action<bool> done);
    }

    public static class Platform
    {
        static bool playing;

        public static IPlatformBackend Backend { get; private set; } = new LocalBackend();
        public static bool AdRunning { get; private set; }

        public static void Use(IPlatformBackend backend) => Backend = backend;

        // Reports gameplay start/stop only on changes, as the SDK expects.
        public static void SetPlaying(bool value)
        {
            if (value == playing) return;
            playing = value;
            if (value) Backend.GameplayStart();
            else Backend.GameplayStop();
        }

        public static void Midgame(Action done)
        {
            AdRunning = true;
            SetPlaying(false);
            Backend.ShowMidgame(() => { AdRunning = false; done(); });
        }

        public static void Rewarded(Action<bool> done)
        {
            AdRunning = true;
            SetPlaying(false);
            Backend.ShowRewarded(ok => { AdRunning = false; done(ok); });
        }

        sealed class LocalBackend : IPlatformBackend
        {
            public bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;
            public bool PreferLowQuality => SystemInfo.processorCount <= 4 || SystemInfo.systemMemorySize <= 2048;
            public bool RewardedAvailable => false;

            public void Init(Action ready) => ready();
            public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
            public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
            public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
            public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
            public void Flush() => PlayerPrefs.Save();
            public void GameplayStart() { }
            public void GameplayStop() { }
            public void HappyTime() { }
            public void ShowMidgame(Action done) => done();
            public void ShowRewarded(Action<bool> done) => done(false);
        }
    }
}
