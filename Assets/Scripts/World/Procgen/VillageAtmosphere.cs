using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Ambiente del poblado y sus alrededores (no el menú): un invierno más frío y con más profundidad.
// Niebla fría en la distancia (el castillo y el Frostspire se leen en silueta), luz ambiental de tres tonos,
// un volumen de posprocesado propio (balance de blancos frío, sombras azuladas, algo más de contraste, brillo suave
// en farolas y ventanas, viñeta ligera). La nevada ya está en la escena. Se conservan la luz del sol y
// las sombras de la escena; los enemigos se siguen leyendo por sus ojos emisivos.
public class VillageAtmosphere : MonoBehaviour
{
    public static readonly Color FogColor = new Color(0.74f, 0.8f, 0.88f);
    public const float FogDensity = 0.0042f;

    public static VillageAtmosphere Build(Transform parent)
    {
        var go = new GameObject("Ambiente_Poblado");
        go.transform.SetParent(parent, false);
        return go.AddComponent<VillageAtmosphere>();
    }

    void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogDensity = FogDensity;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.84f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.7f);
        RenderSettings.ambientGroundColor = new Color(0.34f, 0.36f, 0.42f);
        // Sol de invierno: algo más frío, con sombras algo más marcadas.
        foreach (Light l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional && l.isActiveAndEnabled)
            {
                l.color = Color.Lerp(l.color, new Color(1f, 0.95f, 0.88f), 0.6f);
                l.shadowStrength = Mathf.Max(l.shadowStrength, 0.85f);
            }
        PostFx();
    }

    private void PostFx()
    {
        var volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 5f;
        VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();
        p.name = "Posprocesado del poblado";
        var wb = p.Add<WhiteBalance>(true);
        wb.temperature.Override(-12f);
        wb.tint.Override(2f);
        var ca = p.Add<ColorAdjustments>(true);
        ca.contrast.Override(12f);
        ca.saturation.Override(-8f);
        ca.postExposure.Override(-0.1f);
        var smh = p.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.92f, 0.96f, 1.08f, -0.04f));
        smh.highlights.Override(new Vector4(1.02f, 1.01f, 1f, 0f));
        var bloom = p.Add<Bloom>(true);
        bloom.intensity.Override(0.35f);
        bloom.threshold.Override(1.1f);
        bloom.scatter.Override(0.6f);
        var vig = p.Add<Vignette>(true);
        vig.intensity.Override(0.22f);
        vig.smoothness.Override(0.5f);
        vig.color.Override(new Color(0.05f, 0.08f, 0.14f));
        volume.sharedProfile = p;
    }
}
