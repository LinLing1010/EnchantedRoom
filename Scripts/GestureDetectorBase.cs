using UnityEngine;

public abstract class GestureDetectorBase : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] protected float detectionInterval = 0.05f;

    private float _timer;

    // ---- 全局锁 ----
    private static GestureDetectorBase _activeDetector;

    /// <summary>当前是否有其他手势占用</summary>
    protected bool IsBlocked => _activeDetector != null && _activeDetector != this;

    /// <summary>子类在手势开始时调用</summary>
    protected bool TryAcquire()
    {
        if (_activeDetector != null && _activeDetector != this)
            return false;
        _activeDetector = this;
        return true;
    }

    /// <summary>子类在手势结束时调用</summary>
    protected void Release()
    {
        if (_activeDetector == this)
        {
            Debug.Log($"[GestureLock] Released by {GetType().Name}");
            _activeDetector = null;
        }
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= detectionInterval)
        {
            _timer = 0f;
            OnDetectionTick();
        }
    }

    protected abstract void OnDetectionTick();

    void OnDisable() => Release(); // 防止禁用后锁死
}