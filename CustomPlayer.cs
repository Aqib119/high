using Fusion;
using QuranMetaverse;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 👈 new Input System
using UnityEngine.SceneManagement;
using AttitudeSensor = UnityEngine.InputSystem.AttitudeSensor;
using Gyroscope = UnityEngine.InputSystem.Gyroscope;

namespace SimpleFPS
{
    public class CustomPlayer : Player
    {
        private Quaternion _lastGyro = Quaternion.identity;
        private bool _hasReloaded = false;
        float angleThreshold = 30f;
        [Networked] public NetworkBool IsRoped { get; set; }

        public override void Spawned()
        {

            base.Spawned();

            // ✅ Reset gyro baseline each spawn
            _lastGyro = Quaternion.identity;
            EnableSounds(false);
#if !UNITY_EDITOR
    if (Gyroscope.current != null)
    {
        InputSystem.EnableDevice(Gyroscope.current);
        Gyroscope.current.MakeCurrent(); // 👈 force it active
    }

    if (AttitudeSensor.current != null)
    {
        InputSystem.EnableDevice(AttitudeSensor.current);
        AttitudeSensor.current.MakeCurrent();
        // 👈 force it active
        _lastGyro = AttitudeSensor.current.attitude.ReadValue();
    }
#endif
        }
        public float angle;
        protected override void ProcessInput(NetworkedInput input)
        {

            if (IsRoped)
            {
                // Completely freeze player
                //Weapons.StopFire();
                //KCC.SetVelocity(Vector3.zero);
                return;
            }


            if (!Scriptables.Environment.GameIsRun)
                return;

            //if (HasInputAuthority == false)
            //    return;

            // Vector2 lookDelta = Vector2.zero;
            Vector2 lookDelta = input.LookRotationDelta;

            // 🔹 Apply rotation always (local prediction ke liye bhi zaroori hai)
            // KCC.AddLookRotation(lookDelta, -89f, 89f);
            ApplyLookRotation(lookDelta, -16f, 60f);

            Debug.Log($"lookDelta={lookDelta}");


            // ─────────────────────
            // Reload when looking down
            // ─────────────────────
            float pitch = _pitch;

            // Debug.Log($"pitch000 = {pitch}");

            if (pitch > 180) pitch -= 360;

            if (pitch > 40f) // threshold degrees
            {
                if (!_hasReloaded)
                {
                    Weapons.Reload();
                    _hasReloaded = true;
                }
            }
            else
            {
                _hasReloaded = false;
            }
            if (!GameManager.IsPracticeMode)
            {
                if (Object.HasInputAuthority && LoadingUI.Instance.calibrationPanel.activeSelf )// && !CalibrationManagerFusion.Instance.isLocalReady)
                {
                    //CheckTilt();
                    // float pitch1 = KCC.TransformRotation.eulerAngles.x;
#if !UNITY_EDITOR
                    angle = Vector3.Angle(Vector3.down, Input.acceleration);
#endif
                    Debug.Log("Angle- " + angle + "  --angleThreshold  " + angleThreshold);
                    //if (angle < angleThreshold)
                    if (angle > 85 && angle < 95)
                    {
                        RPC_SetReady(true);
                        Debug.Log("pitch1 =");

                    }
                    else
                    {
                        RPC_SetReady(false);
                    }
                if (StopCalibrtion)
                {
                    RPC_SetReady(true);
                }


                }

            }



            // ─────────────────────
            // Gravity
            // ─────────────────────
            // ─────────────────────
            // Movement
            // ─────────────────────
            var inputDirection = transform.rotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);

            // Reset Gyro
            if (input.Buttons.WasPressed(_previousButtons, EInputButton.ResetGyro))
            {
                ResetGyro();
            }

            var jumpImpulse = 0f;

            if (input.Buttons.WasPressed(_previousButtons, EInputButton.Jump) && IsGrounded)
                jumpImpulse = JumpForce;

            MovePlayer(inputDirection * MoveSpeed, jumpImpulse);
            RefreshCamera();

            if (jumpImpulse > 0f)
                _jumpCount++;

            // ─────────────────────
            // Fire / Reload button
            // ─────────────────────
            if (input.Buttons.IsSet(EInputButton.Fire))
            {
                bool justPressed = input.Buttons.WasPressed(_previousButtons, EInputButton.Fire);
                Weapons.Fire(justPressed);
                Health.StopImmortality();
            }
            else if (input.Buttons.IsSet(EInputButton.Reload))
            {
                Weapons.Reload();
            }

            // ─────────────────────
            // Weapon switching
            // ─────────────────────
            if (input.Buttons.WasPressed(_previousButtons, EInputButton.Pistol))
                Weapons.SwitchWeapon(EWeaponType.Pistol);
            else if (input.Buttons.WasPressed(_previousButtons, EInputButton.Rifle))
                Weapons.SwitchWeapon(EWeaponType.Rifle);
            else if (input.Buttons.WasPressed(_previousButtons, EInputButton.Shotgun))
                Weapons.SwitchWeapon(EWeaponType.Shotgun);

