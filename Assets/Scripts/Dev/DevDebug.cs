using UnityEngine;

public static class DevDebug
{
    // When true, normal dev logs remain enabled. We keep this true by default.
    public static bool EnableVerbose = true;

    // By default, silence Unity's logger on startup. Use the methods below to emit
    // health logs which temporarily enable the logger for that single call.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Debug.unityLogger.logEnabled = false;
    }

    public static void Log(string msg)
    {
        if (EnableVerbose) Debug.Log(msg);
    }

    public static void LogWarning(string msg)
    {
        if (EnableVerbose) Debug.LogWarning(msg);
    }

    public static void LogError(string msg)
    {
        if (EnableVerbose) Debug.LogError(msg);
    }

    // Always print enemy health lines regardless of EnableVerbose
    public static void LogEnemyHealth(string msg)
    {
        bool previous = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = true;
        Debug.Log("[EnemyHealth] " + msg);
        Debug.unityLogger.logEnabled = previous;
    }

    public static void LogPlayerHealth(string msg)
    {
        bool previous = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = true;
        Debug.Log("[PlayerHealth] " + msg);
        Debug.unityLogger.logEnabled = previous;
    }
}
