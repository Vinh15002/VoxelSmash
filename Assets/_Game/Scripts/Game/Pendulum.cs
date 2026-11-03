using UnityEngine;

namespace VoxelSmash.Game
{
    // Swings a weapon part back and forth about a local axis (the mace's chain and ball). Restarts
    // from the side each time the weapon is shown, so every swing starts the same way.
    public sealed class Pendulum : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float amplitude = 40f;
        public float frequency = 2.2f;

        Quaternion rest;
        float time;

        void Awake() => rest = transform.localRotation;

        void OnEnable() => time = 0f;

        void Update()
        {
            time += Time.deltaTime;
            float angle = amplitude * Mathf.Cos(time * frequency * 2f * Mathf.PI);
            transform.localRotation = rest * Quaternion.AngleAxis(angle, axis);
        }
    }
}
