using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attached to each outside scene root GameObject.
/// Generates scene content from config, and handles open/close.
/// </summary>
public class OutsideScene : MonoBehaviour
{
    public OutsideSceneConfig config;

    private bool _generated = false;

    // -------------------------------------------------------
    // JSON structure matching pts_flower.json format
    // -------------------------------------------------------
    [System.Serializable]
    public class Vec3Data { public float x, y, z; }

    [System.Serializable]
    public class Vec4Data { public float x, y, z, w = 1f; }

    [System.Serializable]
    public class PointData
    {
        public Vec3Data position;
        public Vec4Data rotation;
        public Vec3Data scale;
    }

    [System.Serializable]
    public class PointList
    {
        public int count;
        public List<PointData> points;
    }

    // -------------------------------------------------------
    // Called by OutsideManager after RebuildRoom completes
    // -------------------------------------------------------
    public void Generate()
    {
        if (_generated) return;
        _generated = true;

        if (config == null)
        {
            Debug.LogError($"[OutsideScene] {name} has no config assigned!");
            return;
        }

        // Random seed
        Random.State prevState = Random.state;
        if (config.randomSeed != 0)
            Random.InitState(config.randomSeed);

        // Place singleton prefabs (sky sphere, terrain, etc.)
        foreach (var prefab in config.singletonPrefabs)
        {
            if (prefab != null)
                Instantiate(prefab, transform);
        }

        // Place weighted prefabs from point data
        if (config.pointDataJson != null && config.weightedPrefabs != null && config.weightedPrefabs.Count > 0)
        {
            var pointList = JsonUtility.FromJson<PointList>(config.pointDataJson.text);
            if (pointList?.points != null)
            {
                foreach (var pt in pointList.points)
                {
                    GameObject prefab = PickPrefab(config.weightedPrefabs);
                    if (prefab == null) continue;

                    // Instantiate under this scene's root, using local position
                    GameObject instance = Instantiate(prefab, transform);
                    instance.transform.localPosition = new Vector3(pt.position.x, pt.position.y, -pt.position.z);
                    instance.transform.localRotation = new Quaternion(-pt.rotation.x, -pt.rotation.y, pt.rotation.z, pt.rotation.w);
                    instance.transform.localScale = new Vector3(pt.scale.x, pt.scale.y, pt.scale.z);
                }
            }
            else
            {
                Debug.LogWarning($"[OutsideScene] {config.sceneId} point data is empty or malformed");
            }
        }

        // Restore random state
        if (config.randomSeed != 0)
            Random.state = prevState;

        // Hide after generation
        gameObject.SetActive(false);

        Debug.Log($"[OutsideScene] {config.sceneId} generated");
    }

    // -------------------------------------------------------
    // Called by OutsideManager when a door opens
    // -------------------------------------------------------
    public LightSettings Open()
    {
        gameObject.SetActive(true);
        if (config == null) return null;
        return config.lightSettings;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // -------------------------------------------------------
    // Weighted random prefab selection
    // -------------------------------------------------------
    private GameObject PickPrefab(List<WeightedPrefab> pool)
    {
        float total = 0f;
        foreach (var p in pool) total += p.weight;

        float roll   = Random.Range(0f, total);
        float cursor = 0f;
        foreach (var p in pool)
        {
            cursor += p.weight;
            if (roll <= cursor) return p.prefab;
        }
        return pool[^1].prefab;
    }
}