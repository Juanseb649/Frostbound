using System.Collections;
using UnityEngine;

// Hace latir la emisión de una pieza (por ejemplo, el colgante de Steve) para llamar la atención.
public class EmissionPulse : MonoBehaviour
{
    [Tooltip("Parte del nombre del objeto que brilla. Se busca entre los hijos, también en la ropa equipada.")]
    public string partName = "Pendant";
    public Color emission = new Color(0.31f, 0.82f, 1f);
    public float minIntensity = 0.8f;
    public float maxIntensity = 1.4f;
    public float period = 2f;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private Renderer _target;
    private MaterialPropertyBlock _block;

    IEnumerator Start()
    {
        yield return null;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r.name.Contains(partName)) { _target = r; break; }
        _block = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (_target == null) return;
        float t = 0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / Mathf.Max(0.1f, period));
        _target.GetPropertyBlock(_block);
        _block.SetColor(EmissionId, emission * Mathf.Lerp(minIntensity, maxIntensity, t));
        _target.SetPropertyBlock(_block);
    }
}
