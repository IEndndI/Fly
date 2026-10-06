using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EffectPlayer : MonoBehaviour
{
    public ChapterCard chapterCard;
    public Image flashOverlay;


    public GameObject[] sceneObjects;

    public float shakeDuration = 0.4f;
    public float flashDuration = 0.3f;

    public float jumpDuration = 0.3f;
    public float jumpHeight = 30f;
    public TimelinePlayer timelinePlayer;

    public AudioPlayer audioPlayer;
    public AutoLock autoLock;          // 留空会自己找
    public BlankPage blankPage;        // 留空会自己找
    public PageFx pageFx;              // 留空会自己找
    public DreamFx dreamFx;            // 留空会自己找
    public NightFx nightFx;            // 留空会自己找

    // 章扉页：12 栏落下（咚咚咚）→ 扉页出现时一声钟；从标题色页进来的那次没有落栏，只有钟
    IEnumerator CardSounds(bool fromTitle)
    {
        if (audioPlayer == null) yield break;
        if (!fromTitle)
        {
            audioPlayer.PlaySfx("Fly/sfx_drop");
            yield return new WaitForSeconds(chapterCard.ColumnsTotal + 0.2f);
        }
        audioPlayer.PlaySfx("Fly/sfx_chapter");
    }

    // 扉页在不在演：DialogueManager 的 PlayEffects 靠它决定等多久（点击加速会提前结束）
    public bool CardBusy { get; private set; }

    IEnumerator RunCard(string id)
    {
        CardBusy = true;
        yield return chapterCard.Cover(id);
        CardBusy = false;
    }

    public IEnumerator Shake(float duration, RectTransform target)
    {
        
        if (target == null) yield break;
        if (duration < 0) 
            {
                duration = shakeDuration;
            }

        
        Vector3 originalPos = target.anchoredPosition3D;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.anchoredPosition3D = originalPos + new Vector3(Random.Range(-10f, 10f), 0, 0);
            yield return null;
        }
        target.anchoredPosition3D = originalPos;
    }

    public IEnumerator Flash(float duration)
    {
        if (flashOverlay == null) yield break;
        if (duration < 0)
            {
                duration = flashDuration;
            }
        

        float half = duration / 2f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            Color c = flashOverlay.color; c.a = Mathf.Lerp(0f, 1f, elapsed / half);
            flashOverlay.color = c;
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            Color c = flashOverlay.color; c.a = Mathf.Lerp(1f, 0f, elapsed / half);
            flashOverlay.color = c;
            yield return null;
        }
    }

    public IEnumerator Jump(float duration, RectTransform target)
    {
        if (target == null) yield break;
        if (duration < 0 )
            {
                duration = jumpDuration;
            }
        
        Vector3 originalPos = target.anchoredPosition3D;
        float elapsed = 0f;
        

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            target.anchoredPosition3D = originalPos + new Vector3(0, Mathf.Sin(progress * Mathf.PI) * jumpHeight, 0);
            yield return null;
        }
        
        target.anchoredPosition3D = originalPos;
    }

    public void Play(Command cmd, RectTransform target)
    {
        if (cmd.name == "shake") StartCoroutine(Shake(cmd.duration, target));
        else if (cmd.name == "flash") StartCoroutine(Flash(cmd.duration));
        else if (cmd.name == "jump") StartCoroutine(Jump(cmd.duration, target));
        else if (cmd.name == "play") timelinePlayer.Play(cmd.arg);
        else if (cmd.name == "se") audioPlayer.PlaySE(cmd);
        else if (cmd.name == "card")
        {
            bool fromTitle = chapterCard.EntryCoverActive;     // 要在 Cover 跑起来之前问，它会把这面旗收掉
            StartCoroutine(RunCard(cmd.arg));
            StartCoroutine(CardSounds(fromTitle));
        }
        else if (cmd.name == "uncard") { StartCoroutine(chapterCard.Reveal()); if (audioPlayer != null) audioPlayer.PlaySfx("Fly/sfx_drop"); }
        else if (cmd.name == "lock")    // lock:on / lock:off —— 自动播放锁（AutoLock）
        {
            if (autoLock == null) autoLock = FindObjectOfType<AutoLock>();
            if (autoLock != null) autoLock.Set(cmd.arg != "off");
            else Debug.LogWarning("场景里没有 AutoLock");
        }
        else if (cmd.name == "blank" || cmd.name == "unblank")   // 「他醒了。」的白纸页（BlankPage）
        {
            if (blankPage == null) blankPage = FindObjectOfType<BlankPage>();
            if (blankPage == null) Debug.LogWarning("场景里没有 BlankPage");
            else if (cmd.name == "blank") blankPage.Show(DialogueManager.currentLine);
            else blankPage.Hide();
        }
        else if (cmd.name == "fx")      // fx:lv0 … fx:lv4 —— 第 8 幕书页散开（PageFx）
        {
            if (pageFx == null) pageFx = FindObjectOfType<PageFx>();
            int lv; if (pageFx != null && cmd.arg != null && int.TryParse(cmd.arg.Replace("lv", ""), out lv)) pageFx.Set(lv);
            else Debug.LogWarning("fx 没生效：" + cmd.arg);
        }
        else if (cmd.name == "dream")   // dream:on / birds / pass / gliders / off —— 第四章的梦（DreamFx）
        {
            if (dreamFx == null) dreamFx = FindObjectOfType<DreamFx>();
            if (dreamFx != null) dreamFx.Command(cmd.arg); else Debug.LogWarning("场景里没有 DreamFx");
        }
        else if (cmd.name == "night")   // night:stars / bugs / off —— 结局 A 的夜路（NightFx）
        {
            if (nightFx == null) nightFx = FindObjectOfType<NightFx>();
            if (nightFx != null) nightFx.Command(cmd.arg); else Debug.LogWarning("场景里没有 NightFx");
        }
        else if (cmd.name == "show" || cmd.name == "hide")
        {
            foreach (GameObject go in sceneObjects)
                if (go.name == cmd.arg) go.SetActive(cmd.name == "show");
        }
        else Debug.LogWarning("未知效果：" + cmd.name);
    }

    public float GetDuration(Command cmd)
    {
        if (cmd.duration >= 0) return cmd.duration;

        if (cmd.name == "shake") return shakeDuration;
        else if (cmd.name == "flash") return flashDuration;
        else if (cmd.name == "jump") return jumpDuration;
        else if (cmd.name == "play") return timelinePlayer.GetDuration(cmd.arg);
        else if (cmd.name == "se")
        {
            if (cmd.flags.Contains("full")) return audioPlayer.GetSELength(cmd.arg);
            return 0f;
        }
        else if (cmd.name == "card") return chapterCard.ColumnsTotal + ChapterCard.CARD_HOLD;   // 实际等待走 CardBusy，不走秒数
        else if (cmd.name == "uncard") return 0.25f + chapterCard.ColumnsTotal;              // 空2
        else return 0f;
    }
}