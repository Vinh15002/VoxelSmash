using UnityEngine;

namespace VoxelSmash.Game
{
    // Spins a weapon model about one of its own axes (the thrown shuriken).
    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float degreesPerSecond = 1440f;

        void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
