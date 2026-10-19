using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Fuente del poblado (Fuente_Helada), congelada y nevada, con la estatua del pingüino fundador: pileta de sillería
// blanca de nieve con moldura, columna con basa y capitel, taza con labio de la que bajan seis chorros helados, hielo
// lechoso con grietas en la pileta y en la taza (Frostbound/QuartzCrystal), escarcha, carámbanos, nieve en los bordes,
// sobre el hielo y al pie, y el pingüino pintado con sus colores (Frostbound/PenguinStatue).
// Las piezas son mallas de revolución guardadas en Assets/Meshes/Fuente.
// Se monta desde SetupVillage.BuildFountain (al construir el poblado) o, sobre el poblado ya hecho, con
// Tools/Frostbound/Mundo/Fuente del poblado (exec SetupFountain.Run por el bridge), que solo rehace este grupo.
public static class SetupFountain
{
    private const string ScenePath = "Assets/Scenes/01_Village.unity";
    private const string MeshFolder = "Assets/Meshes/Fuente";
    private const string MatFolder = "Assets/Materials/Village/Fuente";
    private const string GroupName = "Fuente_Helada";

    // Medidas (metros). La pileta cabe en el colisionador de siempre (cápsula de radio 3.3).
    // El hielo de la pileta llega casi al borde (el agua se heló a rebosar); el de la taza, hasta el labio.
    private const float IceLow = 0.74f, IceHigh = 2.765f, BowlLip = 1.42f, ImpactRadius = 1.9f, BasinInner = 2.75f;
    private const int Streams = 6;
    private const float StreamPhaseDeg = 30f;

    [MenuItem("Tools/Frostbound/Mundo/Fuente del poblado")]
    public static void Menu() => Debug.Log("[Fuente] " + Run());

    public static string Run()
    {
        if (EditorApplication.isPlaying) return "en Play: no se toca la escena";
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject old = GameObject.Find(GroupName);
        if (old == null) return "no hay " + GroupName + " en " + scene.name;
        Transform g = old.transform;
        for (int i = g.childCount - 1; i >= 0; i--) Object.DestroyImmediate(g.GetChild(i).gameObject);
        string r = Build(g);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return r;
    }

