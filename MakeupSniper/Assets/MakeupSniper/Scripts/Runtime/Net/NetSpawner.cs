using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace MakeupSniper.Net
{
    /// <summary>
    /// Когда хост запускает сервер — создаёт матч. Когда кто-то подключился — создаёт ему человечка.
    /// </summary>
    public class NetSpawner : MonoBehaviour
    {
        public NetworkManager network;
        public NetworkObject matchPrefab;
        public NetworkObject playerPrefab;

        NetworkObject match;

        void Start()
        {
            if (network == null) network = GetComponent<NetworkManager>();
            network.ServerManager.OnServerConnectionState += OnServerState;
            network.SceneManager.OnClientLoadedStartScenes += OnClientLoaded;
        }

        void OnDestroy()
        {
            if (network == null) return;
            network.ServerManager.OnServerConnectionState -= OnServerState;
            network.SceneManager.OnClientLoadedStartScenes -= OnClientLoaded;
        }

        void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started && match == null && matchPrefab != null)
            {
                match = network.GetPooledInstantiated(matchPrefab, Vector3.zero, Quaternion.identity, true);
                network.ServerManager.Spawn(match);
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && !network.ServerManager.Started)
            {
                match = null;
            }
        }

        void OnClientLoaded(NetworkConnection conn, bool asServer)
        {
            if (!asServer || playerPrefab == null) return;
            Vector3 pos = new Vector3(0f, 0.05f, 3.4f + 0.5f * (conn.ClientId % 4));
            var w = World.Instance;
            if (w != null && w.shooterSpawns.Length > 0) pos = w.shooterSpawns[Mathf.Abs(conn.ClientId) % w.shooterSpawns.Length].position;
            NetworkObject nob = network.GetPooledInstantiated(playerPrefab, pos, Quaternion.Euler(0f, 180f, 0f), true);
            network.ServerManager.Spawn(nob, conn);
            network.SceneManager.AddOwnerToDefaultScene(nob);
        }
    }
}
