using UnityEngine;
using Mirror;

public class NetworkPlayerSetup : NetworkBehaviour
{
    [Header("Wyłącz na zdalnych graczach (np. kamera, audio listener itp.)")]
    public Behaviour[] disableForRemote;

    public override void OnStartLocalPlayer()
    {
        // tu później podepniesz kamerę na local playera, UI itd.
    }

    public override void OnStartClient()
    {
        if (!isLocalPlayer)
        {
            foreach (var b in disableForRemote)
                if (b) b.enabled = false;
        }
    }
}
