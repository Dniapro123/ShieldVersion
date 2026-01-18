using System;
using Mirror;
using UnityEngine;

public enum PlayerRole { Builder, Attacker }

public class PlayerRoleNet : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnRoleSync))]
    public PlayerRole role = PlayerRole.Builder;

    public event Action<PlayerRole> OnRoleChanged;

    void OnRoleSync(PlayerRole oldRole, PlayerRole newRole)
    {
        role = newRole;
        OnRoleChanged?.Invoke(newRole);
    }

    public bool IsBuilder => role == PlayerRole.Builder;
    public bool IsAttacker => role == PlayerRole.Attacker;
}
