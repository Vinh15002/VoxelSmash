using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Renders each weapon model once into a transparent texture for the inventory buttons, so the
    // icons always match the 3D weapon (and need no emoji font).
    public static class WeaponIcons
    {
        const int Size = 192;
        static readonly Vector3 Stage = new(0f, -500f, 0f);

        public static Texture[] Render(WeaponDef[] weapons, int layer)
        {
            var camGo = new GameObject("Icon Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << layer;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = .05f;
            cam.farClipPlane = 20f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = false;
            data.renderPostProcessing = false;

            var icons = new Texture[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                var holder = new GameObject("Icon Stage") { layer = layer }.transform;
                holder.position = Stage;
                var mirror = new GameObject("Mirror") { layer = layer }.transform;
                mirror.SetParent(holder, false);
                mirror.localScale = new Vector3(1f, 1f, -1f);
                var model = WeaponModels.Build(weapons[i].type, layer);
                model.transform.SetParent(mirror, false);
                // Turn the model three-quarters so depth reads; rockets would otherwise face nose-on.
                bool lengthwise = weapons[i].type is WeaponType.Rocket or WeaponType.Bullets;
                holder.rotation = Quaternion.Euler(lengthwise ? 15f : 8f, lengthwise ? 70f : -22f, 0f);

                var bounds = new Bounds(Stage, Vector3.zero);
                bool first = true;
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = r.bounds; first = false; }
                    else bounds.Encapsulate(r.bounds);
                }
                float radius = bounds.extents.magnitude;
                float distance = radius / Mathf.Sin(cam.fieldOfView * .5f * Mathf.Deg2Rad) * .92f;
                cam.transform.SetPositionAndRotation(bounds.center + Vector3.back * distance, Quaternion.identity);

                var texture = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { name = weapons[i].displayName + " Icon" };
                texture.Create();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                icons[i] = texture;
                // Destroy is deferred to end of frame; hide now so the next icon renders alone.
                holder.gameObject.SetActive(false);
                Object.Destroy(holder.gameObject);
            }
            Object.Destroy(camGo);
            return icons;
        }
    }
}
