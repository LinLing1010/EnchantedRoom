using System;
using UnityEngine;

public static class GestureEvents
{
    // ── 持续状态类 ──────────────────────────────
    public static event Action OnLeftPalmUpStart;
    public static event Action OnLeftPalmUpEnd;

    // ── 瞬间触发类 ──────────────────────────────
    public static event Action<UnityEngine.Vector3> OnRightIndexTap;

    // 触发方法
    public static void Fire_LeftPalmUpStart()                    => OnLeftPalmUpStart?.Invoke();
    public static void Fire_LeftPalmUpEnd()                      => OnLeftPalmUpEnd?.Invoke();
    public static void Fire_RightIndexTap(UnityEngine.Vector3 pos) => OnRightIndexTap?.Invoke(pos);
    
    // MiniRoom 放置完成，解锁手势系统
    public static event Action OnMiniRoomPlaced;
    public static void Fire_MiniRoomPlaced() => OnMiniRoomPlaced?.Invoke();

// PalmDown 检测（由 PalmDownDetector 触发）
    public static event Action OnLeftPalmDownStart;
    public static event Action OnLeftPalmDownEnd;
    public static void Fire_LeftPalmDownStart() => OnLeftPalmDownStart?.Invoke();
    public static void Fire_LeftPalmDownEnd()   => OnLeftPalmDownEnd?.Invoke();

    // PalmUp=false + Open=true → 自由掉落（由 PalmUpDetector 新增检测触发）
    public static event Action OnFreeDrop;
    public static void Fire_FreeDrop() => OnFreeDrop?.Invoke();
    

    // 小球进入 MiniRoom TriggerBox，携带 MiniRoom 局部坐标
    public static event Action<Vector3> OnBallEnteredMiniRoom;
    public static void Fire_BallEnteredMiniRoom(Vector3 localPos) => OnBallEnteredMiniRoom?.Invoke(localPos);
    
    // 双手同时 Pinch（场景切换）
    public static event Action OnBothHandsPinchStart;
    public static event Action OnBothHandsPinchEnd;
    public static void Fire_BothHandsPinchStart() => OnBothHandsPinchStart?.Invoke();
    public static void Fire_BothHandsPinchEnd()   => OnBothHandsPinchEnd?.Invoke();

    // ── Gun / Door ──────────────────────────────
    public static event Action<DoorController> OnGunAimingDoor;
    public static event Action                 OnGunAimLost;
    public static event Action                 OnGunShot;
    public static event Action                 OnDoorSequenceComplete;
    public static void Fire_GunAimingDoor(DoorController door) => OnGunAimingDoor?.Invoke(door);
    public static void Fire_GunAimLost()                       => OnGunAimLost?.Invoke();
    public static void Fire_GunShot()                          => OnGunShot?.Invoke();
    public static void Fire_DoorSequenceComplete()             => OnDoorSequenceComplete?.Invoke();
    
    // ── 双手捧花 CupHands ──────────────────────────────
    public static event Action<Vector3> OnCupHandsStart;   // 捧手开始，参数=双手掌心中点
    public static event Action          OnCupHandsEnd;     // 捧手结束
    public static void Fire_CupHandsStart(Vector3 palmCenter) => OnCupHandsStart?.Invoke(palmCenter);
    public static void Fire_CupHandsEnd()                     => OnCupHandsEnd?.Invoke();
 
    // ── 花朵放置完成 ──────────────────────────────
    public static event Action<Vector3> OnFlowerPlaced;    // 花朵成功放置，参数=放置位置
    public static void Fire_FlowerPlaced(Vector3 pos)      => OnFlowerPlaced?.Invoke(pos);
}