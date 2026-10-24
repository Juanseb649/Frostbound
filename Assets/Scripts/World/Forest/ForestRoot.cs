using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Raíz corrupta del Bosque: late con savia mientras vive; al arrancarla (QuestMarker) se seca y se hunde en la nieve.
public class ForestRoot : MonoBehaviour
{
    public readonly List<Transform> parts = new List<Transform>();
    public readonly List<Renderer> veins = new List<Renderer>();
    public new Light light;
    public GameObject pool;

    private static Material _dry;

    public void Bind(QuestMarker marker)
    {
        if (marker.Used) Wither(1f);
        else marker.Activated += () => StartCoroutine(Die());
    }

    private IEnumerator Die()
    {
        AudioManager.Play3D(AudioId.Corrupt_Death, transform.position, 0.8f, 2f, 25f, 0.1f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.6f)
        {
            Wither(t);
            yield return null;
        }
        Wither(1f);
    }

    // 0 = viva, 1 = seca y medio hundida.
    private void Wither(float k)
    {
        if (_dry == null) _dry = MenuRuins.Lit("Raíz seca", new Color(0.3f, 0.28f, 0.26f), 0.05f);
        foreach (Transform p in parts) p.localPosition = Vector3.down * 0.9f * k;
        if (k >= 1f) foreach (Renderer r in veins) r.sharedMaterial = _dry;
        if (k >= 1f && light != null) light.enabled = false;
        float f = Mathf.Lerp(1f, 0.35f, k);
        if (pool != null) pool.transform.localScale = new Vector3(4.5f * f, 0.02f, 3.15f * f);
    }
}
