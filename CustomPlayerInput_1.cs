/*
using UnityEngine;
using UnityEngine.InputSystem;
using Gyroscope = UnityEngine.InputSystem.Gyroscope;
using Fusion;

namespace SimpleFPS
{
    [DefaultExecutionOrder(-10)]
    public class CustomPlayerInput_1 : PlayerInput, IBeforeUpdate
    {
        protected bool wasScreenPressed;
        protected float rightButtonValue;
        protected bool isPressed;
        private Rect validTouchZone;
        private bool isClickAllowed;
        private bool isThrownInputPressed;
        private bool wasThrownInpuPressed;

        public float moveRange;
        private float _startZ;
        private float _direction = 1;
        private float _startDirection = 1;

        // Gyro
        private Quaternion gyroInitialRotation;
        private bool gyroEnabled;

        private void OnEnable()
        {
            EventChannelManager.AddListener<bool>(GenericEventType.OnClickInput, (state) => isClickAllowed = state);
            EventChannelManager.AddListener<bool>(GenericEventType.OnThrowInput, UpdateThrowInputState);

            // Enable Gyro + Attitude Sensor
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Gyroscope.current != null)
            {
                InputSystem.EnableDevice(Gyroscope.current);
                gyroEnabled = true;
            }

            if (AttitudeSensor.current != null)
            {
                InputSystem.EnableDevice(AttitudeSensor.current);
                gyroEnabled = true;
                gyroInitialRotation = AttitudeSensor.current.attitude.ReadValue();
            }
#else
            gyroEnabled = false;
#endif
        }

        private void OnDisable()
        {
            EventChannelManager.RemoveListener<bool>(GenericEventType.OnClickInput, (state) => isClickAllowed = state);
            EventChannelManager.RemoveListener<bool>(GenericEventType.OnThrowInput, UpdateThrowInputState);
        }

        private void Start()
        {
            validTouchZone = new Rect(10, 10, Screen.width - 20, Screen.height * (4 / 5f));
            var playerRef = Object.InputAuthority;
            _startZ = transform.position.z;
            _startDirection = _direction = playerRef.AsIndex > 1 ? -1 : 1;
        }

        private void OnDrawGizmos()
        {
            if (Camera.main == null) return;

            validTouchZone = new Rect(10, 10, Screen.width - 20, Screen.height * (4 / 5f));

            Vector3 bottomLeft = Camera.main.ScreenToWorldPoint(new Vector3(validTouchZone.xMin, validTouchZone.yMin, Camera.main.nearClipPlane));
            Vector3 bottomRight = Camera.main.ScreenToWorldPoint(new Vector3(validTouchZone.xMax, validTouchZone.yMin, Camera.main.nearClipPlane));
            Vector3 topLeft = Camera.main.ScreenToWorldPoint(new Vector3(validTouchZone.xMin, validTouchZone.yMax, Camera.main.nearClipPlane));
            Vector3 topRight = Camera.main.ScreenToWorldPoint(new Vector3(validTouchZone.xMax, validTouchZone.yMax, Camera.main.nearClipPlane));

            Gizmos.color = Color.green;

            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);
        }

        void IBeforeUpdate.BeforeUpdate()
        {
            if (HasInputAuthority == false)
                return;

            HandleMovmentInput();
            HandleCursorState();

            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                // prefer gyro if available
                if (gyroEnabled)
                {
                    HandleGyroscopeInput();
                }
                else
                {
                    HandleTouchInput();
                }
            }
            else if (Mouse.current != null)
            {
                HandelMouseInput();
            }

            HandleThrowInput();
        }

        private void HandleMovmentInput()
        {
            float currentOffset = (transform.position.z - _startZ);

            if (currentOffset >= moveRange / 2f)
            {
                _direction = Mathf.Lerp(_direction, 1, 0.35f);
            }
            else if (currentOffset <= -moveRange / 2f)
            {
                _direction = Mathf.Lerp(_direction, -1, 0.35f);
            }

            _accumulatedInput.MoveDirection = new Vector2(_direction * _startDirection, 0);
        }

        void HandleCursorState()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        bool IsTouchPositionValid(Vector2 value)
        {
            return validTouchZone.Contains(value);
        }

        void HandleTouchInput()
        {
            var primaryTouch = Touchscreen.current.touches[0];

            if (primaryTouch.isInProgress && IsTouchPositionValid(primaryTouch.startPosition.value))
            {
                var touchDelta = primaryTouch.delta.ReadValue();
                var lookRotationDelta = new Vector2(-touchDelta.y, touchDelta.x);
                lookRotationDelta *= LookSensitivity * 3 / 60f;
                _lookRotationAccumulator.Accumulate(lookRotationDelta);
            }

            if (wasScreenPressed)
            {
                _accumulatedInput.Buttons.Set(EInputButton.Fire, primaryTouch.isInProgress == false);
                wasScreenPressed = false;
            }
            wasScreenPressed = primaryTouch.isInProgress && IsTouchPositionValid(primaryTouch.startPosition.value) && isClickAllowed;
        }

        void HandelMouseInput()
        {
            var mouse = Mouse.current;
            var mouseDelta = mouse.delta.ReadValue();

            var lookRotationDelta = new Vector2(-mouseDelta.y, mouseDelta.x);
            lookRotationDelta *= LookSensitivity / 60f;
            _lookRotationAccumulator.Accumulate(lookRotationDelta);

            float rightButtonValue = mouse.rightButton.ReadValue();
            if (wasScreenPressed)
            {
                _accumulatedInput.Buttons.Set(EInputButton.Fire, rightButtonValue < 0.15f);
                wasScreenPressed = false;
            }
            wasScreenPressed = rightButtonValue > 0.8f;
        }

        private void UpdateThrowInputState(bool state)
        {
            if (!HasInputAuthority)
                return;

            isThrownInputPressed = state;
        }

        private void HandleThrowInput()
        {
            if (!HasInputAuthority)
                return;

            if (wasThrownInpuPressed)
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throw, isThrownInputPressed == false);
                wasThrownInpuPressed = false;
            }
            else
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throw, false);
            }
            wasThrownInpuPressed = isThrownInputPressed;
        }
        

        [SerializeField, Range(0.1f, 10f)]
        public  float looksSensitivity = 2.0f; // drag in Inspector
        
        //private void HandleGyroscopeInput()
        //{

            
        //    // Gyro orientation
        //    if (AttitudeSensor.current != null && AttitudeSensor.current.IsActuated())
        //    {
        //        Quaternion gyroAttitude = AttitudeSensor.current.attitude.ReadValue();

        //        // Convert coordinate system (important)
        //        gyroAttitude = new Quaternion(gyroAttitude.x, gyroAttitude.y, -gyroAttitude.z, -gyroAttitude.w);

        //        // Optional: calibrate to initial rotation
        //        Quaternion delta = Quaternion.Inverse(gyroInitialRotation) * gyroAttitude;
        //        _accumulatedInput.LookRotationStart = delta.eulerAngles;

        //        _accumulatedInput.Buttons.Set(EInputButton.GyroStart, true);
        //    }

        //    // Gyro angular velocity → look rotation delta
        //    if (Gyroscope.current != null)
        //    {
        //        Vector3 gyroDelta = Gyroscope.current.angularVelocity.ReadValue();

        //        // Apply sensitivity (scales how much movement you get from gyro)
        //        var lookRotationDelta = new Vector2(-gyroDelta.x, -gyroDelta.y);
        //        looksSensitivity = GlobalData.Sencivity;
        //        lookRotationDelta *= looksSensitivity;
        //        _lookRotationAccumulator.Accumulate(lookRotationDelta);
        //    }

        //    var primaryTouch = Touchscreen.current.touches[0];
        //    if (wasScreenPressed)
        //    {
        //        _accumulatedInput.Buttons.Set(EInputButton.Fire, primaryTouch.isInProgress == false);
        //        wasScreenPressed = false;
        //    }
        //    wasScreenPressed = primaryTouch.isInProgress &&
        //                       IsTouchPositionValid(primaryTouch.startPosition.value) &&
        //                       isClickAllowed;
        //}


        //private void HandleGyroscopeInput()
        //{
        //    // Gyro orientation
        //    if (AttitudeSensor.current != null && AttitudeSensor.current.IsActuated())
        //    {
        //        Quaternion gyroAttitude = AttitudeSensor.current.attitude.ReadValue();

        //        // Convert coordinate system (important)
        //        gyroAttitude = new Quaternion(gyroAttitude.x, gyroAttitude.y, -gyroAttitude.z, -gyroAttitude.w);

        //        // Optional: calibrate to initial rotation
        //        Quaternion delta = Quaternion.Inverse(gyroInitialRotation) * gyroAttitude;
        //        _accumulatedInput.LookRotationStart = delta.eulerAngles;

        //        _accumulatedInput.Buttons.Set(EInputButton.GyroStart, true);
        //    }

        //    // Gyro angular velocity → look rotation delta
        //    if (Gyroscope.current != null)
        //    {
        //        Vector3 gyroDelta = Gyroscope.current.angularVelocity.ReadValue();

        //        // Map x/y properly
        //        var lookRotationDelta = new Vector2(-gyroDelta.x, -gyroDelta.y);
        //        lookRotationDelta *= LookSensitivity * 25 / 60f;
        //        _lookRotationAccumulator.Accumulate(lookRotationDelta);
        //    }

        //    var primaryTouch = Touchscreen.current.touches[0];
        //    if (wasScreenPressed)
        //    {
        //        _accumulatedInput.Buttons.Set(EInputButton.Fire, primaryTouch.isInProgress == false);
        //        wasScreenPressed = false;
        //    }
        //    wasScreenPressed = primaryTouch.isInProgress && IsTouchPositionValid(primaryTouch.startPosition.value) && isClickAllowed;
        //}
    }
}
*/


