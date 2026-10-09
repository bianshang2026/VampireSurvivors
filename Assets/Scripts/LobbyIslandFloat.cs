using UnityEngine;

public class LobbyIslandFloat : MonoBehaviour
{
    public float amplitude = 0.1f;
    public float speed = 0.5f;
    public float phase;

    Vector3 _basePos;

    void Start()
    {
        _basePos = transform.position;
    }

    void Update()
    {
        var p = _basePos;
        p.y += Mathf.Sin(Time.time * speed + phase) * amplitude;
        transform.position = p;
    }
}
