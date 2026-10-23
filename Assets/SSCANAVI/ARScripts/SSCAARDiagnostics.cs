using UnityEngine;
using UnityEngine.XR.ARFoundation;

// SSCA NAVI: tells the student what AR is doing until tracking starts,
// placed just above the SSCAARGuide instruction box.
// Tick "Show Debug Info" to see the raw AR Session / camera state.
public class SSCAARDiagnostics : MonoBehaviour
{
    [Tooltip("Show the raw AR Session and AR Camera Manager state (for troubleshooting).")]
    public bool showDebugInfo = false;

    private GUIStyle hintStyle;
    private ARCameraManager cameraManager;

    private void Start()
    {
        cameraManager = FindFirstObjectByType<ARCameraManager>();

        Debug.Log("SSCA AR Diagnostics started.");
    }

    private void OnGUI()
    {
        string hint = GetHint();
        if (hint == null && !showDebugInfo)
            return;

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = 17;
            hintStyle.wordWrap = true;
            hintStyle.alignment = TextAnchor.MiddleCenter;
            hintStyle.normal.textColor = Color.yellow;
        }

        // Same 420-wide layout as SSCAARGuide.
        float scale = Mathf.Max(0.6f, Screen.width / 420f);
        float height = Screen.height / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        if (showDebugInfo)
        {
            string cameraStatus = cameraManager != null
                ? (cameraManager.enabled ? "Enabled" : "Disabled")
                : "NOT FOUND";
            hint = (hint != null ? hint + "\n" : "") +
                   "AR Session: " + ARSession.state + " | Camera: " + cameraStatus;
        }

        GUI.Box(new Rect(8, height - 172, 405, 62), "");
        GUI.Label(new Rect(14, height - 170, 393, 58), hint, hintStyle);

        GUI.matrix = Matrix4x4.identity;
    }

    private static string GetHint()
    {
        switch (ARSession.state)
        {
            case ARSessionState.SessionTracking:
                return null;
            case ARSessionState.Unsupported:
                return "This phone does not support ARCore.";
            case ARSessionState.NeedsInstall:
            case ARSessionState.Installing:
                return "Installing Google Play Services for AR...";
            case ARSessionState.SessionInitializing:
                return "Move your phone slowly and point it at the floor...";
            default:
                return "Starting the AR camera...";
        }
    }
}
