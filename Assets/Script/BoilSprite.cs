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
        timer += Time.deltaTime;                         // 空2：这个你在 FadeVolume 里写过
        if (timer >= 1f / fps)
        {
            timer -= 1f / fps;
            index = (index + 1) % 3;             // 空3：转到最后一张后回到 0
            image.sprite = frames[index];
        }
    }
}