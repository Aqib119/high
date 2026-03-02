using Bluesoftec.Abilities;
using Bluesoftec.Abilities.RopeLasso;

using DG.Tweening;
using Fusion;
using QuranMetaverse;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

//#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
//#error This sample doesn't support currently selected platform, please switch to Windows, Mac, Linux in Build Settings.
//#endif

namespace SimpleFPS
{
    public enum RematchState { None = 0, Requested = 1, Accepted = 2, Declined = 3, Timeout = 4 }

    /// <summary>
    /// Runtime data structure to hold player information which must survive events like player death/disconnect.
    /// </summary>
    public struct PlayerData : INetworkStruct
    {
        [Networked, Capacity(24)]
        public string Nickname { get => default; set { } }
        public PlayerRef PlayerRef;
        public int Kills;
        public int Deaths;
        public int LastKillTick;
        public int StatisticPosition;
        public bool IsAlive;
        public bool IsConnected;
    }

    public enum EGameplayState
    {
        Skirmish = 0,
        Running = 1,
        Finished = 2,
    }

    /// <summary>
    /// Drives gameplay logic - state, timing, handles player connect/disconnect/spawn/despawn/death, calculates statistics.
    /// </summary>
    public class Gameplay : NetworkBehaviour
    {
        public GameUI GameUI;
        public Player PlayerPrefab;
        public float GameDuration = 180f;
        public float PlayerRespawnTime = 5f;
        public float DoubleDamageDuration = 30f;
        public float DelayedTimeoutDuration = 10f;

        [Networked]
        [Capacity(2)]
        [HideInInspector]
        public NetworkDictionary<PlayerRef, PlayerData> PlayerData { get; }

        [Networked]
        [Capacity(2)]
        [HideInInspector]
        public NetworkDictionary<PlayerRef, bool> RematchVotes { get; }

        [Networked]
        [HideInInspector]
        public TickTimer RemainingTime { get; set; }
        [Networked]
        [HideInInspector]
        public EGameplayState State { get; set; }

        [Networked]
        [HideInInspector]
        public TickTimer DelayedTimeoutTimer { get; set; }

        public bool DoubleDamageActive => State == EGameplayState.Running && RemainingTime.RemainingTime(Runner).GetValueOrDefault() < DoubleDamageDuration;

        private bool _isNicknameSent;
        private float _runningStateTime;
        private List<Player> _spawnedPlayers = new(2);
        private List<PlayerRef> _pendingPlayers = new(2);
        private List<PlayerData> _tempPlayerData = new(2);
        private List<Transform> _recentSpawnPoints = new(4);
        [SerializeField] Button _rematchButton;
       public  PotionManager potionmanager;

        // --- NEW networked fields for rematch flow ---
        [Networked, HideInInspector] public RematchState CurrentRematchState { get; set; }
        [Networked, HideInInspector] public PlayerRef RematchRequester { get; set; }
        [Networked, HideInInspector] public TickTimer RematchTimer { get; set; }

        public GameObject AIPrefab; // 👈 AI prefab reference
        public int AICount = 1;       // کتنے bots spawn کرنے ہیں

