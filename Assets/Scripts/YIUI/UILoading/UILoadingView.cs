using System;
using YIUIBind;
using YIUIFramework;
using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace YIUI.UILoading
{
    /// <summary>
    /// Author  YIUI
    /// Date    2025.11.4
    /// </summary>
    public sealed partial class UILoadingView:UILoadingViewBase
    {

        #region 生命周期
        
        protected override void Initialize()
        {
            Debug.Log($"UILoadingView Initialize");
        }

        protected override void Start()
        {
        }

        protected override void OnEnable()
        {
        }

        protected override void OnDisable()
        {
        }

        protected override void OnDestroy()
        {
            Debug.Log($"UILoadingView OnDestroy");
        }

        protected override async UniTask<bool> OnOpen()
        {
            await UniTask.CompletedTask;
            Debug.Log($"UILoadingView OnOpen");
            return true;
        }

        protected override async UniTask<bool> OnOpen(ParamVo param)
        {
            return await base.OnOpen(param);
        }
        
        #endregion

        #region Event开始


       
        protected override void OnEventProgressAction(float p1)
        {
            
        }
         #endregion Event结束

    }
}