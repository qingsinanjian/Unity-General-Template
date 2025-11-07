using YIUIFramework;

namespace YIUICodeGenerated
{
    /// <summary>
    /// 由YIUI工具自动创建 请勿手动修改
    /// 用法: UIBindHelper.InternalGameGetUIBindVoFunc = YIUICodeGenerated.UIBindProvider.Get;
    /// </summary>
    public static class UIBindProvider
    {
        public static UIBindVo[] Get()
        {
            var BasePanel     = typeof(BasePanel);
            var BaseView      = typeof(BaseView);
            var BaseComponent = typeof(BaseComponent);
            var list          = new UIBindVo[2];
            list[0] = new UIBindVo
            {
                PkgName     = YIUI.UILoading.UILoadingPanelBase.PkgName,
                ResName     = YIUI.UILoading.UILoadingPanelBase.ResName,
                CodeType    = BasePanel,
                BaseType    = typeof(YIUI.UILoading.UILoadingPanelBase),
                CreatorType = typeof(YIUI.UILoading.UILoadingPanel),
            };
            list[1] = new UIBindVo
            {
                PkgName     = YIUI.UILoading.UILoadingViewBase.PkgName,
                ResName     = YIUI.UILoading.UILoadingViewBase.ResName,
                CodeType    = BaseView,
                BaseType    = typeof(YIUI.UILoading.UILoadingViewBase),
                CreatorType = typeof(YIUI.UILoading.UILoadingView),
            };

            return list;
        }
    }
}