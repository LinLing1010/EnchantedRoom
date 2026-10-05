using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class VertexColorDeformer : MonoBehaviour
{
    [Header("Default Size")]
    public float defaultWidth = 1f;
    public float defaultHeight = 2f;

    [Header("Target Size (manual input before MRUK data）")]
    public float targetWidth = 0.9f;
    public float targetHeight = 2.1f;

    private Mesh _mesh;
    private Vector3[] _originalVertices;
    private Vector3[] _deformedVertices;
    private Color[] _colors;

    void Start()
    {
        // 触发独立拷贝，必须在缓存前
        _mesh = GetComponent<MeshFilter>().mesh;
        _originalVertices = _mesh.vertices;
        _deformedVertices = new Vector3[_originalVertices.Length];
        _colors = _mesh.colors;

        if (_colors.Length == 0)
            Debug.LogError("没有顶点色！请检查模型导入设置。");

        Apply();
    }

    // 方便在 Inspector 里改值后立刻看效果
    void OnValidate()
    {
        if (_mesh == null) return;
        Apply();
    }

    void Apply()
    {
        float deltaWidth  = targetWidth  - defaultWidth;
        float deltaHeight = targetHeight - defaultHeight;

        for (int i = 0; i < _originalVertices.Length; i++)
        {
            //if (_colors[i].g > 0.01f)
                //Debug.Log($"顶点{i}: R={_colors[i].r:F3} G={_colors[i].g:F3} B={_colors[i].b:F3}");
            Vector3 pos = _originalVertices[i];
            float r = _colors.Length > i ? _colors[i].r : 0f;
            float g = _colors.Length > i ? _colors[i].g : 0f;

            // R接近1 → 沿 X 轴移动（宽度）
            if (_colors[i].r > 0.99f)
                pos.x += deltaWidth;

            // G接近1 → 沿 Y 轴移动（高度）
            if (_colors[i].g >= 1f)
                pos.y += deltaHeight;

            _deformedVertices[i] = pos;
        }

        _mesh.vertices = _deformedVertices;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }
}