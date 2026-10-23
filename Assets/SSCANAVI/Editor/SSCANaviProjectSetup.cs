using UnityEditor;
using UnityEngine;

// Runs every time Unity opens or recompiles the project:
// 1. Creates Resources/SSCA_Unlit.mat, so the URP Unlit shader is not stripped
//    from the phone build (otherwise the 3D map and cyan arrows are invisible).
// 2. Keeps both app scenes ticked in the build, AR camera first.
[InitializeOnLoad]
public static class SSCANaviProjectSetup
{
    public const string ResourcesFolder = "Assets/SSCANAVI/Resources";
    public const string MaterialPath = ResourcesFolder + "/SSCA_Unlit.mat";

    static SSCANaviProjectSetup()
    {
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureUnlitMaterial();
        SSCANaviSceneBuilder.EnsureBuildScenes(false);
    }

    public static void EnsureUnlitMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("SSCA NAVI: URP Unlit shader not found; cannot create " + MaterialPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets/SSCANAVI", "Resources");

        AssetDatabase.CreateAsset(new Material(shader), MaterialPath);
        AssetDatabase.SaveAssets();
        Debug.Log("SSCA NAVI: Created " + MaterialPath + " so the 3D map and arrows render on the phone.");
    }
}
