using System;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Brings the selected weapon to just in front of the impact point, fires the impact callback at
    // the swing's contact phase, then pulls it back out. Swung weapons fly in from below-right of the
    // camera; thrown ones fly the same line pointed along it; dropped ones fall from above (see
    // WeaponMotion). Thrown and dropped weapons, and the explosive ones, vanish on impact.
    public sealed class WeaponAnimator : MonoBehaviour
    {
        Camera cam;
        WeaponDef[] weapons;
        GameObject[] models;
        TrailRenderer[] trails;
        Transform root;
        int active = -1;
        float elapsed;
        bool impacted;
        Vector3 start, end;
        Quaternion aim = Quaternion.identity;
        Action onImpact;

        public bool Busy => active >= 0;

        // `trailMaterial` draws a short streak, in each weapon's trail colour, behind it while it moves.
        public void Init(WeaponDef[] defs, Camera camera, Material trailMaterial)
        {
            weapons = defs;
            cam = camera;
            root = new GameObject("Weapon").transform;
            root.SetParent(transform, false);
            // Models are authored with +z toward the viewer; Unity's camera looks down +z.
            var mirror = new GameObject("Mirror").transform;
            mirror.SetParent(root, false);
            mirror.localScale = new Vector3(1f, 1f, -1f);
            models = new GameObject[defs.Length];
            trails = new TrailRenderer[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                models[i] = WeaponModels.Build(defs[i]);
                models[i].transform.SetParent(mirror, false);
                // Imported models mark where the streak starts; procedural ones streak from the head.
                var tip = FindChild(models[i].transform, "TrailTip");
                if (!tip)
                {
                    tip = new GameObject("TrailTip").transform;
                    tip.SetParent(models[i].transform, false);
                    tip.localPosition = new Vector3(0f, .55f, 0f);
                }
                var trail = trails[i] = tip.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMaterial;
                trail.time = .16f;
                trail.minVertexDistance = .03f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, .14f), new Keyframe(1f, 0f));
                var color = defs[i].trailColor;
                trail.colorGradient = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.Lerp(color, Color.white, .35f), 0f), new GradientColorKey(color, 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(.8f, 0f), new GradientAlphaKey(0f, 1f) }
                };
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;
                models[i].SetActive(false);
            }
        }

        static Transform FindChild(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        public void Play(int index, Vector3 point, Action impact)
        {
            Cancel();
            active = index;
            elapsed = 0f;
            impacted = false;
            onImpact = impact;
            var weapon = weapons[index];
            var toCamera = (cam.transform.position - point).normalized;
            aim = Quaternion.identity;
            if (weapon.motion == WeaponMotion.Dropped)
            {
                // Lands on the near face, so it falls slightly in front of the sculpture.
                end = point + toCamera * .25f;
                start = end + Vector3.up * 3.2f + toCamera * .4f;
            }
            else
            {
                // The origin is the grip; place it so the striking face, not the grip, lands on the target.
                end = point + toCamera * .38f - cam.transform.rotation * weapon.strikeOffset;
                start = end + toCamera * 2.1f + cam.transform.right * 1.2f + Vector3.down * 1.35f;
                // Point the model's +z along its flight, in camera space.
                if (weapon.motion == WeaponMotion.Thrown)
                    aim = Quaternion.LookRotation(Quaternion.Inverse(cam.transform.rotation) * (end - start), Vector3.up);
            }
            if (!weapon.showInHand) return;
            models[index].SetActive(true);
            Pose(weapon, Attack.Swing(weapon, 0f));
            trails[index].Clear();
            trails[index].emitting = true;
        }

        public void Cancel()
        {
            if (active >= 0)
            {
                trails[active].emitting = false;
                models[active].SetActive(false);
            }
            active = -1;
            onImpact = null;
        }

        // Knock: tilt back (degrees, negative raises a head held left of the grip) and approach (0..1)
        // over the phase. Arrives early while lifting the head high, snaps down with growing speed to
        // land at contact, rebounds a little, then pulls away.
        const float KnockLift = 115f, KnockRebound = 22f;

        static void Knock(SwingState s, out float reach, out float tilt)
        {
            float c = s.contactPhase;
            if (s.phase < c)
            {
                float q = s.phase / c;
                reach = Mathf.SmoothStep(0f, 1f, q / .45f);
                float strike = Mathf.Clamp01((q - .45f) / .55f);
                tilt = -Mathf.Lerp(35f, KnockLift, Mathf.Clamp01(q / .45f)) * (1f - strike * strike);
                return;
            }
            float r = (s.phase - c) / (1f - c);
            tilt = -KnockRebound * Mathf.Sin(Mathf.Clamp01(r / .4f) * Mathf.PI);
            reach = 1f - Mathf.SmoothStep(0f, 1f, (r - .4f) / .6f);
        }

        void Pose(WeaponDef weapon, SwingState s)
        {
            float t = s.reach;
            float rotation = s.rotation;
            if (weapon.motion == WeaponMotion.Knock)
            {
                Knock(s, out t, out float tilt);
                rotation = tilt * Mathf.Deg2Rad;
            }
            // A drop accelerates: undo the swing's ease-out and square it.
            if (weapon.motion == WeaponMotion.Dropped && !s.contact)
            {
                float u = Mathf.Asin(Mathf.Clamp01(t)) / (Mathf.PI / 2f);
                t = u * u;
            }
            root.SetPositionAndRotation(Vector3.LerpUnclamped(start, end, t), cam.transform.rotation * aim);
            // The model sits under a z mirror, which turns rotation about (x, y, z) by a into rotation
            // about (x, y, -z) by -a. Thrown weapons fly straight; their models spin themselves.
            float angle = weapon.motion == WeaponMotion.Thrown ? 0f : -rotation * Mathf.Rad2Deg;
            models[active].transform.localRotation = Quaternion.AngleAxis(angle, Vector3.Scale(weapon.swingAxis, new Vector3(1f, 1f, -1f)));
        }

        void Update()
        {
            if (active < 0) return;
            elapsed += Time.deltaTime;
            var weapon = weapons[active];
            var s = Attack.Swing(weapon, elapsed);
            if (weapon.showInHand && models[active].activeSelf) Pose(weapon, s);
            if (s.contact && !impacted)
            {
                impacted = true;
                if (weapon.motion is WeaponMotion.Thrown or WeaponMotion.Dropped || weapon.Explosive)
                {
                    trails[active].emitting = false;
                    models[active].SetActive(false);
                }
                var callback = onImpact;
                callback?.Invoke();
                if (active < 0) return; // the impact finished the level and cancelled the swing
            }
            if (s.done) Cancel();
        }
    }
}
