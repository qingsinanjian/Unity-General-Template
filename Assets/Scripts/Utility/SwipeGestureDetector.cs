using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using GameCreator.Runtime.Common;

/// <summary>
/// 基于纯新输入系统的屏幕滑动事件检测（支持区域限制）
/// </summary>
public class SwipeGestureDetector : Singleton<SwipeGestureDetector>
{
    private PlayerInputActions inputActions;
    
    // UI检测模式枚举
    public enum UIDetectionMode
    {
        IgnoreUI,       // 忽略UI，只要滑动就触发
        RespectUI       // 尊重UI，在UI上时不触发
    }

    // 滑动区域限制模式
    public enum SwipeZoneMode
    {
        None,           // 无区域限制
        Custom,         // 自定义区域
        LeftOnly,       // 只检测左侧区域开始的右滑
        RightOnly,      // 只检测右侧区域开始的左滑
        EdgeOnly        // 只检测边缘区域开始的滑动
    }

    [Header("UI Interaction Settings")]
    public UIDetectionMode uiDetectionMode = UIDetectionMode.RespectUI;

    [Header("Swipe Zone Settings")]
    public SwipeZoneMode swipeZoneMode = SwipeZoneMode.EdgeOnly;
    
    [Header("Custom Zone Settings (当模式为Custom时生效)")]
    [Range(0, 1)] public float leftZoneWidth = 0.3f;    // 左侧区域宽度比例
    [Range(0, 1)] public float rightZoneWidth = 0.3f;   // 右侧区域宽度比例
    [Range(0, 1)] public float topZoneHeight = 0.3f;    // 顶部区域高度比例
    [Range(0, 1)] public float bottomZoneHeight = 0.3f; // 底部区域高度比例
    
    [Header("Edge Zone Settings (当模式为EdgeOnly时生效)")]
    [Range(0, 0.5f)] public float edgeThreshold = 0.1f; // 边缘阈值比例

    [System.Serializable]
    public class SwipeEvent : UnityEvent<Vector2>
    {
    }

    public SwipeEvent onSwipeLeft { get; private set; } = new SwipeEvent();
    public SwipeEvent onSwipeRight { get; private set; } = new SwipeEvent();
    public SwipeEvent onSwipeUp { get; private set; } = new SwipeEvent();
    public SwipeEvent onSwipeDown { get; private set; } = new SwipeEvent();
    public UnityEvent onSwipe { get; private set; } = new UnityEvent();

    [Header("Swipe Settings")] 
    public float minSwipeDistance = 300f;
    public float maxSwipeTime = 1f;

    // 新输入系统相关变量
    private InputAction touchPositionAction;
    private InputAction touchContactAction;
    private Vector2 touchStartPos;
    private Vector2 currentTouchPos;
    private bool isDragging = false;
    private float touchStartTime;

    // 区域检测相关
    private TouchStartZone touchStartZone = TouchStartZone.None;

    // 调试相关
    private bool enableDebugLogs = true;

    // 存储已注册的监听器
    private List<UnityAction<Vector2>> leftSwipeListeners = new List<UnityAction<Vector2>>();
    private List<UnityAction<Vector2>> rightSwipeListeners = new List<UnityAction<Vector2>>();
    private List<UnityAction<Vector2>> upSwipeListeners = new List<UnityAction<Vector2>>();
    private List<UnityAction<Vector2>> downSwipeListeners = new List<UnityAction<Vector2>>();
    private List<UnityAction> anySwipeListeners = new List<UnityAction>();

