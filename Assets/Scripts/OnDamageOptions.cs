using UnityEngine;

public struct OnDamageOptions
{
    public int damage;
    public Vector3 point;
    public Vector3 normal;
    public Vector3 damageDirection;
    public PlayerController inflictingPlayer;
    public bool drawBlood;

    public OnDamageOptions(int damage, Vector3 point, Vector3 normal, Vector3 damageDirection, PlayerController inflictingPlayer, bool drawBlood)
    {
        this.damage = damage;
        this.point = point;
        this.normal = normal;
        this.damageDirection = damageDirection;
        this.inflictingPlayer = inflictingPlayer;
        this.drawBlood = drawBlood;
    }
}