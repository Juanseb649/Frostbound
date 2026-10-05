using System.Collections.Generic;
using UnityEngine;

// Herramienta para construir escenarios con primitivas: coloca piezas bajo un padre,
// añade colisiones aparte y al final combina todo en una malla por material.
public class MeshKit
{
    public readonly Transform visual, colliders;
    public Mesh cone, prism;
    public Material fallback;

    public MeshKit(Transform root, Mesh cone, Mesh prism, Material fallback)
    {
        visual = Child(root, "Piezas");
        colliders = Child(root, "Colisiones");
        this.cone = cone;
        this.prism = prism;
        this.fallback = fallback;
    }

    private static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    public void Box(Vector3 center, Vector3 euler, Vector3 size, Material mat, bool collider = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Place(go, center, euler, size, mat);
        if (collider) ColliderBox(center, euler, size);
    }

    public void Cylinder(Vector3 center, float diameter, float height, Material mat, bool collider = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Place(go, center, Vector3.zero, new Vector3(diameter, height * 0.5f, diameter), mat);
        if (collider) ColliderBox(center, Vector3.zero, new Vector3(diameter * 0.8f, height, diameter * 0.8f));
    }

    public void Cone(Vector3 basePos, float diameter, float height, Material mat, Vector3 euler = default)
    {
        if (cone == null) return;
        Mesh(cone, basePos, euler, new Vector3(diameter, height, diameter), mat);
    }

    public void Prism(Vector3 basePos, Vector3 euler, Vector3 scale, Material mat)
    {
        if (prism == null) return;
        Mesh(prism, basePos, euler, scale, mat);
    }

    public void Mesh(Mesh mesh, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
    {
        var go = new GameObject("Pieza", typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        Place(go, pos, euler, scale, mat);
    }

    public void ColliderBox(Vector3 center, Vector3 euler, Vector3 size)
    {
        var go = new GameObject("Colision");
        go.transform.SetParent(colliders, false);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<BoxCollider>().size = size;
    }

    private void Place(GameObject go, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
    {
        Collider c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        go.transform.SetParent(visual, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat != null ? mat : fallback;
    }

    public void Combine(string prefix)
    {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        Matrix4x4 toLocal = visual.worldToLocalMatrix;
        foreach (MeshFilter mf in visual.GetComponentsInChildren<MeshFilter>())
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null) continue;
            Material m = mr.sharedMaterial;
            if (!groups.TryGetValue(m, out List<CombineInstance> list)) groups[m] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
        }
        for (int i = visual.childCount - 1; i >= 0; i--) Object.Destroy(visual.GetChild(i).gameObject);
        foreach (var pair in groups)
        {
            var mesh = new UnityEngine.Mesh { name = prefix + "_" + (pair.Key != null ? pair.Key.name : "sin_material"), indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(pair.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(visual, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = pair.Key;
        }
    }

    public static Material Tint(Material template, string name, Color color, Color? emission = null, float smoothness = -1f)
    {
        var m = new Material(template) { name = name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (smoothness >= 0f && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        }
        return m;
    }
}
