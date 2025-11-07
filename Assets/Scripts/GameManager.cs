using DG.Tweening;
using UnityEngine;
using YIUI.UILoading;
using YIUIFramework;

public sealed partial class GameManager : MonoSingleton<GameManager>
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RuntimeInitializeOnLoadMethod()
    {
        SingletonMgr.Initialize();
        GameManager.Inst.gameObject.tag = nameof(GameManager);
        DontDestroyOnLoad(GameManager.Inst.gameObject);
        Debug.Log("GameManager.RuntimeInitializeOnLoadMethod");
    }

    private void Awake()
    {
        // 设置固定帧率（例如60FPS）
        Application.targetFrameRate = 60;
        // 垂直同步（与显示器刷新率匹配）
        QualitySettings.vSyncCount = 1;
        // 隐藏iOS状态栏
        Screen.fullScreen = true;
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
    }
    
    private async void Start()
    {
        await InitUI();
        SRDebug.Init();
        await PanelMgr.Inst.OpenPanelAsync<UILoadingPanel>();
        DOTween.Init();
    }
}
