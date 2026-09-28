using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Tela suelta de la ropa (capas, sobrevestas, faldas, gabardinas): se simula con Cloth de Unity.
// La parte de arriba queda sujeta al cuerpo y el resto cuelga, se mece y se aparta cuando el arma la toca,
// en vez de atravesarla. Colisiona con el cuerpo del pingüino y con la hoja del arma equipada.
public class OutfitCloth : MonoBehaviour
{
    [Tooltip("Piezas de ropa que son tela suelta (parte del nombre de la malla).")]
    public string[] looseParts = { "Cape", "Cloak", "Kilt", "Coat" };
    [Tooltip("Fracción superior de la pieza que queda cosida al cuerpo.")]
    [Range(0f, 0.8f)] public float pinnedTop = 0.22f;
    [Tooltip("Cuánto puede separarse del cuerpo la parte más baja (m).")]
    public float maxSwing = 0.16f;
    [Range(0f, 1f)] public float stiffness = 0.85f;
    [Range(0f, 1f)] public float damping = 0.25f;

    private readonly List<Cloth> _cloths = new List<Cloth>();
    private CapsuleCollider _body;
    private CapsuleCollider _blade;

    // La llama Equipment después de vestir al pingüino y poner el arma.
    public void Refresh(CapsuleCollider blade)
    {
        _blade = blade;
        EnsureBody();
        _cloths.RemoveAll(c => c == null);
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!smr.name.StartsWith("Outfit_") || !IsLoose(smr.name)) continue;
            Cloth cloth = smr.GetComponent<Cloth>();
            if (cloth == null)
            {
                cloth = smr.gameObject.AddComponent<Cloth>();
                StartCoroutine(Configure(cloth));
            }
            if (!_cloths.Contains(cloth)) _cloths.Add(cloth);
        }
        foreach (Cloth c in _cloths) SetColliders(c);
    }

    private bool IsLoose(string objName)
    {
        foreach (string p in looseParts)
            if (objName.EndsWith("_" + p) || objName.EndsWith(p)) return true;
        return false;
    }

    private IEnumerator Configure(Cloth cloth)
    {
        // Las partículas de la tela existen unos frames después de crear el componente; al iniciar la partida
        // los primeros frames aún no tienen la pose del esqueleto, así que se esperan varios.
        Vector3[] verts = null;
        for (int f = 0; f < 60; f++)
        {
            yield return null;
            if (cloth == null) yield break;
            verts = cloth.vertices;
            if (f >= 4 && verts != null && verts.Length > 0) break;
        }
        if (verts == null || verts.Length == 0) yield break;
        verts = RestHeights(cloth, verts);
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector3 v in verts)
        {
            minY = Mathf.Min(minY, v.y);
            maxY = Mathf.Max(maxY, v.y);
        }
        float height = Mathf.Max(0.001f, maxY - minY);
        float pinY = maxY - height * pinnedTop;
        var coeffs = new ClothSkinningCoefficient[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            float below = Mathf.Clamp01((pinY - verts[i].y) / Mathf.Max(0.001f, pinY - minY));
            coeffs[i].maxDistance = verts[i].y >= pinY ? 0f : Mathf.Lerp(0.01f, maxSwing, below);
            coeffs[i].collisionSphereDistance = float.MaxValue;
        }
        cloth.coefficients = coeffs;
        cloth.stretchingStiffness = stiffness;
        cloth.bendingStiffness = 0.5f;
        cloth.damping = damping;
        cloth.friction = 0.4f;
        cloth.useGravity = true;
        cloth.worldVelocityScale = 0.15f;
        cloth.worldAccelerationScale = 0.4f;
        cloth.enableContinuousCollision = true;
        SetColliders(cloth);
        ResetCloth(cloth);
    }

    // Vuelve a colocar la tela sobre el cuerpo: durante el frame sin coeficientes las partículas quedan sueltas
    // y, si el personaje aparece o se teletransporta, se quedan enredadas y separadas de la armadura.
    private static void ResetCloth(Cloth cloth)
    {
        if (cloth == null) return;
        cloth.enabled = false;
        cloth.enabled = true;
        cloth.ClearTransformMotion();
    }

    private Vector3 _lastPos;
    private int _settleFrames = 10;

    void LateUpdate()
    {
        Vector3 pos = transform.position;
        bool jumped = (pos - _lastPos).sqrMagnitude > 1f;
        _lastPos = pos;
        // Los primeros frames (el personaje se coloca en su punto de inicio) y cualquier salto brusco reinician la tela.
        if (_settleFrames > 0 || jumped)
        {
            if (_settleFrames > 0) _settleFrames--;
            foreach (Cloth c in _cloths)
                if (c != null) c.ClearTransformMotion();
            if (jumped) foreach (Cloth c in _cloths) ResetCloth(c);
        }
    }

    // Posiciones de la tela sin simular (malla con la pose del esqueleto), en el espacio del personaje:
    // las partículas simuladas pueden estar balanceándose y dar alturas distintas cada vez.
    private Vector3[] RestHeights(Cloth cloth, Vector3[] simulated)
    {
        var smr = cloth.GetComponent<SkinnedMeshRenderer>();
        if (smr == null || smr.sharedMesh == null) return simulated;
        var baked = new Mesh();
        smr.BakeMesh(baked, true);
        Vector3[] raw = baked.vertices;
        Destroy(baked);
        var unique = new List<Vector3>();
        var seen = new HashSet<Vector3Int>();
        Vector3[] source = smr.sharedMesh.vertices;
        for (int i = 0; i < source.Length && i < raw.Length; i++)
        {
            Vector3 v = source[i];
            var key = new Vector3Int(Mathf.RoundToInt(v.x * 100000f), Mathf.RoundToInt(v.y * 100000f), Mathf.RoundToInt(v.z * 100000f));
            if (seen.Add(key)) unique.Add(raw[i]);
        }
        Vector3[] rest;
        if (unique.Count == simulated.Length) rest = unique.ToArray();
        else if (raw.Length == simulated.Length) rest = raw;
        else return simulated;
        for (int i = 0; i < rest.Length; i++) rest[i] = transform.InverseTransformPoint(smr.transform.TransformPoint(rest[i]));
        return rest;
    }

    private void SetColliders(Cloth cloth)
    {
        if (cloth == null) return;
        var capsules = new List<CapsuleCollider>();
        if (_body != null) capsules.Add(_body);
        if (_blade != null) capsules.Add(_blade);
        cloth.capsuleColliders = capsules.ToArray();
    }

    // Cápsula del cuerpo (en la cadera, sigue al agacharse) para que la tela no atraviese al pingüino.
    private void EnsureBody()
    {
        if (_body != null) return;
        Transform hips = null;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == "Hips") { hips = t; break; }
        Renderer body = null;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r.name.StartsWith("Penguin_Body")) { body = r; break; }
        if (hips == null || body == null) return;

        Bounds b = body.bounds;
        var go = new GameObject("TelaColision_Cuerpo");
        go.transform.SetParent(hips, false);
        go.transform.position = b.center;
        go.transform.rotation = transform.rotation;
        go.layer = 2;
        _body = go.AddComponent<CapsuleCollider>();
        _body.isTrigger = true;
        _body.direction = 1;
        Vector3 scale = go.transform.lossyScale;
        float s = Mathf.Max(0.0001f, scale.y);
        _body.radius = Mathf.Min(b.extents.x, b.extents.z) * 0.92f / s;
        _body.height = b.size.y * 0.95f / s;
    }
}
