using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

public static class SSCANaviSceneBuilder
{
    private const string Directory3D = "Assets/SSCANAVI/Scenes";
    private const string ARScenePath = Directory3D + "/SSCA_AR_Navigation.unity";
    private const string MapScenePath = Directory3D + "/SSCA_NAVI_3D_Demo.unity";

    [MenuItem("Tools/SSCA NAVI/Create 3D Demo Scene")]
    public static void CreateScene()
    {
        if(!Directory.Exists(Directory3D))Directory.CreateDirectory(Directory3D);
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GameObject root=new GameObject("SSCA NAVI - Demo Controller");
        root.AddComponent<SSCANaviDemo>();
        GameObject camera=new GameObject("Main Camera");camera.tag="MainCamera";
        Camera cam=camera.AddComponent<Camera>();cam.fieldOfView=55;
        GameObject sun=new GameObject("Directional Light");
        Light light=sun.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.7f;
        sun.transform.rotation=Quaternion.Euler(45,-35,0);
        EditorSceneManager.SaveScene(scene,MapScenePath);
        SetUpBuildScenes();
        AssetDatabase.Refresh();Selection.activeObject=root;
        EditorUtility.DisplayDialog("SSCA NAVI Ready","Scene created and saved! Press the PLAY button (top center). Choose From and To at the top.","OK");
    }

    // The phone app opens on the AR camera; "3D Map" switches to the dollhouse map and back.
    [MenuItem("Tools/SSCA NAVI/Set Up Build Scenes")]
    public static void SetUpBuildScenes()
    {
        SSCANaviProjectSetup.EnsureUnlitMaterial();
        EnsureBuildScenes(true);
    }

    // AR scene ticked at position 0, 3D map ticked; other scenes are kept as they are.
    public static void EnsureBuildScenes(bool showDialogs)
    {
        if(!File.Exists(ARScenePath)||!File.Exists(MapScenePath))
        {
            if(showDialogs)EditorUtility.DisplayDialog("SSCA NAVI","Missing scene:\n"+ARScenePath+"\nor\n"+MapScenePath,"OK");
            return;
        }

        List<EditorBuildSettingsScene> scenes=new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(ARScenePath,true),
            new EditorBuildSettingsScene(MapScenePath,true)
        };
        foreach(EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if(existing.path!=ARScenePath&&existing.path!=MapScenePath)scenes.Add(existing);

        EditorBuildSettingsScene[] current=EditorBuildSettings.scenes;
        bool changed=current.Length!=scenes.Count;
        for(int i=0;!changed&&i<scenes.Count;i++)
            changed=current[i].path!=scenes[i].path||current[i].enabled!=scenes[i].enabled;

        if(changed)
        {
            EditorBuildSettings.scenes=scenes.ToArray();
            Debug.Log("SSCA NAVI: Build scenes set: 0 = SSCA_AR_Navigation, 1 = SSCA_NAVI_3D_Demo (both ticked)");
        }
        else if(showDialogs)
        {
            Debug.Log("SSCA NAVI: Build scenes already correct.");
        }
    }
}
