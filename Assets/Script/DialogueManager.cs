using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

[System.Serializable]

public class DialogueLine
{
    public string flow;
    public string speakerName;  // null=沿用，""={clear}，其他=设置
    public string text;         // null 或 ""=纯效果行，其他=台词
    public string characterSpriteName;
    public string backgroundSpriteName;
    public string hideTarget;
    public string effectString;
    public string bgmName;      // null=沿用，"{clear}"=停止，其他=切换到这首
    public string ambient;      // null=不变，其他=要开/关的环境音（可逗号并列）
    public string lineType;     // null=沿用，其他=台词 / 心声 / 旁白 / 书中
}

public class Command
{
    public string name;
    public float duration = -1f;
    public bool hasPos = false;   // 这条指令有没有写位置
    public Vector2 pos;           // 位置（AE 坐标）
    public string arg;             //（argument，参数的意思）
    public HashSet<string> flags = new HashSet<string>();
    public Dictionary<string, string> namedArgs = new Dictionary<string, string>();
}

public class DialogueManager : MonoBehaviour
{
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;
    public CharacterStage stage;
    public Image backgroundImage;
    public BoilSprite backgroundLine;   // 背景上面那层会抖的线稿
    public EffectPlayer effectPlayer;
    public ChoiceMenu choiceMenu;
    public AudioPlayer audioPlayer;
    public AutoLock autoLock;           // 自动播放锁，留空会自己找
    public static float blockUntil;     // 在这之前点击无效（白纸、结尾这类不许被点掉的演出用）
    public static string currentLine;   // 正在打的这句（白纸页要把它写在纸中间）
    public int CurrentIndex { get; private set; } = -1;   // 正在演的这句在表格里是第几行（四角小字 GameChrome 读）
    public string CurrentLineType => currentLineType;     // 台词 / 心声 / 旁白 / 书中（BookMode 读）
    public static bool autoMode, skipMode;               // 快捷栏的 Auto / Skip（QuickMenu 切换）

    public List<DialogueLine> dialogueLines;
    public DialogueLoader loader;
    public string dialogueFileName = "Fly";
    public float typingSpeed = 0.05f;

    private bool isTyping = false;
    private string currentSpeaker = "";
    private string currentLineType = "旁白";   // 沿用到下一个写了类型的格子
    private bool isSkipping = false;
    private bool waitingForClick = false;
    private bool clickReceived = false;


    private Dictionary<string, int> labels = new Dictionary<string, int>();

    void BuildLabels()
    {
        labels.Clear();
        for (int i = 0; i < dialogueLines.Count; i++)
        {
            if (string.IsNullOrEmpty(dialogueLines[i].flow)) continue;
            string flow = dialogueLines[i].flow;
            if (flow.StartsWith("label:"))
            {
                string labelName = flow.Substring(6);
                if (labels.ContainsKey(labelName))
                    Debug.LogWarning("标签重名：" + labelName + "，后面的会覆盖前面的");
                labels[labelName] = i;
                Debug.Log("找到标签：" + labelName + "，在第 " + i + " 行");
            }
        }
    }

    void CheckLabels()
    {
        foreach (DialogueLine line in dialogueLines)
        {
            string flow = line.flow;
            if (string.IsNullOrEmpty(flow)) continue;

            string target = null;
            if (flow.StartsWith("jump:"))
            {
                target = flow.Substring(5);   // 跟 RunFrom 里真正跳转时的取法保持一致，检查才有意义
            }
            else if (flow.StartsWith("option:"))
            {
                string[] parts = flow.Substring(7).Split('>');
                if (parts.Length < 2)
                {
                    Debug.LogWarning("选项缺少「>目标」：" + flow);
                    continue;
                }
                target = parts[1].Trim();
            }

            if (target != null && !labels.ContainsKey(target))
                Debug.LogWarning("找不到标签「" + target + "」：" + flow);
        }
    }

