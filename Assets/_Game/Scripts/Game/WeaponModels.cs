using UnityEngine;
using VoxelSmash.Core;
using M = VoxelSmash.Game.ProceduralMeshes;

namespace VoxelSmash.Game
{
    // Low-poly weapon models built from primitives, ported shape-for-shape from the reference.
    // Coordinates follow three.js (+z toward the viewer); place the result under a parent with
    // localScale z = -1 so it reads the same in Unity's camera space.
    public static class WeaponModels
    {
        static Material red, darkRed, metal, orange, black, gold, blue, green, cream, glow, rocketBlue, bulletRed;

        static Material Lit(Color32 color, float metallic, float smoothness)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        static void EnsureMaterials()
        {
            if (red) return;
            red = Lit(new Color32(0xf6, 0x08, 0x12, 255), .05f, .8f);
            darkRed = Lit(new Color32(0xa8, 0x08, 0x12, 255), 0f, .7f);
            metal = Lit(new Color32(0xbf, 0xcb, 0xd6, 255), .75f, .77f);
            orange = Lit(new Color32(0xe1, 0x7a, 0x16, 255), 0f, .57f);
            black = Lit(new Color32(0x22, 0x24, 0x2a, 255), 0f, .55f);
            gold = Lit(new Color32(0xff, 0xd3, 0x36, 255), .35f, .7f);
            blue = Lit(new Color32(0x16, 0x89, 0xed, 255), .2f, .8f);
            green = Lit(new Color32(0x67, 0xd9, 0x22, 255), 0f, .75f);
            cream = Lit(new Color32(0xfb, 0xf2, 0xdf, 255), 0f, .5f);
            // Runtime keywords would be stripped from the build, so the "glowing" face is just bright.
            glow = Lit(new Color32(0xff, 0xf4, 0xa0, 255), 0f, .5f);
            rocketBlue = Lit(new Color32(0x12, 0x77, 0xde, 255), .3f, .74f);
            bulletRed = Lit(new Color32(0xd5, 0x20, 0x20, 255), .3f, .74f);
        }

        static Transform Part(Transform g, Mesh mesh, Material mat, Vector3 pos = default, Vector3? scale = null, Vector3 rotation = default)
        {
            var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = g.gameObject.layer;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var t = go.transform;
            t.SetParent(g, false);
            t.localPosition = pos;
            t.localScale = scale ?? Vector3.one;
            t.localRotation = Quaternion.Euler(rotation * Mathf.Rad2Deg);
            return t;
        }

        static Vector3 V(float x, float y, float z) => new(x, y, z);

