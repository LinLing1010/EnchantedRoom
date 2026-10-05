using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

[ExecuteInEditMode]
public class VFXPlayer : MonoBehaviour
{
    [Header("Data")]
    public TextAsset jsonFile;

    [Header("Prefabs")]
    public GameObject[] prefabs;

    [Header("Player Settings")]
    public float fps = 24f;
    public bool loop = true;
    public bool auto = true;

    [Header("Preview")]
    [Range(1, 160)]
    public int frame = 1;

    // 内部
    private Dictionary<int, List<VFXPoint>> allFrames;
    private Dictionary<int, GameObject> activeInstances = new Dictionary<int, GameObject>();
    private Dictionary<int, Queue<GameObject>> pool = new Dictionary<int, Queue<GameObject>>();
    private float timer;
    private bool isPlaying;
    private int lastFrame = -1;
    private int totalFrames;
    private bool dataLoaded;
    private int lastPreviewFrame = -1;

    private class VFXPoint
    {
        public int id;
        public int type;
        public Vector3 pos;
        public Quaternion rot;
        public float scl;
    }

    void Awake()
    {
        if (Application.isPlaying)
        {
            LoadData();
            if (auto) Play();
        }
    }

    void LoadData()
    {
        if (jsonFile == null) return;
        if (dataLoaded) return;

        JObject root = JObject.Parse(jsonFile.text);
        allFrames = new Dictionary<int, List<VFXPoint>>();
        totalFrames = 0;

        foreach (var kvp in root)
        {
            int frameNum = int.Parse(kvp.Key);
            if (frameNum > totalFrames) totalFrames = frameNum;

            List<VFXPoint> points = new List<VFXPoint>();
            foreach (JObject pt in kvp.Value)
            {
                JArray pos = (JArray)pt["pos"];
                JArray rot = (JArray)pt["rot"];

                points.Add(new VFXPoint
                {
                    id = (int)pt["id"],
                    type = (int)pt["type"],
                    pos = new Vector3((float)pos[0], (float)pos[1], (float)pos[2]),
                    rot = new Quaternion((float)rot[0], (float)rot[1], (float)rot[2], (float)rot[3]),
                    scl = (float)pt["scl"]
                });
            }
            allFrames[frameNum] = points;
        }

        dataLoaded = true;
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            if (!isPlaying) return;

            timer += Time.deltaTime;
            int f = Mathf.FloorToInt(timer * fps) + 1;

            if (f > totalFrames)
            {
                if (loop) { timer = 0; f = 1; }
                else { Stop(); return; }
            }

            if (f == lastFrame) return;
            lastFrame = f;
            frame = f;
            UpdateFrame(f);
        }
        else
        {
            // Editor 预览: 拖动 frame 滑条时刷新
            if (frame != lastPreviewFrame)
            {
                lastPreviewFrame = frame;
                ShowPreview(frame);
            }
        }
    }

    void ShowPreview(int f)
    {
        LoadData();
        if (!dataLoaded) return;

        // 清除旧预览
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        f = Mathf.Clamp(f, 1, totalFrames);
        if (!allFrames.ContainsKey(f)) return;

        foreach (var p in allFrames[f])
        {
            int type = Mathf.Clamp(p.type, 0, prefabs.Length - 1);
            if (prefabs[type] == null) continue;

#if UNITY_EDITOR
            GameObject go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefabs[type], transform);
#else
            GameObject go = Instantiate(prefabs[type], transform);
#endif
            go.transform.localPosition = p.pos;
            go.transform.localRotation = p.rot;
            go.transform.localScale = Vector3.one * p.scl;
            go.hideFlags = HideFlags.DontSave;
        }
    }

    void UpdateFrame(int f)
    {
        if (!allFrames.ContainsKey(f)) return;

        var points = allFrames[f];
        HashSet<int> currentIds = new HashSet<int>();

        foreach (var p in points)
        {
            currentIds.Add(p.id);

            if (!activeInstances.TryGetValue(p.id, out GameObject go))
            {
                go = GetFromPool(p.type);
                go.transform.SetParent(transform);
                activeInstances[p.id] = go;
            }

            go.transform.localPosition = p.pos;
            go.transform.localRotation = p.rot;
            go.transform.localScale = Vector3.one * p.scl;
        }

        List<int> toRemove = new List<int>();
        foreach (var kvp in activeInstances)
        {
            if (!currentIds.Contains(kvp.Key))
            {
                ReturnToPool(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (int id in toRemove)
            activeInstances.Remove(id);
    }

    GameObject GetFromPool(int type)
    {
        type = Mathf.Clamp(type, 0, prefabs.Length - 1);
        if (pool.ContainsKey(type) && pool[type].Count > 0)
        {
            var go = pool[type].Dequeue();
            go.SetActive(true);
            return go;
        }
        return Instantiate(prefabs[type]);
    }

    void ReturnToPool(GameObject go)
    {
        go.SetActive(false);
        if (!pool.ContainsKey(0)) pool[0] = new Queue<GameObject>();
        pool[0].Enqueue(go);
    }

    void RecycleAll()
    {
        foreach (var kvp in activeInstances)
            ReturnToPool(kvp.Value);
        activeInstances.Clear();
    }

    public void Trigger() { Play(); }

    public void Play()
    {
        timer = 0;
        lastFrame = -1;
        isPlaying = true;
    }

    public void Stop()
    {
        isPlaying = false;
        RecycleAll();
    }

    public void Pause() { isPlaying = false; }

    public void Resume() { isPlaying = true; }

    void OnDisable()
    {
        if (Application.isPlaying) RecycleAll();
    }
}