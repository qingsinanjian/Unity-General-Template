using System;
using System.Collections.Generic;
using I2.Loc;
using UnityEngine;
using YIUIFramework;
using UniTask = Cysharp.Threading.Tasks.UniTask;

public sealed partial class GameManager : MonoSingleton<GameManager>
    {
        private Dictionary<int, UnityEngine.Object> m_Resources = new ();

        
        private async UniTask InitUI()
        {
            if (SingletonMgr.Disposing || SingletonMgr.IsQuitting) return;
            //关联UI工具中自动生成绑定代码 Tools >> YIUI自动化工具 >> 发布 >> UI自动生成绑定替代反射代码
            UIBindHelper.InternalGameGetUIBindVoFunc = YIUICodeGenerated.UIBindProvider.Get;
            YIUILoadDI.LoadAssetFunc                 = LoadUI;       //同步加载
            YIUILoadDI.LoadAssetAsyncFunc            = LoadUIAsync;  //异步加载
            YIUILoadDI.ReleaseAction                 = ReleaseAction;   //释放
            
            //以下是YIUI中已经用到的管理器 在这里初始化
            await MgrCenter.Inst.Register(I2LocalizeMgr.Inst);
            await MgrCenter.Inst.Register(CountDownMgr.Inst);
            //await MgrCenter.Inst.Register(RedDotMgr.Inst);
            await MgrCenter.Inst.Register(PanelMgr.Inst);
            await UniTask.WaitForEndOfFrame(this);
            var _init = await MgrCenter.Inst.ManagerAsyncInit();
            //UIHelper.ChangeToPortrait();
            //UIHelper.ChangeToLandscapeLeft();
            
            if (_init)
            {
                var layoutRoot = GameObject.Find("YIUILayerRoot").GetComponent<RectTransform>();
                layoutRoot.anchoredPosition = new Vector2(0, 0);
                layoutRoot.sizeDelta = new Vector2(0, 0);
                Debug.Log("初始化UI成功");
            }
            else
            {
                Debug.LogError("初始化失败");
            }

        }
        
        
        private (UnityEngine.Object, int) LoadUI(string packageName, string resName, Type arg3)
        {
            resName = resName.Contains("UI") ? $"Prefabs/{resName}" : resName;
            return LoadAsset($"YIUI/{packageName}", resName, arg3);
        }
        
        private async Cysharp.Threading.Tasks.UniTask<(UnityEngine.Object, int)> LoadUIAsync(string packageName, string resName, Type arg3)
        {
            resName = resName.Contains("UI") ? $"Prefabs/{resName}" : resName;
            return await LoadAssetAsync($"YIUI/{packageName}", resName, arg3);
        }
        
        public (UnityEngine.Object, int) LoadAsset(string packageName, string resName, Type arg3)
        {
            var obj = Resources.Load($"{packageName}/{resName}", arg3);
            return LoadResult(obj);
        }
        
        public async Cysharp.Threading.Tasks.UniTask<(UnityEngine.Object, int)> LoadAssetAsync(string packageName, string resName, Type arg3)
        {
            var resourcesRequest = Resources.LoadAsync($"{packageName}/{resName}", arg3);
            await resourcesRequest;
            return LoadResult(resourcesRequest.asset);
        }
        
        /// <summary>
        /// 释放方法
        /// </summary>
        /// <param name="hashCode">加载时所给到的唯一ID</param>
        public void ReleaseAction(int hashCode)
        {
            if (m_Resources.TryGetValue(hashCode, out var value))
            {
                if (value is not GameObject or Component or AssetBundle)
                {
                    Resources.UnloadAsset(value);
                }
                m_Resources.Remove(hashCode);
            }
            else
            {
                Debug.LogError($"释放了一个未知Code:{hashCode}");
            }
        }
        
        //Demo中对YooAsset加载后的一个简单返回封装
        //只有成功加载才返回 否则直接释放
        private (UnityEngine.Object, int) LoadResult(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                var hashCode = obj.GetHashCode();
                m_Resources.Add(hashCode, obj);
                return (obj, hashCode);
            }
            else
            {
                return (null, 0);
            }
        }

        public void UnloadUnusedAssets()
        {
            Resources.UnloadUnusedAssets();
        }
    }
