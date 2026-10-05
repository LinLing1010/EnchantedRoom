using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class WeightedPrefab
{
    public GameObject prefab;
    public float weight = 1f;
}

[System.Serializable]
public class LightSettings
{
    public Color   color      = Color.white;
    public float   intensity  = 1f;
    public Vector3 localEuler = new Vector3(50f, 0f, 0f);
}

[CreateAssetMenu(menuName = "Outside/SceneConfig", fileName = "OutsideSceneConfig")]
public class OutsideSceneConfig : ScriptableObject
{
    [Header("Identity")]
    public string sceneId;

    [Header("PCG Point Data")]
    public TextAsset pointDataJson;
    public int       randomSeed = 0;

    [Header("Weighted Prefabs")]
    public List<WeightedPrefab> weightedPrefabs;

    [Header("Singleton Prefabs")]
    public List<GameObject> singletonPrefabs;

    [Header("Light Settings")]
    public LightSettings lightSettings;

    [Header("Wall Cover")]
    public Texture2D secondBaseColorTexture;     // 门开时切换的贴图，null = 跳过
    [Tooltip("Lerp 从 0→1 的持续时间（秒）")]
    public float wallLerpInDuration  = 1.0f;
    [Tooltip("Lerp 从 1→0 的持续时间（秒）")]
    public float wallLerpOutDuration = 6.0f;

    [Header("Door FX")]
    public GameObject doorFxPrefab;   // 门洞地面特效，null = 跳过
    public AudioClip  doorOpenSound;  // 开门音效，null = 跳过
    public AudioClip  doorCloseSound; // 关门音效，null = 跳过

    [Header("Timing")]
    public float stayOpenExtraTime = 6f; // 关门后等多久再 lerp 回去
}
