using SeedeXR.Kionjo;
using UnityEngine;

/// <summary>
/// Stands in for a person in a headset so you can try Kionjo without one: the head drifts on a
/// slow figure eight, the hands circle, and a "beep" event is marked every 2 seconds.
/// Turn it off (or delete it) when you record with a real rig.
/// </summary>
public class KionjoSampleRigMover : MonoBehaviour
{
    public Transform head, leftHand, rightHand;
    public bool move = true;
    float _nextBeep = 2f;

    void Update()
    {
        if (!move || head == null) return;
        float t = Time.time;
        head.localPosition = new Vector3(Mathf.Sin(t * 0.5f) * 0.3f, 1.6f, -4f + Mathf.Cos(t * 0.25f) * 0.3f);
        head.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.3f) * 20f, 0f);
        if (leftHand != null) leftHand.localPosition = new Vector3(-0.25f + Mathf.Cos(t) * 0.05f, 1.2f + Mathf.Sin(t) * 0.05f, -3.6f);
        if (rightHand != null) rightHand.localPosition = new Vector3(0.25f + Mathf.Sin(t) * 0.05f, 1.2f + Mathf.Cos(t) * 0.05f, -3.6f);
        if (t >= _nextBeep)
        {
            KionjoEvents.Mark("beep", t.ToString("F1"));
            _nextBeep += 2f;
        }
    }
}
