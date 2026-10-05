using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Meta.XR.MRUtilityKit;

public class IndexTapDetector : GestureDetectorBase
{
    [Header("Hand")]
    [SerializeField] private HandVisual handVisual;

    [Header("Detection")]
    [SerializeField] private float tapDistanceThreshold = 0.03f;
    [SerializeField] private float fingerDownDotThreshold = 0.6f;
    [SerializeField] private float cooldown = 0.8f;

    [Header("Marker")]
    [SerializeField] private GameObject markerPrefab;

    private float _cooldownTimer = 0f;
    private GameObject _markerInstance;

    protected override void OnDetectionTick()
    {
        _cooldownTimer -= detectionInterval;

        if (!GestureStateManager.Instance.Is(AppGestureState.ShowingOnPalm))
        {
           // Debug.Log($"[IndexTap] State={GestureStateManager.Instance.CurrentState}, skip");
            return;
        }
        
        if (_cooldownTimer > 0f) return;
        if (handVisual == null) {  return; }

        Vector3 indexTip = GetIndexTipPosition();
        Vector3 indexDir = GetIndexDirection();

        float downDot = Vector3.Dot(indexDir, Vector3.down);
        //Debug.Log($"[IndexTap] downDot={downDot:F2} tip={indexTip}");
        if (downDot < fingerDownDotThreshold) return;

        var room = MRUK.Instance?.GetCurrentRoom();
        if (room == null) { return; }

        Ray ray = new Ray(indexTip, Vector3.down);
        bool isHit = room.Raycast(ray, tapDistanceThreshold * 2f,
            new LabelFilter(MRUKAnchor.SceneLabels.TABLE), out RaycastHit rayHit);

        //Debug.Log($"[IndexTap] isHit={isHit} distance={Vector3.Distance(indexTip, rayHit.point):F3}");

        if (!isHit) return;

        float distance = Vector3.Distance(indexTip, rayHit.point);
        if (distance > tapDistanceThreshold) return;

        _cooldownTimer = cooldown;
        ShowMarker(rayHit.point);
        GestureEvents.Fire_RightIndexTap(rayHit.point);
        //Debug.Log($"[IndexTap] FIRED at {rayHit.point}");
    }
    private void ShowMarker(Vector3 worldPos)
    {
        if (markerPrefab == null) return;

        if (_markerInstance == null)
            _markerInstance = Instantiate(markerPrefab);

        _markerInstance.SetActive(true);
        _markerInstance.transform.position = worldPos;
    }

    private Vector3 GetIndexTipPosition()
    {
        return handVisual.Joints[(int)HandJointId.HandIndexTip].position;
    }

    private Vector3 GetIndexDirection()
    {
        var tip    = handVisual.Joints[(int)HandJointId.HandIndexTip].position;
        var middle = handVisual.Joints[(int)HandJointId.HandIndex3].position;
        return (tip - middle).normalized;
    }
}