using UnityEngine;
using UnityEngine.InputSystem;
using Gyroscope = UnityEngine.InputSystem.Gyroscope;
using Fusion;

namespace SimpleFPS
{
    [DefaultExecutionOrder(-10)]
    public class CustomPlayerInput_1 : PlayerInput, IBeforeUpdate
    {
        protected bool wasScreenPressed;
        protected float rightButtonValue;
        protected bool isPressed;
        private Rect validTouchZone;
        private bool isClickAllowed;
        //private bool isThrownInputPressed;
        private bool isThrownInputPressedroll;
        private bool isThrownInputPressedshield;
        private bool isThrownInputPressedsmoke;
        private bool isThrownInputPressedflash;
        private bool isThrownInputPressedrope;
       // private bool wasThrownInpuPressed;
        private bool wasThrownInpuPressedroll;
        private bool wasThrownInpuPressedshield;
        private bool wasThrownInpuPressedsmoke;
        private bool wasThrownInpuPressedflash;
        private bool wasThrownInpuPressedrope;

        public float moveRange;
        private float _startZ; // Store the initial position
        //private float _startRotationSign; // Store the initial position
        private float _direction = 1; // Store the initial position
        private float _startDirection = 1; // Store the initial position


        public CustomPlayer customPlayer;

        //private void OnEnable()
        //{
        //    EventChannelManager.AddListener<bool>(GenericEventType.OnClickInput, (state) => isClickAllowed = state);
        //    EventChannelManager.AddListener<bool>(GenericEventType.OnThrowInput, UpdateThrowInputState);
        //    if (Gyroscope.current != null) InputSystem.EnableDevice(Gyroscope.current);
        //    if (AttitudeSensor.current != null) InputSystem.EnableDevice(AttitudeSensor.current);
        //}