    // Llena el grupo de la fuente (vacío). El colisionador del grupo se deja como esté.
    public static string Build(Transform g)
    {
        EnsureFolder(MeshFolder);
        EnsureFolder(MatFolder);
        var m = Materials();
        int pieces = 0;

        // ----- Pileta: zócalo, muro con moldura, borde redondeado y cubeta honda (el suelo queda bajo el agua).
        Vector2[] basin =
        {
            new Vector2(3.32f, 0f), new Vector2(3.32f, 0.1f), new Vector2(3.2f, 0.14f), new Vector2(3.17f, 0.2f),
            new Vector2(3.14f, 0.6f), new Vector2(3.2f, 0.64f), new Vector2(3.26f, 0.7f), new Vector2(3.26f, 0.78f),
            new Vector2(3.21f, 0.84f), new Vector2(2.82f, 0.84f), new Vector2(2.76f, 0.79f), new Vector2(2.74f, 0.7f),
            new Vector2(2.72f, 0.16f), new Vector2(2.6f, 0.12f), new Vector2(0f, 0.12f),
        };
        pieces += Piece(g, "Pileta", Lathe("Fuente_Pileta", basin, 72, 0.012f), m["sillar"]);

        // ----- Columna con basa y capitel (la basa queda bajo el agua).
        Vector2[] column =
        {
            new Vector2(0.66f, 0.12f), new Vector2(0.66f, 0.3f), new Vector2(0.56f, 0.36f), new Vector2(0.5f, 0.44f),
            new Vector2(0.4f, 0.5f), new Vector2(0.36f, 1.1f), new Vector2(0.34f, 1.7f), new Vector2(0.36f, 2.0f),
            new Vector2(0.44f, 2.08f), new Vector2(0.44f, 2.16f), new Vector2(0.38f, 2.2f), new Vector2(0.38f, 2.3f),
        };
        pieces += Piece(g, "Columna", Lathe("Fuente_Columna", column, 40, 0.008f), m["sillar"]);

        // ----- Taza: copa que se abre hasta un labio grueso; por dentro, la cubeta y un pedestal para la estatua.
        Vector2[] bowl =
        {
            new Vector2(0.38f, 2.28f), new Vector2(0.6f, 2.36f), new Vector2(1.0f, 2.48f), new Vector2(1.3f, 2.6f),
            new Vector2(1.42f, 2.66f), new Vector2(1.45f, 2.72f), new Vector2(1.42f, 2.78f), new Vector2(1.34f, 2.79f),
            new Vector2(1.3f, 2.74f), new Vector2(1.24f, 2.6f), new Vector2(0.58f, 2.56f), new Vector2(0.52f, 2.58f),
            new Vector2(0.52f, 2.86f), new Vector2(0.57f, 2.9f), new Vector2(0.57f, 2.95f), new Vector2(0f, 2.95f),
        };
        pieces += Piece(g, "Taza", Lathe("Fuente_Taza", bowl, 56, 0.006f), m["sillar"]);

        // ----- Hielo: pileta y taza heladas, y seis chorros que se congelaron al caer del labio.
        pieces += Piece(g, "Hielo_Pileta", Disc("Fuente_HieloPileta", BasinInner, IceLow, 72, 12), m["hielo"], ShadowCastingMode.Off);
        pieces += Piece(g, "Hielo_Taza", Ring("Fuente_HieloTaza", 0.5f, 1.325f, IceHigh, 56), m["hielo"], ShadowCastingMode.Off);
        for (int i = 0; i < Streams; i++)
        {
            float deg = StreamPhaseDeg + i * 360f / Streams;
            pieces += Piece(g, "Chorro_Helado_" + (i + 1), Stream("Fuente_ChorroHelado"), m["hielo"], ShadowCastingMode.On, Quaternion.Euler(0f, -deg, 0f));
        }

        // ----- Escarcha: placas blancas por el borde del hielo (más anchas lejos de los chorros) y carámbanos bajo la taza.
        pieces += Piece(g, "Escarcha_Pileta", FrostRing("Fuente_Escarcha", BasinInner - 0.01f, 0.3f, IceLow + 0.006f, 72), m["escarcha"], ShadowCastingMode.Off);
        Mesh cone = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/Cone_LowPoly.asset");
        var rng = new System.Random(7);
        int icicles = 0;
        for (int i = 0; i < Streams; i++)
            for (int k = 0; k < 2; k++)
            {
                // Entre chorro y chorro, colgando del vientre de la taza.
                float deg = StreamPhaseDeg + (i + 0.5f) * 360f / Streams + (k == 0 ? -9f : 9f) + (float)(rng.NextDouble() * 6 - 3);
                float len = 0.22f + (float)rng.NextDouble() * 0.38f;
                var ic = new GameObject("Carambano");
                ic.transform.SetParent(g, false);
                ic.transform.localPosition = Polar(deg, 1.36f) + Vector3.up * 2.6f;
                ic.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                ic.transform.localScale = new Vector3(0.07f, len, 0.07f);
                ic.isStatic = true;
                if (cone != null) ic.AddComponent<MeshFilter>().sharedMesh = cone;
                ic.AddComponent<MeshRenderer>().sharedMaterial = m["carambano"];
                icicles++;
            }

        // ----- Nieve: casi todo el borde de la pileta y el labio de la taza, parches sobre el hielo y ventisquero al pie.
        float[,] rim = { { 4f, 118f }, { 124f, 238f }, { 244f, 356f } };
        for (int i = 0; i < rim.GetLength(0); i++)
        {
            Vector2[] cap = { new Vector2(3.24f, 0.835f), new Vector2(3.18f, 0.875f), new Vector2(3.0f, 0.895f), new Vector2(2.86f, 0.87f), new Vector2(2.81f, 0.835f) };
            pieces += Piece(g, "Nieve_Borde_" + (i + 1), Lathe("Fuente_NieveBorde_" + (i + 1), cap, 18, 0.012f, startDeg: rim[i, 0], endDeg: rim[i, 1], taper: true), m["nieve"]);
        }
        float[,] lip = { { 8f, 150f }, { 160f, 292f }, { 300f, 352f } };
        for (int i = 0; i < lip.GetLength(0); i++)
        {
            Vector2[] cap = { new Vector2(1.455f, 2.75f), new Vector2(1.44f, 2.81f), new Vector2(1.38f, 2.83f), new Vector2(1.32f, 2.8f), new Vector2(1.3f, 2.77f) };
            pieces += Piece(g, "Nieve_Taza_" + (i + 1), Lathe("Fuente_NieveTaza_" + (i + 1), cap, 20, 0.008f, startDeg: lip[i, 0], endDeg: lip[i, 1], taper: true), m["nieve"]);
        }
        Vector3[] patches = { new Vector3(-1.2f, 0f, 1.6f), new Vector3(1.05f, 0f, -1.75f), new Vector3(-1.95f, 0f, -0.75f), new Vector3(2.15f, 0f, 0.55f) };
        for (int i = 0; i < patches.Length; i++)
            pieces += Piece(g, "Nieve_Hielo_" + (i + 1), Patch("Fuente_NieveHielo_" + (i + 1), patches[i] + Vector3.up * IceLow, 0.35f + i * 0.08f, i + 3), m["nieve"]);
        float[,] drift = { { 40f, 140f }, { 190f, 250f }, { 280f, 350f } };
        for (int i = 0; i < drift.GetLength(0); i++)
        {
            Vector2[] d = { new Vector2(3.75f, 0f), new Vector2(3.55f, 0.08f), new Vector2(3.38f, 0.16f), new Vector2(3.3f, 0.2f), new Vector2(3.27f, 0.12f) };
            pieces += Piece(g, "Nieve_Pie_" + (i + 1), Lathe("Fuente_NievePie_" + (i + 1), d, 28, 0.02f, startDeg: drift[i, 0], endDeg: drift[i, 1], taper: true), m["nieve"]);
        }

        // ----- Estatua del pingüino fundador sobre su pedestal (la misma figura, pintada con sus colores).
        GameObject statueModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Penguin.obj");
        if (statueModel != null)
        {
            var statue = (GameObject)PrefabUtility.InstantiatePrefab(statueModel, g);
            statue.name = "Estatua_Fundador";
            statue.transform.localPosition = new Vector3(0f, 2.95f, 0f);
            statue.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            statue.transform.localScale = Vector3.one;
            Bounds? b = Bounds(statue);
            float h = b.HasValue ? b.Value.size.y : 1f;
            statue.transform.localScale = Vector3.one * (1.45f / Mathf.Max(0.05f, h));
            Bounds? b2 = Bounds(statue);
            if (b2.HasValue) statue.transform.position += Vector3.up * (g.position.y + 2.95f - b2.Value.min.y);
            foreach (Renderer r in statue.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = m["estatua"];
                r.sharedMaterials = mats;
            }
            foreach (Collider c in statue.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Transform t in statue.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }

        // ----- Luz de relleno suave y sonda de reflejos para el brillo del hielo.
        var lightGo = new GameObject("Luz_Fuente");
        lightGo.transform.SetParent(g, false);
        lightGo.transform.localPosition = new Vector3(0f, 3.4f, 0f);
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.7f, 0.85f, 1f);
        l.intensity = 0.55f;
        l.range = 6f;
        l.shadows = LightShadows.None;

        var probeGo = new GameObject("Sonda_Fuente");
        probeGo.transform.SetParent(g, false);
        probeGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.resolution = 128;
        probe.size = new Vector3(10f, 6f, 10f);
        probe.importance = 2;
        probe.shadowDistance = 30f;

        AssetDatabase.SaveAssets();
        return "fuente helada: " + pieces + " piezas, " + Streams + " chorros helados, " + icicles + " carámbanos";
    }

