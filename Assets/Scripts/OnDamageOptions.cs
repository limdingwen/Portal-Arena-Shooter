using UnityEngine;

public struct OnDamageOptions
{
    public int damage;
    public Vector3 point;
    public Vector3 normal;

    public OnDamageOptions(int damage, Vector3 point, Vector3 normal)
    {
        this.damage = damage;
        this.point = point;
        this.normal = normal;
    }
}