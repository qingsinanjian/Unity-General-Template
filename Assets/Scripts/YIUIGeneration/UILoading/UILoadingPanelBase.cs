using System;
using YIUIBind;
using YIUIFramework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace YIUI.UILoading
{



    /// <summary>
    /// 由YIUI工具自动创建 请勿手动修改
    /// </summary>
    public abstract class UILoadingPanelBase:BasePanel
    {
        public const string PkgName = "UILoading";
        public const string ResName = "UILoadingPanel";
        
        public override EWindowOption WindowOption => EWindowOption.BanTween;
        public override EPanelLayer Layer => EPanelLayer.Top;
        public override EPanelOption PanelOption => EPanelOption.TimeCache;
        public override EPanelStackOption StackOption => EPanelStackOption.Visible;
        public override int Priority => 0;
        protected override float CachePanelTime => 10;

        public YIUI.UILoading.UILoadingView u_UILoadingView { get; private set; }

        
        protected sealed override void UIBind()
        {
            u_UILoadingView = CDETable.FindUIBase<YIUI.UILoading.UILoadingView>("LoadingView");

        }

        protected sealed override void UnUIBind()
        {

        }
     
   
   
    }
}