        //private void Start()
        //{
        //    potionmanager = GetComponent<PotionManager>();
        //}
        private void OnEnable()
        {
            EventChannelManager.AddListener<Unit>(GenericEventType.VoidReMatchButtonClikced, OnRemacthClicked);
            EventChannelManager.AddListener<PlayerRef>(GenericEventType.VoidOnOthorPlayerLeft, OnPlayerLeft);
        }
        /*
                ////private void OnRemacthClicked(Unit unit)
                ////{
                ////    Debug.Log(this.Format($"On Remacth clicked{HasStateAuthority}"));

                ////    RPC_AskRematch(Runner.LocalPlayer);
                ////}

                ////private void CheckRematchVotes()
                ////{
                ////    Debug.Log(RematchVotes.Count);

                ////    if (RematchVotes.Count < PlayerData.Count)
                ////    {
                ////        Debug.Log(this.Format("case 1"));
                ////        return;
                ////    }

                ////    foreach (var vote in RematchVotes)
                ////    {
                ////        Debug.Log(this.Format("case 2"));
                ////        if (!vote.Value) return; // At least one hasn't agreed
                ////    }

                ////    // All agreed — host starts rematch
                ////    if (HasStateAuthority)
                ////    {
                ////        RPC_StartRematch();
                ////    }

                ////}

                ////[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
                ////private void RPC_StartRematch()
                ////{
                ////    RematchVotes.Clear(); // Reset votes
                ////    EventChannelManager.RaiseEvent<Unit>(GenericEventType.VoidOnReMatchAccpted, Unit.Default);
                ////    if (HasStateAuthority)
                ////        StartGameplay();
                ////}
                */
        #region Rematch Methods
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_AskRematch(PlayerRef playerRef)
        {
            Debug.Log("here ");
            if (HasStateAuthority)
            {
                Debug.Log("here 1");
                if (RematchVotes.ContainsKey(playerRef))
                    RematchVotes.Set(playerRef, true);
                else
                    RematchVotes.Add(playerRef, true);

                Debug.Log($"here 3{RematchVotes.Count}");
                CheckRematchVotes();

            }
        }
        // ================= Rematch Logic =================
        // Called when local player clicks rematch button (via EventChannel)
        private void OnRemacthClicked(Unit unit)
        {
            // send request to state authority to start rematch request flow
            RPC_RequestRematch(Runner.LocalPlayer);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowRematchUI(PlayerRef requester, PlayerRef responder)
        {
            if (UI_Rematch.Instance != null)
            {
                UI_Rematch.Instance.ShowWaitingUI(requester, responder);
            }
        }
        // Client -> StateAuthority : request a rematch
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestRematch(PlayerRef requester)
        {
            if (!HasStateAuthority)
                return;

            // ignore if a rematch is already in progress
            if (CurrentRematchState == RematchState.Requested)
                return;

            // initialize rematch request
            CurrentRematchState = RematchState.Requested;
            RematchRequester = requester;

            RematchVotes.Clear();
            // give requester an automatic YES vote
            if (RematchVotes.ContainsKey(requester))
                RematchVotes.Set(requester, true);
            else
                RematchVotes.Add(requester, true);

            // start 10-second timer
            RematchTimer = TickTimer.CreateFromSeconds(Runner, 10f);

            // ✅ اب سب clients پر UI update کرو
            PlayerRef responder = GetOtherPlayerOf(requester);
            RPC_ShowRematchUI(requester, responder);
            // Inform UI: show waiting for requester, show responder popup for the other player
            // We will call UI_Rematch if present (client-side UI will show only to local relevant players)
            //if (UI_Rematch.Instance != null)
            //{
            //    // Show waiting to requester (on clients) and show popup to possible responder
            //    UI_Rematch.Instance.ShowWaitingUI(requester, GetOtherPlayerOf(requester));
            //}
        }
        // Client -> StateAuthority : responder replies (accept or decline)
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RespondRematch(PlayerRef playerRef, bool accept)
        {
            if (!HasStateAuthority) return;
            if (CurrentRematchState != RematchState.Requested) return;

            // set vote for this player
            if (RematchVotes.ContainsKey(playerRef))
                RematchVotes.Set(playerRef, accept);
            else
                RematchVotes.Add(playerRef, accept);

            if (!accept)
            {
                // someone declined -> cancel and send both back to menu
                CurrentRematchState = RematchState.Declined;
                // notify only requester that it was declined
                if (UI_Rematch.Instance != null && Runner.LocalPlayer == RematchRequester)
                {
                    UI_Rematch.Instance.ShowMessage("The other player declined the rematch.");
                }
                // optional: notify UI
                //if (UI_Rematch.Instance != null)
                //    UI_Rematch.Instance.ShowMessage("The other player declined the rematch.");
                // load main menu for everyone (StateAuthority makes the call)
                Runner.LoadScene("MainMenuTest", LoadSceneMode.Single);
                return;
            }

            // if accepted, check if all have voted yes
            CheckRematchVotes();
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Show(PlayerRef requester, PlayerRef responder)
        {
            if (UI_Rematch.Instance != null)
            {
                UI_Rematch.Instance.ShowWaitingUI(requester, responder);
            }
        }
        private void CheckRematchVotes()
        {
            // require votes from all players present in PlayerData
            if (RematchVotes.Count < PlayerData.Count)
                return;

            foreach (var vote in RematchVotes)
            {
                if (!vote.Value)
                    return; // someone voted no
            }

            // All agreed — start rematch
            if (HasStateAuthority)
            {
                CurrentRematchState = RematchState.Accepted;
                RematchVotes.Clear(); // reset
                // notify clients (you already had this RPC before; reuse it)
                RPC_StartRematch();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_StartRematch()
        {
            if (UI_Rematch.Instance != null)
                UI_Rematch.Instance.ShowMessage("The player accept the rematch.");
            // Reset UI on clients
            EventChannelManager.RaiseEvent<Unit>(GenericEventType.VoidOnReMatchAccpted, Unit.Default);

            potionmanager.ResetHealth();
            // On server/state authority start gameplay (respawn, timers etc)
            if (HasStateAuthority)
            {
                CurrentRematchState = RematchState.None;
                RematchVotes.Clear();

                // 🟢 Reset requester so next rematch flow starts fresh
                RematchRequester = default;
                StartGameplay();
            }
        }
        #endregion
        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority == false)
                return;

            Debug.Log("FixedUpdateNetwork");
            // your existing connection / spawn logic
            PlayerManager.UpdatePlayerConnections(Runner, SpawnPlayer, DespawnPlayer);

            if (State == EGameplayState.Skirmish && PlayerData.Count > 1)
            {
                StartGameplay();
               
            }

            if (State == EGameplayState.Running)
            {
                _runningStateTime += Runner.DeltaTime;
                var sessionInfo = Runner.SessionInfo;
                if (sessionInfo.IsVisible && (_runningStateTime > 60f || sessionInfo.PlayerCount >= sessionInfo.MaxPlayers))
                {
                    sessionInfo.IsVisible = false;
                }
            }

            // Rematch timer check
            if (CurrentRematchState == RematchState.Requested)
            {
                if (RematchTimer.Expired(Runner))
                {
                    // timeout handling
                    CurrentRematchState = RematchState.Timeout;
                    if (UI_Rematch.Instance != null)
                        UI_Rematch.Instance.ShowMessage("Rematch request timed out.");
                    Runner.LoadScene("MainMenuTest", LoadSceneMode.Single);
                }
            }
        }
        // helper: return the other player (assumes two players)
        private PlayerRef GetOtherPlayerOf(PlayerRef p)
        {
            foreach (var pl in Runner.ActivePlayers)
            {
                if (pl != p) return pl;
            }
            return PlayerRef.None;
        }

        // ================= end rematch logic =================


        private void OnDisable()
        {
            EventChannelManager.RemoveListener<Unit>(GenericEventType.VoidReMatchButtonClikced, OnRemacthClicked);
            EventChannelManager.RemoveListener<PlayerRef>(GenericEventType.VoidOnOthorPlayerLeft, OnPlayerLeft);
        }
        public void PlayerLostToAI()
        {
            GameUI.OnAIKilled();
            GameUI.PlayerWinView.ShowData(false, true); // ❌ LOSS
            DOVirtual.DelayedCall(0.05f, StopGameplay);
        }
        public void AIKilled()
        {
            GameUI.OnAIKilled();
            GameUI.PlayerWinView.ShowData(true, true);
            DOVirtual.DelayedCall(0.05f, StopGameplay);
        }
        public void PlayerKilled(PlayerRef killerPlayerRef, PlayerRef victimPlayerRef, EWeaponType weaponType, bool isCriticalKill)
        {
            if (HasStateAuthority == false)
                return;

            // Update statistics of the killer player.
            if (PlayerData.TryGet(killerPlayerRef, out PlayerData killerData))
            {
                killerData.Kills++;
                killerData.LastKillTick = Runner.Tick;
                PlayerData.Set(killerPlayerRef, killerData);
            }

            // Update statistics of the victim player.
            var playerData = PlayerData.Get(victimPlayerRef);
            playerData.Deaths++;
            playerData.IsAlive = false;
            PlayerData.Set(victimPlayerRef, playerData);

            // Inform all clients about the kill via RPC.
            RPC_PlayerKilled(killerPlayerRef, victimPlayerRef, weaponType, isCriticalKill);

            //TODO uncomment 
            //StartCoroutine(RespawnPlayer(victimPlayerRef, PlayerRespawnTime));

            //RecalculateStatisticPositions();
        }

        public override void Spawned()
        {
            if (Runner.Mode == SimulationModes.Server)
            {
                Application.targetFrameRate = TickRate.Resolve(Runner.Config.Simulation.TickRateSelection).Server;
            }

            if (Runner.GameMode == GameMode.Shared)
            {
                throw new System.NotSupportedException("This sample doesn't support Shared Mode, please start the game as Server, Host or Client.");
            }
            Debug.Log("Spawnd THy Player");
            GameUI.Runner = Runner;

        }

        ////public override void FixedUpdateNetwork()
        ////{
        ////    if (HasStateAuthority == false)
        ////        return;

        ////    // PlayerManager is a special helper class which iterates over list of active players (NetworkRunner.ActivePlayers) and call spawn/despawn callbacks on demand.
        ////    PlayerManager.UpdatePlayerConnections(Runner, SpawnPlayer, DespawnPlayer);

        ////    // Start gameplay when there are enough players connected.
        ////    if (State == EGameplayState.Skirmish && PlayerData.Count > 1)
        ////    {
        ////        StartGameplay();
        ////    }

        ////    if (State == EGameplayState.Running)
        ////    {
        ////        _runningStateTime += Runner.DeltaTime;

        ////        var sessionInfo = Runner.SessionInfo;

        ////        // Hide the match after 60 seconds. Players won't be able to randomly connect to existing game and start new one instead.
        ////        // Joining via party code should work.
        ////        if (sessionInfo.IsVisible && (_runningStateTime > 60f || sessionInfo.PlayerCount >= sessionInfo.MaxPlayers))
        ////        {
        ////            sessionInfo.IsVisible = false;
        ////        }


        ////        //if (RemainingTime.Expired(Runner))
        ////        //{
        ////        //	StopGameplay();
        ////        //}
        ////    }

        ////    // CheckRematchVotes();

        ////}

        public override void Render()
        {
            if (Runner.Mode == SimulationModes.Server)
                return;

            // Every client must send its nickname to the server when the game is started.
            if (_isNicknameSent == false)
            {
                RPC_SetPlayerNickname(Runner.LocalPlayer, PlayerPrefs.GetString("Photon.Menu.Username"));
                _isNicknameSent = true;
            }
        }

        private void SpawnPlayer(PlayerRef playerRef)
        {
            if (PlayerData.TryGet(playerRef, out var playerData) == false)
            {
                playerData = new PlayerData();
                playerData.PlayerRef = playerRef;
                playerData.Nickname = playerRef.ToString();
                playerData.StatisticPosition = int.MaxValue;
                playerData.IsAlive = false;
                playerData.IsConnected = false;
            }

            if (playerData.IsConnected == true)
                return;

            Debug.LogWarning($"{playerRef} connected.");

            playerData.IsConnected = true;
            playerData.IsAlive = true;

            PlayerData.Set(playerRef, playerData);

            var spawnPoint = Edited_GetSpawnPoint(playerRef);
            var player = Runner.Spawn(PlayerPrefab, spawnPoint.position, spawnPoint.rotation, playerRef);

            // Set player instance as PlayerObject so we can easily get it from other locations.
            Runner.SetPlayerObject(playerRef, player.Object);


            if (GameManager.IsPracticeMode)
            {
                Scriptables.Environment.GameIsRun = true;
                if (LoadingUI.Instance != null)
                    LoadingUI.Instance.Hide();
            }
            else
            {
                //PlayerSpawned();

            }

            RecalculateStatisticPositions();
            ResetGyro();
        }
        private void DespawnPlayer(PlayerRef playerRef, Player player)
        {
            if (PlayerData.TryGet(playerRef, out var playerData) == true)
            {
                if (playerData.IsConnected == true)
                {
                    Debug.LogWarning($"{playerRef} disconnected.");
                }

                playerData.IsConnected = false;
                playerData.IsAlive = false;
                PlayerData.Set(playerRef, playerData);
            }

            Runner.Despawn(player.Object);
            _rematchButton.gameObject.SetActive(false);
            RecalculateStatisticPositions();
        }

        private IEnumerator RespawnPlayer(PlayerRef playerRef, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            if (Runner == null)
                yield break;

         //   GameUI.GameOverView._menuButton.interactable = false;
            //GameUI.GameOverView._rematchButton.interactable = false;



            // Despawn old player object if it exists.
            var playerObject = Runner.GetPlayerObject(playerRef);
            if (playerObject != null)
            {
                Runner.Despawn(playerObject);
            }

            // Don't spawn the player for disconnected clients.
            if (PlayerData.TryGet(playerRef, out PlayerData playerData) == false || playerData.IsConnected == false)
                yield break;

            // Update player data.
            playerData.IsAlive = true;
            PlayerData.Set(playerRef, playerData);

            var spawnPoint = Edited_GetSpawnPoint(playerRef); //GetSpawnPoint();
            var player = Runner.Spawn(PlayerPrefab, spawnPoint.position, spawnPoint.rotation, playerRef);

            // Set player instance as PlayerObject so we can easily get it from other locations.
            Runner.SetPlayerObject(playerRef, player.Object);
            ResetGyro();
        }


        protected Transform Edited_GetSpawnPoint(PlayerRef playerRef)
        {
            var spawnPoints = Runner.SimulationUnityScene.GetComponents<SpawnPoint>(false)
                              .OrderBy(sp => sp.transform.GetSiblingIndex()).ToArray();

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("[Gameplay] No SpawnPoint found in scene!");
                return null;
            }

            int playerIndex = Mathf.Clamp(playerRef.AsIndex - 1, 0, spawnPoints.Length - 1);
            Debug.Log(this.Format($"player index {playerIndex}, total spawnPoints {spawnPoints.Length}"));
            return spawnPoints[playerIndex].transform;
        }

        private Transform GetSpawnPoint()
        {
            Transform spawnPoint = default;

            // Iterate over all spawn points in the scene.
            var spawnPoints = Runner.SimulationUnityScene.GetComponents<SpawnPoint>(false);
            for (int i = 0, offset = Random.Range(0, spawnPoints.Length); i < spawnPoints.Length; i++)
            {
                spawnPoint = spawnPoints[(offset + i) % spawnPoints.Length].transform;

                if (_recentSpawnPoints.Contains(spawnPoint) == false)
                    break;
            }

            // Add spawn point to list of recently used spawn points.
            _recentSpawnPoints.Add(spawnPoint);

            // Ignore only last 3 spawn points.
            if (_recentSpawnPoints.Count > 3)
            {
                _recentSpawnPoints.RemoveAt(0);
            }

            return spawnPoint;
        }

        private void StartGameplay()
        {
            GlobalData.isGameEnd = false;
            Debug.LogError("GlobalData.isGameEndaa" + GlobalData.isGameEnd);
            potionmanager.ResetHealth();

            // Stop all respawn coroutines.
            StopAllCoroutines();

            State = EGameplayState.Running;
            RemainingTime = TickTimer.CreateFromSeconds(Runner, GameDuration);

            // Reset player data after skirmish and respawn players.
            foreach (var playerPair in PlayerData)
            {
                var data = playerPair.Value;

                data.Kills = 0;
                data.Deaths = 0;
                data.StatisticPosition = int.MaxValue;
                data.IsAlive = false;

                PlayerData.Set(data.PlayerRef, data);

                StartCoroutine(RespawnPlayer(data.PlayerRef, 0f));

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // --- Extra debug info ----
            Debug.Log($"[Gameplay] StartGameplay called. IsPracticeMode={GameManager.IsPracticeMode}, HasStateAuthority={HasStateAuthority}, Runner={(Runner == null ? "NULL" : Runner.ToString())}");
            if (Runner != null)
            {
                Debug.Log($"[Gameplay] Runner.IsRunning={Runner.IsRunning}, GameMode={Runner.GameMode}, SessionPlayers={Runner.SessionInfo.PlayerCount}");
            }

            // Safety checks before spawning AI
            if (!GameManager.IsPracticeMode)
            {
                // Debug.Log("[Gameplay] Not PracticeMode — skipping AI spawn.");

                //  RPC_Loading();
                RPC_SetGameState(true);
                return;
            }

            if (!HasStateAuthority)
            {
                Debug.LogWarning("[Gameplay] Can't spawn AI: this instance does not have State Authority.");
                RPC_SetGameState(true);
                return;
            }

            if (AICount <= 0)
            {
                Debug.LogWarning("[Gameplay] AICount <= 0, skipping AI spawn.");
                RPC_SetGameState(true);
                return;
            }

            if (AIPrefab.Equals(default(NetworkPrefabRef)))
            {
                Debug.LogError("[Gameplay] AIPrefab is not assigned (NetworkPrefabRef is default). Please assign AI prefab in the inspector and register it in Fusion Network Prefabs.");
            }

            // --- Spawn AIs via StateAuthority RPC (safe even if this instance isn't state authority)
            if (GameManager.IsPracticeMode)
            {
                // call the RPC — the StateAuthority instance will perform the actual Runner.Spawn
                // RPC_SpawnPracticeAIs(AICount);
                RPC_SetGameState(true);
                Debug.Log($"[Gameplay] Requested spawn of {AICount} AI(s) via RPC_SpawnPracticeAIs.");
            }



            RPC_SetGameState(true);
        }

        private void StopGameplay()
        {
            RecalculateStatisticPositions();

            State = EGameplayState.Finished;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (HasStateAuthority)
            {
                EventChannelManager.RaiseEvent<PlayerRef>(GenericEventType.VoidGameOver, Runner.LocalPlayer);
                RPC_SetGameState(false);
            }
        }

        public void ResetGyro(GameObject player)
        {
            var localPlayer = Runner.LocalPlayer;

            if (localPlayer != PlayerRef.None) // IsValid ki jagah
            {
                var playerObj = Runner.GetPlayerObject(localPlayer);
                if (playerObj != null)
                {

                    if (playerObj.GetComponent<CustomPlayerInput_1>())
                    {
                        Debug.Log("✅ Gyro reset to new reference! Local Player");
                        playerObj.GetComponent<CustomPlayerInput_1>().ResetGyro();
                    }
                    else
                    {

                        Debug.LogError("✅ Gyro Nott Gette Local Player");
                    }

                }
                else
                {
                    Debug.LogWarning("⚠️ Local player object not found!");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ Local player not valid!");
            }
        }
        public void ResetGyro()
        {
            var localPlayer = Runner.LocalPlayer;

            if (localPlayer != PlayerRef.None) // IsValid ki jagah
            {
                var playerObj = Runner.GetPlayerObject(localPlayer);
                if (playerObj != null)
                {

                    if (playerObj.GetComponent<CustomPlayerInput_1>())
                    {
                        Debug.Log("✅ Gyro reset to new reference! Local Player");
                        playerObj.GetComponent<CustomPlayerInput_1>().ResetGyro();
                    }
                    else
                    {

                        Debug.LogError("✅ Gyro Nott Gette Local Player");
                    }

                }
                else
                {
                    Debug.LogWarning("⚠️ Local player object not found!");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ Local player not valid!");
            }
        }




        public void LeftPlayer(PlayerRef @ref)
        {
            RPC_PlayerKilled(@ref, Runner.LocalPlayer, EWeaponType.None, false, true);
        }


        public void LeftPlayerShutDown()
        {
             GameUI.ShutDown();
        }
        private void RecalculateStatisticPositions()
        {
            if (State == EGameplayState.Finished)
                return;

            _tempPlayerData.Clear();

            foreach (var pair in PlayerData)
            {
                _tempPlayerData.Add(pair.Value);
            }

            _tempPlayerData.Sort((a, b) =>
            {
                if (a.Kills != b.Kills)
                    return b.Kills.CompareTo(a.Kills);

                return a.LastKillTick.CompareTo(b.LastKillTick);
            });

            for (int i = 0; i < _tempPlayerData.Count; i++)
            {
                var playerData = _tempPlayerData[i];
                playerData.StatisticPosition = playerData.Kills > 0 ? i + 1 : int.MaxValue;

                PlayerData.Set(playerData.PlayerRef, playerData);
            }
        }
        [Networked]
        public int SpawnedPlayers { get; set; }

        public void RPC_PlayerFullyReady()
        {
            if (Object.HasStateAuthority == false) return;

            SpawnedPlayers++;

            Debug.Log("Player spawned: " + SpawnedPlayers);

            // When both players spawned → tell everyone to hide loading
            if (SpawnedPlayers >= 2)
            {
              RPC_CloseLoadingPanels();
            }
        }

        // RPC to all machines
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_CloseLoadingPanels()
        {
            Debug.Log("Close Loading Panels on all clients");
            DOVirtual.DelayedCall(4f, () =>
            {
                LoadingUI.Instance.Hide();
                LoadingUI.Instance.calibrationPanel.SetActive(true);
                StartCalibration();
            });

        }

        [Networked] private TickTimer CalibrationTimer { get; set; }
        [Networked] private int CalibrationDuration { get; set; }

      


        public void StartCalibration()
        {
            if (!Object.HasStateAuthority) return;

            CalibrationDuration = UnityEngine.Random.Range(5, 11); // 1-10 sec
            CalibrationTimer = TickTimer.CreateFromSeconds(Runner, CalibrationDuration);

            // RPC_StartCalibrationUI(CalibrationDuration);
            StartCoroutine(CalibrationRoutine(CalibrationDuration));
        }


        private IEnumerator CalibrationRoutine(int duration)
        {
            yield return new WaitForSeconds(duration);
            CheckCalibrationResults();
        }
        private void CheckCalibrationResults()
        {
            if (!Object.HasStateAuthority) return;

            var players = Runner.ActivePlayers;

            foreach (var playerRef in players)
            {
                var obj = Runner.GetPlayerObject(playerRef);
                var player = obj.GetComponent<CustomPlayer>();

                if (!player.IsReady)
                {
                    player.GetComponent<Health>().ApplyDamageinStart(30);
                    Debug.Log(playerRef + " failed calibration → -30% HP");
                }
                else
                {
                    Debug.Log(playerRef + " passed calibration");
                }
                player.EnableSounds(true);
                player.StopCalibrtion = true;
            }
          
            //ContentPlayer.Instence.TryStartGameWhenBothReady();
        }

       




        [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
        private void RPC_SetGameState(bool gameState)
        {
            Scriptables.Environment.GameIsRun = gameState;

        }

        //[Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
        //private void RPC_Loading()
        //{

        //    if (LoadingUI.Instance != null)
        //    {
        //        Debug.Log("loading false");
        //       // DG.Tweening.DOVirtual.DelayedCall(2, () => LoadingUI.Instance.Hide());
        //       LoadingUI.Instance.Hide();
        //    }
        //}

        [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
        private void RPC_PlayerKilled(PlayerRef killerPlayerRef, PlayerRef victimPlayerRef, EWeaponType weaponType, bool isCriticalKill, bool isDisconnected = false)
        {
            string killerNickname = "";
            string victimNickname = "";

            if (PlayerData.TryGet(killerPlayerRef, out PlayerData killerData))
            {
                killerNickname = killerData.Nickname;

            }

            if (PlayerData.TryGet(victimPlayerRef, out PlayerData victimData))
            {
                victimNickname = victimData.Nickname;
            }

            GameUI.PlayerWinView.ShowData(win: Runner.LocalPlayer == killerPlayerRef, isDisconnected);
          
            EventChannelManager.RaiseEvent<PlayerRef>(GenericEventType.OnPlayerKilled, victimPlayerRef);

            DOVirtual.DelayedCall(0.05f, StopGameplay);

        }


        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        private void RPC_SetPlayerNickname(PlayerRef playerRef, string nickname)
        {
            var playerData = PlayerData.Get(playerRef);
            playerData.Nickname = nickname;
            PlayerData.Set(playerRef, playerData);
        }

        private async void OnPlayerLeft(PlayerRef @ref)
        {
            if (Runner.LocalPlayer == @ref)
                return;

            if (HasStateAuthority)
                DelayedTimeoutTimer = TickTimer.CreateFromSeconds(Runner, DelayedTimeoutDuration);

            // wait for some time to check if player is still connected to server
            await Task.Delay((int)DelayedTimeoutTimer.RemainingTime(Runner).GetValueOrDefault() * 1000);

            if (Runner == null || !Scriptables.Environment.GameIsRun)
                return;

            RPC_PlayerKilled(Runner.LocalPlayer, @ref, EWeaponType.None, false, true);
        }




        //public Dictionary<PlayerRef, bool> ready = new Dictionary<PlayerRef, bool>();

        //public void PlayerReady(PlayerRef player)
        //{
        //    ready[player] = true;
        //    Debug.Log("ready Player " + ready[player]);

        //    if (ready.Count == 2 &&
        //       ready.Values.All(x => x == true))
        //    {
        //        RPC_StartGame();
        //    }
        //}

        //[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        //void RPC_StartGame()
        //{
        //    // UI close etc
        //    LoadingUI.Instance.calibrationPanel.SetActive(false);
        //    // isLocalReady = true;
        //    //  GameStart();
        //}


        #region AI
        /// <summary>

        /// </summary>
        /// <param name="count"></param>
        // RPC that will be called by anyone but executed on StateAuthority
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SpawnPracticeAIs(int count)
        {
            if (!HasStateAuthority)
            {
                Debug.LogWarning("[Gameplay] RPC_SpawnPracticeAIs received but this instance is not StateAuthority.");
                return;
            }

            if (AIPrefab.Equals(default(NetworkPrefabRef)))
            {
                Debug.LogError("[Gameplay] AIPrefab is not assigned!");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var spawnPoint = GetSpawnPoint();
                if (spawnPoint == null)
                {
                    Debug.LogError($"[Gameplay] No spawn point found for AI #{i}");
                    continue;
                }

                try
                {
                    var aiObj = Runner.Spawn(AIPrefab, spawnPoint.position, spawnPoint.rotation, PlayerRef.None);
                    if (aiObj != null)
                        Debug.Log($"[Gameplay] (StateAuthority) spawned AI #{i} at {spawnPoint.position}");
                    else
                        Debug.LogError($"[Gameplay] Runner.Spawn returned null for AI #{i}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Gameplay] Exception when spawning AI #{i}: {ex.Message}");
                }
            }
        }

        #endregion

    }
}

