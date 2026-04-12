using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class UITrigger : MonoBehaviour
{
    public GameObject popUpCanvas; // Assign the Canvas in the Inspector
    public KeyCode interactionKey = KeyCode.F; // Configurable hotkey in the Inspector
    public string sceneIdentifier = "0"; // Single variable for scene loading (index or name)

    private bool isUIVisible = false; // Tracks if the UI is currently visible

    public UnityEvent OnTrigger;

    void Start ()
    {
        if (popUpCanvas != null)
        {
            popUpCanvas.SetActive(false); // Ensure the canvas is hidden initially
        }
        else
        {
            Debug.LogWarning("Canvas is not assigned in the Inspector on " + gameObject.name);
        }

        
    }

    void Update ()
    {
        // Check if the UI is visible and the player presses the interaction key
        if (isUIVisible && Input.GetKeyDown(interactionKey))
        {
            Enter();
        }
    }

    void Enter()
    {
        OnTrigger?.Invoke();
        LoadScene();
    }

    // Public wrapper so external UI buttons can invoke the same enter behavior.
    public void TriggerFromButton()
    {
        if (!isUIVisible)
        {
            return;
        }

        Enter();
    }

    // Method to show the associated UI element
    public void ShowUI ()
    {
        if (popUpCanvas != null)
        {
            popUpCanvas.SetActive(true);
            isUIVisible = true;
            Debug.Log("Showing UI for " + gameObject.name);
        }
    }

    // Method to hide the associated UI element
    public void HideUI ()
    {
        if (popUpCanvas != null)
        {
            popUpCanvas.SetActive(false);
            isUIVisible = false;
            Debug.Log("Hiding UI for " + gameObject.name);
        }
    }

    // Method to load the specified scene by index or name
    private void LoadScene ()
    {
        int sceneIndex;
        if (int.TryParse(sceneIdentifier,out sceneIndex)) // If it's a number, load by index
        {
            if (sceneIndex >= 0 && sceneIndex < SceneManager.sceneCountInBuildSettings)
            {
                Debug.Log("Loading scene by index: " + sceneIndex);
                Moddwyn.SceneLoader.Instance.LoadScene(sceneIndex);
            }
            else
            {
                Debug.LogError("Scene index " + sceneIndex + " is out of range. Check your Build Settings.");
            }
        }
        else // Otherwise, treat it as a scene name
        {
            if (SceneExists(sceneIdentifier))
            {
                Debug.Log("Loading scene by name: " + sceneIdentifier);
                Moddwyn.SceneLoader.Instance.LoadScene(sceneIdentifier);
            }
            else
            {
                Debug.LogError("Scene name '" + sceneIdentifier + "' does not exist. Check your Build Settings.");
            }
        }
    }

    // Helper method to check if a scene exists in build settings
    private bool SceneExists (string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string extractedSceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (extractedSceneName == sceneName)
                return true;
        }
        return false;
    }
}
