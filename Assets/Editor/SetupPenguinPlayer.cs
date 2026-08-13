using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupPenguinPlayer
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Penguin/Setup Penguin as Player")]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject penguinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Penguin.obj");
        if (penguinPrefab == null)
        {
            EditorUtility.DisplayDialog("Penguin", "No se encontró Assets/Penguin.obj en el proyecto.", "OK");
            return;
        }

        GameObject existingPlayer = GameObject.Find("Player");
        if (existingPlayer != null) Object.DestroyImmediate(existingPlayer);

        GameObject player = new GameObject("Player");
        player.AddComponent<Rigidbody>();
        CapsuleCollider hitbox = player.AddComponent<CapsuleCollider>();
        player.AddComponent<PlayerController>();

        // Sistema de clases: añade stats y asigna la clase Caballero por defecto.
        CharacterStats stats = player.AddComponent<CharacterStats>();
        CharacterClass knight = AssetDatabase.LoadAssetAtPath<CharacterClass>("Assets/Data/Classes/Knight_Caballero.asset");
        if (knight != null)
        {
            stats.characterClass = knight;
            stats.InitFromClass(knight);
        }

        GameObject penguin = (GameObject)Object.Instantiate(penguinPrefab, player.transform);
        penguin.name = "Penguin";
        ApplyClassColor(penguin, stats);

        PenguinAnimator animator = player.AddComponent<PenguinAnimator>();
        animator.model = penguin.transform;

        Bounds? bounds = GetBounds(penguin);
        if (bounds.HasValue)
        {
            Bounds b = bounds.Value;
            penguin.transform.localPosition = new Vector3(0f, -b.min.y, 0f);
            hitbox.height = b.size.y;
            hitbox.radius = Mathf.Max(b.size.x, b.size.z) * 0.25f;
            hitbox.center = new Vector3(0f, b.size.y * 0.5f, 0f);
        }

        CreateGround();

        GameObject cameraGO = GameObject.Find("Main Camera");
        if (cameraGO != null)
        {
            CameraFollow follow = cameraGO.GetComponent<CameraFollow>();
            if (follow == null) follow = cameraGO.AddComponent<CameraFollow>();
            follow.target = player.transform;
            follow.offset = new Vector3(0f, 9f, -8f);
            cameraGO.transform.position = player.transform.position + follow.offset;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog("Penguin",
            "¡Listo! Revertido al pingüino .obj descargado con animación procedural.\n" +
            "Pulsa Play (▶) para probarlo.", "OK");
    }

    private static Bounds? GetBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return null;
        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);
        return combined;
    }

    // Tiñe el material del pingüino con el color de su clase (instancia propia, sin tocar el asset).
    private static void ApplyClassColor(GameObject penguin, CharacterStats stats)
    {
        if (stats == null || stats.characterClass == null) return;
        Renderer[] renderers = penguin.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            Material mat = new Material(r.sharedMaterial);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", stats.characterClass.classColor);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", stats.characterClass.classColor);
            r.sharedMaterial = mat;
        }
    }

    private static void CreateGround()
    {
        GameObject existing = GameObject.Find("Ground");
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.rotation = Quaternion.identity;
        ground.transform.localScale = new Vector3(20f, 1f, 20f);
        ground.isStatic = true;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null)
        {
            Material mat = new Material(shader);
            Texture2D snowTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/snow_seamless.png");
            if (snowTex == null)
                snowTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/snow_forts.jpg");

            if (snowTex != null)
            {
                mat.SetTexture("_BaseMap", snowTex);
                mat.SetTexture("_MainTex", snowTex);
            }
            else
            {
                mat.color = new Color(0.88f, 0.94f, 0.98f); // Icy Club Penguin snow blue-white
            }
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateAsset(mat, "Assets/Materials/Ground.mat");
            ground.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}