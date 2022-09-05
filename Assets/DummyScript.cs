using UnityEngine;

public class DummyScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    private void Start() { Module.GetComponent<KMSelectable>().OnFocus += () => Module.HandlePass(); }
}