    // ---------- Materiales ----------

    private static Dictionary<string, Material> Materials()
    {
        var m = new Dictionary<string, Material>();
        Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/Stone.mat");
        // Sillería color nieve: la piedra del poblado casi blanca y muy nevada; la sillería apenas se intuye.
        m["sillar"] = Derived("FuenteSillar", stone, mat =>
        {
            mat.SetFloat("_Tile", 1.4f);
            mat.SetFloat("_Variation", 0.08f);
            mat.SetFloat("_Snow", 0.75f);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.SetColor("_BaseColor", new Color(0.93f, 0.95f, 0.98f));
        });
        // Hielo lechoso con grietas (las vetas del cuarzo), blanco azulado: pileta, taza y chorros helados.
        Material quartz = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Cuarzo.mat");
        m["hielo"] = Derived("FuenteHielo", quartz, mat =>
        {
            mat.SetColor("_BaseColor", new Color(0.88f, 0.93f, 0.97f));
            mat.SetColor("_DepthColor", new Color(0.62f, 0.74f, 0.85f));
            mat.SetColor("_VeinColor", new Color(0.98f, 0.99f, 1f));
            mat.SetFloat("_Veins", 0.6f);
            mat.SetFloat("_VeinScale", 2.2f);
            mat.SetFloat("_Depth", 0.4f);
            mat.SetFloat("_Smoothness", 0.8f);
            mat.SetFloat("_Glow", 0.02f);
        });
        Material ice = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/Ice.mat");
        m["escarcha"] = Derived("FuenteEscarcha", ice, mat =>
        {
            mat.SetColor("_BaseColor", new Color(0.94f, 0.97f, 1f));
            mat.SetFloat("_Smoothness", 0.35f);
        });
        m["carambano"] = Derived("FuenteCarambano", ice, mat =>
        {
            mat.SetColor("_BaseColor", new Color(0.78f, 0.9f, 0.98f));
            mat.SetFloat("_Smoothness", 0.9f);
        });
        m["nieve"] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Village/Snow.mat");
        // Estatua: el pingüino con sus colores (espalda azul noche, pecho blanco, pico y patas naranjas), plumas y
        // escarcha arriba. Las zonas están medidas sobre Assets/Penguin.obj (Y arriba, pico hacia +Z).
        m["estatua"] = Mat("FuenteEstatua", Shader.Find("Frostbound/PenguinStatue"), mat =>
        {
            mat.SetFloat("_BeakMin", 0.135f);
            mat.SetFloat("_BeakMax", 0.295f);
            mat.SetFloat("_BeakZ", 0.235f);
            mat.SetFloat("_FeetY", -0.4f);
            mat.SetVector("_EyePos", new Vector4(0.032f, 0.322f, 0.19f, 0f));
            mat.SetFloat("_EyeSize", 0.024f);
            mat.SetFloat("_Feathers", 0.75f);
        });
        return m;
    }

