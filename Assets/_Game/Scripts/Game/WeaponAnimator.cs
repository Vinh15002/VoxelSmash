using System;
using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Flies the selected weapon in from below-right of the camera to just in front of the impact
    // point, fires the impact callback at the swing's contact phase, then pulls it back out.
    public sealed class WeaponAnimator : MonoBehaviour
    {
        Camera cam;
        WeaponDef[] weapons;
        GameObject[] models;
        Transform root;
        int active = -1;
        float elapsed;
        bool impacted;
        Vector3 start, end;
        Action onImpact;

        public bool Busy => active >= 0;

        public void Init(WeaponDef[] defs, Camera camera)
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
            for (int i = 0; i < defs.Length; i++)
            {
                models[i] = WeaponModels.Build(defs[i].type);
                models[i].transform.SetParent(mirror, false);
                models[i].SetActive(false);
            }
        }

        public void Play(int index, Vector3 point, Action impact)
        {
            Cancel();
            active = index;
            elapsed = 0f;
            impacted = false;
            onImpact = impact;
            var toCamera = (cam.transform.position - point).normalized;
            end = point + toCamera * .38f;
            start = end + toCamera * 2.1f + cam.transform.right * 1.2f + Vector3.down * 1.35f;
            models[index].SetActive(true);
            Pose(Attack.Swing(weapons[index], 0f));
        }

        public void Cancel()
        {
            if (active >= 0) models[active].SetActive(false);
            active = -1;
            onImpact = null;
        }

        void Pose(SwingState s)
        {
            root.SetPositionAndRotation(Vector3.LerpUnclamped(start, end, s.reach), cam.transform.rotation);
            models[active].transform.localRotation = Quaternion.Euler(0f, 0f, s.rotation * Mathf.Rad2Deg);
        }

        void Update()
        {
            if (active < 0) return;
            elapsed += Time.deltaTime;
            var weapon = weapons[active];
            var s = Attack.Swing(weapon, elapsed);
            Pose(s);
            if (s.contact && !impacted)
            {
                impacted = true;
                // Thrown weapons vanish into their own effect on impact.
                if (weapon.type is WeaponType.Bomb or WeaponType.Rocket or WeaponType.Lightning) models[active].SetActive(false);
                var callback = onImpact;
                callback?.Invoke();
                if (active < 0) return; // the impact finished the level and cancelled the swing
            }
            if (s.done) Cancel();
        }
    }
}