        private void OnEnable()
        {
            EventChannelManager.AddListener<bool>(GenericEventType.OnClickInput, (state) => isClickAllowed = state);

          //  EventChannelManager.AddListener<bool>(GenericEventType.OnThrowInput, UpdateThrowInputState);

            EventChannelManager.AddListener<bool>(GenericEventType.OnThrowInputSmoke, UpdateThrowInputStatesmoke);
            EventChannelManager.AddListener<bool>(GenericEventType.OnThrowInputflash, UpdateThrowInputStateflash);
            EventChannelManager.AddListener<bool>(GenericEventType.OnThrowInputshield, UpdateThrowInputStateshield);
            EventChannelManager.AddListener<bool>(GenericEventType.OnThrowInputroll, UpdateThrowInputStateroll);
            EventChannelManager.AddListener<bool>(GenericEventType.OnThrowInputrope, UpdateThrowInputStaterope);

            // 🔹 Force enable devices
            if (Gyroscope.current != null)
            {
                InputSystem.EnableDevice(Gyroscope.current);
               // Debug.Log("[GyroDebug] Gyroscope enabled.");
            }
            else
            {
                Debug.LogWarning("[GyroDebug] Gyroscope.current is NULL!");
            }

            if (AttitudeSensor.current != null)
            {
                InputSystem.EnableDevice(AttitudeSensor.current);
              //  Debug.Log("[GyroDebug] AttitudeSensor enabled.");
            }
            else
            {
                Debug.LogWarning("[GyroDebug] AttitudeSensor.current is NULL!");
            }
        }



