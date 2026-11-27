using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_6000_0_OR_NEWER
using PhysMat = UnityEngine.PhysicsMaterial;
using PhysCombine = UnityEngine.PhysicsMaterialCombine;
#else
using PhysMat = UnityEngine.PhysicMaterial;
using PhysCombine = UnityEngine.PhysicMaterialCombine;
#endif

namespace PinballDodgeball
{
    public static class DBUtil
    {
        public static Vector3 GetVelocity(this Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(this Rigidbody rb, Vector3 v)
        {
            if (rb.isKinematic) return;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return a + ab * t;
        }

        /// <summary>向き（XZ）→ Y 回転の角度</summary>
        public static float YawOf(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        public static Shader DefaultShader()
        {
            Shader s = null;
            if (GraphicsSettings.currentRenderPipeline != null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Unlit/Color");
            return s;
        }

        public static Material CreateMaterial(string name, Color color)
        {
            var m = new Material(DefaultShader()) { name = name };
            m.color = color;
            return m;
        }

        public static void RemoveCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Object.Destroy(c);
            else Object.DestroyImmediate(c);
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) RemoveCollider(go);
            return go;
        }

        // ---- 物理マテリアル（実行時に作って付ける） ----
        static readonly Dictionary<Vector2, PhysMat> physCache = new Dictionary<Vector2, PhysMat>();

        public static void ApplyPhysics(Collider c, float bounciness, float friction)
        {
            if (c == null) return;
            var key = new Vector2(bounciness, friction);
            if (!physCache.TryGetValue(key, out var m) || m == null)
            {
                m = new PhysMat("DB_" + bounciness + "_" + friction)
                {
                    bounciness = bounciness,
                    dynamicFriction = friction,
                    staticFriction = friction,
                    bounceCombine = PhysCombine.Maximum,
                    frictionCombine = PhysCombine.Minimum,
                };
                physCache[key] = m;
            }
            c.sharedMaterial = m;
        }
    }
}
