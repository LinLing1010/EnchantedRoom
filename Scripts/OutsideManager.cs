using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages all outside scenes: generation and activation.
/// Call Init() after RebuildRoom completes, ActivateRandom() when a door opens.
/// </summary>
public class OutsideManager : MonoBehaviour
{
    [Header("Outside Scenes")]
    public List<OutsideScene> scenes;

    [Header("Default Outside (always visible, no door needed)")]
    public OutsideScene defaultScene;

    [Header("Direct Light")]
    public LightController lightController;

    [Header("Debug")]
    public bool debugMode = false;

    private float _baseRotY;
    private Vector3 _basePosition;
    private OutsideScene _activeScene;
    private int _lastActiveIndex = -1;
    
    
    public OutsideSceneConfig _activeSceneConfig => _activeScene?.config;
    
    
    // -------------------------------------------------------
    // Hide everything immediately on Awake, before any rendering
    void Awake()
    {
        foreach (var scene in scenes)
            if (scene != null) scene.gameObject.SetActive(false);
     
        if (defaultScene != null) defaultScene.gameObject.SetActive(false);
    }

    // -------------------------------------------------------
    // Called by RebuildRoom once the room is built
    // -------------------------------------------------------
    public void Init(Vector3 floorPosition, float biggestWindowRotY)
    {
        _basePosition = floorPosition;
        _baseRotY     = biggestWindowRotY;

        foreach (var scene in scenes)
        {
            if (scene == null) continue;
            scene.transform.position = _basePosition;
            scene.transform.rotation = Quaternion.Euler(0f, _baseRotY, 0f);
            scene.Generate();
        }

        // Default outside: align rotation only, then activate
        if (defaultScene != null)
        {
            defaultScene.transform.rotation = Quaternion.Euler(0f, _baseRotY, 0f);
            defaultScene.Generate();
            defaultScene.Open();
        }

        if (debugMode && scenes.Count > 0)
        {
            _activeScene = scenes[0];
            _activeScene.Open();
        }

        Debug.Log($"[OutsideManager] Initialized with {scenes.Count} scenes");
    }

    // -------------------------------------------------------
    // Called when a door opens
    // -------------------------------------------------------
    public void ActivateRandom()
    {
        if (scenes == null || scenes.Count == 0) return;

        _activeScene?.Close();
        defaultScene?.Close();

        int index;
        if (scenes.Count == 1)
        {
            index = 0;
        }
        else
        {
            do { index = Random.Range(0, scenes.Count); }
            while (index == _lastActiveIndex);
        }
    
        _lastActiveIndex = index;
        _activeScene = scenes[index];

        _activeScene.transform.rotation = Quaternion.Euler(0f, _baseRotY, 0f);

        LightSettings ls = _activeScene.Open();

        if (lightController != null && ls != null)
        {
            Quaternion worldLightRot = _activeScene.transform.rotation * Quaternion.Euler(ls.localEuler);
            lightController.LerpTo(ls, worldLightRot);
        }

        Debug.Log($"[OutsideManager] Activated scene: {_activeScene.config?.sceneId}");
    }

    // -------------------------------------------------------
    // Called when a door closes
    // -------------------------------------------------------
    public void Deactivate()
    {
        _activeScene?.Close();
        _activeScene = null;
        if (defaultScene != null)
        {
            defaultScene.Open();
        
            // 切回 default light
            if (lightController != null && defaultScene.config?.lightSettings != null)
            {
                LightSettings ls = defaultScene.config.lightSettings;
                Quaternion worldLightRot = defaultScene.transform.rotation * Quaternion.Euler(ls.localEuler);
                lightController.LerpTo(ls, worldLightRot);
            }
        }
    }
}