    public Command LoadCommand(string command)
    {
        // 全角符号已经在 DialogueLoader.NormalizeCommand 里统一换成半角了
        string[] tokens = command.Split(':');

        Command newLine = new Command();

        string rawName = tokens[0].Trim();
        if (!string.IsNullOrEmpty(rawName)) newLine.name = rawName;

        for (int i = 1; i < tokens.Length; i++)
        {
            string token = tokens[i].Trim();
            if (token.Length < 1)
            {
                Debug.LogWarning("Command数据格式不完整" );
                continue;
            }
                
                

            if (float.TryParse(token, out float result))
            {
                newLine.duration = result;
            }

            else if (token.StartsWith("pos(") && token.EndsWith(")"))
            {
                string inner = token.Substring(4, token.Length - 5); // 取出括号里的 "960,540"
                string[] xy = inner.Split(',');
                if (xy.Length == 2 && float.TryParse(xy[0], out float x) && float.TryParse(xy[1], out float y))
                {
                    newLine.hasPos = true;
                    newLine.pos = new Vector2(x, y);
                }
                else
                {
                    Debug.LogWarning("pos 格式应为 pos(x,y)：" + token);
                }
            }
            else if (token.Contains("(") && token.EndsWith(")"))
            {
                int open = token.IndexOf('(');                       // 左括号在第几位
                string key = token.Substring(0, open);               // 括号前面："out"
                string value = token.Substring(open + 1, token.Length - open - 2);      // 括号里面："1"
                newLine.namedArgs[key] = value;
            }

            else if (token == "left" || token == "center" || token == "right")
            {
                newLine.hasPos = true;
                if (token == "left") newLine.pos = new Vector2(480, 540);
                else if (token == "center") newLine.pos = new Vector2(960, 540);
                else newLine.pos = new Vector2(1440, 540);
            }
            else if (token == "full" || token == "stop")
            {
                newLine.flags.Add(token);
            }

            else
            {
                if (newLine.arg == null)
                {
                    newLine.arg = token;
                }
                else
                {
                    Debug.LogWarning("警告：认不出这个参数" + tokens[i]);
                }
            }
            

        }
        return newLine;
    }

