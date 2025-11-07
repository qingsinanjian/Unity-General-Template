using UnityEditor;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ChangeTMPFontWindow : EditorWindow
{
    [MenuItem("Tools/TMP字体更换工具")]
    public static void Open()
    {
        GetWindow<ChangeTMPFontWindow>("更换TMP字体");
    }

    private bool _specifyFontReplace = false;
    private TMP_FontAsset _oldFont;
    private TMP_FontAsset _targetFont;
    private FontStyles _targetFontStyle = FontStyles.Normal;

    void OnGUI()
    {
        _specifyFontReplace = EditorGUILayout.Toggle("指定字体更换", _specifyFontReplace);
        if (_specifyFontReplace)
        {
            _oldFont = (TMP_FontAsset)EditorGUILayout.ObjectField("原字体", _oldFont, typeof(TMP_FontAsset), false);
        }

        _targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("目标字体", _targetFont, typeof(TMP_FontAsset), false);
        _targetFontStyle = (FontStyles)EditorGUILayout.EnumPopup("字体样式", _targetFontStyle);

        if (GUILayout.Button("执行更换"))
        {
            ChangeTMPFonts();
        }
    }

    void ChangeTMPFonts()
    {
        // 查找所有TextMeshProUGUI组件
        TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        
        List<TextMeshProUGUI> textsToChange = new List<TextMeshProUGUI>();
        foreach (TextMeshProUGUI text in allTexts)
        {
            if (!EditorUtility.IsPersistent(text.gameObject))
            {
                textsToChange.Add(text);
            }
        }

        int changedCount = 0;
        foreach (TextMeshProUGUI text in textsToChange)
        {
            if (_specifyFontReplace && text.font != _oldFont) 
                continue;
            
            Undo.RecordObject(text, "Change TMP Font");
            text.font = _targetFont;
            text.fontStyle = _targetFontStyle;
            EditorUtility.SetDirty(text);
            changedCount++;
        }

        Debug.Log($"成功更换了 {changedCount} 个TMP文本组件的字体。");
    }
}