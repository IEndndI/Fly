using UnityEngine;

// 存档和设置（都放在 PlayerPrefs 里，关掉游戏也还在）。
// 存档只记一件事：读到了表格的第几行。每句台词开始时自动存一次；快捷栏的 Save 是手动再存一次并给个提示。
// 读档时 DialogueManager 从这一行所在那一段的 label 开始，把背景、音乐、各种效果的状态快进到这一行（RestoreUpTo）。
public static class SaveSystem
{
    const string KeyRow = "fly.save.row", KeyTime = "fly.save.time";

    public static bool HasSave => PlayerPrefs.HasKey(KeyRow);
    public static int SavedRow => PlayerPrefs.GetInt(KeyRow, -1);
    public static string SavedTime => PlayerPrefs.GetString(KeyTime, "");

    // 标题界面点「继续」时填上，游戏场景的 DialogueManager.Start 读到以后从这一行开始
    public static int PendingRow = -1;

    public static void Save(int row)
    {
        if (row < 0) return;
        PlayerPrefs.SetInt(KeyRow, row);
        PlayerPrefs.SetString(KeyTime, System.DateTime.Now.ToString("yyyy.MM.dd HH:mm"));
        WriteSlotKeys(0, row, true);        // 「折角 · 自动」= 0 号档（分镜的自动档）
        PlayerPrefs.Save();
    }

    // ───────── 12 个档位（分镜第 7 幕：0 号是自动档，1~11 手动）─────────
    public const int SlotCount = 12;
    public static int SlotRow(int i)
    {
        int r = PlayerPrefs.GetInt("fly.slot" + i + ".row", -1);
        if (i == 0 && r < 0 && HasSave) r = SavedRow;      // 旧存档迁移：老的「继续」行号当 0 号档
        return r;
    }
    public static string SlotDate(int i)
    {
        string d = PlayerPrefs.GetString("fly.slot" + i + ".date", "");
        if (i == 0 && d == "" && HasSave) d = SavedTime;
        return d;
    }
    public static bool SlotAuto(int i)
    {
        if (i == 0 && !PlayerPrefs.HasKey("fly.slot0.row")) return true;   // 0 号天生是自动档
        return PlayerPrefs.GetInt("fly.slot" + i + ".auto", 0) == 1;
    }

    public static void WriteSlot(int i, int row, bool auto)
    {
        WriteSlotKeys(i, row, auto);
        PlayerPrefs.Save();
    }

    static void WriteSlotKeys(int i, int row, bool auto)
    {
        PlayerPrefs.SetInt("fly.slot" + i + ".row", row);
        PlayerPrefs.SetString("fly.slot" + i + ".date", System.DateTime.Now.ToString("yyyy.MM.dd HH:mm"));
        PlayerPrefs.SetInt("fly.slot" + i + ".auto", auto ? 1 : 0);
    }

    // ───────── 设置（默认值和分镜一样）─────────
    public static float TextDelay { get => PlayerPrefs.GetFloat("fly.set.text", 0.04f); set => PlayerPrefs.SetFloat("fly.set.text", value); }  // 每个字几秒
    public static float AutoDelay { get => PlayerPrefs.GetFloat("fly.set.auto", 1f); set => PlayerPrefs.SetFloat("fly.set.auto", value); }    // 自动翻页：打完字再等几秒
    public static float Music { get => PlayerPrefs.GetFloat("fly.set.music", 0.7f); set => PlayerPrefs.SetFloat("fly.set.music", value); }     // 0~1
    public static float Sfx { get => PlayerPrefs.GetFloat("fly.set.sfx", 1f); set => PlayerPrefs.SetFloat("fly.set.sfx", value); }             // 0~1
    public static float Box { get => PlayerPrefs.GetFloat("fly.set.box", 1f); set => PlayerPrefs.SetFloat("fly.set.box", value); }             // 对话条不透明度 0.4~1（分镜的「对话条」）
    public static float Ambience { get => PlayerPrefs.GetFloat("fly.set.amb", 1f); set => PlayerPrefs.SetFloat("fly.set.amb", value); }        // 环境声 0~1（分镜的「环境声」）
    public static float DreamFps { get => PlayerPrefs.GetFloat("fly.set.fps", 12f); set => PlayerPrefs.SetFloat("fly.set.fps", value); }       // 蜡笔抖动帧率 6/8/12/24（分镜的「梦境帧率」）

    public static void ResetSettings()
    {
        TextDelay = 0.04f; AutoDelay = 1f; Music = 0.7f; Sfx = 1f; Box = 1f; Ambience = 1f; DreamFps = 12f;
    }

    // 分镜里音乐总线 = 设置 × 0.5（默认 70% → 0.35），表格里写的 vol(0.35) 就是按默认值算的，这里换算回倍数
    public static float MusicScale => Music / 0.7f;
}
