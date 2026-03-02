using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using SimpleFPS;
using UnityEngine.InputSystem;

public class InputLocal : SimpleFPS.PlayerInput, INetworkRunnerCallbacks
    {
    private NetworkRunner _runners;
    private SimpleFPS.CustomPlayerInput_1 inputSource;


    void Start()
    {
        _runners = FindObjectOfType<NetworkRunner>();
        if (_runners != null)
            _runners.AddCallbacks(this); // 👈 Zaroori


        inputSource = GetComponent<SimpleFPS.CustomPlayerInput_1>();
        if (!GameManager.IsPracticeMode)
        {
            StartCoroutine(StartCall());
        }
    }

    private IEnumerator StartCall()
    {
        // Wait until scene is fully loaded + player camera activated
        yield return new WaitForSeconds(1f);

        // Now tell host: I am ready
        UI_Rematch.Instance.Gameplay.RPC_PlayerFullyReady();
    }


    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
       
    }
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (inputSource != null && inputSource.HasInputAuthority)
        {
            // get from inputSource via public method
            var data = inputSource.GetAccumulatedInput();

            input.Set(data);

         // Debug.Log($"[GyroDebug] OnInput sending LookRotationDelta");
        }
    }



    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
       // Debug.Log($"Player {player.PlayerId} left!");
    }
    private Quaternion _lastGyro = Quaternion.identity;
//    public void OnInput(NetworkRunner runner, NetworkInput input)
//    {
//        if (HasInputAuthority == false)
//            return;

//        var data = new NetworkedInput();

//        // ✅ Movement (WASD or touch joystick)
//        if (Keyboard.current != null)
//        {
//            float x = (Keyboard.current.aKey.isPressed ? -1 : 0) +
//                      (Keyboard.current.dKey.isPressed ? 1 : 0);
//            float y = (Keyboard.current.sKey.isPressed ? -1 : 0) +
//                      (Keyboard.current.wKey.isPressed ? 1 : 0);

//            data.MoveDirection = new Vector2(x, y);
//        }

//        // ✅ Jump / Fire
//        var buttons = default(NetworkButtons);
//        if (Keyboard.current != null)
//        {
//            if (Keyboard.current.spaceKey.isPressed) buttons.Set(EInputButton.Jump, true);
//            if (Mouse.current != null && Mouse.current.leftButton.isPressed) buttons.Set(EInputButton.Fire, true);
//        }
//        data.Buttons = buttons;

//#if UNITY_EDITOR
//        data.LookRotationDelta = new Vector2(UnityEngine.Input.GetAxis("Mouse X"), UnityEngine.Input.GetAxis("Mouse Y"));
//#else

//  data.MoveDirection = Vector2.zero; // joystick ka code idhar

//    // Look (gyro)
//    if (AttitudeSensor.current != null)
//    {
//        Quaternion raw = AttitudeSensor.current.attitude.ReadValue();
//        Quaternion currentGyro = new Quaternion(raw.x, raw.y, -raw.z, -raw.w);

//        if (_lastGyro == Quaternion.identity)
//            _lastGyro = currentGyro;

//        Quaternion delta = Quaternion.Inverse(_lastGyro) * currentGyro;
//        Vector3 deltaEuler = delta.eulerAngles;
//        if (deltaEuler.x > 180) deltaEuler.x -= 360;
//        if (deltaEuler.y > 180) deltaEuler.y -= 360;

//        data.LookRotationDelta = new Vector2(deltaEuler.y, deltaEuler.x);
//        _lastGyro = currentGyro;
//    }

//#endif
//        data.MoveDirection = _accumulatedInput.MoveDirection;
//        data.Buttons = _accumulatedInput.Buttons;

//        input.Set(data);
//    }



    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Connected to server");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner)
    {
        Debug.Log("Disconnected from server");
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
        throw new System.NotImplementedException();
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        throw new System.NotImplementedException();
    }
}
