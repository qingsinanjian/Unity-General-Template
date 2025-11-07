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
    public abstract class UILoadingViewBase:BaseView
    {
        public const string PkgName = "UILoading";
        public const string ResName = "UILoadingView";
        
        public override EWindowOption WindowOption => EWindowOption.None;
        public override EViewWindowType ViewWindowType => EViewWindowType.View;
        public override EViewStackOption StackOption => EViewStackOption.VisibleTween;
        public UnityEngine.UI.Slider u_ComLoadingSlider { get; private set; }
        public YIUIBind.UIDataValueFloat u_DataLoadingProgress { get; private set; }
        protected UIEventP1<float> u_EventProgress { get; private set; }
        protected UIEventHandleP1<float> u_EventProgressHandle { get; private set; }

        
        protected sealed override void UIBind()
        {
            u_ComLoadingSlider = ComponentTable.FindComponent<UnityEngine.UI.Slider>("u_ComLoadingSlider");
            u_DataLoadingProgress = DataTable.FindDataValue<YIUIBind.UIDataValueFloat>("u_DataLoadingProgress");
            u_EventProgress = EventTable.FindEvent<UIEventP1<float>>("u_EventProgress");
            u_EventProgressHandle = u_EventProgress.Add(OnEventProgressAction);

        }

        protected sealed override void UnUIBind()
        {
            u_EventProgress.Remove(u_EventProgressHandle);

        }
     
        protected virtual void OnEventProgressAction(float p1){}
   
   
    }
}