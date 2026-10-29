using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Procedurally synthesised placeholder sounds (no third-party audio). One family per weapon
    // sound, with variants played round-robin like the reference; at most five play at once.
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 22050, Voices = 5;

        readonly Dictionary<WeaponSound, AudioClip[]> clips = new();
        AudioClip victory;
        AudioSource[] sources;
        int nextSource, variant;

        public void Init()
        {
            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
            clips[WeaponSound.Glove] = new[] { Thump("glove-1", 95f, .30f), Thump("glove-2", 82f, .30f), Thump("glove-3", 108f, .28f) };
            clips[WeaponSound.Hammer] = new[] { Clank("hammer-1", 1f), Clank("hammer-2", 1.12f) };
            clips[WeaponSound.Slice] = new[] { Swish("slice") };
            clips[WeaponSound.Explosion] = new[] { Boom("explosion") };
            clips[WeaponSound.Zap] = new[] { Zap("zap") };
            clips[WeaponSound.Shot] = new[] { Shot("shot") };
            clips[WeaponSound.Poison] = new[] { Bubbles("poison") };
            victory = Fanfare("victory");
        }

        public void Play(WeaponSound sound)
        {
            var set = clips[sound];
            PlayClip(set[variant++ % set.Length], .95f + UnityEngine.Random.value * .1f);
        }

        public void PlayVictory()
        {
            foreach (var s in sources) s.Stop();
            PlayClip(victory, 1f);
        }

        public void StopAll()
        {
            foreach (var s in sources) s.Stop();
        }

        void PlayClip(AudioClip clip, float pitch)
        {
            // Steal the oldest voice once all five are busy.
            var source = sources[nextSource];
            nextSource = (nextSource + 1) % Voices;
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.Play();
        }

        // ---- Synthesis ---------------------------------------------------------------------------

        static AudioClip Make(string name, float seconds, Func<float, System.Random, float> sample, int seed = 1)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var random = new System.Random(seed);
            float peak = 1e-4f;
            for (int i = 0; i < n; i++)
            {
                data[i] = sample((float)i / Rate, random);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            // Normalise and add a 4 ms fade-out so every clip ends without a click.
            float gain = .9f / peak;
            int fade = Mathf.Min(n, Rate / 250);
            for (int i = 0; i < n; i++) data[i] *= gain * Mathf.Min(1f, (n - i) / (float)fade);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(System.Random r) => (float)r.NextDouble() * 2f - 1f;

        static AudioClip Thump(string name, float pitch, float seconds)
        {
            float phase = 0f, low = 0f;
            return Make(name, seconds, (t, r) =>
            {
                float f = pitch * (1f + 1.6f * Mathf.Exp(-t * 40f));
                phase += 2f * Mathf.PI * f / Rate;
                low += (Noise(r) - low) * .25f;
                return Mathf.Sin(phase) * Mathf.Exp(-t * 14f) + low * 1.4f * Mathf.Exp(-t * 60f);
            }, name.GetHashCode());
        }

        static AudioClip Clank(string name, float tone)
        {
            float[] partials = { 523f, 1231f, 2109f, 3397f };
            return Make(name, .45f, (t, r) =>
            {
                float s = 0f;
                for (int k = 0; k < partials.Length; k++)
                    s += Mathf.Sin(2f * Mathf.PI * partials[k] * tone * t) * Mathf.Exp(-t * (9f + k * 5f)) / (k + 1);
                return s + Noise(r) * .8f * Mathf.Exp(-t * 90f) + Mathf.Sin(2f * Mathf.PI * 70f * t) * .7f * Mathf.Exp(-t * 25f);
            }, name.GetHashCode());
        }

        static AudioClip Swish(string name)
        {
            float lp = 0f, prev = 0f;
            return Make(name, .38f, (t, r) =>
            {
                // Noise through a sweeping one-pole low-pass, then a crude high-pass: a rising whoosh.
                float cutoff = Mathf.Lerp(.05f, .6f, t / .38f);
                lp += (Noise(r) - lp) * cutoff;
                float hp = lp - prev;
                prev = lp;
                return hp * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .38f));
            }, 11);
        }

        static AudioClip Boom(string name)
        {
            float brown = 0f, lp = 0f;
            return Make(name, 1.25f, (t, r) =>
            {
                brown = Mathf.Clamp(brown + Noise(r) * .08f, -1f, 1f);
                lp += (brown - lp) * .2f;
                float crack = Noise(r) * Mathf.Exp(-t * 30f);
                float body = Mathf.Sin(2f * Mathf.PI * (48f - 20f * t) * t) * Mathf.Exp(-t * 4f);
                return lp * 3f * Mathf.Exp(-t * 2.6f) + crack * .6f + body * .9f;
            }, 21);
        }

        static AudioClip Zap(string name)
        {
            float phase = 0f, freq = 600f;
            return Make(name, .5f, (t, r) =>
            {
                if ((int)(t * Rate) % (Rate / 100) == 0) freq = 300f + (float)r.NextDouble() * 900f;
                phase = (phase + freq / Rate) % 1f;
                float saw = phase * 2f - 1f;
                return (saw * .7f + Noise(r) * .5f) * Mathf.Exp(-t * 5f) * (Mathf.Sin(t * 180f) > -.3f ? 1f : .3f);
            }, 31);
        }

        static AudioClip Shot(string name) => Make(name, .28f, (t, r) =>
            Noise(r) * Mathf.Exp(-t * 35f) + Mathf.Sin(2f * Mathf.PI * 150f * t) * Mathf.Exp(-t * 30f), 41);

        static AudioClip Bubbles(string name)
        {
            float phase = 0f;
            return Make(name, .72f, (t, r) =>
            {
                // Six short rising blips: pops of a bubbling flask.
                float local = t % .12f;
                float f = 300f + 400f * (local / .12f) + 60f * Mathf.Floor(t / .12f);
                phase += 2f * Mathf.PI * f / Rate;
                return Mathf.Sin(phase) * Mathf.Exp(-local * 30f) * Mathf.Exp(-t * 1.5f);
            }, 51);
        }

        static AudioClip Fanfare(string name)
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            return Make(name, 1.75f, (t, r) =>
            {
                int i = Mathf.Min(notes.Length - 1, (int)(t / .16f));
                float local = t - i * .16f, f = notes[i];
                float tri = Mathf.Abs((t * f) % 1f * 4f - 2f) - 1f;
                float decay = i == notes.Length - 1 ? Mathf.Exp(-local * 2.2f) : Mathf.Exp(-local * 8f);
                return (tri * .7f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * .2f) * decay;
            }, 61);
        }
    }
}