    List<string> SplitTopLevel(string text, char separator)
    {
        List<string> result = new List<string>();
        string current = "";
        int depth = 0;

        foreach (char letter in text)
        {
            if (letter == '(' || letter == '[')
            {
                depth ++;
            }
            else if (letter == ')' || letter == ']')
            {
                depth --;
            }

            if ((letter == separator ) && depth == 0)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += letter;
            }
            
        }
        result.Add(current);
        return result;
    }

    void Start()
    {
        LoadSpriteIndex();
        if (autoLock == null) autoLock = FindObjectOfType<AutoLock>();
        dialogueLines = loader.LoadDialogueFromFile(dialogueFileName);
        BuildLabels();
        CheckLabels();
        int start = 0;
        if (SaveSystem.PendingRow >= 0 && SaveSystem.PendingRow < dialogueLines.Count)   // 标题点了「继续」
        {
            start = SaveSystem.PendingRow; SaveSystem.PendingRow = -1;
            RestoreUpTo(start);
        }
        StartCoroutine(RunFrom(start));
    }

    void Update()
    {
        if (Time.time < blockUntil) return;              // 演出还没完：点击不跳过、不加速
        if (SysPanels.UiOpen > 0) return;                // 回想 / 设置开着
        if (Input.GetMouseButtonDown(0) && PointerOnQuickMenu()) return;   // 点的是快捷栏按钮，不算翻页
        if (autoLock != null && autoLock.IsLocked) { autoMode = skipMode = false; }   // 自动播放锁优先
        if (autoLock != null && autoLock.IsLocked)       // 自动播放中：点击不跳过、不加速，只晃一下锁
        {
            if (Input.GetMouseButtonDown(0)) autoLock.Nudge();
            return;
        }
        if (Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                isSkipping = true;
            }
            else if (waitingForClick)
            {
                clickReceived = true;
            }
        }

        // 键盘（分镜同款）：空格 / 回车 = 点击翻页；A = 自动开关；Esc = 合上书回标题
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (isTyping) isSkipping = true;
            else if (waitingForClick) clickReceived = true;
        }
        if (Input.GetKeyDown(KeyCode.A)) { autoMode = !autoMode; skipMode = false; }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (effectPlayer != null && effectPlayer.CardBusy) return;   // 扉页正演着：先让它演完
            if (effectPlayer != null && effectPlayer.chapterCard != null) effectPlayer.chapterCard.ExitToTitle();
        }
    }

    // 台词列为空（没填或{clear}）的行，就是纯效果行
    bool IsEffectRow(DialogueLine line)
    {
        return string.IsNullOrEmpty(line.text) && (line.flow == null || !line.flow.StartsWith("option:"));
    }

    bool IsOptionRow(DialogueLine line)
    {
        return line.flow != null && line.flow.StartsWith("option:");
    }

    IEnumerator RunFrom(int index)
    {
        while (index < dialogueLines.Count)
        {
            DialogueLine line = dialogueLines[index];

            if (IsOptionRow(line))
            {
                List<string> texts = new List<string>();
                List<string> targets = new List<string>();

                while (index < dialogueLines.Count && IsOptionRow(dialogueLines[index]))
                {
                    string body = dialogueLines[index].flow.Substring(7);
                    string[] parts = body.Split('>');
                    texts.Add(parts[0].Trim());
                    targets.Add(parts[1].Trim());
                    index++;
                }

                skipMode = false;                              // 到抉择就停下 Skip
                choiceMenu.Show(texts);
                while (choiceMenu.chosenIndex == -1) yield return null;

                string targetLabel = targets[choiceMenu.chosenIndex];
                choiceMenu.Clear();
                if (audioPlayer != null) audioPlayer.PlaySfx("Fly/sfx_select");

                if (labels.TryGetValue(targetLabel, out int target)) index = target;
                else Debug.LogWarning("找不到标签：" + targetLabel);
                continue;
            }

            // 把这一行，连同紧跟在它后面的所有纯效果行，打包成一个"演出序列"
            List<DialogueLine> sequence = new List<DialogueLine>();
            sequence.Add(line);
            int next = index + 1;
            // label 行开始新的一段，不打包进上一句（不然下一章的扉页会在这一章最后一句一开始就落下来）
            while (next < dialogueLines.Count && IsEffectRow(dialogueLines[next])
                   && !(dialogueLines[next].flow != null && dialogueLines[next].flow.StartsWith("label:")))
            {
                sequence.Add(dialogueLines[next]);
                next++;
            }

            // 检查这个序列里有没有 wait:click
            bool sequenceHasWaitClick = false;
            foreach (DialogueLine row in sequence)
            {
                if (row.effectString != null && row.effectString.Contains("wait:click"))
                    sequenceHasWaitClick = true;
            }

            if (IsEffectRow(line))
            {
                // 情况1：这一段开头就没有台词 → 整段演出播完自动翻
                yield return StartCoroutine(RunSequence(sequence));
            }
            else
            {
                // 情况2：有台词 → 演出和打字同时开始
                bool autoAdvance = false;
                string typedText = line.text;
                if (typedText.EndsWith("{nw}"))
                {
                    autoAdvance = true;
                    typedText = typedText.Substring(0, typedText.Length - 4);
                }

                currentLine = typedText; CurrentIndex = index;
                SaveSystem.Save(index);                        // 每句开始时自动存档（标题的「继续」从这里接）
                Coroutine sequenceRun = StartCoroutine(RunSequence(sequence)); // 不等它，让它在后台跑
                yield return StartCoroutine(TypeText(typedText));              // 等打字完成

                if (sequenceHasWaitClick)
                {
                    yield return sequenceRun; // 点击交给演出用，等整段演出播完再自动翻
                }
                else if (autoLock != null && autoLock.IsLocked)
                {
                    for (float w = 0f, need = autoLock.ReadTime(typedText); w < need; w += Time.deltaTime * FlightVideo.Speed) yield return null; // 锁住时：留够阅读时间，自己翻（长按 1.5 倍）
                    while (FlightVideo.Instance != null && FlightVideo.Instance.Active && !FlightVideo.Instance.AtSegmentEnd) yield return null;   // 飞行画面也走完这句的那一段
                    if (audioPlayer != null) audioPlayer.PlaySfx("Fly/sfx_page");
                }
                else if (!autoAdvance)
                {
                    yield return StartCoroutine(WaitForClick()); // 默认：等点击翻下一句
                }
            }

            index = next; // 跳过已经打包进序列的效果行

            foreach (DialogueLine row in sequence)
            {
                if (string.IsNullOrEmpty(row.flow)) continue;
                else if (row.flow.StartsWith("label:"))
                {
                    // 标签行不做任何事
                }
                
                else if (row.flow.StartsWith("jump:"))
                {
                    string labelName = row.flow.Substring(5);
                    if (labels.TryGetValue(labelName, out int target))
                    {
                        index = target;
                        Debug.Log("跳转到标签：" + labelName + "，第 " + index + " 行");
                        break;
                    }
                    else
                    {
                        Debug.LogWarning("找不到标签：" + labelName);
                    }
                }

                else if (row.flow == "end" || row.flow.StartsWith("end:"))
                {
                    Debug.Log("剧情结束");
                    if (row.flow.StartsWith("end:"))      // end:a / end:b：先播这一支的结尾，再回标题
                    {
                        EndingPlayer ending = FindObjectOfType<EndingPlayer>();
                        if (ending != null) yield return StartCoroutine(ending.Play(row.flow.Substring(4).Trim(), audioPlayer, LoadCommand));
                        else Debug.LogWarning("场景里没有 EndingPlayer");
                    }
                    SceneManager.LoadScene(0);
                    yield break;
                }

                else
                {
                    Debug.LogWarning("未知 flow 指令：" + row.flow);
                }
            }
        }
    }

    // 按顺序执行序列里的每一行：应用画面变化 → 播这一行的效果并等它结束 → 下一行
    IEnumerator RunSequence(List<DialogueLine> rows)
    {
        string seqSpeaker;
        seqSpeaker = currentSpeaker;
        foreach (DialogueLine row in rows)
        {
            if (row.speakerName != null) seqSpeaker = row.speakerName; // null=沿用，""={clear}，其他=设置
            ApplySpeakerAndVisuals(row, seqSpeaker);
            yield return StartCoroutine(PlayEffects(row.effectString , seqSpeaker));
        }
    }

        // 图片索引：文件名 → Resources 里的路径（编辑器自动生成的 SpriteIndex.txt）
    private Dictionary<string, string> spriteIndex = new Dictionary<string, string>();

    void LoadSpriteIndex()
    {
        TextAsset file = Resources.Load<TextAsset>("SpriteIndex");
        if (file == null) { Debug.LogWarning("找不到 SpriteIndex.txt"); return; }
        foreach (string row in file.text.Split('\n'))
        {
            string[] parts = row.TrimEnd('\r').Split('\t');
            if (parts.Length < 2) continue;     // 空1：空行、格式不对的就跳过
            spriteIndex[parts[0]] = parts[1];   // 空2
        }
    }

        // 安静地找：先当路径找，找不到再查索引（只写文件名也行）
    Sprite FindSprite(string spriteName)
    {
        Sprite sprite = Resources.Load<Sprite>(spriteName);
        if (sprite == null && spriteIndex.TryGetValue(spriteName, out string path))
            sprite = Resources.Load<Sprite>(path);   // 空3
        return sprite;
    }

    Sprite LoadSprite(string spriteName)
    {
        Sprite sprite = FindSprite(spriteName);
        if (sprite == null) Debug.LogWarning("找不到图片" + spriteName);
        return sprite;
    }

    void ApplySpeakerAndVisuals(DialogueLine line , string speaker)
    {

        if (line.speakerName != null)
        {
            speakerNameText.text = line.speakerName;
            currentSpeaker = line.speakerName;
            StartCoroutine(Tweens.Tween(0.4f, Tweens.Bezier(0.34f, 1.56f, 0.64f, 1f),
                p => namePlate.rectTransform.anchoredPosition = new Vector2(-24f * (1f - p), 0f)));
        }
        if (line.text == "") dialogueText.text = "";

        if (line.lineType != null)                // 空2：什么情况才算「这一格写了东西」？
        {
            currentLineType = line.lineType;
            ApplyLineType(currentLineType);
        }

        // 先隐藏、再显示：同一行里"全员下场 + 新人登场"才能按预期工作
        if (!string.IsNullOrEmpty(line.hideTarget)) HideCharacters(line.hideTarget,speaker);
        if (!string.IsNullOrEmpty(line.characterSpriteName)) ShowCharacters(line.characterSpriteName,speaker);

        if (!string.IsNullOrEmpty(line.backgroundSpriteName))
        {
            string bgName = line.backgroundSpriteName;
            // 新格式：bgName_base 是水彩底，bgName_line0~2 是三张线稿
            Sprite bgBase = FindSprite(bgName + "_base");
            if (bgBase != null)
            {
                if (backgroundImage.sprite != bgBase && !HardCutBg(line)) BgFade.Snapshot(backgroundImage, backgroundLine);   // 换背景：旧的淡出、新的落定（章节开头 / 视频刚结束：直接切）
                backgroundImage.sprite = bgBase;
                Sprite[] lines = new Sprite[3];
                for (int i = 0; i < 3; i++)                       // 空3
                    lines[i] = LoadSprite(bgName + "_line" + i);  // 空4
                backgroundLine.SetFrames(lines);
            }
            else
            {
                // 旧的单张背景：照原来的做法，并且把线稿层藏起来
                Sprite bg = LoadSprite(bgName);
                if (bg != null) backgroundImage.sprite = bg;
                backgroundLine.SetFrames(new Sprite[0]);
            }
        }

        if (!string.IsNullOrEmpty(line.bgmName))
        {
            audioPlayer.PlayBGM(LoadCommand(line.bgmName));
        }

        if (!string.IsNullOrEmpty(line.ambient))
        {
            ApplyAmbient(line.ambient);
        }
    }

    // 立绘列：可能是 "微笑"、"小红_微笑:pos(1440,540)"、或用逗号并列的多个
    void ShowCharacters(string cell , string speaker)
    {
        foreach (string part in SplitTopLevel(cell, ','))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            Command cmd = LoadCommand(part.Trim());

            string characterName;
            string fileName;
            if (cmd.name.Contains("_"))
            {
                characterName = cmd.name.Split('_')[0]; // "小红_微笑" → "小红"
                fileName = cmd.name;
            }
            else if (string.IsNullOrEmpty(speaker))
            {
                Debug.LogWarning("简写立绘需要说话人：" + part);
                continue;
            }
            else
            {
                characterName = speaker;
                fileName = speaker + "_" + cmd.name;
            }

                        // 新格式：Fly/Char/ 下的 文件名_base + 文件名_line0~2；找不到就当旧立绘
            Sprite[] lines = new Sprite[0];
            Sprite sprite = FindSprite(fileName + "_base");
            if (sprite != null)
            {
                lines = new Sprite[3];
                for (int i = 0; i < lines.Length; i++)
                    lines[i] = FindSprite(fileName + "_line" + i);
            }
            else sprite = LoadSprite(fileName);

            float height = 0;   // 0 = 表里没写 h()，保持预制体原来的大小
            if (cmd.namedArgs.TryGetValue("h", out string hText)) float.TryParse(hText, out height);   // 空4
            if (sprite != null) stage.Show(characterName, sprite, lines, cmd.hasPos, cmd.pos, height);  // 空5
        }
    }

    // 隐藏列：TRUE = 当前说话人，all = 所有人，其他 = 角色名（可逗号并列）
    void HideCharacters(string cell , string speaker)
    {
        foreach (string part in SplitTopLevel(cell, ','))
        {
            string target = part.Trim();
            if (target == "") continue;

            if (target.ToUpper() == "TRUE")
            {
                if (!string.IsNullOrEmpty(speaker)) stage.Hide(speaker);
            }
            else if (target.ToLower() == "all")
            {
                stage.HideAll();
            }
            else
            {
                stage.Hide(target);
            }
        }
    }
    
    // 鼠标是不是点在快捷栏的按钮上
    bool PointerOnQuickMenu()
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, hits);
        foreach (var h in hits) if (h.gameObject.GetComponentInParent<QuickMenu>() != null) return true;
        return false;
    }

    // 读档：从这一行所在那一段的 label 开始，把背景、音乐、句子类型、说话人和会持续的效果（锁、书页散开、梦、夜路、显示/隐藏）快进到这一行
    void RestoreUpTo(int row)
    {
        int s = row;
        while (s > 0 && !(dialogueLines[s].flow != null && dialogueLines[s].flow.StartsWith("label:"))) s--;
        string bg = null, bgm = null, type = null, speaker = null;
        for (int i = s; i < row; i++)
        {
            DialogueLine l = dialogueLines[i];
            if (!string.IsNullOrEmpty(l.backgroundSpriteName)) bg = l.backgroundSpriteName;
            if (!string.IsNullOrEmpty(l.bgmName)) bgm = l.bgmName;
            if (l.lineType != null) type = l.lineType;
            if (l.speakerName != null) speaker = l.speakerName;
            if (string.IsNullOrEmpty(l.effectString)) continue;
            foreach (string raw in SplitTopLevel(l.effectString, ','))
            {
                string t = raw.Trim();
                if (t.StartsWith("lock:") || t.StartsWith("flight:") || t.StartsWith("fx:") || t.StartsWith("night:") || t.StartsWith("show:") || t.StartsWith("hide:") || (t.StartsWith("dream:") && t != "dream:pass"))
                    effectPlayer.Play(LoadCommand(t), null);
            }
        }
        ApplySpeakerAndVisuals(new DialogueLine { backgroundSpriteName = bg, bgmName = bgm, lineType = type, speakerName = speaker }, speaker ?? currentSpeaker);
    }

    // 跳到表格的某一行（回想里点一句、读档页读档用）：先把会一直赖着的状态全部收掉，再从那里重开
    public void JumpTo(int row)
    {
        if (dialogueLines == null || row < 0 || row >= dialogueLines.Count) return;
        StopAllCoroutines();
        autoMode = skipMode = false;
        isTyping = false; isSkipping = false; waitingForClick = false; clickReceived = false;
        if (autoLock != null) autoLock.Set(false);
        if (effectPlayer != null)
        {
            effectPlayer.StopAllCoroutines();
            if (effectPlayer.pageFx != null) effectPlayer.pageFx.Set(0);
            if (effectPlayer.dreamFx != null) effectPlayer.dreamFx.Command("off");
            if (effectPlayer.nightFx != null) effectPlayer.nightFx.Command("off");
            if (effectPlayer.blankPage != null) effectPlayer.blankPage.Hide();
            foreach (GameObject go in effectPlayer.sceneObjects) if (go != null) go.SetActive(false);
            if (effectPlayer.chapterCard != null) effectPlayer.chapterCard.ResetImmediate();
            if (SceneCam.instance != null) SceneCam.instance.SnapHome();     // 镜头回原位：别的章的姿势别带过来
        }
        RestoreUpTo(row);
        StartCoroutine(RunFrom(row));
    }

    IEnumerator WaitForClick()
    {
        waitingForClick = true;
        clickReceived = false;
        float t0 = Time.time;
        while (!clickReceived)
        {
            if (skipMode && Time.time - t0 > 0.09f) break;                                                        // Skip：几乎不停
            if (autoMode && Time.time - t0 > SaveSystem.AutoDelay + 0.038f * (currentLine ?? "").Length) break;   // Auto：按字数多等一会儿
            yield return null;
        }
        waitingForClick = false;
        clickReceived = false;
        if (audioPlayer != null) audioPlayer.PlaySfx("Fly/sfx_page");   // 翻页声
    }

    // 章节一开头（label 之后还没出过台词：扉页盖着）、飞行视频收尾那一行：背景直接切，不做淡变
    bool HardCutBg(DialogueLine line)
    {
        if (line.effectString != null && line.effectString.Contains("flight:")) return true;
        int i = dialogueLines.IndexOf(line);
        for (int k = i - 1; k >= 0; k--)
        {
            var L = dialogueLines[k];
            if (L.flow != null && L.flow.StartsWith("label:")) return true;
            if (!string.IsNullOrEmpty(L.text) && (L.flow == null || !L.flow.StartsWith("option:"))) return false;
        }
        return true;
    }

    // 按 {w} 分段打字，段与段之间等点击
    IEnumerator TypeText(string content)
    {
        dialogueText.text = "";
        string[] segments = content.Split(new string[] { "{w}" }, System.StringSplitOptions.None);

        for (int s = 0; s < segments.Length; s++)
        {
            if (s > 0) yield return StartCoroutine(WaitForClick());
            yield return StartCoroutine(TypeSegment(segments[s]));
        }
    }

    IEnumerator TypeSegment(string segment)
    {
        isTyping = true;
        isSkipping = false;
        string textBefore = dialogueText.text;

        foreach (char letter in segment)
        {
            if (skipMode) isSkipping = true;
            if (isSkipping)
            {
                dialogueText.text = textBefore + segment;
                break;
            }
            dialogueText.text += letter;
            if (audioPlayer != null && "，。、！？“”…—：；（） ".IndexOf(letter) < 0)   // 每写一个字一声铅笔（标点不响）
                audioPlayer.PlaySfx("Fly/sfx_pencil" + Random.Range(0, 4));
            yield return new WaitForSeconds(SaveSystem.TextDelay / FlightVideo.Speed);   // 文字速度在设置里调（原来的 typingSpeed 不再用）
        }
        isTyping = false;
    }

    // 播放一行的效果，并等这一行结束（时长 = 所有效果和 wait 里最长的那个）
    IEnumerator PlayEffects(string effectString, string speaker)
    {
        if (string.IsNullOrEmpty(effectString)) yield break;

        List<string> tokens = SplitTopLevel(effectString,',');

        List<Command> result = new List<Command>();
        HashSet<string> usedNames = new HashSet<string>();

        bool hasWaitClick = false;
        bool hasWaitSeconds = false;
        float waitSeconds = 0f;
        RectTransform target = stage.GetTarget(speaker);
        foreach (string rawToken in tokens)
        {
            string token = rawToken.Trim();
            if (token == "wait:click") hasWaitClick = true;
            else if (token.StartsWith("wait:"))
            {
                float.TryParse(token.Substring(5), out waitSeconds);
                hasWaitSeconds = true;
            }
            else if (!string.IsNullOrEmpty(token)) 
                {
                    Command cmd = LoadCommand(token);

                    if (!usedNames.Contains(cmd.name))
                    {
                        usedNames.Add(cmd.name);
                        result.Add(cmd);
                    }
                    
                }
        }

        foreach (Command cmd in result)
        {
            effectPlayer.Play(cmd, target);
        }

        // 章扉页自己管时长：点击可以加速（分镜同款），不等固定秒数
        if (effectPlayer.CardBusy)
        {
            while (effectPlayer.CardBusy) yield return null;
            yield break;
        }

        if (hasWaitClick)
        {
            yield return StartCoroutine(WaitForClick());
            yield break;
        }

        float duration = hasWaitSeconds ? waitSeconds : 0f;
        foreach (Command cmd in result)
        {
            duration = Mathf.Max(duration, effectPlayer.GetDuration(cmd));
        }
        yield return new WaitForSeconds(duration);
    }

    void ApplyAmbient(string cell)
    {
        foreach (string part in SplitTopLevel(cell, ','))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            Command cmd = LoadCommand(part.Trim());

            if (cmd.flags.Contains("stop")) audioPlayer.StopAmbient(cmd);
            else audioPlayer.PlayAmbient(cmd);
        }
    }

        // 名牌三态：台词 = 钴蓝实心名牌，心声 = 纸白描边，旁白 / 书中 = 不显示名牌
    public Image namePlate;                       // 名牌底图
    public UnityEngine.UI.Outline namePlateLine;  // 描边（心声用）
    public TMPro.TextMeshProUGUI modeLabel;       // 左下角的类型小字
    Color cobalt = new Color(0f, 0.278f, 0.671f);
    Color paper  = new Color(0.949f, 0.925f, 0.875f);

    void ApplyLineType(string type)
    {
        switch (type)
        {
            case "台词":
                namePlate.gameObject.SetActive(true);
                namePlate.color = cobalt;
                speakerNameText.color = paper;
                namePlateLine.enabled = false;          // 空1：台词要不要描边？
                modeLabel.text = "台词 · LINE";
                break;
            case "心声":
                namePlate.gameObject.SetActive(true);
                namePlate.color = paper;                // 空2：底色换成什么？
                speakerNameText.color = cobalt;          // 空3：字换成什么颜色才看得见？
                namePlateLine.enabled = true;
                modeLabel.text = "心声 · THOUGHT";
                break;
            default:                                   // 旁白、书中
                namePlate.gameObject.SetActive(false);  // 空4
                modeLabel.text = type == "书中" ? "书中 · IN THE BOOK" : "旁白 · NARRATION";
                break;
        }
    }
}