        private void OnDisable()
        {
            EventChannelManager.RemoveListener<bool>(GenericEventType.OnClickInput, (state) => isClickAllowed = state);
           // EventChannelManager.RemoveListener<bool>(GenericEventType.OnThrowInput, UpdateThrowInputState);
            EventChannelManager.RemoveListener<bool>(GenericEventType.OnThrowInputSmoke, UpdateThrowInputStatesmoke);
            EventChannelManager.RemoveListener<bool>(GenericEventType.OnThrowInputflash, UpdateThrowInputStateflash);
            EventChannelManager.RemoveListener<bool>(GenericEventType.OnThrowInputshield, UpdateThrowInputStateshield);
            EventChannelManager.RemoveListener<bool>(GenericEventType.OnThrowInputroll, UpdateThrowInputStateroll);
            EventChannelManager.RemoveListener<bool>(GenericEventType.OnThrowInputrope, UpdateThrowInputStaterope);
        }

        private void Start()
        {
            validTouchZone = new Rect(10, 10, Screen.width - 20, Screen.height * (4 / 5f));
            var playerRef = Object.InputAuthority;
            _startZ = transform.position.z;
            _startDirection = _direction = playerRef.AsIndex > 1 ? -1 : 1; //-1 or 1 for 1 2


            if (SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = true;
                //  Vector3 gyroDelta = Gyroscope.current.angularVelocity.ReadValue();
                _gyroOffset = Gyroscope.current.angularVelocity.ReadValue(); // store initial baseline
            }


        }

        private void OnDrawGizmos()
        {
            if (Camera.main == null) return;

            validTouchZone = new Rect(10, 10, Screen.width - 20, Screen.height * (4 / 5f));

            // Convert Rect corners from screen space to world space
            Vector3 bottomLeft = Camera.main.ScreenToWorldPoint(new Vector3(validTouchZone.xMin, validTouchZone.yMin, Camera.main.nearClipPlane));
            Vector3 bottomRight = Camera.main.ScreenToWorldPoint(new Vector3(validTouchZone.xMax, validTouchZone.yMin, Camera.main.nearClipPlane));
            Vector3 topLeft = Camera.main.ScreenToWorldPoint(new Vector3(validTouchZone.xMin, validTouchZone.yMax, Camera.main.nearClipPlane));
            Vector3 topRight = Camera.main.ScreenToWorldPoint(new Vector3(validTouchZone.xMax, validTouchZone.yMax, Camera.main.nearClipPlane));

            // Set Gizmos color
            Gizmos.color = Color.green;

            // Draw box edges
            Gizmos.DrawLine(bottomLeft, bottomRight); // Bottom edge
            Gizmos.DrawLine(bottomRight, topRight);   // Right edge
            Gizmos.DrawLine(topRight, topLeft);       // Top edge
            Gizmos.DrawLine(topLeft, bottomLeft);     // Left edge
        }

        void IBeforeUpdate.BeforeUpdate()
        {


            //if (HasInputAuthority == false)
            //    return;

            HandleMovmentInput();
            HandleCursorState();

            // 🔹 Gyro ko independent rakho
            if (SystemInfo.supportsGyroscope)
            {
                HandleGyroscopeInput();
            }
            else if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                HandleTouchInput();
            }
            else if (Mouse.current != null)
            {
                HandelMouseInput();
            }

            HandleThrowInput();

            
        }

        private void HandleMovmentInput()
        {
            float currentOffset = (transform.position.z - _startZ);

            // Change direction when reaching range limit
            if (currentOffset >= moveRange / 2f)
            {
                _direction = Mathf.Lerp(_direction, 1, 0.35f); // Move left
            }
            else if (currentOffset <= -moveRange / 2f)
            {
                _direction = Mathf.Lerp(_direction, -1, 0.35f); // Move left
            }

            _accumulatedInput.MoveDirection = new Vector2(_direction * _startDirection, 0);
        }

