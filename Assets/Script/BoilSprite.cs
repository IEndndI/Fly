using UnityEngine;
using UnityEngine.UI;

// 蜡笔抖动：把几张线稿按固定帧率轮流显示
public class BoilSprite : MonoBehaviour
{
    public Sprite[] frames;     // 在 Inspector 里拖 3 张线稿进来
    public float fps = 12f;     // 每秒换几张

    Image image;
    float timer;                // 距离上次换图过了几秒
    int index;                  // 现在显示第几张

    void Start()
    {
        image = GetComponent<Image>();              // 空1：要拿哪个组件？
    }

    void Update()
    {
        if (frames.Length == 0) return;   // 没有线稿：这一帧什么都不做
        float rate = SaveSystem.DreamFps > 0.1f ? SaveSystem.DreamFps : fps;   // 设置里的「梦境帧率」（分镜同款）
        timer += Time.deltaTime;                         // 空2：这个你在 FadeVolume 里写过
        if (timer >= 1f / rate)
        {
            timer -= 1f / rate;
            index = (index + 1) % frames.Length;             // 空3：转到最后一张后回到 0
            image.sprite = frames[index];
        }
    }

        // 别的脚本叫它换一套线稿；传空数组 = 这张背景没有线稿
    public void SetFrames(Sprite[] newFrames)
    {
        frames = newFrames;
        index = 0;
        timer = 0f;
        if (image == null) image = GetComponent<Image>();   // 可能 Start 还没来得及跑
        image.enabled = frames.Length > 0;                // 空1：有线稿才显示
        if (image.enabled) image.sprite = frames[0];      // 空2：马上换成新的第一张
    }
}