using UnityEngine;

// SSCA NAVI: the flat cyan guide arrow shared by the AR view and the 3D map.
public static class SSCAArrowMesh
{
    public static readonly Color BrightCyan = new Color(0f, 1f, 1f);
    public static readonly Color DimCyan = new Color(0f, 0.55f, 0.65f);

    // Unity strips shaders that no asset uses, so Shader.Find alone fails in a
    // phone build. Resources/SSCA_Unlit.mat (created by SSCANaviProjectSetup)
    // keeps the URP Unlit shader in the build; the others are fallbacks.
    private static readonly string[] FallbackShaders =
    {
        "Universal Render Pipeline/Unlit",
        "Unlit/Color",
        "Sprites/Default",   // always included by Unity
        "UI/Default"         // always included by Unity
    };

    public static Material CreateMaterial(Color color)
    {
        Material material = null;
        Material template = Resources.Load<Material>("SSCA_Unlit");
        if (template != null)
        {
            material = new Material(template);
        }
        else
        {
            foreach (string shaderName in FallbackShaders)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    material = new Material(shader);
                    break;
                }
            }
        }

        if (material == null)
        {
            Debug.LogError("SSCA NAVI: Unable to find an unlit shader.");
            return null;
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        return material;
    }

    // Arrow pointing along +Z, lying flat on the floor.
    public static Mesh Create(float scale = 1f)
    {
        Mesh mesh = new Mesh();
        mesh.name = "SSCA Arrow";
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.12f, 0, -0.30f) * scale,
            new Vector3( 0.12f, 0, -0.30f) * scale,
            new Vector3( 0.12f, 0,  0.05f) * scale,
            new Vector3( 0.30f, 0,  0.05f) * scale,
            new Vector3( 0.00f, 0,  0.48f) * scale,
            new Vector3(-0.30f, 0,  0.05f) * scale,
            new Vector3(-0.12f, 0,  0.05f) * scale
        };
        // Two-sided triangles so the flat mesh is visible from both sides.
        int[] top = {0, 2, 1, 0, 6, 2, 6, 3, 2, 6, 5, 3, 5, 4, 3};
        int[] triangles = new int[top.Length * 2];
        for (int i = 0; i < top.Length; i += 3)
        {
            triangles[i] = top[i];
            triangles[i + 1] = top[i + 1];
            triangles[i + 2] = top[i + 2];
            int b = top.Length + i;
            triangles[b] = top[i + 2];
            triangles[b + 1] = top[i + 1];
            triangles[b + 2] = top[i];
        }
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    // Light pulse that travels along the route in the walking direction.
    public static Color Pulse(float distanceAlongRoute)
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(distanceAlongRoute * 1.5f - Time.time * 4f);
        return Color.Lerp(DimCyan, BrightCyan, wave);
    }
}
