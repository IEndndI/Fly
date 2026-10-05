using UnityEngine;
using UnityEngine.EventSystems;

// 鼠标移上去：下划线伸出来；移开：缩回去
public class ChoiceButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform underline;
    Coroutine anim;   // 遥控器

    public void OnPointerEnter(PointerEventData e) { Slide(1f); }   // 空4：伸到多长？
    public void OnPointerExit(PointerEventData e)  { Slide(0f); }   // 空5

    void Slide(float to)
    {
        if (anim != null) StopCoroutine(anim);   // 先停掉旧的，免得两个动画抢下划线
        float from = underline.localScale.x;
        anim = StartCoroutine(Tweens.Tween(0.2f, Tweens.EaseOut,
            p => underline.localScale = new Vector3(Mathf.Lerp(from, to, p), 1f, 1f)));
    }
}