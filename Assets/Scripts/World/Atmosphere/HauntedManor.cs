using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Decorado 3D del menú. Ahora muestra el poblado nevado de noche (casas con ventanas encendidas, calle empedrada,
// campanario, iglús y pinos) con la cordillera y el Frostspire detrás. La mansión de abajo es un concepto guardado
// para una versión posterior (aún no está en el juego): se recupera con HauntedManor.Mode = Scenery.Manor.
//
// Concepto de la mansión: una mansión gótica abandonada, tragada por el hielo corrupto del Frost, en una colina nevada
// de noche. Ventanas con luz azul que late (solo una conserva una vela cálida), torre con el chapitel roto, tejado
// hundido, verja de hierro con el portón entreabierto, lápidas, árboles muertos, cuervos, bruma baja y, cada poco,
// un destello del Frost que tiñe la niebla. Es el fondo de 00_MainMenu; el héroe aparece en el camino, ante el portón.
public class HauntedManor : MonoBehaviour
{
    public static readonly Vector3 HeroSpot = new Vector3(0f, 0.06f, 1.6f);
    public static readonly Vector3 TitleCamPos = new Vector3(0.8f, 1.6f, -14f);
    public static readonly Vector3 TitleCamLook = new Vector3(0f, 6f, 16f);
    public const float TitleFov = 35f;
    // Encuadre del héroe: él queda en el tercio izquierdo y el derecho libre para las ranuras.
    // Algo apartado y alto para que detrás se vea la mansión y no solo los pilares del portón; los pies quedan
    // por encima de los botones de abajo.
    public static readonly Vector3 HeroCamPos = new Vector3(1.4f, 1.7f, -4.6f);
    public static readonly Vector3 HeroCamLook = new Vector3(0.2f, 1.3f, 8f);
    public const float HeroFov = 34f;

    // La casa se encoge sin deformarse (escala uniforme) anclada al pie de la fachada: el porche, el portón y el camino
    // no se mueven y por encima del tejado queda cielo para la cordillera del fondo.
    public const float ManorScale = 0.72f;
    public static readonly Vector3 ManorAnchor = new Vector3(0f, 0f, 12.4f);

    private const float FogDensity = 0.017f;
    private static readonly Color FlashFog = new Color(0.12f, 0.26f, 0.48f);
    private static readonly Color FlashMist = new Color(0.45f, 0.75f, 1f, 0.24f);
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static Mesh _cube, _cyl, _sphere, _prism, _cone;

    private MeshKit _kit, _manorKit;
    private Transform _manorPivot;
    private Material _stone, _stoneDark, _trim, _roof, _wood, _bark, _path, _snow, _iron, _ice, _crystal, _winDark, _winCandle, _lamp, _moon, _mistMat;
    private readonly Material[] _winFrost = new Material[3];
    private readonly Color[] _winFrostBase = new Color[3];
    private Color _crystalBase, _candleBase, _mistBase, _fogBase;
    private readonly List<Light> _frostLights = new List<Light>();
    private readonly List<float> _frostLightBase = new List<float>();
    private readonly List<(Transform t, Vector3 basePos, float speed, float phase)> _mist = new List<(Transform, Vector3, float, float)>();
    private Transform _curtain, _cam;
    private float _nextFlash, _flashStart = -100f;

    private readonly Transform[] _crows = new Transform[3], _wingL = new Transform[3], _wingR = new Transform[3];
    private readonly Vector3[] _crowOffset = new Vector3[3];
    private Vector3 _flockPos, _flockVel;
    private int _flockSize;
    private bool _flockActive;
    private float _nextFlock;

    private DepthOfField _dof;
    private ParticleSystem.ForceOverLifetimeModule _snowWind;
    private bool _built;

    private readonly System.Random _r = new System.Random(1931);

    public static HauntedManor Instance { get; private set; }

    // Qué se construye detrás del menú. Se lee en Awake, así que hay que fijarlo antes de crear el componente.
    public enum Scenery { Village, Manor }
    public static Scenery Mode = Scenery.Village;
    private static bool IsVillage => Mode == Scenery.Village;

    private float R(float a, float b) => a + (float)_r.NextDouble() * (b - a);

    void Awake()
    {
        Instance = this;
        Build();
        _built = true;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // La niebla de la noche helada se aclara un poco para que la mansión y la luna se lean desde la cámara.
        RenderSettings.fogDensity = FogDensity;
        _fogBase = RenderSettings.fogColor;
        // Cielo HDRI nocturno (Poly Haven) en lugar del degradado procedural, si está preparado.
        Material sky = Resources.Load<Material>("Materials/CieloNoche");
        if (sky != null)
        {
            RenderSettings.skybox = sky;
            DynamicGI.UpdateEnvironment();
        }
        Camera cam = Camera.main;
        _cam = cam != null ? cam.transform : null;
        // La cordillera y el Frostspire están a 200-260 m: que la cámara llegue a dibujarlos.
        if (cam != null) cam.farClipPlane = Mathf.Max(cam.farClipPlane, 400f);
        _nextFlash = Time.unscaledTime + Random.Range(5f, 9f);
        _nextFlock = Time.unscaledTime + Random.Range(3f, 6f);
    }

    private void Build()
    {
        Meshes();
        Materials();
        _kit = new MeshKit(transform, _cone, _prism, _stone);
        _manorPivot = new GameObject("Mansión_Escala").transform;
        _manorPivot.SetParent(transform, false);
        _manorPivot.localScale = Vector3.one * ManorScale;
        _manorPivot.localPosition = ManorAnchor * (1f - ManorScale);
        _manorKit = new MeshKit(_manorPivot, _cone, _prism, _stone);
        Ground();
        if (IsVillage) Village();
        else
        {
            // Fase 2: la mansión modelada en Blender (Resources/Models/Mansion). Sin ella, la de primitivas.
            GameObject model = Resources.Load<GameObject>("Models/Mansion");
            if (model != null) PlaceModel(model);
            else
            {
                // La de primitivas también va dentro del pivote de la casa.
                MeshKit main = _kit;
                _kit = _manorKit;
                Manor();
                Porch();
                Windows();
                _kit = main;
            }
            Corruption();
            FenceAndGate();
            Path();
            Graveyard();
            Trees();
        }
        Lantern();
        Cordillera();
        _kit.Combine("Mansión");
        _manorKit.Combine("Casa");
        ProjectSurfaceUVs(_kit);
        ProjectSurfaceUVs(_manorKit);
        Moon();
        if (!IsVillage)
        {
            Curtain();
            Lights();
        }
        PostFx();
        Snow();
        Mist();
        GroundFog();
        Embers();
        Crows();
    }

    private void Materials()
    {
        // Superficies con textura y relieve procedurales (ProceduralSurfaces); el color base va algo más claro
        // porque la textura lo oscurece en las juntas y las vetas.
        // Fase 2: materiales con texturas reales de Poly Haven (Resources/Materials/Manor, los crea SetupManorMaterials),
        // teñidos hacia el azul de la noche. Si no están, se usan las superficies procedurales de la fase 1.
        _stone = Real("castle_brick_07", new Color(0.5f, 0.55f, 0.66f)) ?? ProceduralSurfaces.Make("Piedra de la mansión", ProceduralSurfaces.Kind.Blocks, new Color(0.24f, 0.27f, 0.34f), 0.5f);
        _stoneDark = Real("castle_brick_07", new Color(0.3f, 0.34f, 0.43f)) ?? ProceduralSurfaces.Make("Piedra oscura", ProceduralSurfaces.Kind.Blocks, new Color(0.13f, 0.15f, 0.2f), 0.45f, 1.2f);
        _trim = Real("castle_wall_varriation", new Color(0.55f, 0.6f, 0.7f)) ?? ProceduralSurfaces.Make("Cornisa", ProceduralSurfaces.Kind.Rough, new Color(0.3f, 0.34f, 0.42f), 0.5f);
        _roof = Real("roof_slates_02", new Color(0.4f, 0.44f, 0.55f)) ?? ProceduralSurfaces.Make("Pizarra", ProceduralSurfaces.Kind.Slate, new Color(0.11f, 0.12f, 0.16f), 0.75f, 1.3f);
        _wood = Real("dark_wooden_planks", new Color(0.6f, 0.56f, 0.58f)) ?? ProceduralSurfaces.Make("Madera podrida", ProceduralSurfaces.Kind.Planks, new Color(0.17f, 0.13f, 0.11f), 0.5f);
        _bark = Real("bark_willow", new Color(0.5f, 0.54f, 0.64f)) ?? ProceduralSurfaces.Make("Corteza escarchada", ProceduralSurfaces.Kind.Bark, new Color(0.26f, 0.28f, 0.34f), 0.5f, 1.4f);
        _path = Real("cobblestone_floor_001", new Color(0.5f, 0.55f, 0.65f)) ?? ProceduralSurfaces.Make("Losa", ProceduralSurfaces.Kind.Rough, new Color(0.22f, 0.24f, 0.29f), 0.6f, 1.2f);
        _snow = Real("snow_02", new Color(0.62f, 0.68f, 0.8f)) ?? ProceduralSurfaces.Make("Nieve de noche", ProceduralSurfaces.Kind.Snow, new Color(0.5f, 0.56f, 0.66f), 0.7f, 0.6f);
        _iron = MenuRuins.Lit("Hierro forjado", new Color(0.07f, 0.08f, 0.1f), 0.5f, 0.6f);
        _ice = MenuRuins.Lit("Hielo", new Color(0.55f, 0.75f, 0.95f), 0.92f, 0f, new Color(0.08f, 0.18f, 0.35f));
        _crystalBase = new Color(0.2f, 0.5f, 1.8f);
        _crystal = MenuRuins.Lit("Cristal del Frost", new Color(0.12f, 0.3f, 0.85f), 0.9f, 0f, _crystalBase);
        for (int i = 0; i < 3; i++)
        {
            _winFrostBase[i] = new Color(0.35f, 0.85f, 2.4f) * (0.85f + i * 0.12f);
            _winFrost[i] = MenuRuins.Lit("Ventana del Frost " + (char)('A' + i), new Color(0.25f, 0.5f, 0.9f), 0.9f, 0f, _winFrostBase[i]);
        }
        _winDark = MenuRuins.Lit("Ventana rota", new Color(0.02f, 0.03f, 0.05f), 0.95f);
        _candleBase = new Color(3.2f, 1.8f, 0.7f);
        _winCandle = MenuRuins.Lit("Ventana con vela", new Color(1f, 0.75f, 0.45f), 0.6f, 0f, _candleBase);
        _lamp = MenuRuins.Lit("Cristal del farol", new Color(1f, 0.85f, 0.6f), 0.6f, 0f, new Color(4f, 2.6f, 1.2f));
        _moon = MenuRuins.Lit("Luna", Color.white, 0f, 0f, new Color(7f, 7.4f, 8f));
    }

    // Copia de un material real de la mansión con su tinte nocturno, o null si no existe.
    private static Material Real(string id, Color tint)
    {
        var src = Resources.Load<Material>("Materials/Manor/" + id);
        if (src == null) return null;
        var m = new Material(src) { name = id + " (noche)" };
        m.SetColor("_BaseColor", tint);
        return m;
    }

    // Metros por repetición de cada superficie con textura (0 = sin textura).
    private float TilingOf(Material m)
    {
        if (m == _stone || m == _stoneDark) return 2f;
        if (m == _trim || m == _wood || m == _plaster) return 1.5f;
        if (m == _roof) return 2.2f;
        if (m == _bark) return 1f;
        if (m == _path) return 1.2f;
        if (m == _snow) return 4f;
        return 0f;
    }

    // Las mallas combinadas con textura reciben UVs proyectadas en el mundo.
    private void ProjectSurfaceUVs(MeshKit kit)
    {
        foreach (MeshFilter mf in kit.visual.GetComponentsInChildren<MeshFilter>())
        {
            float meters = TilingOf(mf.GetComponent<MeshRenderer>().sharedMaterial);
            if (meters > 0f) ProceduralSurfaces.ProjectUVs(mf.sharedMesh, meters);
        }
    }

    // Coloca la mansión modelada: cada malla lleva un material con nombre (Piedra, Pizarra, FrostA…) que se cambia
    // por el de la escena, y una copia propia de la malla con UVs proyectadas como el resto del decorado.
    private void PlaceModel(GameObject prefab)
    {
        GameObject go = Instantiate(prefab, _manorPivot, false);
        go.name = "Mansión modelada";
        foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] mats = mr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = ModelMaterial(mats[i] != null ? mats[i].name : "");
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.On;
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            mf.sharedMesh = Instantiate(mf.sharedMesh);
            float meters = TilingOf(mats[0]);
            if (meters > 0f) ProceduralSurfaces.ProjectUVs(mf.sharedMesh, meters);
        }
    }

    private Material ModelMaterial(string name)
    {
        switch (name)
        {
            case "Piedra": return _stone;
            case "PiedraOscura": return _stoneDark;
            case "Cornisa": return _trim;
            case "Pizarra": return _roof;
            case "Madera": return _wood;
            case "Hierro": return _iron;
            case "Nieve": return _snow;
            case "FrostA": return _winFrost[0];
            case "FrostB": return _winFrost[1];
            case "FrostC": return _winFrost[2];
            case "Rota": return _winDark;
            case "Vela": return _winCandle;
            default: return _stone;
        }
    }

    // ---------- Terreno ----------

    private void Ground()
    {
        _kit.Box(new Vector3(0f, -0.25f, 30f), Vector3.zero, new Vector3(220f, 0.5f, 220f), _snow);
        // La colina sobre la que se alza la mansión y las lomas de los lados.
        _kit.Mesh(_sphere, new Vector3(0f, -3f, 40f), Vector3.zero, new Vector3(80f, 12f, 34f), _snow);
        _kit.Mesh(_sphere, new Vector3(-28f, -2f, 30f), Vector3.zero, new Vector3(40f, 10f, 30f), _snow);
        _kit.Mesh(_sphere, new Vector3(30f, -2f, 34f), Vector3.zero, new Vector3(44f, 12f, 30f), _snow);
        Vector4[] drifts =
        {
            new Vector4(-5f, 4.6f, 5f, 1.2f), new Vector4(5.5f, 4.5f, 6f, 1.3f), new Vector4(-11.5f, 4.4f, 6f, 1.1f), new Vector4(11.8f, 4.6f, 5f, 1.2f),
            new Vector4(-3.6f, 14.3f, 4f, 1.4f), new Vector4(7.8f, 15.9f, 5f, 1.4f), new Vector4(-9.5f, 15.9f, 4f, 1.3f),
            new Vector4(-1.7f, -4f, 1.2f, 5f), new Vector4(1.8f, -2f, 1.1f, 6f), new Vector4(-1.8f, 7f, 1f, 4f), new Vector4(1.7f, 8f, 1.1f, 3f),
            new Vector4(-5f, 9f, 3f, 2f), new Vector4(-3f, -8f, 6f, 3f), new Vector4(5f, -7f, 5f, 2.5f)
        };
        foreach (Vector4 d in drifts)
            _kit.Mesh(_sphere, new Vector3(d.x, -0.1f, d.y), new Vector3(0f, R(-10f, 10f), 0f), new Vector3(d.z, R(0.5f, 0.9f), d.w), _snow);
    }

    // ---------- El poblado (fondo actual del menú) ----------

    private Material _pine, _plaster;
    private readonly List<Vector3> _chimneys = new List<Vector3>();

    // El poblado de noche: una calle empedrada que sube hacia el campanario entre casas de piedra, madera y revoque
    // con tejados nevados y ventanas encendidas, iglús, pinos y faroles. La cordillera queda detrás.
    private void Village()
    {
        _pine = MenuRuins.Lit("Pino nevado", new Color(0.09f, 0.16f, 0.15f), 0.2f);
        _plaster = Real("castle_wall_varriation", new Color(0.62f, 0.62f, 0.68f)) ?? ProceduralSurfaces.Make("Revoque", ProceduralSurfaces.Kind.Rough, new Color(0.36f, 0.37f, 0.42f), 0.5f);
        Street();
        // A los lados de la calle miran a la calle; las del fondo miran a la cámara.
        House(new Vector3(-6.9f, 0f, 7.5f), -90f, 4.2f, 5f, 3.6f, 0);
        House(new Vector3(-7.6f, 0f, 13.8f), -90f, 4.8f, 5.6f, 4.6f, 1);
        House(new Vector3(-7.1f, 0f, 20.2f), -90f, 4.2f, 5f, 3.8f, 2);
        House(new Vector3(-13.6f, 0f, 10f), -80f, 5f, 5f, 4.2f, 3);
        House(new Vector3(-14.4f, 0f, 17.5f), -95f, 4.6f, 6f, 5f, 4);
        House(new Vector3(-11.8f, 0f, 25.5f), -60f, 4.2f, 5f, 4.2f, 5);
        House(new Vector3(7f, 0f, 8.4f), 90f, 4.2f, 5f, 3.8f, 6);
        House(new Vector3(7.8f, 0f, 14.6f), 90f, 5f, 6f, 4.8f, 7);
        House(new Vector3(7.2f, 0f, 21.2f), 90f, 4.2f, 5f, 3.6f, 8);
        House(new Vector3(14f, 0f, 11.2f), 100f, 4.6f, 5f, 4.2f, 9);
        House(new Vector3(14.6f, 0f, 19.4f), 85f, 5f, 5.6f, 4.6f, 10);
        House(new Vector3(12f, 0f, 26.5f), 60f, 4.2f, 5f, 4f, 11);
        House(new Vector3(-5.4f, 0f, 29f), 0f, 4.6f, 5f, 4.2f, 12);
        House(new Vector3(5.8f, 0f, 29.5f), 0f, 4.6f, 5f, 4f, 13);
        BellTower(new Vector3(0f, 0f, 31f));
        Igloo(new Vector3(-10.5f, 0f, 1.5f), 35f);
        Igloo(new Vector3(11.5f, 0f, 3f), -40f);
        Igloo(new Vector3(-17.5f, 0f, 4.5f), 20f);
        Pines();
        StreetLamp(new Vector3(2.7f, 0f, 9.5f), -1f);
        StreetLamp(new Vector3(-2.7f, 0f, 17f), 1f);
        // La luz cálida de las ventanas se derrama sobre la calle y la nieve.
        PointLight("Luz de ventanas", new Vector3(-4f, 1.8f, 13.8f), new Color(1f, 0.66f, 0.38f), 7f, 1.8f, true);
        PointLight("Luz de ventanas", new Vector3(4.4f, 1.8f, 14.6f), new Color(1f, 0.66f, 0.38f), 7f, 1.8f, true);
        PointLight("Luz del campanario", new Vector3(0f, 4f, 27.6f), new Color(1f, 0.7f, 0.42f), 10f, 2.2f, true);
        for (int i = 0; i < _chimneys.Count; i += 3) Smoke(_chimneys[i]);
    }

    // Calle empedrada desde la cámara hasta el campanario, con una plazoleta redonda donde espera el héroe.
    private void Street()
    {
        for (float z = -11f; z < 28f; z += R(0.78f, 0.92f))
        {
            if (R(0f, 1f) < 0.05f) continue;
            float w = R(2.9f, 3.3f), d = R(0.62f, 0.78f);
            _kit.Box(new Vector3(R(-0.1f, 0.1f), 0.03f, z), new Vector3(0f, R(-4f, 4f), 0f), new Vector3(w, 0.08f, d), _path);
        }
        _kit.Cylinder(new Vector3(0f, 0.02f, HeroSpot.z), 5.4f, 0.05f, _path);
    }

    // Casa de una planta y desván: zócalo, muros, tejado a dos aguas con nieve en la mitad alta, puerta con marco,
    // ventanas (casi todas encendidas; alguna rota y alguna ya tocada por el Frost) y chimenea.
    // La fachada con la puerta mira a -z local.
    private void House(Vector3 p, float yaw, float w, float d, float h, int v)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 e = new Vector3(0f, yaw, 0f);
        Vector3 L(float x, float y, float z) => p + q * new Vector3(x, y, z);
        Material wall = v % 3 == 1 ? _wood : v % 3 == 2 ? _plaster : _stone;

        _kit.Box(L(0f, 0.3f, 0f), e, new Vector3(w + 0.3f, 0.6f, d + 0.3f), _stoneDark);
        _kit.Box(L(0f, h * 0.5f, 0f), e, new Vector3(w, h, d), wall);
        if (wall == _plaster)
        {
            // Entramado de madera en la fachada.
            foreach (float s in new[] { -1f, 1f })
                _kit.Box(L(s * (w * 0.5f - 0.08f), h * 0.5f, -d * 0.5f - 0.03f), e, new Vector3(0.16f, h, 0.08f), _wood);
            _kit.Box(L(0f, h * 0.56f, -d * 0.5f - 0.03f), e, new Vector3(w, 0.14f, 0.08f), _wood);
        }
        _kit.Box(L(0f, h + 0.06f, 0f), e, new Vector3(w + 0.25f, 0.12f, d + 0.25f), _trim);
        float rh = w * 0.62f;
        _kit.Prism(L(0f, h + 0.1f, 0f), e, new Vector3(w + 0.8f, rh, d + 0.7f), _roof);
        // Nieve: el mismo triángulo, más pequeño, apoyado en la cumbrera.
        _kit.Prism(L(0f, h + 0.1f + rh * 0.42f, 0f), e, new Vector3((w + 0.8f) * 0.58f + 0.06f, rh * 0.58f + 0.08f, d + 0.78f), _snow);

        float side = v % 2 == 0 ? 1f : -1f;
        _kit.Box(L(side * w * 0.2f, 0.98f, -d * 0.5f - 0.02f), e, new Vector3(1.15f, 1.9f, 0.06f), _trim);
        _kit.Box(L(side * w * 0.2f, 0.95f, -d * 0.5f - 0.06f), e, new Vector3(0.9f, 1.7f, 0.08f), wall == _wood ? _stoneDark : _wood);
        Window(L(-side * w * 0.22f, h * 0.6f, -d * 0.5f - 0.04f), e, new Vector3(0.62f, 0.78f, 0.06f), WindowMat(v, 0));
        if (h > 4.2f) Window(L(side * w * 0.2f, h * 0.8f, -d * 0.5f - 0.04f), e, new Vector3(0.5f, 0.6f, 0.06f), WindowMat(v, 1));
        Window(L(0f, h + 0.1f + rh * 0.3f, -(d + 0.7f) * 0.5f - 0.03f), e, new Vector3(0.45f, 0.55f, 0.06f), WindowMat(v, 2));
        foreach (float s in new[] { -1f, 1f })
            foreach (float zz in new[] { -d * 0.22f, d * 0.22f })
                Window(L(s * (w * 0.5f + 0.04f), h * 0.55f, zz), e, new Vector3(0.06f, 0.72f, 0.58f), WindowMat(v, s > 0f ? 3 : 4));

        if (v % 3 != 2)
        {
            Vector3 c = L(w * 0.24f, h + rh * 0.55f, d * 0.18f);
            _kit.Box(c + Vector3.up * 0.5f, e, new Vector3(0.55f, 1.7f, 0.55f), _stoneDark);
            _kit.Box(c + Vector3.up * 1.38f, e, new Vector3(0.64f, 0.1f, 0.64f), _snow);
            _chimneys.Add(c + Vector3.up * 1.45f);
        }
    }

    private Material WindowMat(int v, int i)
    {
        int k = (v * 3 + i * 5) % 11;
        if (k == 3) return _winDark;
        if (k == 7) return _winFrost[v % 3];
        return _winCandle;
    }

    // Ventana con su alféizar nevado debajo.
    private void Window(Vector3 c, Vector3 euler, Vector3 size, Material m)
    {
        _kit.Box(c, euler, size, m);
        Vector3 sill = size.x < size.z ? new Vector3(0.16f, 0.06f, size.z + 0.14f) : new Vector3(size.x + 0.14f, 0.06f, 0.16f);
        _kit.Box(c + Vector3.down * (size.y * 0.5f + 0.03f), euler, sill, _snow);
    }

    // Campanario de piedra al final de la calle: cuerpo, campanas encendidas, chapitel en cruz y nieve.
    private void BellTower(Vector3 p)
    {
        Vector3 up = Vector3.up;
        _kit.Box(p + up * 0.6f, Vector3.zero, new Vector3(4.4f, 1.2f, 4.4f), _stoneDark);
        _kit.Box(p + up * 4.2f, Vector3.zero, new Vector3(3.5f, 7.2f, 3.5f), _stone);
        _kit.Box(p + up * 7.9f, Vector3.zero, new Vector3(3.9f, 0.3f, 3.9f), _trim);
        _kit.Box(p + up * 9f, Vector3.zero, new Vector3(3.3f, 2f, 3.3f), _stoneDark);
        foreach (Vector3 n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
        {
            Vector3 size = n.x != 0f ? new Vector3(0.06f, 1.3f, 1f) : new Vector3(1f, 1.3f, 0.06f);
            _kit.Box(p + up * 9f + n * 1.68f, Vector3.zero, size, _winCandle);
        }
        _kit.Box(p + up * 10.1f, Vector3.zero, new Vector3(3.9f, 0.25f, 3.9f), _trim);
        foreach (float yaw in new[] { 0f, 90f })
        {
            _kit.Prism(p + up * 10.2f, new Vector3(0f, yaw, 0f), new Vector3(4.1f, 4f, 4.1f), _roof);
            _kit.Prism(p + up * (10.2f + 4f * 0.45f), new Vector3(0f, yaw, 0f), new Vector3(4.1f * 0.55f + 0.06f, 4f * 0.55f + 0.08f, 4.2f), _snow);
        }
        _kit.Cone(p + up * 14.1f, 0.22f, 1.2f, _iron);
        Window(p + new Vector3(0f, 5.4f, -1.79f), Vector3.zero, new Vector3(0.7f, 1.3f, 0.06f), _winCandle);
        _kit.Box(p + new Vector3(0f, 2.3f, -1.8f), Vector3.zero, new Vector3(1.5f, 2.4f, 0.08f), _trim);
        _kit.Box(p + new Vector3(0f, 2.2f, -1.84f), Vector3.zero, new Vector3(1.2f, 2.2f, 0.08f), _wood);
    }

    // Iglú medio enterrado con su túnel y la luz de dentro asomando por la entrada.
    private void Igloo(Vector3 p, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 e = new Vector3(0f, yaw, 0f);
        _kit.Mesh(_sphere, p, e, new Vector3(4.2f, 3.4f, 4.2f), _snow);
        _kit.Mesh(_sphere, p + q * new Vector3(0f, 0f, -2f), e, new Vector3(1.6f, 2f, 2f), _snow);
        _kit.Box(p + q * new Vector3(0f, 0.5f, -2.96f), e, new Vector3(0.75f, 0.9f, 0.08f), _winCandle);
    }

    private void Pines()
    {
        // Dos en primer plano enmarcan la vista; el resto rodea el poblado y sube por la loma del fondo.
        Pine(new Vector3(-6.4f, 0f, -3f), 7f);
        Pine(new Vector3(7.4f, 0f, -2.6f), 6.4f);
        Pine(new Vector3(-18.5f, 0f, 9f), 8f);
        Pine(new Vector3(19f, 0f, 13f), 7.5f);
        Pine(new Vector3(-20f, 0f, 21f), 8.5f);
        Pine(new Vector3(20.5f, 0f, 24f), 8f);
        for (int i = 0; i < 14; i++)
            Pine(new Vector3(R(-28f, 28f), R(0.5f, 2.5f), R(32f, 44f)), R(6f, 10f));
    }

    // Pino de tres pisos con nieve en la parte alta de cada uno.
    private void Pine(Vector3 p, float h)
    {
        _kit.Cylinder(p + Vector3.up * h * 0.12f, h * 0.07f, h * 0.24f, _bark);
        for (int k = 0; k < 3; k++)
        {
            float t = k / 3f;
            float y = h * (0.18f + t * 0.27f), dia = h * (0.62f - t * 0.17f), ht = h * (0.42f - t * 0.06f);
            Vector3 e = new Vector3(0f, R(0f, 45f), 0f);
            _kit.Cone(p + Vector3.up * y, dia, ht, _pine, e);
            _kit.Cone(p + Vector3.up * (y + ht * 0.45f), dia * 0.57f, ht * 0.57f, _snow, e);
        }
    }

    // Farol de la calle; el brazo apunta hacia la calzada (side = +1 a la derecha, -1 a la izquierda).
    private void StreetLamp(Vector3 p, float side)
    {
        _kit.Box(p + new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.3f, 0.25f, 0.3f), _iron);
        _kit.Cylinder(p + new Vector3(0f, 1.3f, 0f), 0.12f, 2.6f, _iron);
        _kit.Box(p + new Vector3(side * 0.3f, 2.5f, 0f), Vector3.zero, new Vector3(0.62f, 0.06f, 0.06f), _iron);
        Vector3 lamp = p + new Vector3(side * 0.55f, 2.18f, 0f);
        _kit.Box(lamp, Vector3.zero, new Vector3(0.24f, 0.34f, 0.24f), _lamp);
        _kit.Box(lamp + Vector3.down * 0.19f, Vector3.zero, new Vector3(0.3f, 0.04f, 0.3f), _iron);
        _kit.Cone(lamp + Vector3.up * 0.17f, 0.36f, 0.22f, _iron);
        PointLight("Luz del farol", lamp + Vector3.down * 0.05f, new Color(1f, 0.68f, 0.38f), 7f, 2.4f, true);
    }

    // Humo de chimenea: sube despacio y el viento lo tumba.
    private void Smoke(Vector3 top)
    {
        var go = new GameObject("Humo");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = top;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.6f, 0.65f, 0.75f, 0.3f);
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = 4f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.12f;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = MenuRuins.Transparent("Humo", MenuRuins.Soft(64), Color.white, false);
        ps.Play();
    }

    // ---------- La mansión ----------

    private void Manor()
    {
        // Cuerpo central de dos plantas sobre un zócalo.
        _kit.Box(new Vector3(0f, 0.5f, 19f), Vector3.zero, new Vector3(10.6f, 1f, 8.6f), _stoneDark);
        _kit.Box(new Vector3(0f, 5f, 19f), Vector3.zero, new Vector3(10f, 8f, 8f), _stone);
        _kit.Box(new Vector3(0f, 5.2f, 19f), Vector3.zero, new Vector3(10.15f, 0.18f, 8.15f), _trim);
        _kit.Box(new Vector3(0f, 9.05f, 19f), Vector3.zero, new Vector3(10.5f, 0.3f, 8.5f), _trim);
        foreach (float x in new[] { -5f, 5f }) Buttress(new Vector3(x, 0f, 15f), 8.8f);

        // Tejado hundido entre x=2 y x=4.2: las vigas quedan al aire.
        RoofX(new Vector3(-1.65f, 9.2f, 19f), 7.3f, 8.6f, 4.2f);
        RoofX(new Vector3(4.75f, 9.2f, 19f), 1.1f, 8.6f, 4.2f);
        float slope = Mathf.Atan2(4.2f, 4.3f) * Mathf.Rad2Deg, rafter = Mathf.Sqrt(4.2f * 4.2f + 4.3f * 4.3f);
        foreach (float x in new[] { 2.4f, 3.1f, 3.8f })
        {
            float front = x == 3.1f ? rafter * 0.55f : rafter;
            _kit.Box(new Vector3(x, 9.2f, 14.7f) + new Vector3(0f, Mathf.Sin(slope * Mathf.Deg2Rad), Mathf.Cos(slope * Mathf.Deg2Rad)) * front * 0.5f,
                new Vector3(-slope, 0f, 0f), new Vector3(0.14f, 0.14f, front), _wood);
            _kit.Box(new Vector3(x, 11.3f, 21.15f), new Vector3(slope, 0f, 0f), new Vector3(0.14f, 0.14f, rafter), _wood);
        }
        _kit.Box(new Vector3(2.5f, 13.35f, 19f), Vector3.zero, new Vector3(1f, 0.16f, 0.16f), _wood);
        _kit.Box(new Vector3(3.6f, 10.4f, 17.2f), new Vector3(-25f, 10f, 15f), new Vector3(1.6f, 0.15f, 2.6f), _roof);

        // Hastial gótico sobre la puerta.
        _kit.Prism(new Vector3(0f, 9.2f, 16.1f), Vector3.zero, new Vector3(3.2f, 3.4f, 2.6f), _roof);
        _kit.Prism(new Vector3(0f, 9.2f, 14.86f), Vector3.zero, new Vector3(2.7f, 3f, 0.2f), _stone);
        _kit.Mesh(MenuRuins.Octahedron(), new Vector3(0f, 10.4f, 14.74f), Vector3.zero, new Vector3(0.6f, 0.6f, 0.1f), _winDark);

        // Alas a los lados, un poco retrasadas.
        foreach (float s in new[] { -1f, 1f })
        {
            float x = s * 8f;
            _kit.Box(new Vector3(x, 0.5f, 19.25f), Vector3.zero, new Vector3(6.3f, 1f, 5.9f), _stoneDark);
            _kit.Box(new Vector3(x, 4f, 19.25f), Vector3.zero, new Vector3(6f, 6f, 5.5f), _stone);
            _kit.Box(new Vector3(x, 7.05f, 19.25f), Vector3.zero, new Vector3(6.4f, 0.25f, 5.9f), _trim);
            RoofX(new Vector3(x, 7.2f, 19.25f), 6.4f, 5.9f, 3f);
            Buttress(new Vector3(s * 11f, 0f, 16.5f), 6.6f);
        }

        // Torre de la esquina con el chapitel roto; la punta cayó sobre el tejado.
        _kit.Box(new Vector3(-5.6f, 0.55f, 15f), Vector3.zero, new Vector3(3.6f, 1.1f, 3.6f), _stoneDark);
        _kit.Box(new Vector3(-5.6f, 5.5f, 15f), Vector3.zero, new Vector3(3.2f, 11f, 3.2f), _stone);
        _kit.Box(new Vector3(-5.6f, 6.5f, 15f), Vector3.zero, new Vector3(3.35f, 0.2f, 3.35f), _trim);
        _kit.Box(new Vector3(-5.6f, 11.2f, 15f), Vector3.zero, new Vector3(3.7f, 0.4f, 3.7f), _trim);
        _kit.Cone(new Vector3(-5.6f, 11.4f, 15f), 4f, 3.4f, _roof, new Vector3(0f, 22.5f, 6f));
        _kit.Cone(new Vector3(-3.4f, 12.3f, 17.6f), 0.9f, 1.5f, _roof, new Vector3(-30f, 40f, 95f));

        // Chimeneas.
        _kit.Box(new Vector3(-3.8f, 12.3f, 20.5f), Vector3.zero, new Vector3(0.85f, 4.2f, 0.85f), _stoneDark);
        _kit.Box(new Vector3(-3.8f, 14.45f, 20.5f), Vector3.zero, new Vector3(1.05f, 0.2f, 1.05f), _trim);
        _kit.Box(new Vector3(4.75f, 12f, 21.5f), Vector3.zero, new Vector3(0.8f, 3.6f, 0.8f), _stoneDark);
        _kit.Box(new Vector3(4.75f, 13.85f, 21.5f), Vector3.zero, new Vector3(1f, 0.2f, 1f), _trim);
    }

    private void Buttress(Vector3 p, float h)
    {
        _kit.Box(p + new Vector3(0f, h * 0.5f, 0f), Vector3.zero, new Vector3(0.7f, h, 0.7f), _stoneDark);
        _kit.Box(p + new Vector3(0f, 1f, -0.3f), new Vector3(-12f, 0f, 0f), new Vector3(0.85f, 2f, 1f), _stoneDark);
        _kit.Box(p + new Vector3(0f, h + 0.1f, 0f), Vector3.zero, new Vector3(0.85f, 0.2f, 0.85f), _trim);
    }

    // Tejado a dos aguas con la cumbrera a lo largo de x y manchas de nieve en la vertiente que da a la cámara.
    private void RoofX(Vector3 baseCenter, float xLen, float zDepth, float h)
    {
        _kit.Prism(baseCenter, new Vector3(0f, 90f, 0f), new Vector3(zDepth, h, xLen), _roof);
        float half = zDepth * 0.5f, a = Mathf.Atan2(h, half);
        Vector3 normal = new Vector3(0f, half, -h).normalized;
        int patches = Mathf.Max(1, Mathf.RoundToInt(xLen / 1.8f));
        for (int i = 0; i < patches; i++)
        {
            float u = R(0.08f, 0.55f);
            Vector3 p = baseCenter + new Vector3(R(-xLen * 0.5f + 0.4f, xLen * 0.5f - 0.4f), u * h, -half + u * half) + normal * 0.06f;
            _kit.Box(p, new Vector3(-a * Mathf.Rad2Deg, 0f, 0f), new Vector3(R(0.8f, 2.2f), 0.08f, R(0.6f, 1.4f)), _snow);
        }
    }

    private void Porch()
    {
        for (int i = 0; i < 4; i++)
        {
            float depth = 2f - i * 0.5f;
            _kit.Box(new Vector3(0f, 0.125f + i * 0.25f, 12.6f - depth * 0.5f), Vector3.zero, new Vector3(4.4f - i * 0.1f, 0.25f, depth), _stoneDark);
        }
        _kit.Box(new Vector3(0f, 0.5f, 13.8f), Vector3.zero, new Vector3(4.8f, 1f, 2.4f), _stoneDark);
        _kit.Cylinder(new Vector3(-2.1f, 2.5f, 12.9f), 0.35f, 3f, _trim);
        // La columna derecha está partida; un tambor quedó en la nieve.
        _kit.Cylinder(new Vector3(2.1f, 2.1f, 12.9f), 0.35f, 2.2f, _trim);
        Limb(new Vector3(2.9f, 0.18f, 11.2f), new Vector3(3.6f, 0.18f, 11.9f), 0.35f, _trim);
        _kit.Box(new Vector3(0f, 4.05f, 12.9f), new Vector3(0f, 0f, -3f), new Vector3(5f, 0.3f, 0.3f), _wood);
        _kit.Prism(new Vector3(0f, 4.15f, 13.75f), new Vector3(0f, 0f, -4f), new Vector3(5.4f, 1.5f, 2.8f), _roof);
        // Puerta con arco apuntado.
        _kit.Box(new Vector3(0f, 2.4f, 14.93f), Vector3.zero, new Vector3(1.7f, 2.8f, 0.14f), _wood);
        _kit.Prism(new Vector3(0f, 3.8f, 14.93f), Vector3.zero, new Vector3(1.7f, 0.9f, 0.14f), _wood);
        _kit.Prism(new Vector3(0f, 3.8f, 14.97f), Vector3.zero, new Vector3(2.2f, 1.2f, 0.1f), _trim);
        foreach (float s in new[] { -1f, 1f })
            _kit.Box(new Vector3(s * 0.98f, 2.4f, 14.88f), Vector3.zero, new Vector3(0.25f, 2.8f, 0.22f), _trim);
    }

    // ---------- Ventanas ----------

    // 0-2: luz del Frost (tres tonos que laten a destiempo) · 3: rota y oscura · 4: la vela.
    private void Windows()
    {
        Window(new Vector3(-3.6f, 3.4f, 15f), 1.1f, 1.9f, 3);
        Window(new Vector3(3.6f, 3.4f, 15f), 1.1f, 1.9f, 0);
        Window(new Vector3(-3.4f, 6.9f, 15f), 1.1f, 1.9f, 1);
        Window(new Vector3(0f, 6.9f, 15f), 1.1f, 1.9f, 2);
        Window(new Vector3(3.4f, 6.9f, 15f), 1.1f, 1.9f, 0);
        Window(new Vector3(-9.6f, 3f, 16.5f), 1f, 1.6f, 2);
        Window(new Vector3(-9.6f, 5.4f, 16.5f), 1f, 1.6f, 3);
        Window(new Vector3(6.8f, 3f, 16.5f), 1f, 1.6f, 1);
        Window(new Vector3(9.4f, 3f, 16.5f), 1f, 1.6f, 3);
        Window(new Vector3(6.8f, 5.4f, 16.5f), 1f, 1.6f, 0);
        Window(new Vector3(9.4f, 5.4f, 16.5f), 1f, 1.6f, 4);
        Window(new Vector3(-5.6f, 8.2f, 13.4f), 0.8f, 1.5f, 1);
    }

    private void Window(Vector3 c, float w, float h, int kind)
    {
        float z = c.z;
        Material glass = kind <= 2 ? _winFrost[kind] : kind == 3 ? _winDark : _winCandle;
        _kit.Box(new Vector3(c.x, c.y, z - 0.11f), Vector3.zero, new Vector3(w, h, 0.04f), glass);
        _kit.Prism(new Vector3(c.x, c.y + h * 0.5f, z - 0.11f), Vector3.zero, new Vector3(w, w * 0.55f, 0.04f), glass);
        // Marco de piedra: jambas, alféizar y arco apuntado por detrás del cristal.
        foreach (float s in new[] { -1f, 1f })
            _kit.Box(new Vector3(c.x + s * (w * 0.5f + 0.1f), c.y, z - 0.1f), Vector3.zero, new Vector3(0.2f, h + 0.1f, 0.2f), _trim);
        _kit.Box(new Vector3(c.x, c.y - h * 0.5f - 0.08f, z - 0.16f), Vector3.zero, new Vector3(w + 0.55f, 0.16f, 0.32f), _trim);
        _kit.Box(new Vector3(c.x, c.y - h * 0.5f + 0.02f, z - 0.2f), Vector3.zero, new Vector3(w + 0.4f, 0.06f, 0.24f), _snow);
        _kit.Prism(new Vector3(c.x, c.y + h * 0.5f, z - 0.04f), Vector3.zero, new Vector3(w + 0.4f, w * 0.75f, 0.08f), _trim);
        // Parteluz y travesaño de hierro.
        _kit.Box(new Vector3(c.x, c.y + 0.1f, z - 0.14f), Vector3.zero, new Vector3(0.05f, h + w * 0.4f, 0.03f), _iron);
        _kit.Box(new Vector3(c.x, c.y + h * 0.15f, z - 0.14f), Vector3.zero, new Vector3(w, 0.05f, 0.03f), _iron);
        if (kind != 3) return;
        for (int i = 0; i < 3; i++)
            _kit.Box(new Vector3(c.x + R(-w * 0.4f, w * 0.4f), c.y - h * 0.3f + R(0f, h * 0.3f), z - 0.13f), new Vector3(0f, 0f, R(-40f, 40f)),
                new Vector3(R(0.1f, 0.25f), R(0.2f, 0.5f), 0.02f), _ice);
    }

    // ---------- Hielo corrupto ----------

    private void Corruption()
    {
        // Brota del suelo al pie de los muros, junto al camino y en el portón.
        Crystals(new Vector3(-4.2f, 0f, 14.4f), 5, Vector3.up, 1.1f);
        Crystals(new Vector3(5.6f, 0f, 16f), 4, Vector3.up, 1f);
        Crystals(new Vector3(-10.5f, 0f, 16.1f), 4, Vector3.up, 1f);
        Crystals(new Vector3(-7.4f, 0f, 13f), 5, Vector3.up, 1.2f);
        Crystals(new Vector3(2.7f, 0f, 12.2f), 3, Vector3.up, 0.8f);
        Crystals(new Vector3(2.4f, 0f, 4.7f), 4, Vector3.up, 0.9f);
        Crystals(new Vector3(1.6f, 0f, -1.2f), 3, Vector3.up, 0.6f);
        Crystals(new Vector3(-1.8f, 0f, 7.6f), 3, Vector3.up, 0.7f);
        Crystals(new Vector3(-5.2f, 0f, 8.3f), 3, Vector3.up, 0.7f);
        Crystals(new Vector3(7.5f, 0f, 1.5f), 3, Vector3.up, 0.8f);
        // Por las grietas de la fachada.
        Crystals(new Vector3(-1.6f, 7.9f, 15f), 4, Vector3.back, 0.8f);
        Crystals(new Vector3(4.6f, 4.6f, 15f), 3, Vector3.back, 0.7f);
        Crystals(new Vector3(-5.6f, 5.6f, 13.4f), 3, Vector3.back, 0.7f);
        Crystals(new Vector3(8f, 2f, 16.5f), 3, Vector3.back, 0.6f);
        Crystals(new Vector3(-8.6f, 6.6f, 16.5f), 3, Vector3.back, 0.6f);
        // Y revienta por el hueco del tejado.
        Crystals(new Vector3(3.1f, 10.6f, 18.4f), 7, Vector3.up, 2.2f);

        // Carámbanos en los aleros.
        Icicles(new Vector3(-5.3f, 9.2f, 14.65f), new Vector3(2f, 9.2f, 14.65f));
        Icicles(new Vector3(4.2f, 9.2f, 14.65f), new Vector3(5.3f, 9.2f, 14.65f));
        Icicles(new Vector3(-7.4f, 11f, 13.12f), new Vector3(-3.8f, 11f, 13.12f));
        Icicles(new Vector3(-11.2f, 7.2f, 16.25f), new Vector3(-7.3f, 7.2f, 16.25f));
        Icicles(new Vector3(5f, 7.2f, 16.25f), new Vector3(11.2f, 7.2f, 16.25f));
        Icicles(new Vector3(-2.7f, 4.1f, 12.4f), new Vector3(-2.7f, 4.1f, 15f));
        Icicles(new Vector3(2.7f, 3.75f, 12.4f), new Vector3(2.7f, 3.75f, 15f));
    }

    private void Crystals(Vector3 p, int count, Vector3 normal, float size)
    {
        for (int i = 0; i < count; i++)
        {
            float h = R(0.5f, 1.3f) * size;
            Vector3 dir = (normal + new Vector3(R(-0.6f, 0.6f), R(-0.6f, 0.6f), R(-0.6f, 0.6f))).normalized;
            Vector3 jitter = Vector3.ProjectOnPlane(new Vector3(R(-0.4f, 0.4f), R(-0.4f, 0.4f), R(-0.4f, 0.4f)), normal) * size;
            MeshKit kit = p.z >= ManorAnchor.z - 0.5f ? _manorKit : _kit;
            kit.Mesh(MenuRuins.Octahedron(), p + jitter + dir * h * 0.25f, Quaternion.FromToRotation(Vector3.up, dir).eulerAngles,
                new Vector3(h * 0.28f, h, h * 0.28f), _crystal);
        }
    }

    private void Icicles(Vector3 a, Vector3 b)
    {
        float len = Vector3.Distance(a, b);
        for (float d = R(0.1f, 0.3f); d < len; d += R(0.22f, 0.5f))
        {
            float l = R(0.2f, 1.1f);
            _manorKit.Mesh(MenuRuins.Octahedron(), Vector3.Lerp(a, b, d / len) + Vector3.down * l * 0.42f, new Vector3(0f, R(0f, 90f), 0f), new Vector3(0.11f, l, 0.11f), _ice);
        }
    }

    // ---------- Verja, portón y camino ----------

    private void FenceAndGate()
    {
        const float z = 4f;
        foreach (float s in new[] { -1f, 1f })
        {
            Pillar(new Vector3(s * 1.6f, 0f, z), 0.7f, 2.6f, true);
            Pillar(new Vector3(s * 8f, 0f, z), 0.5f, 2f, false);
            Pillar(new Vector3(s * 15f, 0f, z), 0.5f, 2f, false);
            Bars(s * 1.95f, s * 7.75f, z);
            Bars(s * 8.25f, s * 14.75f, z);
        }
        // Portón entreabierto: la hoja derecha cede hacia dentro.
        Leaf(new Vector3(-1.25f, 0f, z), Vector3.right, -25f);
        Leaf(new Vector3(1.25f, 0f, z), Vector3.left, 70f);
    }

    private void Pillar(Vector3 p, float w, float h, bool gate)
    {
        _kit.Box(p + new Vector3(0f, h * 0.5f, 0f), Vector3.zero, new Vector3(w, h, w), _stoneDark);
        _kit.Box(p + new Vector3(0f, h + 0.09f, 0f), Vector3.zero, new Vector3(w + 0.2f, 0.18f, w + 0.2f), _trim);
        _kit.Box(p + new Vector3(0f, h + 0.22f, 0f), Vector3.zero, new Vector3(w + 0.12f, 0.08f, w + 0.12f), _snow);
        if (gate) _kit.Mesh(_sphere, p + new Vector3(0f, h + 0.45f, 0f), Vector3.zero, Vector3.one * 0.42f, _trim);
    }

    private void Bars(float x0, float x1, float z)
    {
        float a = Mathf.Min(x0, x1), b = Mathf.Max(x0, x1);
        for (float x = a + 0.15f; x < b; x += 0.3f)
        {
            if (R(0f, 1f) < 0.06f) continue;
            const float h = 1.85f;
            bool bent = R(0f, 1f) < 0.08f;
            _kit.Box(new Vector3(x, h * 0.5f, z), new Vector3(0f, 0f, bent ? R(-14f, 14f) : 0f), new Vector3(0.045f, h, 0.045f), _iron);
            if (!bent) _kit.Cone(new Vector3(x, h, z), 0.1f, 0.2f, _iron);
        }
        foreach (float y in new[] { 0.35f, 1.6f })
            _kit.Box(new Vector3((a + b) * 0.5f, y, z), Vector3.zero, new Vector3(b - a, 0.06f, 0.06f), _iron);
    }

    private void Leaf(Vector3 pivot, Vector3 closedDir, float yaw)
    {
        const float len = 1.25f;
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * closedDir;
        float heading = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        for (float u = 0.08f; u < len; u += 0.2f)
        {
            float h = 2f + 0.35f * (u / len);
            Vector3 p = pivot + dir * u;
            _kit.Box(p + Vector3.up * h * 0.5f, Vector3.zero, new Vector3(0.045f, h, 0.045f), _iron);
            _kit.Cone(p + Vector3.up * h, 0.1f, 0.22f, _iron);
        }
        foreach (float y in new[] { 0.3f, 1f, 1.75f })
            _kit.Box(pivot + dir * len * 0.5f + Vector3.up * y, new Vector3(0f, heading, 0f), new Vector3(0.06f, 0.06f, len), _iron);
        _kit.Box(pivot + Vector3.up * 1.15f, Vector3.zero, new Vector3(0.08f, 2.3f, 0.08f), _iron);
    }

    private void Path()
    {
        for (float z = -11f; z < 10.4f; z += R(0.78f, 0.92f))
        {
            if (R(0f, 1f) < 0.07f) continue;
            float w = R(1.7f, 2.1f), d = R(0.62f, 0.78f);
            Vector3 p = new Vector3(R(-0.1f, 0.1f), 0.03f, z);
            if (R(0f, 1f) < 0.15f)
            {
                // Losa partida en dos.
                foreach (float s in new[] { -1f, 1f })
                    _kit.Box(p + Vector3.right * s * w * 0.26f, new Vector3(0f, R(-8f, 8f), 0f), new Vector3(w * 0.47f, 0.08f, d), _path);
            }
            else _kit.Box(p, new Vector3(0f, R(-5f, 5f), 0f), new Vector3(w, 0.08f, d), _path);
        }
    }

    // ---------- Lápidas, árboles y farol ----------

    private void Graveyard()
    {
        Tombstone(new Vector3(-4.2f, 0f, 7f), false);
        Tombstone(new Vector3(-6f, 0f, 9.2f), true);
        Tombstone(new Vector3(-3.4f, 0f, 10.4f), false);
        Tombstone(new Vector3(4.8f, 0f, 7.6f), false);
        Tombstone(new Vector3(6.4f, 0f, 6.2f), true);
    }

    private void Tombstone(Vector3 p, bool cross)
    {
        p.y = -0.08f;
        Quaternion rot = Quaternion.Euler(R(-12f, 12f), R(-25f, 25f), R(-14f, 14f));
        Part(_cube, p, rot, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.9f, 0.15f, 0.4f), _stoneDark);
        if (cross)
        {
            Part(_cube, p, rot, new Vector3(0f, 0.75f, 0f), Vector3.zero, new Vector3(0.18f, 1.5f, 0.16f), _stone);
            Part(_cube, p, rot, new Vector3(0f, 1.1f, 0f), Vector3.zero, new Vector3(0.8f, 0.16f, 0.16f), _stone);
            Part(_cube, p, rot, new Vector3(0f, 1.52f, 0f), Vector3.zero, new Vector3(0.2f, 0.05f, 0.18f), _snow);
            return;
        }
        Part(_cube, p, rot, new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.7f, 1f, 0.16f), _stone);
        Part(_cyl, p, rot, new Vector3(0f, 1f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.7f, 0.08f, 0.7f), _stone);
        Part(_cube, p, rot, new Vector3(0f, 1.36f, 0f), Vector3.zero, new Vector3(0.36f, 0.05f, 0.18f), _snow);
    }

    private void Trees()
    {
        // Dos en primer plano enmarcan la vista; el resto, más atrás.
        Tree(new Vector3(-5.6f, 0f, -2.5f), 6.5f, 3);
        Tree(new Vector3(6.6f, 0f, -2f), 6f, 3);
        Tree(new Vector3(-9.5f, 0f, 9f), 5.5f, 3);
        Tree(new Vector3(11f, 0f, 9.5f), 5.8f, 3);
        Tree(new Vector3(-14f, 0f, 19f), 7f, 3);
        Tree(new Vector3(15f, 0f, 21f), 6.5f, 3);
        for (int i = 0; i < 8; i++)
            Tree(new Vector3(R(-30f, 30f), R(0.5f, 2.5f), R(30f, 42f)), R(5f, 8f), 2);
    }

    private void Tree(Vector3 p, float h, int depth)
    {
        Vector3 dir = (Vector3.up + new Vector3(R(-0.15f, 0.15f), 0f, R(-0.15f, 0.15f))).normalized;
        Branch(p + Vector3.down * 0.2f, dir, h * 0.45f, h * 0.06f, depth);
    }

    private void Branch(Vector3 start, Vector3 dir, float len, float d, int depth)
    {
        Vector3 end = start + dir * len;
        Limb(start, end, d, _bark);
        if (depth == 0)
        {
            if (R(0f, 1f) < 0.4f) _kit.Mesh(MenuRuins.Octahedron(), end, new Vector3(0f, R(0f, 90f), 0f), new Vector3(0.07f, 0.2f, 0.07f), _ice);
            return;
        }
        int n = depth == 3 ? 3 : _r.Next(2, 4);
        for (int i = 0; i < n; i++)
        {
            Vector3 side = Vector3.Cross(dir, new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f))).normalized;
            Vector3 next = (Quaternion.AngleAxis(R(25f, 55f), side) * dir + Vector3.up * 0.15f).normalized;
            Branch(Vector3.Lerp(start, end, R(0.65f, 1f)), next, len * R(0.55f, 0.75f), d * 0.62f, depth - 1);
        }
    }

    // El farol junto al héroe: su luz cálida es la principal del personaje.
    private void Lantern()
    {
        Vector3 p = new Vector3(-2.3f, 0f, 0.9f);
        _kit.Box(p + new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.3f, 0.25f, 0.3f), _iron);
        _kit.Cylinder(p + new Vector3(0f, 1.3f, 0f), 0.12f, 2.6f, _iron);
        _kit.Box(p + new Vector3(0.3f, 2.5f, 0f), Vector3.zero, new Vector3(0.62f, 0.06f, 0.06f), _iron);
        Vector3 lamp = p + new Vector3(0.55f, 2.18f, 0f);
        _kit.Box(lamp, Vector3.zero, new Vector3(0.24f, 0.34f, 0.24f), _lamp);
        _kit.Box(lamp + Vector3.down * 0.19f, Vector3.zero, new Vector3(0.3f, 0.04f, 0.3f), _iron);
        _kit.Cone(lamp + Vector3.up * 0.17f, 0.36f, 0.22f, _iron);
        _kit.Box(lamp + Vector3.up * 0.3f, Vector3.zero, new Vector3(0.02f, 0.06f, 0.02f), _iron);
        PointLight("Luz del farol", lamp + new Vector3(0f, -0.05f, -0.05f), new Color(1f, 0.68f, 0.38f), 7f, 3.2f, true);
    }

    // Cordillera nevada detrás de la mansión, en dos capas (media y lejana) y con el Frostspire y su punta de cristal.
    // Va con su propio sombreador (Resources/Shaders/MountainBackdrop): la niebla del menú la borraría a esta
    // distancia. No se combina ni proyecta sombras.
    // A la izquierda, fuera del logo: equilibra la luna de la derecha y su cristal se ve en 16:9, 21:9 y 32:9.
    private const float SpireX = -112f;

    private void Cordillera()
    {
        var root = new GameObject("Cordillera").transform;
        root.SetParent(transform, false);
        Shader sh = Resources.Load<Shader>("Shaders/MountainBackdrop");
        if (sh == null) return;
        Material near = new Material(sh) { name = "Cordillera media" };
        near.SetFloat("_Haze", 0.1f);
        near.SetFloat("_Top", 70f);
        Material far = new Material(sh) { name = "Cordillera lejana" };
        far.SetFloat("_Haze", 0.2f);
        far.SetFloat("_Top", 100f);
        far.SetColor("_Snow", new Color(0.82f, 0.88f, 1f));

        var rng = new System.Random(4207);
        float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        // Las montañas son las protagonistas (el héroe es un pingüino): la capa lejana es una pared de cumbres que
        // llena el cielo hasta el borde de arriba, por detrás del logo, de lado a lado (también en 21:9 y 32:9).
        int seed = 0;
        for (float x = -300f; x <= 300f; x += Rr(18f, 30f))
        {
            // Junto al Frostspire, más bajas: que nada le quite el protagonismo.
            float h = Mathf.Abs(x - SpireX) < 35f ? Rr(55f, 70f) : Rr(70f, 92f);
            Peak(root, new Vector3(x, -2f, Rr(200f, 235f)), h, Rr(1.6f, 2.2f), far, seed++);
        }
        // Capa media: a los lados de la casa, sin tapar el centro, para enmarcarla.
        for (float x = -240f; x <= 240f; x += Rr(20f, 30f))
        {
            // Con la mansión, el centro queda libre para ella; detrás del poblado la cordillera lo cierra todo.
            if ((!IsVillage && Mathf.Abs(x) < 22f) || Mathf.Abs(x - SpireX) < 45f) continue;
            Peak(root, new Vector3(x, -2f, Rr(130f, 165f)), Rr(45f, 70f), Rr(1.5f, 2.1f), near, seed++);
        }
        // El Frostspire: el más alto y esbelto, a la izquierda de la casa; su cristal queda dentro del cuadro.
        const float spireHeight = 95f;
        Vector3 spire = new Vector3(SpireX, -2f, 235f);
        // Material propio: más helado y sin apenas bruma, para que se lea por delante de la cordillera.
        Material spireMat = new Material(sh) { name = "Frostspire" };
        spireMat.SetColor("_Snow", new Color(0.88f, 0.95f, 1f));
        spireMat.SetColor("_Rock", new Color(0.2f, 0.32f, 0.58f));
        spireMat.SetFloat("_Haze", 0.05f);
        spireMat.SetFloat("_Top", spireHeight);
        Peak(root, spire, spireHeight, 1.0f, spireMat, 99);
        Material tip = new Material(sh) { name = "Punta del Frostspire" };
        tip.SetColor("_Snow", new Color(0.35f, 0.6f, 1f));
        tip.SetColor("_Rock", new Color(0.25f, 0.45f, 0.9f));
        tip.SetColor("_Emission", new Color(0.5f, 1.3f, 3.2f));
        tip.SetFloat("_Haze", 0f);
        tip.SetFloat("_Top", 120f);
        var crystal = new GameObject("Punta de cristal", typeof(MeshFilter), typeof(MeshRenderer));
        crystal.transform.SetParent(root, false);
        crystal.transform.localPosition = spire + new Vector3(0f, spireHeight * 1.04f + 6f, 0f);
        crystal.transform.localScale = new Vector3(6f, 24f, 6f);
        crystal.GetComponent<MeshFilter>().sharedMesh = MenuRuins.Octahedron();
        var cr = crystal.GetComponent<MeshRenderer>();
        cr.sharedMaterial = tip;
        cr.shadowCastingMode = ShadowCastingMode.Off;
    }

    private void Peak(Transform parent, Vector3 basePos, float height, float widthRatio, Material mat, int seed)
    {
        var go = new GameObject("Pico", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = basePos;
        go.transform.localRotation = Quaternion.Euler(0f, seed * 47f, 0f);
        go.transform.localScale = new Vector3(height * widthRatio, height, height * widthRatio);
        go.GetComponent<MeshFilter>().sharedMesh = MountainMeshes.Peak(SeedUtil.Combine(1931, seed));
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    // ---------- Piezas sueltas (no se combinan) ----------

    private void Moon()
    {
        var moon = new GameObject("Luna", typeof(MeshFilter), typeof(MeshRenderer));
        moon.transform.SetParent(transform, false);
        // Arriba a la derecha: el logo del menú ocupa el centro del cielo.
        moon.transform.localPosition = new Vector3(19f, 17f, 40f);
        moon.transform.localScale = Vector3.one * 5f;
        moon.GetComponent<MeshFilter>().sharedMesh = _sphere;
        var mr = moon.GetComponent<MeshRenderer>();
        mr.sharedMaterial = _moon;
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    // La cortina de la ventana con la vela: se mece y recorta su silueta contra la luz cálida.
    private void Curtain()
    {
        var pivot = new GameObject("Cortina").transform;
        pivot.SetParent(_manorPivot, false);
        pivot.localPosition = new Vector3(9.15f, 6.15f, 16.5f - 0.145f);
        var cloth = new GameObject("Tela", typeof(MeshFilter), typeof(MeshRenderer));
        cloth.transform.SetParent(pivot, false);
        cloth.transform.localPosition = new Vector3(0f, -0.72f, 0f);
        cloth.transform.localScale = new Vector3(0.42f, 1.45f, 0.012f);
        cloth.GetComponent<MeshFilter>().sharedMesh = _cube;
        var mr = cloth.GetComponent<MeshRenderer>();
        mr.sharedMaterial = MenuRuins.Lit("Cortina", new Color(0.05f, 0.03f, 0.04f), 0.1f);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        _curtain = pivot;
    }

    // Luces: con la luna y el relleno de NightFrostAtmosphere suman 8; solo la luna proyecta sombras.
    private void Lights()
    {
        // Las de la casa se encogen con ella (posición y alcance).
        PointLight("Luz de la vela", new Vector3(9.4f, 5.2f, 15.8f), new Color(1f, 0.66f, 0.36f), 5.5f * ManorScale, 2.2f, true, _manorPivot);
        FrostLight(new Vector3(-3.4f, 5.2f, 13.2f), 9f * ManorScale, 2.2f, _manorPivot);
        FrostLight(new Vector3(3.2f, 9.5f, 14.5f), 10f * ManorScale, 2.4f, _manorPivot);
        FrostLight(new Vector3(-8.6f, 3.6f, 14.6f), 8f * ManorScale, 1.8f, _manorPivot);
        FrostLight(new Vector3(1.2f, 1.2f, 6f), 7f, 1.6f);
    }

    private void FrostLight(Vector3 p, float range, float intensity, Transform parent = null)
    {
        _frostLights.Add(PointLight("Luz del Frost", p, new Color(0.35f, 0.6f, 1f), range, intensity, false, parent));
        _frostLightBase.Add(intensity);
    }

    private Light PointLight(string name, Vector3 local, Color color, float range, float intensity, bool flicker, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.transform.localPosition = local;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        if (flicker)
        {
            FlickerLight f = go.AddComponent<FlickerLight>();
            f.baseIntensity = intensity;
            f.intensityVariation = intensity * 0.18f;
            f.speed = 6f;
        }
        return l;
    }

    // Retoque del posprocesado de la noche helada, como un grade de cine: más exposición y menos contraste (si no,
    // muros y nieve se hunden en negro), sombras frías y luces algo cálidas, viñeta cerrada, grano de película,
    // una aberración cromática mínima en los bordes y profundidad de campo cuando la cámara mira al héroe.
    private void PostFx()
    {
        var go = new GameObject("Posprocesado de la mansión");
        go.transform.SetParent(transform, false);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 11f;
        VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();
        p.name = "Mansión helada";
        ColorAdjustments ca = p.Add<ColorAdjustments>();
        ca.postExposure.Override(0.75f);
        ca.contrast.Override(8f);
        ShadowsMidtonesHighlights smh = p.Add<ShadowsMidtonesHighlights>();
        smh.shadows.Override(new Vector4(0.9f, 0.95f, 1.12f, 0f));
        smh.highlights.Override(new Vector4(1.06f, 1.02f, 0.95f, 0f));
        Vignette vig = p.Add<Vignette>();
        vig.intensity.Override(0.48f);
        FilmGrain grain = p.Add<FilmGrain>();
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.22f);
        grain.response.Override(0.8f);
        ChromaticAberration chroma = p.Add<ChromaticAberration>();
        chroma.intensity.Override(0.08f);
        _dof = p.Add<DepthOfField>();
        _dof.mode.Override(DepthOfFieldMode.Bokeh);
        _dof.focusDistance.Override(Vector3.Distance(HeroCamPos, HeroSpot));
        _dof.aperture.Override(2.8f);
        _dof.focalLength.Override(70f);
        _dof.active = false;
        volume.sharedProfile = p;
    }

    // Enfoca al héroe (fondo desenfocado) en las pantallas que lo muestran de cerca; lo llama FrostMenu.
    public static void FocusHero(bool on)
    {
        if (Instance != null && Instance._dof != null) Instance._dof.active = on;
    }

    // Brasas que suben del farol: el único fuego de la escena, junto a la vela.
    private void Embers()
    {
        var go = new GameObject("Brasas del farol");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(-1.75f, 2.2f, 0.9f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.04f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.8f, 0.4f));
        var em = ps.emission;
        em.rateOverTime = 6f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 1.2f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = MenuRuins.Transparent("Brasa", MenuRuins.Soft(16), Color.white, true);
        ps.Play();
    }

    // Niebla de suelo: grandes nubes tenues que se arrastran despacio por la nieve, por delante y por detrás de la verja.
    private void GroundFog()
    {
        var go = new GameObject("Niebla de suelo");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.4f, 6f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 20f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.6f, 0.72f, 0.92f, 0.1f);
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = 2.4f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 0.3f, 22f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = MenuRuins.Transparent("Niebla de suelo", MenuRuins.Soft(64), Color.white, false);
        ps.Play();
    }

    private void Snow()
    {
        var go = new GameObject("Nieve");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 10f, 6f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 11f;
        main.startSpeed = 0.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.maxParticles = 900;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.03f;
        main.startColor = new Color(0.85f, 0.92f, 1f, 0.8f);
        main.prewarm = true;
        var em = ps.emission;
        em.rateOverTime = 80f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 0.5f, 26f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.3f;
        // El viento empuja la nieve de lado; en las ráfagas (Update) sopla más fuerte.
        _snowWind = ps.forceOverLifetime;
        _snowWind.enabled = true;
        _snowWind.space = ParticleSystemSimulationSpace.World;
        _snowWind.x = new ParticleSystem.MinMaxCurve(0.15f);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = MenuRuins.Transparent("Copo", MenuRuins.Soft(32), new Color(1f, 1f, 1f, 0.9f), false);
        ps.Play();
    }

    // Bruma baja que se arrastra entre las lápidas y bancos más altos al fondo.
    private void Mist()
    {
        _mistBase = new Color(0.55f, 0.68f, 0.9f, 0.14f);
        _mistMat = MenuRuins.Transparent("Bruma baja", MenuRuins.Soft(64), _mistBase, false);
        for (int i = 0; i < 12; i++)
        {
            bool far = i >= 9;
            Vector3 p = far ? new Vector3(R(-20f, 20f), R(3f, 6f), R(28f, 40f)) : new Vector3(R(-12f, 12f), R(0.3f, 0.9f), R(1f, 18f));
            Vector3 size = far ? new Vector3(R(26f, 34f), R(5f, 7f), 1f) : new Vector3(R(9f, 15f), R(1.4f, 2.4f), 1f);
            var q = new GameObject("Bruma", typeof(MeshFilter), typeof(MeshRenderer));
            q.transform.SetParent(transform, false);
            q.transform.localPosition = p;
            q.transform.localScale = size;
            q.GetComponent<MeshFilter>().sharedMesh = Quad();
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _mistMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            _mist.Add((q.transform, p, R(0.12f, 0.3f), R(0f, 6f)));
        }
    }

    private void Crows()
    {
        Material crow = MenuRuins.Lit("Cuervo", new Color(0.02f, 0.02f, 0.03f), 0.3f);
        for (int i = 0; i < _crows.Length; i++)
        {
            var root = new GameObject("Cuervo").transform;
            root.SetParent(transform, false);
            Piece(root, MenuRuins.Octahedron(), Vector3.zero, new Vector3(0.25f, 0.2f, 0.7f), crow);
            _wingL[i] = Wing(root, -1f, crow);
            _wingR[i] = Wing(root, 1f, crow);
            root.gameObject.SetActive(false);
            _crows[i] = root;
        }
    }

    private Transform Wing(Transform body, float side, Material m)
    {
        var pivot = new GameObject("Ala").transform;
        pivot.SetParent(body, false);
        pivot.localPosition = new Vector3(side * 0.06f, 0f, 0f);
        Piece(pivot, _cube, new Vector3(side * 0.45f, 0f, 0f), new Vector3(0.9f, 0.02f, 0.26f), m);
        return pivot;
    }

    private static void Piece(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material m)
    {
        var go = new GameObject("Pieza", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---------- Animación ----------

    void Update()
    {
        // Si Unity recompila en pleno Play, los materiales creados por código se pierden: no hay nada que animar.
        if (!_built || _winFrost[0] == null) return;
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        float flash = Flash(t);

        // Ráfagas de viento: cada pocos segundos la nieve sopla de lado con más fuerza.
        float gust = Mathf.Pow(Mathf.PerlinNoise(t * 0.18f, 4.2f), 3f) * 3.5f;
        _snowWind.x = new ParticleSystem.MinMaxCurve(0.15f + gust);

        // Ventanas del Frost: cada tono late a su ritmo y a veces se apaga un instante.
        float pulse = 0f;
        for (int i = 0; i < 3; i++)
        {
            float n = Mathf.PerlinNoise(t * (0.9f + i * 0.35f), 3.1f + i * 7.7f);
            float drop = Mathf.PerlinNoise(t * 0.45f, 11f + i * 5f) < 0.22f ? 0.3f : 1f;
            float k = (0.55f + n * 0.7f) * drop;
            pulse += k / 3f;
            _winFrost[i].SetColor(EmissionId, _winFrostBase[i] * (k + flash * 2.4f));
        }
        _crystal.SetColor(EmissionId, _crystalBase * (1f + flash * 1.8f));
        _winCandle.SetColor(EmissionId, _candleBase * (0.75f + Mathf.PerlinNoise(t * 5f, 2.3f) * 0.5f));
        for (int i = 0; i < _frostLights.Count; i++)
            _frostLights[i].intensity = _frostLightBase[i] * (0.7f + pulse * 0.35f + flash * 1.6f);
        RenderSettings.fogColor = Color.Lerp(_fogBase, FlashFog, flash * 0.65f);
        _mistMat.SetColor(BaseColorId, Color.Lerp(_mistBase, FlashMist, flash));

        if (_curtain != null)
            _curtain.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * 4f + (Mathf.PerlinNoise(t * 0.6f, 9f) - 0.5f) * 10f);

        Vector3 camPos = _cam != null ? _cam.position : TitleCamPos;
        for (int i = 0; i < _mist.Count; i++)
        {
            var m = _mist[i];
            m.t.localPosition = m.basePos + new Vector3(Mathf.Sin(t * m.speed * 0.3f + m.phase) * 2.5f, 0f, Mathf.Cos(t * m.speed * 0.2f + m.phase) * 0.6f);
            Vector3 dir = m.t.position - camPos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) m.t.rotation = Quaternion.LookRotation(dir);
        }

        Flock(t, dt);
    }

    // Destello del Frost cada 12-20 s: sube de golpe y se apaga despacio.
    private float Flash(float t)
    {
        if (t >= _nextFlash)
        {
            _flashStart = t;
            _nextFlash = t + Random.Range(12f, 20f);
        }
        float e = t - _flashStart;
        if (e < 0f || e > 3f) return 0f;
        return e < 0.15f ? e / 0.15f : Mathf.Exp(-(e - 0.15f) * 2.2f);
    }

    // Una bandada de uno a tres cuervos cruza el cielo de vez en cuando.
    private void Flock(float t, float dt)
    {
        if (!_flockActive)
        {
            if (t < _nextFlock) return;
            float side = Random.value < 0.5f ? 1f : -1f;
            _flockPos = new Vector3(-26f * side, Random.Range(11f, 16f), Random.Range(24f, 34f));
            _flockVel = new Vector3(side * Random.Range(4.5f, 6.5f), Random.Range(-0.3f, 0.3f), Random.Range(-1f, 1f));
            _flockSize = Random.Range(1, _crows.Length + 1);
            for (int i = 0; i < _crows.Length; i++)
            {
                _crowOffset[i] = new Vector3(-side * i * 1.4f, Random.Range(-0.5f, 0.5f), Random.Range(-1.2f, 1.2f));
                _crows[i].gameObject.SetActive(i < _flockSize);
            }
            _flockActive = true;
        }
        _flockPos += _flockVel * dt;
        Quaternion heading = Quaternion.LookRotation(_flockVel);
        for (int i = 0; i < _flockSize; i++)
        {
            _crows[i].localPosition = _flockPos + _crowOffset[i] + Vector3.up * Mathf.Sin(t * 1.3f + i) * 0.3f;
            _crows[i].localRotation = heading;
            float glide = Mathf.Clamp01(Mathf.Sin(t * 0.9f + i * 2f) * 2f + 1f) * 0.85f + 0.15f;
            float flap = Mathf.Sin(t * 13f + i * 1.7f) * 38f * glide;
            _wingL[i].localRotation = Quaternion.Euler(0f, 0f, -flap);
            _wingR[i].localRotation = Quaternion.Euler(0f, 0f, flap);
        }
        if (Mathf.Abs(_flockPos.x) > 28f)
        {
            _flockActive = false;
            for (int i = 0; i < _crows.Length; i++) _crows[i].gameObject.SetActive(false);
            _nextFlock = t + Random.Range(7f, 15f);
        }
    }

    // ---------- Mallas ----------

    private void Limb(Vector3 a, Vector3 b, float d, Material m)
    {
        Vector3 dir = b - a;
        _kit.Mesh(_cyl, (a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, dir).eulerAngles, new Vector3(d, dir.magnitude * 0.5f, d), m);
    }

    // Pieza de un objeto inclinado: se coloca relativa a su origen y gira con él.
    private void Part(Mesh mesh, Vector3 origin, Quaternion rot, Vector3 offset, Vector3 euler, Vector3 scale, Material m)
        => _kit.Mesh(mesh, origin + rot * offset, (rot * Quaternion.Euler(euler)).eulerAngles, scale, m);

    private static void Meshes()
    {
        if (_cube == null) _cube = PrimitiveMesh(PrimitiveType.Cube);
        if (_cyl == null) _cyl = PrimitiveMesh(PrimitiveType.Cylinder);
        if (_sphere == null) _sphere = PrimitiveMesh(PrimitiveType.Sphere);
        if (_prism == null) _prism = Prism();
        if (_cone == null) _cone = Cone(8);
    }

    private static Mesh _quad;
    private static Mesh Quad()
    {
        if (_quad == null) _quad = PrimitiveMesh(PrimitiveType.Quad);
        return _quad;
    }

    private static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Mesh m = go.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(go);
        return m;
    }

    // Prisma triangular: base de 1×1 en y=0, arista superior en y=1 a lo largo de z (tejados y arcos apuntados).
    private static Mesh Prism()
    {
        Vector3 a0 = new Vector3(-0.5f, 0f, -0.5f), b0 = new Vector3(0.5f, 0f, -0.5f), c0 = new Vector3(0f, 1f, -0.5f);
        Vector3 a1 = a0 + Vector3.forward, b1 = b0 + Vector3.forward, c1 = c0 + Vector3.forward;
        Vector3 center = new Vector3(0f, 0.33f, 0f);
        var v = new List<Vector3>();
        var t = new List<int>();
        Tri(v, t, center, a0, c0, b0);
        Tri(v, t, center, a1, b1, c1);
        Quad(v, t, center, a0, a1, c1, c0);
        Quad(v, t, center, b0, c0, c1, b1);
        Quad(v, t, center, a0, b0, b1, a1);
        return Finish("Prisma", v, t);
    }

    // Cono de n lados: base de diámetro 1 en y=0 y punta en y=1.
    private static Mesh Cone(int n)
    {
        Vector3 apex = Vector3.up, center = new Vector3(0f, 0.3f, 0f);
        var v = new List<Vector3>();
        var t = new List<int>();
        for (int i = 0; i < n; i++)
        {
            float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.5f, p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.5f;
            Tri(v, t, center, p0, apex, p1);
            Tri(v, t, center, p0, p1, Vector3.zero);
        }
        return Finish("Cono", v, t);
    }

    private static void Quad(List<Vector3> v, List<int> t, Vector3 center, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Tri(v, t, center, a, b, c);
        Tri(v, t, center, a, c, d);
    }

    // Añade un triángulo orientado hacia fuera del centro de la pieza (caras planas, sin depender del orden).
    private static void Tri(List<Vector3> v, List<int> t, Vector3 center, Vector3 a, Vector3 b, Vector3 c)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - center) < 0f) (b, c) = (c, b);
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    private static Mesh Finish(string name, List<Vector3> v, List<int> t)
    {
        var m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
