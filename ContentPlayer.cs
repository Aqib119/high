using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using Newtonsoft.Json.Linq;
using PlayFab;
using PlayFab.ClientModels;
using SimpleFPS;
using UnityEngine;
using UnityEngine.SceneManagement;
using static FriendSystemManager;

public class ContentPlayer : MonoBehaviour, INetworkRunnerCallbacks
{
    public string Hostname;
    public int level;
    public string avatarID;
    public static ContentPlayer Instence;
    public GameObject _playerPrefab;

    public GameObject spawnplayer;

    private void Awake()
    {
        Instence = this;
    }


    public List<PlayerData> playerDatas = new List<PlayerData>();
    public NetworkRunner runner;
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player joined: {player}");

        if (GameManager.IsPracticeMode)
        {
            return;
        }
        this.runner = runner;
        if (runner.IsServer)
        //if (spawnplayer == null)
        {

            var spawnedPlayerObject = runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity, player);

            //  spawnplayer = spawnedPlayerObject.gameObject;
        }
        if (spawnplayer != null)
        {
            Debug.Log("Player joined1111: SendSatat");
            spawnplayer.GetComponent<SendPlayerData>().SendSatat();


        }


        if (runner.ActivePlayers.Count() >= 2)
        {
            if (Safe_Manager.Instance)
                Safe_Manager.Instance.ShowGameHud();
            if (ShowDataMatchmaking.Instence != null)
            {
                ShowDataMatchmaking.Instence.UpdateUI();
            }
            Debug.Log("Both players are ready. Loading next scene...");
            if (runner.IsServer)
            {
                Debug.Log("IsMasterClient. Loading next scene...");
                // runner.LoadScene("DeathmatchTest"); // Replace with your scene name
                StartCoroutine(LoadLevelAtDelay(runner));
            }
        }
    }
    public bool loading;
    IEnumerator LoadLevelAtDelay(NetworkRunner runner)
    {
        yield return new WaitForSeconds(4);
        if (runner.ActivePlayers.Count() >= 2)
        {
            if (!loading)
            {
                Debug.Log("LoadLevelAtDelay  matchmaking...");
                runner.LoadScene("DeathmatchTest");
                loading = true;
            }
        }
        else
        {
            ShowDataMatchmaking.Instence.LeftPlayer();
        }

    }

   
    public void TryStartGameWhenBothReady()
    {
        if (runner == null) return; // safety check

        var players = runner.ActivePlayers;

        if (players.Count() < 2)
            return;

        var p1Obj = runner.GetPlayerObject(players.ElementAt(0));
        var p2Obj = runner.GetPlayerObject(players.ElementAt(1));

        var p1 = p1Obj.GetComponent<CustomPlayer>();
        var p2 = p2Obj.GetComponent<CustomPlayer>();

        if (p1.IsReady && p2.IsReady)
        {
            Debug.Log("Both players ready → Start Game");
            p1.EnableSounds(true);
            p2.EnableSounds(true);
            RPC_CloseCalibrationUI();
        }
    }


    public void Closecalibration() 
    {
        RPC_CloseCalibrationUI();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_CloseCalibrationUI()
    {
        Debug.Log("oadingUI.Instance.calibrationPanel falsee...→ Start Game");
        LoadingUI.Instance.calibrationPanel.SetActive(false);

        GlobalData.isGameEnd = false;
        Debug.LogError("GlobalData.isGameEndcalibrationPanel " + GlobalData.isGameEnd);

        if (runner != null && runner.IsRunning && runner.LocalPlayer != PlayerRef.None)
        {
            CheckEnemy();
            
        }


    }

    public void CheckEnemy()
    {
        if (playerDatas == null || playerDatas.Count < 2)
            return;

        string myPlayFabId = PlayFabSettings.staticPlayer.PlayFabId;

        var enemy = playerDatas.FirstOrDefault(p => p.PlayfebID != myPlayFabId);
        if (enemy == null)
            return;

        GetFullFriendList(friendList =>
        {
            OpponentisFriend = IsFriend(enemy.PlayfebID, friendList);
        });
    }



    public bool OpponentisFriend = false;

    public static bool IsFriend(string playFabId, List<FriendOnlineInfo> friendsList)
    {
        return friendsList.Any(f => f.playFabId == playFabId);
    }

    public static void GetFullFriendList(Action<List<FriendOnlineInfo>> onResult)
    {
        var req = new GetFriendsListRequest
        {
            ProfileConstraints = new PlayerProfileViewConstraints
            {
                ShowDisplayName = true
            }
        };

        PlayFabClientAPI.GetFriendsList(req, result =>
        {
            var allFriends = new List<FriendOnlineInfo>();

            // ✅ DISPLAY NAME COMES ONLY FROM FRIEND LIST
            foreach (var f in result.Friends)
            {
                allFriends.Add(new FriendOnlineInfo
                {
                    playFabId = f.FriendPlayFabId,
                    displayName = f.TitleDisplayName,
                    isOnline = false,
                    AvatarID = ""
                });
            }

            // ✅ CloudScript ONLY gives online + avatar
            var cloudReq = new ExecuteCloudScriptRequest
            {
                FunctionName = "GetFriendsOnlineStatus"
            };

            PlayFabClientAPI.ExecuteCloudScript(cloudReq, res =>
            {
                var json = JObject.Parse(res.FunctionResult.ToString());
                var onlineList = json["friends"]?.ToObject<List<FriendOnlineInfo>>();

                foreach (var f in allFriends)
                {
                    var match = onlineList?.FirstOrDefault(o => o.playFabId == f.playFabId);
                    if (match != null)
                    {
                        f.isOnline = match.isOnline;
                        f.AvatarID = match.AvatarID;
                    }
                }

                onResult?.Invoke(allFriends);
            },
            error =>
            {
                Debug.LogError(error.GenerateErrorReport());
                onResult?.Invoke(allFriends);
            });

        },
        error =>
        {
            Debug.LogError(error.GenerateErrorReport());
            onResult?.Invoke(new List<FriendOnlineInfo>());
        });
    }




    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player.PlayerId} left!");

        if (SceneManager.GetActiveScene().name == "DeathmatchTest")
        {
            //UI_Rematch.Instance.gameUI.Gameplay.LeftPlayer(player);
            if (UI_Rematch.Instance != null)
            {
                //UI_Rematch.Instance.gameUI.DisconnectedView.SetActive(true);
                UI_Rematch.Instance.gameUI.OnHostDisconnect();
            }
        }
        else if (SceneManager.GetActiveScene().name == "MainMenuTest") 
        {
            StartCoroutine(ReturnToMenu());
        }
    }



    //public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    //{
    //    Debug.Log($"Player {player.PlayerId} left!");

    //    if (SceneManager.GetActiveScene().name == "DeathmatchTest")
    //    {
    //        UI_Rematch.Instance.gameUI.Gameplay.LeftPlayer(player);
    //    }
    //}

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {


    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    //public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    //{
    //    if (GlobalData.isGameEnd)
    //        return;
    //    if (SceneManager.GetActiveScene().name == "DeathmatchTest")
    //    {
    //        UI_Rematch.Instance.gameUI.Gameplay.LeftPlayerShutDown();
    //        UI_Rematch.Instance.gameUI.Gameplay.AIKilled();
    //    }
    //}
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Runner shutdown: {shutdownReason}");

        // Always unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Stop gameplay safely
        //GlobalData.isGameEnd = true;

        // Show disconnect UI if still in gameplay
        if (SceneManager.GetActiveScene().name == "DeathmatchTest")
        {
            if (UI_Rematch.Instance != null)
            {
                //UI_Rematch.Instance.gameUI.DisconnectedView.SetActive(true);
                UI_Rematch.Instance.gameUI.OnHostDisconnect();
            }

            // Fallback: force return to menu after short delay
            //runner.StartCoroutine(ReturnToMenu());
        }
        else if (SceneManager.GetActiveScene().name == "MainMenuTest")
        {
           // StartCoroutine(ReturnToMenu());
        }
    }

    public void leave() 
    {
        StartCoroutine(ReturnToMenu());
    }

    IEnumerator ReturnToMenu()
    {
        yield return new WaitForSeconds(1f);

        if (runner != null && runner.IsRunning)
            runner.Shutdown();

        SceneManager.LoadScene("MainMenuTest");
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Connected to server");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner)
    {
        Debug.Log("Disconnected from server");

        if (SceneManager.GetActiveScene().name == "DeathmatchTest")
        {
            UI_Rematch.Instance.gameUI.DisconnectedView.SetActive(true);
            // Auto return to menu
            SceneManager.LoadScene("MainMenuTest");
        }
        else if (SceneManager.GetActiveScene().name == "MainMenuTest")
        {
            StartCoroutine(ReturnToMenu());
        }

    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    // 👇 Newer Fusion versions need these:
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        Debug.Log($"Object {obj.name} entered AOI for player {player.PlayerId}");
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        Debug.Log($"Object {obj.name} exited AOI for player {player.PlayerId}");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log("Disconnected from server");
        if (SceneManager.GetActiveScene().name == "DeathmatchTest")
        {
            UI_Rematch.Instance.gameUI.DisconnectedView.SetActive(true);
        }
        else if (SceneManager.GetActiveScene().name == "MainMenuTest")
        {
            StartCoroutine(ReturnToMenu());
        }
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        throw new System.NotImplementedException();
    }
}

[System.Serializable]
public class PlayerData
{
    public PlayerRef playerRef;
    public string name;
    public string PlayfebID;
    public int level;
    public string id;
    public bool ready;
}
