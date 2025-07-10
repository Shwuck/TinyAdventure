using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataLoaderManager : MonoBehaviour
{
    public List<MonoBehaviour> dataLoaders; // Assign all DataLoader scripts in the Inspector

    private List<IDataLoader> loaders = new List<IDataLoader>();

    void Awake()
    {
        foreach (var loader in dataLoaders)
        {
            if (loader is IDataLoader)
            {
                loaders.Add(loader as IDataLoader);
            }
            else
            {
                Debug.LogError($"{loader.GetType().Name} does not implement IDataLoader interface");
            }
        }
    }

    public IEnumerator LoadAllData()
    {
        foreach (var loader in loaders)
        {
            yield return RunLoader(loader);
        }

        // Call IntegrityChecker after all data is loaded
        IntegrityChecker.Instance.CheckDataIntegrity();
    }

    private IEnumerator RunLoader(IDataLoader loader)
    {
        try
        {
            loader.LoadData();
            Debug.Log($"{loader.GetType().Name} data loaded successfully.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"{loader.GetType().Name} failed to load data: {ex.Message}");
        }

        yield return null; // Optionally, wait a frame between loaders
    }
}

public interface IDataLoader
{
    void LoadData();
}
