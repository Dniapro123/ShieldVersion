using UnityEngine;

public class ReactorHP : MonoBehaviour
{
    public int hp = 200;

    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        if (hp <= 0)
        {
            Destroy(gameObject);
            Debug.Log("Reactor destroyed!");
        }
    }
}
