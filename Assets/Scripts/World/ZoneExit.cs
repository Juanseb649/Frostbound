using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider))]
public class ZoneExit : MonoBehaviour
{
    [Tooltip("Escena a cargar (usa los nombres de SceneIds). Vacío = solo avisa en consola.")]
    public string targetScene = "";
    public string zoneName = "The Foothills";

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;

        if (!string.IsNullOrEmpty(targetScene) && Application.CanStreamedLevelBeLoaded(targetScene))
        {
            if (GameSession.Instance != null) GameSession.Instance.SaveNow();
            SceneManager.LoadScene(targetScene);
            return;
        }
        Debug.Log("[ZoneExit] Saliendo hacia " + zoneName + " (escena aún no creada).");
    }

    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
    }
}