    private static Material Mat(string name, Shader shader, System.Action<Material> setup)
    {
        string path = MatFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.shader = shader;
        setup(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material Derived(string name, Material template, System.Action<Material> setup)
    {
        string path = MatFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(template);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = template.shader;
            mat.CopyPropertiesFromMaterial(template);
        }
        setup(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- Mallas ----------

    // Superficie de revolución de un perfil (radio, altura). Cada tramo del perfil lleva sus propias normales (aristas
    // de moldura nítidas) y el radio se desplaza un poco con ruido (piedra tallada a mano, no torneada).
    private static Mesh Lathe(string name, Vector2[] profile, int segments, float roughness,
        float startDeg = 0f, float endDeg = 360f, bool taper = false, float offset = 0f)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float span = endDeg - startDeg;
        for (int s = 0; s < profile.Length - 1; s++)
        {
            Vector2 a = profile[s], b = profile[s + 1];
            if ((a - b).sqrMagnitude < 1e-8f) continue;
            int baseIndex = verts.Count;
            for (int j = 0; j <= segments; j++)
            {
                float f = j / (float)segments;
                float deg = startDeg + span * f;
                // Los tramos sueltos (nieve) se adelgazan hacia sus extremos.
                float k = taper ? Mathf.Max(0f, Mathf.Sin(f * Mathf.PI)) : 1f;
                foreach (Vector2 pt in new[] { a, b })
                {
                    Vector2 q = pt;
                    if (taper) q = Vector2.Lerp(new Vector2(q.x, profile[0].y), q, Mathf.Pow(k, 0.6f));
                    float r = q.x + offset + roughness * (Mathf.PerlinNoise(deg * 0.21f + q.y * 3.1f, q.y * 5.7f) - 0.5f) * 2f;
                    if (q.x <= 0.0001f) r = 0f;
                    verts.Add(Polar(deg, r) + Vector3.up * q.y);
                    uvs.Add(new Vector2(f * Mathf.Max(1f, span / 30f), q.y));
                }
                Vector2 d = (b - a).normalized;
                Vector2 n2 = new Vector2(d.y, -d.x);
                Vector3 radial = Polar(deg, 1f);
                Vector3 n = (radial * n2.x + Vector3.up * n2.y).normalized;
                norms.Add(n);
                norms.Add(n);
            }
            for (int j = 0; j < segments; j++)
            {
                int i0 = baseIndex + j * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
                tris.Add(i2); tris.Add(i1); tris.Add(i3);
            }
        }
        return Save(name, verts, norms, uvs, tris);
    }

    // Disco del agua: UV en el espacio del disco (centro 0.5, borde a radio 1 en UV*2-1).
    private static Mesh Disc(string name, float radius, float y, int segments, int rings)
    {
        var verts = new List<Vector3> { new Vector3(0f, y, 0f) };
        var norms = new List<Vector3> { Vector3.up };
        var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
        var tris = new List<int>();
        for (int r = 1; r <= rings; r++)
        {
            float rr = radius * r / rings;
            for (int s = 0; s < segments; s++)
            {
                float deg = s * 360f / segments;
                Vector3 p = Polar(deg, rr);
                verts.Add(p + Vector3.up * y);
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(p.x / radius * 0.5f + 0.5f, p.z / radius * 0.5f + 0.5f));
            }
        }
        for (int s = 0; s < segments; s++)
        {
            int a = 1 + s, b = 1 + (s + 1) % segments;
            tris.Add(0); tris.Add(b); tris.Add(a);
        }
        for (int r = 1; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int a = 1 + (r - 1) * segments + s, b = 1 + (r - 1) * segments + (s + 1) % segments;
                int c = a + segments, d = b + segments;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        return Save(name, verts, norms, uvs, tris);
    }

    // Anillo plano (agua de la taza alrededor del pedestal).
    private static Mesh Ring(string name, float inner, float outer, float y, int segments)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int s = 0; s <= segments; s++)
        {
            float deg = s * 360f / segments;
            foreach (float r in new[] { inner, outer })
            {
                Vector3 p = Polar(deg, r);
                verts.Add(p + Vector3.up * y);
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(p.x / outer * 0.5f + 0.5f, p.z / outer * 0.5f + 0.5f));
            }
        }
        for (int s = 0; s < segments; s++)
        {
            int i0 = s * 2;
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 1);
            tris.Add(i0 + 2); tris.Add(i0 + 3); tris.Add(i0 + 1);
        }
        return Save(name, verts, norms, uvs, tris);
    }

    // Escarcha del borde: anillo con el canto interior dentado; se estrecha donde bajan los chorros helados.
    private static Mesh FrostRing(string name, float outer, float width, float y, int segments)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int s = 0; s <= segments; s++)
        {
            float deg = s * 360f / segments;
            float toStream = Mathf.Abs(Mathf.DeltaAngle(deg, StreamPhaseDeg + Mathf.Round((deg - StreamPhaseDeg) / (360f / Streams)) * (360f / Streams)));
            float calm = Mathf.InverseLerp(6f, 22f, toStream);
            float jag = Mathf.PerlinNoise(deg * 0.35f, 1.3f) * 0.75f + (s % 2 == 0 ? 0.25f : 0f);
            float w = Mathf.Max(0.015f, width * calm * jag);
            foreach (float r in new[] { outer - w, outer })
            {
                verts.Add(Polar(deg, r) + Vector3.up * y);
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(deg / 360f, r));
            }
        }
        for (int s = 0; s < segments; s++)
        {
            int i0 = s * 2;
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 1);
            tris.Add(i0 + 2); tris.Add(i0 + 3); tris.Add(i0 + 1);
        }
        return Save(name, verts, norms, uvs, tris);
    }

    // Chorro helado: sale por encima del labio de la taza (eje +X) y baja en parábola hasta el hielo de la pileta;
    // lámina ovalada que se engrosa al bajar (el hielo se acumula abajo) y acaba en un pie ancho.
    private static Mesh Stream(string name)
    {
        const int along = 18, around = 10;
        Vector3 start = new Vector3(BowlLip - 0.12f, IceHigh + 0.04f, 0f);
        Vector3 end = new Vector3(ImpactRadius, IceLow - 0.02f, 0f);
        var pts = new Vector3[along + 1];
        for (int i = 0; i <= along; i++)
        {
            // Tiro horizontal: avanza a velocidad constante y cae cada vez más deprisa (parábola).
            float t = i / (float)along;
            pts[i] = new Vector3(Mathf.Lerp(start.x, end.x, t), Mathf.Lerp(start.y, end.y, t * t), 0f);
        }
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float dist = 0f;
        for (int i = 0; i <= along; i++)
        {
            if (i > 0) dist += Vector3.Distance(pts[i], pts[i - 1]);
            Vector3 fwd = (i < along ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
            Vector3 side = Vector3.forward;
            Vector3 up = Vector3.Cross(fwd, side).normalized;
            float t = i / (float)along;
            // Nace fino del agua de la taza (sin boca de tubo a la vista), se hace lámina sobre el labio y engorda al caer.
            float grow = Mathf.SmoothStep(0f, 1f, t / 0.18f);
            float foot = Mathf.SmoothStep(0f, 1f, (t - 0.82f) / 0.18f);
            float width = (Mathf.Lerp(0.07f, 0.13f, t) + foot * 0.12f) * grow, thick = (Mathf.Lerp(0.03f, 0.07f, t) + foot * 0.08f) * grow + 0.003f;
            for (int j = 0; j <= around; j++)
            {
                float a = j / (float)around * Mathf.PI * 2f;
                Vector3 off = side * (Mathf.Cos(a) * width) + up * (Mathf.Sin(a) * thick);
                verts.Add(pts[i] + off);
                norms.Add((side * Mathf.Cos(a) / width + up * Mathf.Sin(a) / thick).normalized);
                uvs.Add(new Vector2(j / (float)around, dist));
            }
        }
        for (int i = 0; i < along; i++)
            for (int j = 0; j < around; j++)
            {
                int a = i * (around + 1) + j, b = a + 1, c = a + around + 1, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        return Save(name, verts, norms, uvs, tris);
    }

    // Parche de nieve sobre el hielo: cúpula muy baja de contorno irregular.
    private static Mesh Patch(string name, Vector3 center, float radius, int seed)
    {
        const int segments = 24, rings = 4;
        var verts = new List<Vector3> { center + Vector3.up * 0.045f };
        var norms = new List<Vector3> { Vector3.up };
        var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
        var tris = new List<int>();
        for (int r = 1; r <= rings; r++)
        {
            float f = r / (float)rings;
            for (int s = 0; s < segments; s++)
            {
                float deg = s * 360f / segments;
                float edge = radius * (0.7f + 0.55f * Mathf.PerlinNoise(seed * 3.7f + deg * 0.03f, seed * 1.3f));
                Vector3 p = center + Polar(deg, edge * f) + Vector3.up * (0.045f * (1f - f * f) + 0.004f);
                verts.Add(p);
                norms.Add((Vector3.up + Polar(deg, f * 0.35f)).normalized);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(deg * Mathf.Deg2Rad) * f * 0.5f, 0.5f + Mathf.Sin(deg * Mathf.Deg2Rad) * f * 0.5f));
            }
        }
        for (int s = 0; s < segments; s++)
        {
            int a = 1 + s, b = 1 + (s + 1) % segments;
            tris.Add(0); tris.Add(b); tris.Add(a);
        }
        for (int r = 1; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int a = 1 + (r - 1) * segments + s, b = 1 + (r - 1) * segments + (s + 1) % segments;
                int c = a + segments, d = b + segments;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        return Save(name, verts, norms, uvs, tris);
    }

    private static Mesh Save(string name, List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
    {
        string path = MeshFolder + "/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create = mesh == null;
        if (create) mesh = new Mesh();
        mesh.Clear();
        mesh.name = name;
        mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        if (create) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static int Piece(Transform parent, string name, Mesh mesh, Material mat, ShadowCastingMode shadows = ShadowCastingMode.On, Quaternion? rotation = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (rotation.HasValue) go.transform.localRotation = rotation.Value;
        go.isStatic = true;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadows;
        return 1;
    }

    // ---------- Utilidades ----------

    private static Vector3 Polar(float deg, float r)
    {
        float a = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
    }

    private static Bounds? Bounds(GameObject go)
    {
        Bounds? b = null;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (b.HasValue) { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; }
            else b = r.bounds;
        }
        return b;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