        public static GameObject Build(WeaponType type, int layer = 0)
        {
            EnsureMaterials();
            var root = new GameObject(type.ToString()) { layer = layer };
            var g = root.transform;
            var sphere = M.Sphere(24, 16);
            switch (type)
            {
                case WeaponType.Glove:
                    Part(g, sphere, red, V(0, 0, -.1f), V(.56f, .53f, .5f));
                    Part(g, sphere, red, V(-.38f, -.23f, .02f), V(.25f, .33f, .3f));
                    Part(g, M.Box, darkRed, V(0, -.40f, .25f), V(.65f, .4f, .47f));
                    Part(g, M.Torus(.26f, .05f, 10, 28), black, V(0, -.48f, .52f));
                    Part(g, M.Torus(.41f, .015f, 6, 32), darkRed, V(.05f, -.01f, .30f), V(1, .36f, 1), V(.25f, 0, 0));
                    break;
                case WeaponType.Hammer:
                case WeaponType.Axe:
                case WeaponType.Mace:
                    Part(g, M.Cylinder(.085f, .105f, 1.4f), orange, V(0, -.4f, 0), null, V(0, 0, -.2f));
                    if (type == WeaponType.Hammer)
                    {
                        Part(g, M.Cylinder(.22f, .22f, .8f, 24), metal, V(.12f, .22f, 0), null, V(0, 0, Mathf.PI / 2));
                        foreach (float x in new[] { -.32f, .55f })
                            Part(g, M.Cylinder(.27f, .27f, .09f, 24), metal, V(x, .22f, 0), null, V(0, 0, Mathf.PI / 2));
                    }
                    else if (type == WeaponType.Mace)
                    {
                        Part(g, sphere, metal, V(.12f, .25f, 0), V(.4f, .4f, .4f));
                        for (int i = 0; i < 8; i++)
                            Part(g, M.Cone(.1f, .27f, 8), metal, V(.12f + Mathf.Cos(i * .79f) * .4f, .25f + Mathf.Sin(i * .79f) * .4f, 0), null, V(0, 0, i * .79f - Mathf.PI / 2));
                    }
                    else
                    {
                        Part(g, M.Box, metal, V(.35f, .20f, 0), V(.70f, .50f, .10f));
                        Part(g, M.Box, metal, V(.72f, .20f, 0), V(.08f, .6f, .14f));
                    }
                    break;
                case WeaponType.Knife:
                    Part(g, M.Cone(.19f, .9f, 4), metal, V(0, .3f, 0), V(1, 1, .28f));
                    Part(g, M.Cylinder(.10f, .12f, .6f, 12), black, V(0, -.43f, 0));
                    Part(g, M.Box, gold, V(0, -.12f, 0), V(.4f, .07f, .15f));
                    break;
                case WeaponType.Star:
                {
                    var outline = new Vector2[16];
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI / 8, r = i % 4 == 0 ? .7f : i % 2 == 0 ? .35f : .18f;
                        outline[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                    }
                    Part(g, M.Prism("shuriken", outline, .1f), metal, V(0, 0, -.05f));
                    Part(g, M.Torus(.11f, .04f, 8, 20), metal, V(0, 0, .08f));
                    Part(g, sphere, blue, V(0, 0, .1f), V(.08f, .08f, .04f));
                    break;
                }
                case WeaponType.Bomb:
                    Part(g, sphere, black, Vector3.zero, V(.44f, .44f, .44f));
                    Part(g, M.Cylinder(.06f, .06f, .35f, 8), orange, V(.05f, .48f, 0));
                    Part(g, sphere, gold, V(.05f, .66f, 0), V(.07f, .07f, .07f));
                    Part(g, sphere, cream, V(0, -.04f, .43f), V(.20f, .22f, .055f));
                    Part(g, sphere, black, V(-.075f, .02f, .485f), V(.055f, .065f, .025f));
                    Part(g, sphere, black, V(.075f, .02f, .485f), V(.055f, .065f, .025f));
                    break;
                case WeaponType.Chain:
                    Part(g, sphere, metal, Vector3.zero, V(.40f, .40f, .40f));
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI * 2 / 12;
                        Part(g, M.Cone(.085f, .23f, 8), metal, V(Mathf.Cos(a) * .40f, Mathf.Sin(a) * .40f, 0), null, V(0, 0, a - Mathf.PI / 2));
                    }
                    for (int i = 0; i < 10; i++)
                        Part(g, M.Torus(.065f, .017f, 6, 12), black, V(0, .50f + i * .11f, 0), V(.7f, 1.2f, 1), V(0, i % 2 * Mathf.PI / 2, 0));
                    break;
                case WeaponType.Poison:
                    Part(g, M.Box, green, Vector3.zero, V(.46f, .60f, .32f));
                    Part(g, M.Cylinder(.11f, .11f, .22f, 12), gold, V(0, .42f, 0));
                    Part(g, sphere, glow, V(0, 0, .18f), V(.14f, .14f, .04f));
                    Part(g, sphere, black, V(-.05f, .03f, .22f), V(.04f, .04f, .02f));
                    Part(g, sphere, black, V(.05f, .03f, .22f), V(.04f, .04f, .02f));
                    break;
                case WeaponType.Rocket:
                case WeaponType.Bullets:
                    Part(g, M.Cylinder(.14f, .14f, .60f), type == WeaponType.Rocket ? rocketBlue : bulletRed, Vector3.zero, null, V(Mathf.PI / 2, 0, 0));
                    Part(g, M.Cone(.14f, .30f), gold, V(0, 0, -.45f), null, V(-Mathf.PI / 2, 0, 0));
                    for (int i = 0; i < 4; i++) Part(g, M.Box, gold, V(0, 0, .30f), V(.34f, .05f, .25f), V(0, 0, i * Mathf.PI / 2));
                    Part(g, M.Cone(.11f, .6f, 12), orange, V(0, 0, .7f), null, V(Mathf.PI / 2, 0, 0));
                    break;
                default: // Lightning
                    Part(g, M.Octahedron(.45f), gold);
                    Part(g, M.Cylinder(.07f, .07f, 1.3f, 8), metal, V(0, -.55f, 0));
                    break;
            }
            float s = type == WeaponType.Glove ? .66f : type == WeaponType.Bomb ? .62f : .72f;
            g.localScale = Vector3.one * s;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return root;
        }
    }
}
