using System.Collections.Generic;
using UnityEngine;
using TMPro;

// 日文版换字体（v1.3）：中文字体里的汉字是中国写法，假名又要从别的字体借，两种字夹在一起不好看。
// 所以日文版整套换成日文字体——不用一个个脚本去改：TMP 每次重排文字都会广播一个事件（TEXT_CHANGED_EVENT），
// 我们在这里接住，看到中文的三种字体就换成对应的日文字体；换回中文时再换回原来的。
//   MainFont（HarmonyOS Bold 界面字）→ Zen Kaku Gothic New Bold
//   WenKai SDF（正文）         → Klee One（霞鹜文楷就是从它改出来的，气质一样）
//   KuaiLe SDF（手写）         → Klee One SemiBold
// 日文字体是「静态」图集：日文译文里用到的字已经画好了（Tools → 语言：生成日文字体，Editor/LocFontsBake.cs）。放在 Resources/Fonts/ja/。
public static class LocFonts
{
    static readonly Dictionary<string, string> Map = new Dictionary<string, string>
    {
        { "MainFont", "Fonts/ja/ZenKakuBold SDF" },   // MainFont 是用 HarmonyOS 的 Bold 生成的，所以这里也用 Bold
        { "WenKai SDF", "Fonts/ja/Klee SDF" },
        { "KuaiLe SDF", "Fonts/ja/KleeSemi SDF" },
    };
    static readonly Dictionary<string, TMP_FontAsset> loaded = new Dictionary<string, TMP_FontAsset>();
    static readonly Dictionary<TMP_Text, (TMP_FontAsset font, Material mat)> original = new Dictionary<TMP_Text, (TMP_FontAsset, Material)>();   // 换掉之前的字体和材质（换回中文时用）

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        Canvas.willRenderCanvases += Flush;
        Loc.Changed += ApplyAll;
        ApplyAll();
    }

    static void OnTextChanged(Object o) { if (o is TMP_Text t) Apply(t); }

    // 换字体是在 TMP「排完字」的事件里做的，那一刻它不会再重排：旧字体留下的子网格（中文字体第 2、3 张图集上的字）还挂着，
    // 会用旧图集画出乱码（S、日 变成花的或空白）。所以记下来，等这一帧画面渲染之前强制完整重排一次
    static readonly HashSet<TMP_Text> pending = new HashSet<TMP_Text>();
    static void Flush()
    {
        if (pending.Count == 0) return;
        var list = new List<TMP_Text>(pending); pending.Clear();
        foreach (var t in list) if (t != null) t.ForceMeshUpdate(true, true);
    }

    static void ApplyAll()
    {
        foreach (var t in Object.FindObjectsOfType<TMP_Text>(true)) Apply(t);
    }

    static void Apply(TMP_Text t)
    {
        if (t == null || t.font == null) return;
        if (Loc.Lang == "ja")
        {
            if (!Map.TryGetValue(t.font.name, out string path)) return;   // 不在表里（比如巨大的空心数字）：不动
            var ja = Load(path); if (ja == null) return;
            var oldMat = t.fontSharedMaterial;
            bool custom = oldMat != null && oldMat != t.font.material;   // 自己调过材质（比如空心描边的大数字）
            original[t] = (t.font, oldMat);
            t.font = ja;                                                  // 换完以后它的字体名不在表里了，不会反复换
            pending.Add(t);
            if (custom)
            {
                // 换字体会换上新字体的默认材质，描边就丢了：复制一份原来的材质，只把「字从哪张图集里取」换成日文字体的
                var m = new Material(oldMat);
                foreach (int id in new[] { ShaderUtilities.ID_GradientScale, ShaderUtilities.ID_TextureWidth, ShaderUtilities.ID_TextureHeight })
                    m.SetFloat(id, ja.material.GetFloat(id));
                m.SetTexture(ShaderUtilities.ID_MainTex, ja.material.GetTexture(ShaderUtilities.ID_MainTex));
                t.fontSharedMaterial = m;
            }
        }
        else if (original.TryGetValue(t, out var zh))
        {
            original.Remove(t);
            t.font = zh.font;
            if (zh.mat != null) t.fontSharedMaterial = zh.mat;
            pending.Add(t);
        }
    }

    static TMP_FontAsset Load(string path)
    {
        if (!loaded.TryGetValue(path, out var f)) loaded[path] = f = Resources.Load<TMP_FontAsset>(path);
        return f;
    }
}
