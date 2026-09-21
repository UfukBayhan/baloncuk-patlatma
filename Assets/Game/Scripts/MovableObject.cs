using System;
using UnityEngine;
public enum MoveDirection { Up, Down, Left, Right }
public class MovableObject : MonoBehaviour
{
    public float speed = 2;
    public MoveDirection direction = MoveDirection.Up;
    public Action<MovableObject> OnDespawn;
    void Update()
    {
        var rect = (RectTransform)transform;
        var parent = transform.parent as RectTransform;
        if (!parent) return;
        Vector2 axis = direction == MoveDirection.Up ? Vector2.up : direction == MoveDirection.Down ? Vector2.down : direction == MoveDirection.Left ? Vector2.left : Vector2.right;
        rect.anchoredPosition += axis * speed * 65f * Time.deltaTime;
        var pos = rect.anchoredPosition;
        bool outside = direction == MoveDirection.Up ? pos.y > parent.rect.height / 2 + 100 : direction == MoveDirection.Down ? pos.y < -parent.rect.height / 2 - 100 : Mathf.Abs(pos.x) > parent.rect.width / 2 + 100;
        if (outside) { if (OnDespawn != null) OnDespawn(this); else Destroy(gameObject); }
    }
}
