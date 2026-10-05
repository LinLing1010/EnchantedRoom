using UnityEngine;
using System.Collections;

public class RedBallManager : MonoBehaviour
{
    [Header("References")]
    public OVRHand            leftHand;
    public Transform          miniRoomRoot;
    public GameObject         handBall;
    public GameObject         ballPrefab;
    public PalmDisplayHandler palmDisplay;

    [Header("Settings")]
    public float freeDropLifetime = 3f;

    bool      _isUnlocked          = false;
    bool      _ballEnteredMiniRoom = false;
    Coroutine _freeDropRoutine;

    void OnEnable()
    {
        GestureEvents.OnMiniRoomPlaced      += HandleMiniRoomPlaced;
        GestureEvents.OnLeftPalmDownStart   += HandlePalmDownStart;
        GestureEvents.OnFreeDrop            += HandleFreeDrop;
        GestureEvents.OnBallEnteredMiniRoom += HandleBallEnteredMiniRoom;
    }

    void OnDisable()
    {
        GestureEvents.OnMiniRoomPlaced      -= HandleMiniRoomPlaced;
        GestureEvents.OnLeftPalmDownStart   -= HandlePalmDownStart;
        GestureEvents.OnFreeDrop            -= HandleFreeDrop;
        GestureEvents.OnBallEnteredMiniRoom -= HandleBallEnteredMiniRoom;
    }

    void HandleMiniRoomPlaced() => _isUnlocked = true;

    void HandleFreeDrop()
    {
        if (!_isUnlocked) return;
        if (!GestureStateManager.Instance.Is(AppGestureState.ShowingOnPalm)) return;

        palmDisplay?.SetDisplayTarget(null);
        handBall.SetActive(true);
        SetKinematic(handBall, false);
        GestureStateManager.Instance.Exit(AppGestureState.ShowingOnPalm);

        if (_freeDropRoutine != null) StopCoroutine(_freeDropRoutine);
        _freeDropRoutine = StartCoroutine(FreeDropReset());
    }

    void HandlePalmDownStart()
    {
        if (!GestureStateManager.Instance.Is(AppGestureState.ShowingOnPalm)) return;
        if (!IsAboveMiniRoom(leftHand.PointerPose.position)) return;

        GestureStateManager.Instance.TryEnter(AppGestureState.Placing);
        SetKinematic(handBall, false);

        if (_freeDropRoutine != null) StopCoroutine(_freeDropRoutine);
        _freeDropRoutine = StartCoroutine(PlacingTimeout());
    }

    void HandleBallEnteredMiniRoom(Vector3 localPos)
    {
        if (_ballEnteredMiniRoom) return;
        _ballEnteredMiniRoom = true;

        if (_freeDropRoutine != null) StopCoroutine(_freeDropRoutine);

        float      ratio      = 1f / miniRoomRoot.lossyScale.x;
        Vector3    bigWorldPos = -miniRoomRoot.position + localPos;
        GameObject bigBall    = Instantiate(ballPrefab, bigWorldPos, Quaternion.identity);
        bigBall.transform.localScale = handBall.transform.localScale * ratio;
        SetKinematic(bigBall, false);

        GestureStateManager.Instance.TryEnter(AppGestureState.ObjectPlaced);
    }

    IEnumerator FreeDropReset()
    {
        yield return new WaitForSeconds(freeDropLifetime);
        SetKinematic(handBall, true);
        handBall.transform.position = Vector3.zero;
        handBall.SetActive(false);
        palmDisplay?.SetDisplayTarget(handBall);
    }

    IEnumerator PlacingTimeout()
    {
        yield return new WaitForSeconds(freeDropLifetime);
        if (!_ballEnteredMiniRoom)
        {
            SetKinematic(handBall, true);
            handBall.transform.position = Vector3.zero;
            handBall.SetActive(false);
            palmDisplay?.SetDisplayTarget(handBall);
            GestureStateManager.Instance.Exit(AppGestureState.Placing);
        }
    }

    bool IsAboveMiniRoom(Vector3 worldPos)
    {
        Bounds b = new Bounds(miniRoomRoot.position, miniRoomRoot.lossyScale);
        return worldPos.y > b.max.y
            && worldPos.x > b.min.x && worldPos.x < b.max.x
            && worldPos.z > b.min.z && worldPos.z < b.max.z;
    }

    void SetKinematic(GameObject go, bool kinematic)
    {
        var rb = go?.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = kinematic;
    }
}