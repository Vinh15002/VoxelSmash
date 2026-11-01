using System;
using CrazyGames;
using UnityEngine;
using VoxelSmash.Game;

// Connects the game to the CrazyGames SDK. Lives in Assembly-CSharp because the SDK ships without an
// assembly definition; registers itself for WebGL builds and Play mode in the editor (simulated ads).
public sealed class CrazyGamesBridge : IPlatformBackend
{
    bool adsBroken;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        if (Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer) Platform.Use(new CrazyGamesBridge());
    }

    public bool IsWeb => true;
    public bool PreferLowQuality => Application.isMobilePlatform;
    public bool RewardedAvailable => !adsBroken && CrazySDK.IsInitialized && CrazySDK.Ad.AdblockStatus != AdblockStatus.Present;

    public void Init(Action ready) => CrazySDK.Init(ready);

    // Progress lives in the SDK's data module (synced to the player's account), not local storage.
    public int GetInt(string key, int fallback) => CrazySDK.Data.GetInt(key, fallback);
    public void SetInt(string key, int value) => CrazySDK.Data.SetInt(key, value);
    public string GetString(string key, string fallback) => CrazySDK.Data.GetString(key, fallback);
    public void SetString(string key, string value) => CrazySDK.Data.SetString(key, value);
    public void Flush() { }

    public void GameplayStart() => CrazySDK.Game.GameplayStart();
    public void GameplayStop() => CrazySDK.Game.GameplayStop();
    public void HappyTime() => CrazySDK.Game.HappyTime();

    public void ShowMidgame(Action done) =>
        CrazySDK.Ad.RequestAd(CrazyAdType.Midgame, null, _ => done(), done);

    public void ShowRewarded(Action<bool> done) =>
        CrazySDK.Ad.RequestAd(CrazyAdType.Rewarded, null, error =>
        {
            // Ads that can never play (blocked, disabled in Basic Launch) hide the reward buttons.
            if (error.code is "adblock" or "adsDisabled") adsBroken = true;
            done(false);
        }, () => done(true));
}
