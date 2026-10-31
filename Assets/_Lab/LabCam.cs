using UnityEngine;

// 临时跟随摄像机：跟在目标身后、看着它。第 5 课换成 Cinemachine，现在先不用研究。
public class LabCam : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2.5f, -8f);   // 身后 8 米、上方 2.5 米
    public float smooth = 15f;
    [Range(0f, 1f)] public float rollAmount = 0.3f;        // 镜头跟着飞机侧倾多少：0 = 地平线永远水平，1 = 完全跟着转（最晕）

    void LateUpdate()
    {
        if (target == null) return;
        // 镜头自己的"上"：在世界的上和飞机的上之间取 rollAmount。位置和朝向都按这个算，
        // 不然位置跟着飞机全转、朝向只转三成，看起来飞机像绕着头顶上的一根轴在转
        Vector3 up = Vector3.Slerp(Vector3.up, target.up, rollAmount);
        Quaternion frame = Quaternion.LookRotation(target.forward, up);
        Vector3 want = target.position + frame * offset;
        transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        transform.LookAt(target.position + up * 1f, up);
    }
}
