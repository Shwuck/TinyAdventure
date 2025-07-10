using UnityEngine;

public class GameDebugger : MonoBehaviour
{
    private static GameDebugger _instance;
    public static GameDebugger Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find an existing instance
                _instance = FindObjectOfType<GameDebugger>();

                // If no instance found, create a new one
                if (_instance == null)
                {
                    GameObject debuggerObject = new GameObject("GameDebugger");
                    _instance = debuggerObject.AddComponent<GameDebugger>();
                    DontDestroyOnLoad(debuggerObject);
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        // Ensure that there is only one instance
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void LogError(string message)
    {
        Debug.LogError(message);
    }

    public void LogWarning(string message)
    {
        Debug.LogWarning(message);
    }

    public void LogInfo(string message)
    {
        Debug.Log(message);
    }
}
