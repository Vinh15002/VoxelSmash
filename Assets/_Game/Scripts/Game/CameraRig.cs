using UnityEngine;
using VoxelSmash.Core;

namespace VoxelSmash.Game
{
    // Fits the sculpture into the clear rectangle the HUD leaves (between the top bar and the weapon
    // panel, which sits below in portrait and to the right in landscape), then shifts the projection
    // so that rectangle's centre is the visual centre. Also owns hit shake and the victory orbit.
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        const float VictoryDuration = 4.4f;

        Camera cam;
        Vector3 target;
        float viewOffsetNdc, viewOffsetXNdc, shake, punch;
        const float Fov = 37f;
        float orbitTime = -1f;
        Vector3 orbitStart;

        public Camera Camera => cam ? cam : cam = GetComponent<Camera>();

        // Edges are screen pixels: top/bottom measured from the top, left/right from the left.
        // Width/height default to the screen; media capture passes its render-texture size.
        public void Fit(TargetFrame frame, float topPx, float bottomPx, float leftPx, float rightPx, float width = 0f, float height = 0f)
        {
            var c = Camera;
            c.fieldOfView = Fov;
            float w = width > 0f ? width : Screen.width, h = height > 0f ? height : Screen.height;
            float availableHeight = Mathf.Max(24f, bottomPx - topPx), availableWidth = Mathf.Max(24f, rightPx - leftPx);
            float tan = Mathf.Tan(c.fieldOfView * .5f * Mathf.Deg2Rad);
            float distance = Mathf.Max(frame.height / (2f * tan) * h / availableHeight,
                frame.width / (2f * tan * c.aspect) * w / availableWidth);
            target = new Vector3(0f, frame.targetY, 0f);
            c.farClipPlane = Mathf.Max(80f, distance * 2f + frame.height);
            transform.position = new Vector3(distance * .10f, target.y + distance * .15f, distance);
            transform.LookAt(target);
            // Screen y grows downward in the layout; NDC y grows upward.
            float centerFromTop = (topPx + bottomPx) * .5f;
            viewOffsetNdc = 2f * (h * .5f - centerFromTop) / h;
            viewOffsetXNdc = 2f * ((leftPx + rightPx) * .5f - w * .5f) / w;
            orbitTime = -1f;
            ApplyProjection(0f, 0f);
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        // A quick zoom-in that springs back: heavy hits feel like they shove the camera.
        public void Punch(float degrees) => punch = Mathf.Max(punch, degrees);

        public void StartVictoryOrbit()
        {
            orbitTime = 0f;
            orbitStart = transform.position - target;
        }

        void ApplyProjection(float jitterX, float jitterY)
        {
            var c = Camera;
            c.ResetProjectionMatrix();
            var p = c.projectionMatrix;
            // Adding d to m02/m12 moves the image by -d in NDC.
            p.m02 = -(viewOffsetXNdc + jitterX);
            p.m12 = -(viewOffsetNdc + jitterY);
            c.projectionMatrix = p;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (orbitTime >= 0f)
            {
                orbitTime += dt;
                float angle = Mathf.Min(1f, orbitTime / VictoryDuration) * Mathf.PI * .58f;
                transform.position = target + Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up) * orbitStart;
                transform.LookAt(target);
            }
            if (punch > .01f)
            {
                punch *= Mathf.Exp(-dt * 14f);
                Camera.fieldOfView = Fov - punch;
                if (shake <= .001f) ApplyProjection(0f, 0f);
            }
            else if (punch > 0f)
            {
                punch = 0f;
                Camera.fieldOfView = Fov;
                if (shake <= .001f) ApplyProjection(0f, 0f);
            }
            if (shake > .001f)
            {
                shake *= Mathf.Exp(-dt * 19f);
                // Original shake is shake*140 CSS px on a ~800px-tall phone view; express it in NDC.
                const float px = 2f * 140f / 800f;
                ApplyProjection((Random.value - .5f) * shake * px, (Random.value - .5f) * shake * px);
            }
            else if (shake > 0f)
            {
                shake = 0f;
                ApplyProjection(0f, 0f);
            }
        }
    }
}
