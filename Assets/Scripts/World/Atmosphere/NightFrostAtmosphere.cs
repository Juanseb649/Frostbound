using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Ambiente "noche helada" reutilizable: cielo y luz ambiental azul oscuro, luna azul que perfila a los personajes
// desde atrás, niebla azul con profundidad y posprocesado (bloom, viñeta, tonemapping ACES y gradación fría).
// Se pone en cualquier escena oscura (menú, bosque corrupto, ruinas, abismo) y se ajusta con estos valores.
[DefaultExecutionOrder(-500)]
public class NightFrostAtmosphere : MonoBehaviour
{
    [Header("Cielo y luz ambiental")]
    public Color skyTop = new Color(0.02f, 0.04f, 0.08f);
    public Color skyHorizon = new Color(0.1f, 0.18f, 0.3f);
    public Color ambientSky = new Color(0.22f, 0.3f, 0.46f);
    public Color ambientEquator = new Color(0.12f, 0.17f, 0.27f);
    public Color ambientGround = new Color(0.05f, 0.07f, 0.11f);

    [Header("Luna (contraluz)")]
    public Color moonColor = new Color(0.62f, 0.75f, 1f);
    public float moonIntensity = 0.75f;
    public Vector3 moonEuler = new Vector3(28f, 160f, 0f);
    public Color fillColor = new Color(0.4f, 0.52f, 0.75f);
    public float fillIntensity = 0.28f;

    [Header("Niebla")]
    public Color fogColor = new Color(0.07f, 0.12f, 0.2f);
    [Range(0f, 0.1f)] public float fogDensity = 0.03f;

    [Header("Posprocesado")]
    public float bloomIntensity = 1.15f;
    public float bloomThreshold = 0.85f;
    [Range(0f, 1f)] public float vignette = 0.42f;
    public float exposure = 0.15f;
    public float contrast = 14f;
    public Color colorFilter = new Color(0.86f, 0.92f, 1f);
    public float temperature = -18f;

    public Light Moon { get; private set; }
    private Volume _volume;
    private Material _sky;

    void Awake() => Apply();

    public void Apply()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.skybox = SkyMaterial();
        RenderSettings.sun = null;

        foreach (Light l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional && l.transform.parent != transform) l.enabled = false;

        Moon = DirectionalLight("Luna", moonColor, moonIntensity, moonEuler, true);
        DirectionalLight("Relleno frío", fillColor, fillIntensity, new Vector3(35f, -25f, 0f), false);
        RenderSettings.sun = Moon;
        BuildVolume();
        foreach (Camera c in FindObjectsByType<Camera>()) Setup(c);
        DynamicGI.UpdateEnvironment();
    }

    public void Setup(Camera cam)
    {
        if (cam == null || cam.targetTexture != null) return;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowHDR = true;
        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    }

    private Light DirectionalLight(string name, Color color, float intensity, Vector3 euler, bool shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(euler);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = color;
        l.intensity = intensity;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        l.shadowStrength = 0.75f;
        return l;
    }

    // Cielo: degradado procedural del horizonte azul a la noche cerrada.
    private Material SkyMaterial()
    {
        if (_sky != null) return _sky;
        Shader sh = Shader.Find("Skybox/Procedural");
        if (sh == null) return null;
        _sky = new Material(sh) { name = "Cielo noche helada" };
        _sky.SetFloat("_SunSize", 0f);
        _sky.SetFloat("_SunSizeConvergence", 1f);
        _sky.SetFloat("_AtmosphereThickness", 0.35f);
        _sky.SetColor("_SkyTint", skyHorizon);
        _sky.SetColor("_GroundColor", skyTop);
        _sky.SetFloat("_Exposure", 0.35f);
        return _sky;
    }

    private void BuildVolume()
    {
        var go = new GameObject("Posprocesado");
        go.transform.SetParent(transform, false);
        _volume = go.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 10f;
        VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();
        p.name = "Noche helada";

        Bloom bloom = p.Add<Bloom>(true);
        bloom.intensity.Override(bloomIntensity);
        bloom.threshold.Override(bloomThreshold);
        bloom.scatter.Override(0.68f);
        bloom.tint.Override(new Color(0.8f, 0.9f, 1f));

        Vignette vig = p.Add<Vignette>(true);
        vig.intensity.Override(vignette);
        vig.smoothness.Override(0.45f);
        vig.color.Override(new Color(0.01f, 0.02f, 0.05f));

        Tonemapping tone = p.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        ColorAdjustments ca = p.Add<ColorAdjustments>(true);
        ca.postExposure.Override(exposure);
        ca.contrast.Override(contrast);
        ca.saturation.Override(-6f);
        ca.colorFilter.Override(colorFilter);

        WhiteBalance wb = p.Add<WhiteBalance>(true);
        wb.temperature.Override(temperature);

        _volume.sharedProfile = p;
    }
}