        void HandleCursorState()
        {
            // Enter key is used for locking/unlocking cursor in game view.
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    // Cursor.lockState = CursorLockMode.Locked;
                    // Cursor.visible = false;
                }
            }


        }

        bool IsCursorLocked()
        {
            // Accumulate input only if the cursor is locked.
            if (Cursor.lockState != CursorLockMode.Locked)
                return false;
            return true;
        }




        void HandleTouchInput()
        {

            var primaryTouch = Touchscreen.current.touches[0];

            // Handle touch delta for look rotation
            if (primaryTouch.isInProgress && IsTouchPositionValid(primaryTouch.startPosition.value))
            {
                var touchDelta = primaryTouch.delta.ReadValue();
                var lookRotationDelta = new Vector2(-touchDelta.y, touchDelta.x);
                lookRotationDelta *= LookSensitivity * 3 / 60f;
                _lookRotationAccumulator.Accumulate(lookRotationDelta);
            }


            bool firePressed = primaryTouch.isInProgress && isClickAllowed;
            HandleChargeFire(firePressed);
            // Handle touch pressure for button simulation
            //if (wasScreenPressed)
            //{
            //    _accumulatedInput.Buttons.Set(EInputButton.Fire, primaryTouch.isInProgress == false);
            //    wasScreenPressed = false;
            //}
            //wasScreenPressed = primaryTouch.isInProgress && IsTouchPositionValid(primaryTouch.startPosition.value) && isClickAllowed;
        }
        void HandelMouseInput()
        {
            // Read mouse delta for look rotation
            var mouse = Mouse.current;
            var mouseDelta = mouse.delta.ReadValue();

            // Calculate look rotation delta
            var lookRotationDelta = new Vector2(-mouseDelta.y, mouseDelta.x);
            lookRotationDelta *= LookSensitivity / 60f;
            _lookRotationAccumulator.Accumulate(lookRotationDelta);

            // Handle mouse right button
            //float rightButtonValue = mouse.rightButton.ReadValue();
            //if (wasScreenPressed)
            //{
            //    _accumulatedInput.Buttons.Set(EInputButton.Fire, rightButtonValue < 0.15f);
            //    wasScreenPressed = false;
            //}
            //wasScreenPressed = rightButtonValue > 0.8f; //&& validTouchZone.Contains(mouse.position.value);


            bool firePressed = mouse.leftButton.isPressed;
            HandleChargeFire(firePressed);


        }
        [SerializeField] private float aimChargeTime = .5f; // time to reach 100%
        private float currentAimCharge = 0f;
        private bool isCharging = false;
        private bool isFullyCharged = false;
        private void HandleChargeFire(bool firePressed)
        {
            if (firePressed)
            {
                isCharging = true;

                currentAimCharge += Runner.DeltaTime;

                if (currentAimCharge >= aimChargeTime)
                {
                    currentAimCharge = aimChargeTime;
                    isFullyCharged = true;
                }
            }
            else
            {
                // Button Released
                if (isFullyCharged)
                {
                    // ✅ Only fire if 100% reached
                    _accumulatedInput.Buttons.Set(EInputButton.Fire, true);
                }
                else
                {
                    // ❌ Fast click → No fire
                    _accumulatedInput.Buttons.Set(EInputButton.Fire, false);
                }

                // Reset
                isCharging = false;
                isFullyCharged = false;
                currentAimCharge = 0f;
            }
        }



        bool IsTouchPositionValid(Vector2 value)
        {
            return validTouchZone.Contains(value);
        }

        //private void UpdateThrowInputState(bool state)
        //{
        //    if (!HasInputAuthority)
        //        return;

        //    isThrownInputPressed = state;
        //} 
        private void UpdateThrowInputStateshield(bool state)
        {
            if (!HasInputAuthority)
                return;

            isThrownInputPressedshield = state;
        } 
        private void UpdateThrowInputStatesmoke(bool state)
        {
            if (!HasInputAuthority)
                return;

            isThrownInputPressedsmoke = state;
        } private void UpdateThrowInputStateflash(bool state)
        {
            if (!HasInputAuthority)
                return;

            isThrownInputPressedflash = state;
        } 
        private void UpdateThrowInputStaterope(bool state)
        {
            if (!HasInputAuthority)
                return;

            isThrownInputPressedrope = state;
        } 
        private void UpdateThrowInputStateroll(bool state)
        {
            if (!HasInputAuthority)
                return;

            isThrownInputPressedroll = state;
        }

        //public NetworkedInput GetAccumulatedInput()
        //{
        //    return _accumulatedInput;
        //}

        public NetworkedInput GetAccumulatedInput()
        {
            _accumulatedInput.LookRotationDelta += _lookRotationAccumulator.Consume();

            // Debug.Log($"[GyroDebug] GetAccumulatedInput returning LookRotationDelta={_accumulatedInput.LookRotationDelta}");

            var data = _accumulatedInput;
            _accumulatedInput = default;
            return data;
        }



      

        public void ResetGyro()
        {
            Debug.Log("ResetGyroINputt ");
            _accumulatedInput.Buttons.Set(EInputButton.ResetGyro, true);
        }


        private Quaternion _gyroInitialRotation;
        private bool _gyroResetRequested = false;
        private void HandleThrowInput()
        {
            if (!HasInputAuthority)
                return;

            //if (wasThrownInpuPressed)
            //{
            //    _accumulatedInput.Buttons.Set(EInputButton.Throw, isThrownInputPressed == false);
            //    wasThrownInpuPressed = false;
            //}
            //else
            //{
            //    _accumulatedInput.Buttons.Set(EInputButton.Throw, false);
            //}
            //wasThrownInpuPressed = isThrownInputPressed;

            //shield 1
            if (wasThrownInpuPressedshield)
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throwshield, isThrownInputPressedshield == false);
                wasThrownInpuPressedshield = false;
            }
            else
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throwshield, false);
            }
            wasThrownInpuPressedshield = isThrownInputPressedshield;
            //smoke 2
            if (wasThrownInpuPressedsmoke)
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throwsmoke, isThrownInputPressedsmoke == false);
                wasThrownInpuPressedsmoke = false;
            }
            else
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throwsmoke, false);
            }
            wasThrownInpuPressedsmoke = isThrownInputPressedsmoke;
            //roll 3
            if (wasThrownInpuPressedroll)
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throwroll, isThrownInputPressedroll == false);
                wasThrownInpuPressedroll = false;
            }
            else
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throwroll, false);
            }
            wasThrownInpuPressedroll = isThrownInputPressedroll;

            //rope 4
             if (wasThrownInpuPressedrope)
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throwrope, isThrownInputPressedrope == false);
                wasThrownInpuPressedrope = false;
            }
            else
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throwrope, false);
            }
            wasThrownInpuPressedrope = isThrownInputPressedrope;

            //flash 5
            if (wasThrownInpuPressedflash)
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throwflash, isThrownInputPressedflash == false);
                wasThrownInpuPressedflash = false;
            }
            else
            {
                _accumulatedInput.Buttons.Set(EInputButton.Throwflash, false);
            }
            wasThrownInpuPressedflash = isThrownInputPressedflash;


        }
        private Quaternion gyroInitialRotation = Quaternion.identity;

        private Vector3 _gyroOffset = Vector3.zero;
       

        private void HandleGyroscopeInput()
        {
            if (AttitudeSensor.current != null && AttitudeSensor.current.IsActuated())
            {
                // Debug.Log($"[GyroDebug] HandleGyroscopeInput running, Gyro={Gyroscope.current}, Attitude={AttitudeSensor.current}");

                // First-time calibration
                if (gyroInitialRotation == Quaternion.identity)
                    gyroInitialRotation = AttitudeSensor.current.attitude.ReadValue();

                Quaternion raw = AttitudeSensor.current.attitude.ReadValue();
                Quaternion gyro = new Quaternion(raw.x, raw.y, -raw.z, -raw.w);

                // Quaternion delta = Quaternion.Inverse(gyroInitialRotation) * gyro;

                Quaternion delta = gyroInitialRotation * gyro;

                // ✅ Send orientation baseline to Fusion
                _accumulatedInput.LookRotationDelta = delta.eulerAngles;
                _accumulatedInput.Buttons.Set(EInputButton.GyroStart, true);

            }
            if (Gyroscope.current != null)
            {


                Vector3 gyroDelta = Gyroscope.current.angularVelocity.ReadValue();

                // ✅ Send rotation delta to Fusion
                var lookRotationDelta = new Vector2(-gyroDelta.x, -gyroDelta.y);
                lookRotationDelta *= LookSensitivity * 25f / 60f;

                _lookRotationAccumulator.Accumulate(lookRotationDelta);
                _accumulatedInput.LookRotationDelta = lookRotationDelta;
              //  Debug.Log($"[GyroDebug] HandleGyroscopeInput running, lookRotationDelta ={lookRotationDelta}");
            }

            // Fire button support from touch
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                var primaryTouch = Touchscreen.current.touches[0];

                //if (wasScreenPressed)
                //{
                //    _accumulatedInput.Buttons.Set(EInputButton.Fire, primaryTouch.isInProgress == false);
                //    wasScreenPressed = false;
                //    Debug.Log($"Touchscreen.current");
                //}

                //wasScreenPressed = primaryTouch.isInProgress &&
                //                   IsTouchPositionValid(primaryTouch.startPosition.value) &&
                //                   isClickAllowed;


                bool firePressed = primaryTouch.isInProgress &&
                   IsTouchPositionValid(primaryTouch.startPosition.value) &&
                   isClickAllowed;

                HandleChargeFire(firePressed);


            }
        }


    }
}

