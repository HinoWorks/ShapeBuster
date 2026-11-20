using UnityEditor;
using UnityEditor.UI;

/// <summary>
/// Button の標準インスペクターは子クラスのフィールドを出さないので、HButton の効果音の項目を足して出す。
/// </summary>
[CustomEditor(typeof(HButton), true)]
[CanEditMultipleObjects]
public class HButtonEditor : ButtonEditor
{
    SerializedProperty clickSE;
    SerializedProperty hoverSE;
    SerializedProperty disabledClickSE;

    protected override void OnEnable()
    {
        base.OnEnable();
        clickSE = serializedObject.FindProperty("clickSE");
        hoverSE = serializedObject.FindProperty("hoverSE");
        disabledClickSE = serializedObject.FindProperty("disabledClickSE");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("効果音", EditorStyles.boldLabel);
        serializedObject.Update();
        EditorGUILayout.PropertyField(clickSE);
        EditorGUILayout.PropertyField(hoverSE);
        EditorGUILayout.PropertyField(disabledClickSE);
        serializedObject.ApplyModifiedProperties();
    }
}
