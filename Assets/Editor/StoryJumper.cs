using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 剧情跳转：菜单 Tools → 剧情跳转。列出整个剧本（按章分组、能搜），点「▶」就从那一句开始演，不用从头看。
// 没在运行：自动打开 SampleScene、进入 Play，再跳过去；已经在运行：直接跳。
// 跳转用的是游戏自己的 DialogueManager.JumpTo（和回想里点一句、读档是同一条路），背景、音乐、立绘、视频都会补好。
public class StoryJumper : EditorWindow
{
    const string ScriptPath = "Assets/Data/Resources/Fly.txt";
    const string GameScene = "Assets/Scenes/SampleScene.unity";
    const string PendingKey = "Fly.JumpRow";

    class Row { public int index, line; public bool isLabel; public string label, speaker, text, sprite, bg, effect; }
    List<Row> rows = new List<Row>();
    string search = "";
    bool onlySprites;
    Vector2 scroll;

    [MenuItem("Tools/剧情跳转 %#j")]   // 快捷键 Ctrl+Shift+J
    static void Open() { GetWindow<StoryJumper>("剧情跳转").Show(); }

    void OnEnable() { Load(); EditorApplication.update += RepaintWhilePlaying; }
    void OnDisable() { EditorApplication.update -= RepaintWhilePlaying; }
    void RepaintWhilePlaying() { if (EditorApplication.isPlaying) Repaint(); }

    void Load()
    {
        rows.Clear();
        if (!File.Exists(ScriptPath)) return;
        string[] lines = File.ReadAllLines(ScriptPath);
        string label = "";
        for (int i = 1; i < lines.Length; i++)                 // 第 0 行是表头；dialogueLines[k] = 文件第 k+1 行 = 表格第 k+2 行
        {
            string[] c = lines[i].TrimEnd('\r').Split('\t');
            string Get(int k) => k < c.Length ? c[k].Trim().Trim('"') : "";
            string flow = Get(0);
            if (flow.StartsWith("label:")) label = flow.Substring(6);
            rows.Add(new Row { index = i - 1, line = i + 1, isLabel = flow.StartsWith("label:"), label = label, speaker = Get(1), text = Get(2), sprite = Get(3), bg = Get(4), effect = Get(6) });
        }
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
        onlySprites = GUILayout.Toggle(onlySprites, "只看有立绘的", EditorStyles.toolbarButton, GUILayout.Width(90));
        if (GUILayout.Button("重新读表", EditorStyles.toolbarButton, GUILayout.Width(60))) Load();
        EditorGUILayout.EndHorizontal();

        // 章节快捷按钮：跳到每一章（label）的第一句
        EditorGUILayout.BeginHorizontal();
        foreach (var r in rows)
            if (IsLabelRow(r) && GUILayout.Button(r.label, EditorStyles.miniButton)) Jump(r.index);
        EditorGUILayout.EndHorizontal();

        int current = -1;
        if (EditorApplication.isPlaying) { var dm = Object.FindObjectOfType<DialogueManager>(); if (dm != null) current = dm.CurrentIndex; }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        string lastLabel = null;
        foreach (var r in rows)
        {
            if (r.text == "" && !IsLabelRow(r) && string.IsNullOrEmpty(r.sprite)) continue;   // 纯效果行不列出来
            if (onlySprites && string.IsNullOrEmpty(r.sprite)) continue;
            if (search != "" && !(r.text + r.speaker + r.sprite + r.bg + r.effect).Contains(search)) continue;
            if (r.label != lastLabel) { lastLabel = r.label; EditorGUILayout.LabelField(r.label, EditorStyles.boldLabel); }
            if (IsLabelRow(r)) continue;

            var bgCol = GUI.backgroundColor;
            if (r.index == current) GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            EditorGUILayout.BeginHorizontal("box");
            if (GUILayout.Button("▶", GUILayout.Width(26))) Jump(r.index);
            GUILayout.Label(r.line.ToString(), GUILayout.Width(30));
            string who = r.speaker == "" || r.speaker == "{clear}" ? "" : r.speaker + "：";
            string t = r.text.Replace("{w}", "").Replace("{nw}", "").Replace("{clear}", "");
            if (t.Length > 40) t = t.Substring(0, 40) + "…";
            GUILayout.Label(who + t, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(r.sprite)) GUILayout.Label("立绘 " + r.sprite.Split(':')[0], EditorStyles.miniLabel, GUILayout.Width(110));
            if (!string.IsNullOrEmpty(r.bg)) GUILayout.Label("背景 " + r.bg, EditorStyles.miniLabel, GUILayout.Width(110));
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = bgCol;
        }
        EditorGUILayout.EndScrollView();
    }

    static bool IsLabelRow(Row r) => r.isLabel;

    // ───────── 跳转 ─────────
    static void Jump(int index)
    {
        if (EditorApplication.isPlaying)
        {
            var dm = Object.FindObjectOfType<DialogueManager>();
            if (dm != null && dm.dialogueLines != null) { dm.JumpTo(index); return; }
            Debug.LogWarning("现在这个场景里没有剧情（可能在标题）。先停止运行，再点一次就会自动从游戏场景开始。");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (EditorSceneManager.GetActiveScene().path != GameScene) EditorSceneManager.OpenScene(GameScene);
        SessionState.SetInt(PendingKey, index);
        EditorApplication.isPlaying = true;
    }

    // 进入 Play 以后：等 DialogueManager 读完表，再跳
    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.EnteredPlayMode || SessionState.GetInt(PendingKey, -1) < 0) return;
            int frames = 0;
            EditorApplication.CallbackFunction wait = null;
            wait = () =>
            {
                frames++;
                var dm = Object.FindObjectOfType<DialogueManager>();
                if (dm != null && dm.dialogueLines != null && dm.dialogueLines.Count > 0 && frames > 2)
                {
                    int row = SessionState.GetInt(PendingKey, -1);
                    SessionState.EraseInt(PendingKey);
                    EditorApplication.update -= wait;
                    if (row >= 0 && row < dm.dialogueLines.Count) dm.JumpTo(row);
                }
                else if (frames > 600 || !EditorApplication.isPlaying) { SessionState.EraseInt(PendingKey); EditorApplication.update -= wait; }
            };
            EditorApplication.update += wait;
        };
    }
}
