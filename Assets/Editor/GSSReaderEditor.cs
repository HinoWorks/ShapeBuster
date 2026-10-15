using UnityEngine;
using UnityEditor;



//[CustomEditor(typeof(GSSReader))]
[CustomEditor(typeof(DataBase))]
public class GSSReaderInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        //var r = target as GSSReader;
        var master = target as DataBase;
        var t = "スプレッドシート読み込み";
        //EditorGUI.BeginDisabledGroup(r.IsLoading);
        EditorGUI.BeginDisabledGroup(false);
        if (GUILayout.Button(t))
        {
            //r.Reload();
            master.LoadData();
        }
        EditorGUI.EndDisabledGroup();
    }
}