using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Estudio de fotos 3D fuera de la vista del juego: pingüinos reales con su equipo,
// cada toma con su cámara ortográfica que renderiza a una RenderTexture con fondo transparente.
public class PreviewStudio : MonoBehaviour
{
    public class Shot
    {
        public GameObject actor;
        public Camera camera;
        public RenderTexture texture;
        public Transform pivot;
    }

    private static int _nextSlot;
    private int _layer;
    private Light _light;

    public static PreviewStudio Create(string name, int layer)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(0f, -1000f - 50f * _nextSlot, 0f);
        _nextSlot++;
        var studio = go.AddComponent<PreviewStudio>();
        studio._layer = layer;

        var lightGo = new GameObject("StudioLight");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        studio._light = lightGo.AddComponent<Light>();
        studio._light.type = LightType.Directional;
        studio._light.intensity = 1.1f;
        studio._light.cullingMask = 1 << layer;
        studio._light.shadows = LightShadows.None;
        return studio;
    }

    // Crea una toma: el pingüino queda en "offset" dentro del estudio y la cámara lo encuadra.
    public Shot AddShot(GameObject penguinPrefab, Vector3 offset, int width, int height, float orthoSize, Vector3 lookAt, float yaw = 180f)
    {
        var shot = new Shot();
        shot.pivot = new GameObject("Pivot").transform;
        shot.pivot.SetParent(transform, false);
        shot.pivot.localPosition = offset;
        shot.pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);

        if (penguinPrefab != null)
        {
            shot.actor = Instantiate(penguinPrefab, shot.pivot, false);
            shot.actor.name = "Actor";
            SetLayer(shot.actor, _layer);
        }

        shot.texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = name + "_RT" };
        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = offset + lookAt + new Vector3(0f, 0.25f, -6f);
        camGo.transform.LookAt(transform.TransformPoint(offset + lookAt));
        shot.camera = camGo.AddComponent<Camera>();
        shot.camera.orthographic = true;
        shot.camera.orthographicSize = orthoSize;
        shot.camera.cullingMask = 1 << _layer;
        shot.camera.clearFlags = CameraClearFlags.SolidColor;
        shot.camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        shot.camera.nearClipPlane = 0.1f;
        shot.camera.farClipPlane = 20f;
        shot.camera.targetTexture = shot.texture;
        shot.camera.allowHDR = false;
        var data = camGo.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        return shot;
    }

    // Solo el pingüino (sin cámara propia), para tomas con varios actores.
    public Shot AddActor(GameObject penguinPrefab, Vector3 offset, float yaw = 180f)
    {
        var shot = new Shot();
        shot.pivot = new GameObject("Pivot").transform;
        shot.pivot.SetParent(transform, false);
        shot.pivot.localPosition = offset;
        shot.pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        shot.actor = Instantiate(penguinPrefab, shot.pivot, false);
        shot.actor.name = "Actor";
        SetLayer(shot.actor, _layer);
        return shot;
    }

    // Viste al actor con el equipo de la clase y le pone el color de plumaje.
    public void Dress(Shot shot, CharacterClass cls, Color plumage)
    {
        if (shot == null || shot.actor == null) return;
        var outfit = shot.actor.GetComponent<PenguinOutfit>();
        if (outfit != null)
        {
            outfit.useClassOutfit = false;
            outfit.UnequipAll();
            if (cls != null && cls.startingOutfit != null)
                foreach (OutfitItem item in cls.startingOutfit) outfit.Equip(item);
        }
        SetLayer(shot.actor, _layer);
        var look = shot.actor.GetComponent<PenguinAppearance>();
        if (look != null) look.SetPlumage(plumage);
    }

    public void Tint(Shot shot, Color plumage)
    {
        var look = shot?.actor != null ? shot.actor.GetComponent<PenguinAppearance>() : null;
        if (look != null) look.SetPlumage(plumage);
    }

    public static void SetLayer(GameObject go, int layer)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    void OnDestroy()
    {
        foreach (Camera c in GetComponentsInChildren<Camera>(true))
            if (c.targetTexture != null) { c.targetTexture.Release(); c.targetTexture = null; }
    }
}