    // 触摸起始区域枚举
    private enum TouchStartZone
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
        Center
    }

    private void Awake()
    {
        inputActions = new PlayerInputActions();
        touchPositionAction = inputActions.UI.TouchPosition;
        touchContactAction = inputActions.UI.TouchContact;
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        EnsureEventsInitialized();
    }

    void OnEnable()
    {
        RegisterInputActions();
    }

    void OnDisable()
    {
        UnregisterInputActions();
        ResetTouchState();
    }

    void OnDestroy()
    {
        UnregisterInputActions();
        UnregisterAllListeners();
    }

    private void Update()
    {
        // 在拖动状态下，每帧更新当前位置
        if (isDragging)
        {
            currentTouchPos = touchPositionAction.ReadValue<Vector2>();
            
            // 实时检查滑动条件
            CheckForSwipe();
            
            // #if UNITY_EDITOR
            // if (enableDebugLogs)
            // {
            //     Debug.Log(GetInputStateInfo());
            // }
            // #endif
        }
    }

    /// <summary>
    /// 确保所有事件被正确初始化
    /// </summary>
    private void EnsureEventsInitialized()
    {
        if (onSwipeLeft == null) onSwipeLeft = new SwipeEvent();
        if (onSwipeRight == null) onSwipeRight = new SwipeEvent();
        if (onSwipeUp == null) onSwipeUp = new SwipeEvent();
        if (onSwipeDown == null) onSwipeDown = new SwipeEvent();
        if (onSwipe == null) onSwipe = new UnityEvent();
    }

    /// <summary>
    /// 注册输入Action回调
    /// </summary>
    private void RegisterInputActions()
    {
        if (touchContactAction != null && touchPositionAction != null)
        {
            touchPositionAction.Enable();
            touchContactAction.Enable();

            touchContactAction.started += OnTouchStarted;
            touchContactAction.canceled += OnTouchCanceled;
            touchContactAction.performed += OnTouchPerformed;
        }
    }

    /// <summary>
    /// 注销输入Action回调
    /// </summary>
    private void UnregisterInputActions()
    {
        if (touchContactAction != null)
        {
            touchContactAction.started -= OnTouchStarted;
            touchContactAction.canceled -= OnTouchCanceled;
            touchContactAction.performed -= OnTouchPerformed;
            touchContactAction.Disable();
        }

        if (touchPositionAction != null)
        {
            touchPositionAction.Disable();
        }
    }

    /// <summary>
    /// 触摸开始回调
    /// </summary>
    private void OnTouchStarted(InputAction.CallbackContext context)
    {
        // 根据UI检测模式决定是否处理
        if (!ShouldProcessInput())
            return;

        touchStartPos = touchPositionAction.ReadValue<Vector2>();
        currentTouchPos = touchStartPos;
        isDragging = true;
        touchStartTime = Time.time;
        
        // 检测触摸起始区域
        touchStartZone = GetTouchStartZone(touchStartPos);
        
        #if UNITY_EDITOR
        if (enableDebugLogs)
        {
            Debug.Log($"触摸开始: {touchStartPos}, 区域: {touchStartZone}");
        }
        #endif
    }

    /// <summary>
    /// 获取触摸起始区域
    /// </summary>
    private TouchStartZone GetTouchStartZone(Vector2 position)
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        float normalizedX = position.x / screenWidth;
        float normalizedY = position.y / screenHeight;

        switch (swipeZoneMode)
        {
            case SwipeZoneMode.None:
                return TouchStartZone.Center; // 无限制，视为中心区域
                
            case SwipeZoneMode.Custom:
                // 自定义区域检测
                if (normalizedX < leftZoneWidth) return TouchStartZone.Left;
                if (normalizedX > 1 - rightZoneWidth) return TouchStartZone.Right;
                if (normalizedY > 1 - topZoneHeight) return TouchStartZone.Top;
                if (normalizedY < bottomZoneHeight) return TouchStartZone.Bottom;
                return TouchStartZone.Center;
                
            case SwipeZoneMode.LeftOnly:
                // 只检测左侧区域
                return normalizedX < 0.5f ? TouchStartZone.Left : TouchStartZone.Center;
                
            case SwipeZoneMode.RightOnly:
                // 只检测右侧区域
                return normalizedX > 0.5f ? TouchStartZone.Right : TouchStartZone.Center;
                
            case SwipeZoneMode.EdgeOnly:
                // 只检测边缘区域
                if (normalizedX < edgeThreshold) return TouchStartZone.Left;
                if (normalizedX > 1 - edgeThreshold) return TouchStartZone.Right;
                if (normalizedY > 1 - edgeThreshold) return TouchStartZone.Top;
                if (normalizedY < edgeThreshold) return TouchStartZone.Bottom;
                return TouchStartZone.Center;
                
            default:
                return TouchStartZone.Center;
        }
    }

    /// <summary>
    /// 检查滑动是否符合区域限制
    /// </summary>
    private bool IsSwipeAllowedByZone(SwipeDirection direction)
    {
        // 无区域限制时，所有滑动都允许
        if (swipeZoneMode == SwipeZoneMode.None)
            return true;

        // 根据起始区域和滑动方向判断是否允许
        switch (swipeZoneMode)
        {
            case SwipeZoneMode.Custom:
                // 自定义区域逻辑
                return IsCustomZoneSwipeAllowed(direction);
                
            case SwipeZoneMode.LeftOnly:
                // 只允许从左侧区域开始的右滑
                return touchStartZone == TouchStartZone.Left && direction == SwipeDirection.Right;
                
            case SwipeZoneMode.RightOnly:
                // 只允许从右侧区域开始的左滑
                return touchStartZone == TouchStartZone.Right && direction == SwipeDirection.Left;
                
            case SwipeZoneMode.EdgeOnly:
                // 边缘区域滑动限制
                return IsEdgeZoneSwipeAllowed(direction);
                
            default:
                return true;
        }
    }

    /// <summary>
    /// 自定义区域滑动允许判断
    /// </summary>
    private bool IsCustomZoneSwipeAllowed(SwipeDirection direction)
    {
        switch (touchStartZone)
        {
            case TouchStartZone.Left:
                return direction == SwipeDirection.Right; // 左侧区域只允许右滑
            case TouchStartZone.Right:
                return direction == SwipeDirection.Left;  // 右侧区域只允许左滑
            case TouchStartZone.Top:
                return direction == SwipeDirection.Down;  // 顶部区域只允许下滑
            case TouchStartZone.Bottom:
                return direction == SwipeDirection.Up;    // 底部区域只允许上滑
            case TouchStartZone.Center:
                return true; // 中心区域允许所有方向
            default:
                return false;
        }
    }

    /// <summary>
    /// 边缘区域滑动允许判断
    /// </summary>
    private bool IsEdgeZoneSwipeAllowed(SwipeDirection direction)
    {
        switch (touchStartZone)
        {
            case TouchStartZone.Left:
                return direction == SwipeDirection.Right; // 左边缘只允许右滑
            case TouchStartZone.Right:
                return direction == SwipeDirection.Left;  // 右边缘只允许左滑
            case TouchStartZone.Top:
                return direction == SwipeDirection.Down;  // 上边缘只允许下滑
            case TouchStartZone.Bottom:
                return direction == SwipeDirection.Up;    // 下边缘只允许上滑
            case TouchStartZone.Center:
                return false; // 中心区域不允许滑动
            default:
                return false;
        }
    }

    /// <summary>
    /// 触摸持续回调
    /// </summary>
    private void OnTouchPerformed(InputAction.CallbackContext context)
    {
        if (!isDragging) return;
        
        // #if UNITY_EDITOR
        // if (enableDebugLogs)
        // {
        //     Vector2 debugPos = touchPositionAction.ReadValue<Vector2>();
        //     Debug.Log($"触摸持续 - 回调位置: {debugPos}, 当前记录位置: {currentTouchPos}");
        // }
        // #endif
    }

    /// <summary>
    /// 触摸结束/取消回调
    /// </summary>
    private void OnTouchCanceled(InputAction.CallbackContext context)
    {
        if (!isDragging) return;

        // 最终检查滑动（确保不会漏掉快速滑动）
        currentTouchPos = touchPositionAction.ReadValue<Vector2>();
        CheckForSwipe(true);
        
        // #if UNITY_EDITOR
        // if (enableDebugLogs)
        // {
        //     Debug.Log($"触摸结束 - 最终位置: {currentTouchPos}");
        // }
        // #endif
        
        ResetTouchState();
    }

    /// <summary>
    /// 重置触摸状态
    /// </summary>
    private void ResetTouchState()
    {
        isDragging = false;
        touchStartPos = Vector2.zero;
        currentTouchPos = Vector2.zero;
        touchStartTime = 0f;
        touchStartZone = TouchStartZone.None;
    }

    /// <summary>
    /// 检查是否应该处理当前输入（根据UI检测模式）
    /// </summary>
    private bool ShouldProcessInput()
    {
        if (uiDetectionMode == UIDetectionMode.IgnoreUI)
            return true;

        return !IsPointerOverUI();
    }

    /// <summary>
    /// 检查指针是否在UI上
    /// </summary>
    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            return EventSystem.current.IsPointerOverGameObject();
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            int touchId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            return EventSystem.current.IsPointerOverGameObject(touchId);
        }

        return false;
    }

    /// <summary>
    /// 检查滑动条件
    /// </summary>
    private void CheckForSwipe(bool isFinalCheck = false)
    {
        if (!isDragging) return;

        Vector2 swipeDelta = currentTouchPos - touchStartPos;
        float swipeTime = Time.time - touchStartTime;

        if (swipeTime > maxSwipeTime && !isFinalCheck)
        {
            ResetTouchState();
            return;
        }

        float requiredDistance = isFinalCheck ? minSwipeDistance * 0.7f : minSwipeDistance;

        if (swipeDelta.magnitude > requiredDistance)
        {
            ProcessSwipe(swipeDelta);
            if (!isFinalCheck)
            {
                ResetTouchState();
            }
        }
    }

    /// <summary>
    /// 处理滑动方向
    /// </summary>
    private void ProcessSwipe(Vector2 swipeDelta)
    {
        float horizontal = Mathf.Abs(swipeDelta.x);
        float vertical = Mathf.Abs(swipeDelta.y);

        Vector2 swipeDirection = swipeDelta.normalized;

        // 判断主要滑动方向
        if (horizontal > vertical)
        {
            if (swipeDelta.x < 0) // 左滑
            {
                if (IsSwipeAllowedByZone(SwipeDirection.Left))
                {
                    TriggerSwipeLeft(swipeDirection);
                }
                else
                {
                    #if UNITY_EDITOR
                    if (enableDebugLogs)
                    {
                        Debug.Log($"左滑被区域限制阻止，起始区域: {touchStartZone}");
                    }
                    #endif
                }
            }
            else // 右滑
            {
                if (IsSwipeAllowedByZone(SwipeDirection.Right))
                {
                    TriggerSwipeRight(swipeDirection);
                }
                else
                {
                    #if UNITY_EDITOR
                    if (enableDebugLogs)
                    {
                        Debug.Log($"右滑被区域限制阻止，起始区域: {touchStartZone}");
                    }
                    #endif
                }
            }
        }
        else
        {
            if (swipeDelta.y > 0) // 上滑
            {
                if (IsSwipeAllowedByZone(SwipeDirection.Up))
                {
                    TriggerSwipeUp(swipeDirection);
                }
                else
                {
                    #if UNITY_EDITOR
                    if (enableDebugLogs)
                    {
                        Debug.Log($"上滑被区域限制阻止，起始区域: {touchStartZone}");
                    }
                    #endif
                }
            }
            else // 下滑
            {
                if (IsSwipeAllowedByZone(SwipeDirection.Down))
                {
                    TriggerSwipeDown(swipeDirection);
                }
                else
                {
                    #if UNITY_EDITOR
                    if (enableDebugLogs)
                    {
                        Debug.Log($"下滑被区域限制阻止，起始区域: {touchStartZone}");
                    }
                    #endif
                }
            }
        }
    }

    // 滑动方向枚举
    private enum SwipeDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    // 触发左滑事件
    private void TriggerSwipeLeft(Vector2 direction)
    {
        onSwipeLeft?.Invoke(direction);
        onSwipe?.Invoke();
        
        #if UNITY_EDITOR
        if (enableDebugLogs)
        {
            Debug.Log($"左滑检测到！方向: {direction}, 距离: {(currentTouchPos - touchStartPos).magnitude}, 起始区域: {touchStartZone}");
        }
        #endif
    }

    // 触发右滑事件
    private void TriggerSwipeRight(Vector2 direction)
    {
        onSwipeRight?.Invoke(direction);
        onSwipe?.Invoke();
        
        #if UNITY_EDITOR
        if (enableDebugLogs)
        {
            Debug.Log($"右滑检测到！方向: {direction}, 距离: {(currentTouchPos - touchStartPos).magnitude}, 起始区域: {touchStartZone}");
        }
        #endif
    }

    // 触发上滑事件
    private void TriggerSwipeUp(Vector2 direction)
    {
        onSwipeUp?.Invoke(direction);
        onSwipe?.Invoke();
        
        #if UNITY_EDITOR
        if (enableDebugLogs)
        {
            Debug.Log($"上滑检测到！方向: {direction}, 距离: {(currentTouchPos - touchStartPos).magnitude}, 起始区域: {touchStartZone}");
        }
        #endif
    }

    // 触发下滑事件
    private void TriggerSwipeDown(Vector2 direction)
    {
        onSwipeDown?.Invoke(direction);
        onSwipe?.Invoke();
        
        #if UNITY_EDITOR
        if (enableDebugLogs)
        {
            Debug.Log($"下滑检测到！方向: {direction}, 距离: {(currentTouchPos - touchStartPos).magnitude}, 起始区域: {touchStartZone}");
        }
        #endif
    }

    // ========== 可视化区域方法 ==========

    /// <summary>
    /// 获取当前区域设置的屏幕矩形（用于调试可视化）
    /// </summary>
    public Rect[] GetZoneRects()
    {
        List<Rect> zones = new List<Rect>();
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        switch (swipeZoneMode)
        {
            case SwipeZoneMode.Custom:
                if (leftZoneWidth > 0)
                    zones.Add(new Rect(0, 0, screenWidth * leftZoneWidth, screenHeight));
                if (rightZoneWidth > 0)
                    zones.Add(new Rect(screenWidth * (1 - rightZoneWidth), 0, screenWidth * rightZoneWidth, screenHeight));
                if (topZoneHeight > 0)
                    zones.Add(new Rect(0, screenHeight * (1 - topZoneHeight), screenWidth, screenHeight * topZoneHeight));
                if (bottomZoneHeight > 0)
                    zones.Add(new Rect(0, 0, screenWidth, screenHeight * bottomZoneHeight));
                break;

            case SwipeZoneMode.LeftOnly:
                zones.Add(new Rect(0, 0, screenWidth * 0.5f, screenHeight));
                break;

            case SwipeZoneMode.RightOnly:
                zones.Add(new Rect(screenWidth * 0.5f, 0, screenWidth * 0.5f, screenHeight));
                break;

            case SwipeZoneMode.EdgeOnly:
                if (edgeThreshold > 0)
                {
                    zones.Add(new Rect(0, 0, screenWidth * edgeThreshold, screenHeight)); // 左边缘
                    zones.Add(new Rect(screenWidth * (1 - edgeThreshold), 0, screenWidth * edgeThreshold, screenHeight)); // 右边缘
                    zones.Add(new Rect(0, screenHeight * (1 - edgeThreshold), screenWidth, screenHeight * edgeThreshold)); // 上边缘
                    zones.Add(new Rect(0, 0, screenWidth, screenHeight * edgeThreshold)); // 下边缘
                }
                break;
        }

        return zones.ToArray();
    }

    // ========== 注册事件方法 ==========

    /// <summary>
    /// 注册左滑事件监听器
    /// </summary>
    public void RegisterOnSwipeLeft(UnityAction<Vector2> listener)
    {
        if (onSwipeLeft == null) onSwipeLeft = new SwipeEvent();
        
        if (listener != null && !leftSwipeListeners.Contains(listener))
        {
            leftSwipeListeners.Add(listener);
            onSwipeLeft.AddListener(listener);
        }
    }

    /// <summary>
    /// 注册右滑事件监听器
    /// </summary>
    public void RegisterOnSwipeRight(UnityAction<Vector2> listener)
    {
        if (onSwipeRight == null) onSwipeRight = new SwipeEvent();
        
        if (listener != null && !rightSwipeListeners.Contains(listener))
        {
            rightSwipeListeners.Add(listener);
            onSwipeRight.AddListener(listener);
        }
    }

    /// <summary>
    /// 注册上滑事件监听器
    /// </summary>
    public void RegisterOnSwipeUp(UnityAction<Vector2> listener)
    {
        if (onSwipeUp == null) onSwipeUp = new SwipeEvent();
        
        if (listener != null && !upSwipeListeners.Contains(listener))
        {
            upSwipeListeners.Add(listener);
            onSwipeUp.AddListener(listener);
        }
    }

    /// <summary>
    /// 注册下滑事件监听器
    /// </summary>
    public void RegisterOnSwipeDown(UnityAction<Vector2> listener)
    {
        if (onSwipeDown == null) onSwipeDown = new SwipeEvent();
        
        if (listener != null && !downSwipeListeners.Contains(listener))
        {
            downSwipeListeners.Add(listener);
            onSwipeDown.AddListener(listener);
        }
    }

    /// <summary>
    /// 注册任意方向滑动事件监听器
    /// </summary>
    public void RegisterOnAnySwipe(UnityAction listener)
    {
        if (onSwipe == null) onSwipe = new UnityEvent();
        
        if (listener != null && !anySwipeListeners.Contains(listener))
        {
            anySwipeListeners.Add(listener);
            onSwipe.AddListener(listener);
        }
    }

    // ========== 注销事件方法 ==========

    /// <summary>
    /// 注销左滑事件监听器
    /// </summary>
    public void UnregisterOnSwipeLeft(UnityAction<Vector2> listener)
    {
        if (onSwipeLeft == null) return;
        
        if (listener != null && leftSwipeListeners.Contains(listener))
        {
            leftSwipeListeners.Remove(listener);
            onSwipeLeft.RemoveListener(listener);
        }
    }

    /// <summary>
    /// 注销右滑事件监听器
    /// </summary>
    public void UnregisterOnSwipeRight(UnityAction<Vector2> listener)
    {
        if (onSwipeRight == null) return;
        
        if (listener != null && rightSwipeListeners.Contains(listener))
        {
            rightSwipeListeners.Remove(listener);
            onSwipeRight.RemoveListener(listener);
        }
    }

    /// <summary>
    /// 注销上滑事件监听器
    /// </summary>
    public void UnregisterOnSwipeUp(UnityAction<Vector2> listener)
    {
        if (onSwipeUp == null) return;
        
        if (listener != null && upSwipeListeners.Contains(listener))
        {
            upSwipeListeners.Remove(listener);
            onSwipeUp.RemoveListener(listener);
        }
    }

    /// <summary>
    /// 注销下滑事件监听器
    /// </summary>
    public void UnregisterOnSwipeDown(UnityAction<Vector2> listener)
    {
        if (onSwipeDown == null) return;
        
        if (listener != null && downSwipeListeners.Contains(listener))
        {
            downSwipeListeners.Remove(listener);
            onSwipeDown.RemoveListener(listener);
        }
    }

    /// <summary>
    /// 注销任意方向滑动事件监听器
    /// </summary>
    public void UnregisterOnAnySwipe(UnityAction listener)
    {
        if (onSwipe == null) return;
        
        if (listener != null && anySwipeListeners.Contains(listener))
        {
            anySwipeListeners.Remove(listener);
            onSwipe.RemoveListener(listener);
        }
    }

    /// <summary>
    /// 注销所有事件监听器
    /// </summary>
    public void UnregisterAllListeners()
    {
        EnsureEventsInitialized();
        
        foreach (var listener in leftSwipeListeners)
            onSwipeLeft.RemoveListener(listener);
        foreach (var listener in rightSwipeListeners)
            onSwipeRight.RemoveListener(listener);
        foreach (var listener in upSwipeListeners)
            onSwipeUp.RemoveListener(listener);
        foreach (var listener in downSwipeListeners)
            onSwipeDown.RemoveListener(listener);
        foreach (var listener in anySwipeListeners)
            onSwipe.RemoveListener(listener);

        leftSwipeListeners.Clear();
        rightSwipeListeners.Clear();
        upSwipeListeners.Clear();
        downSwipeListeners.Clear();
        anySwipeListeners.Clear();
    }

    /// <summary>
    /// 启用/禁用手势检测
    /// </summary>
    public void SetDetectionEnabled(bool enabled)
    {
        if (enabled)
        {
            RegisterInputActions();
        }
        else
        {
            UnregisterInputActions();
            ResetTouchState();
        }
    }

    /// <summary>
    /// 启用/禁用调试日志
    /// </summary>
    public void SetDebugLogsEnabled(bool enabled)
    {
        enableDebugLogs = enabled;
    }

    /// <summary>
    /// 获取当前输入状态信息（用于调试）
    /// </summary>
    public string GetInputStateInfo()
    {
        if (isDragging)
        {
            Vector2 swipeDelta = currentTouchPos - touchStartPos;
            float swipeTime = Time.time - touchStartTime;
            return $"触摸拖动: 开始={touchStartPos}, 当前={currentTouchPos}, 差值={swipeDelta}, 距离={swipeDelta.magnitude:F1}, 时间={swipeTime:F2}s, 区域={touchStartZone}";
        }

        return "没有检测到拖动";
    }
}