            // ─────────────────────
            // Spray decal
            // ─────────────────────
            if (input.Buttons.WasPressed(_previousButtons, EInputButton.Spray) && HasStateAuthority)
            {
                if (Runner.GetPhysicsScene().Raycast(CameraHandle.position, CameraHandle.forward, out var hit, 2.5f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
                {
                    var sprayOrientation = hit.normal.y > 0.9f ? transform.rotation : Quaternion.identity;
                    Runner.Spawn(SprayPrefab, hit.point, sprayOrientation * Quaternion.LookRotation(-hit.normal));
                }
            }





            //// Now grab the rotation AFTER KCC updated it
            ////bool isHost = Runner.IsServer;

            ////if (isHost)
            ////{
            ////    Vector3 e = KCC.TransformRotation.eulerAngles;

            ////    //// Normalize
            ////    //if (e.y > 180) e.y -= 360;

            ////    //Debug.Log("e.y is host " + e.y);
            ////    //float clampedY = e.y;
            ////    //// Host clamp: 20 → 160
            ////    //clampedY = Mathf.Clamp(e.y, 20f, 160f);

            ////    float y = e.y;
            ////    if ((y > -20f && y < 0f) || (y > 340 && y < 360))
            ////    {
            ////        y = -20f;
            ////        Debug.Log("111e.y " + y);
            ////    }
            ////    else if ((y > -160f && y < -180f) || (y < 200 && y > 180))
            ////    {
            ////        Debug.Log("222e.y " + y);
            ////        y = -160f;
            ////    }
            ////    //if (y > -20f && y < 0f)
            ////    //{
            ////    //    Debug.Log("isHost11e.y " + y);
            ////    //    y = -20f;
            ////    //}
            ////    //else if (y < -160f && y > -180f)
            ////    //{
            ////    //    Debug.Log("isHost22e.y " + y);
            ////    //    y = -160f;
            ////    //}
            ////    else if (y < 20f && y > 0f)
            ////    {
            ////        Debug.Log("isHost33e.y " + y);
            ////        y = 20f;
            ////    }
            ////    else if (y > 160f && y < 180f)
            ////    {
            ////        Debug.Log("isHost44e.y " + y);
            ////        y = 160f;
            ////    }

            ////    Quaternion finalRot = Quaternion.Euler(e.x, y, 0);
            ////    KCC.SetLookRotation(finalRot);
            ////}
            ////else
            ////{
            ////    Vector3 e = KCC.TransformRotation.eulerAngles;

            ////    // Normalize
            ////    //if (e.y > 180) e.y -= 360;
            ////    Debug.Log("e.y " + e.y);

            ////    float y = e.y;

            ////    if ((y > -20f && y < 0f)||(y>340&&y<360) )
            ////    {
            ////        y = -20f;
            ////        Debug.Log("111e.y " + y);
            ////    }
            ////    else if( (y > -160f && y < -180f)||(y<200&&y>180))
            ////    {
            ////        Debug.Log("222e.y " + y);
            ////        y = -160f;
            ////    }
            ////    //else if (y < 20f && y > 0f)
            ////    //{
            ////    //    Debug.Log("333e.y " + y);
            ////    //    y = 20f;
            ////    //}
            ////    //else if (y > 160f && y < 180f)
            ////    //{
            ////    //    Debug.Log("444e.y " + y);
            ////    //    y = 160f;
            ////    //}

            ////    Quaternion finalRot = Quaternion.Euler(e.x, y, 0);
            ////KCC.SetLookRotation(finalRot);
            ////}

            // Re-apply clamped rotation (full rotation, not only yaw)



            _previousButtons = input.Buttons;
        }
        public void SetRoped_Server(bool value)
        {
            if (!Object.HasStateAuthority)
                return;

            IsRoped = value;

            // Inform everyone (UI, effects)
            RPC_OnRopedChanged(value);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnRopedChanged(bool value)
        {
            if (value)
            {
                Debug.Log($"{Object.InputAuthority} has been roped!");
                // UIStatusEffect.Show("You have been roped!");
            }
            else
            {
                // UIStatusEffect.Hide();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_SetRoped(bool value)
        {
            IsRoped = value;

            if (value)
            {
                Debug.Log("You have been roped!");
                //UIStatusEffect.Show("You have been roped!");
            }
            else
            {
                //UIStatusEffect.Hide();
            }
        }


        public void ModifyAimSpeed(float factor, float duration)
        {
            StartCoroutine(AimSpeedCoroutine(factor, duration));
        }


        // 📡 RPC to tell this player to apply slow (called by other players)
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ApplySlow(float factor, float duration)
        {
            ModifyAimSpeed(factor, duration);
        }
        private IEnumerator AimSpeedCoroutine(float factor, float duration)
        {
            float originalSpeed = MoveSpeed; // Assuming you have AimSpeed variable
            MoveSpeed *= factor;
            yield return new WaitForSeconds(duration);
            MoveSpeed = originalSpeed;
        }



        public void EnableSounds(bool isenable)
        {
            JumpSound.enabled = isenable;
            FootSound.enabled = isenable;
            switchSound.enabled = isenable;

        }

        [Networked]
        public bool IsReady { get; set; }

        [Networked]
        public bool StopCalibrtion { get; set; }



        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        public void RPC_SetReady(bool isture)
        {
            IsReady = isture;

            Debug.Log($"{Object.InputAuthority} is READY");

            ContentPlayer.Instence.TryStartGameWhenBothReady();
        }

    }
}