using UnityEngine;
using System.Collections;

public class PlacementHandler : MonoBehaviour
{
    [Header("Target")]
    public GameObject currentTarget;
    public GameObject nextTarget;
    public PalmDisplayHandler palmDisplayHandler;
    public float unlockDelay = 1f; // 延迟秒数

    void OnEnable()  => GestureEvents.OnRightIndexTap += HandleTap;
    void OnDisable() => GestureEvents.OnRightIndexTap -= HandleTap;

    void HandleTap(Vector3 worldPos)
    {
        if (currentTarget == null) return;
        if (!GestureStateManager.Instance.Is(AppGestureState.ShowingOnPalm)) return;

        currentTarget.transform.position = worldPos;
        currentTarget.SetActive(true);
        currentTarget = null;

        GestureStateManager.Instance.TryEnter(AppGestureState.ObjectPlaced);
        if (palmDisplayHandler != null && nextTarget != null)
            palmDisplayHandler.SetDisplayTarget(nextTarget);

        // 停止 IndexTap
        var indexTap = GetComponent<IndexTapDetector>();
        if (indexTap != null) indexTap.enabled = false;

        StartCoroutine(UnlockAfterDelay());
    }

    IEnumerator UnlockAfterDelay()
    {
        yield return new WaitForSeconds(unlockDelay);
        
        GestureEvents.Fire_MiniRoomPlaced();
        GestureStateManager.Instance.Exit(AppGestureState.ObjectPlaced);
        GestureEvents.Fire_LeftPalmUpStart();

        Debug.Log("[Placement] Unlocked after delay");
    }
}