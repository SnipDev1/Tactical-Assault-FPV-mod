using System;
using UnityEngine;
using Il2CppHurricaneVR.Framework.Core;
using Il2CppHurricaneVR.Framework.Core.Player;
using Il2CppHurricaneVR.Framework.Shared;
using Il2CppHurricaneVR.Framework.ControllerInput;
using TacticalAssaultFPV.Drone;
using TacticalAssaultFPV.Config;
using Il2CppInterop.Runtime.Attributes;

namespace TacticalAssaultFPV.Controller;

public class RCTablet : MonoBehaviour
{
    public RCTablet(IntPtr ptr) : base(ptr) { }

    public static bool IsPlayerFrozen { get; private set; } = false;

    private HVRGrabbable _grabbable;
    public static readonly System.Collections.Generic.List<RCTablet> ActiveTablets =
        new System.Collections.Generic.List<RCTablet>();

    private Renderer _screenRenderer;
    private TextMesh _osdText;

    private readonly System.Collections.Generic.List<GameObject> _registeredDrones = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.Dictionary<GameObject, float> _droneBatteries = new System.Collections.Generic.Dictionary<GameObject, float>();
    private int _currentDroneIndex = 0;

    private DronePhysics _pairedDronePhysics;
    private DroneCameraRig _pairedDroneCamera;
    private DroneExplosive _pairedDroneExplosive;
    private Rigidbody _pairedDroneRigidbody;
    private HVRPlayerController _playerController;

    private bool _isHeld;
    private bool _wasHeld;
    private bool _isControlMode;
    private bool _isWarheadArmed;
    private bool _isDroneDetonated;
    private bool _wasRightStickClicked;
    private float _batteryVoltage = 25.2f;
    private float _osdTimer = 0f;
    private const float OsdInterval = 0.08f;

    public float CurrentBatteryVoltage
    {
        get
        {
            if (_pairedDronePhysics != null && _droneBatteries.TryGetValue(_pairedDronePhysics.gameObject, out float v))
            {
                return v;
            }
            return _batteryVoltage;
        }
    }

    [HideFromIl2Cpp]
    public int RegisteredDroneCount => _registeredDrones.Count;

    [HideFromIl2Cpp]
    public int CurrentDroneIndex => _currentDroneIndex;

    [HideFromIl2Cpp]
    public DronePhysics PairedDronePhysics => _pairedDronePhysics;

    [HideFromIl2Cpp]
    public DroneCameraRig PairedDroneCamera => _pairedDroneCamera;

    public static void ResetPlayerFrozen()
    {
        IsPlayerFrozen = false;
        var tablets = UnityEngine.Object.FindObjectsOfType<RCTablet>();
        if (tablets != null)
        {
            foreach (var tab in tablets)
            {
                if (tab != null)
                {
                    tab.ResetFlightState();
                }
            }
        }
    }

    public static void ResetFlightStateStatic() => ResetPlayerFrozen();

    public void ResetFlightState()
    {
        _isControlMode = false;
        IsPlayerFrozen = false;
        if (_pairedDronePhysics != null)
        {
            _pairedDronePhysics.ClimbInput = 0f;
            _pairedDronePhysics.YawInput = 0f;
            _pairedDronePhysics.PitchInput = 0f;
            _pairedDronePhysics.RollInput = 0f;
        }
        UnfreezePlayerMovement();
    }

    private void PruneNullDrones()
    {
        for (int i = _registeredDrones.Count - 1; i >= 0; i--)
        {
            if (_registeredDrones[i] == null)
            {
                _registeredDrones.RemoveAt(i);
            }
        }

        // Clean up stale destroyed GameObject keys from battery telemetry dictionary
        var deadKeys = new System.Collections.Generic.List<GameObject>();
        foreach (var kvp in _droneBatteries)
        {
            if (kvp.Key == null) deadKeys.Add(kvp.Key);
        }
        for (int i = 0; i < deadKeys.Count; i++)
        {
            _droneBatteries.Remove(deadKeys[i]);
        }
    }

    public void Awake()
    {
        if (!ActiveTablets.Contains(this))
        {
            ActiveTablets.Add(this);
        }

        _grabbable = GetComponent<HVRGrabbable>();
        if (_grabbable == null)
        {
            _grabbable = gameObject.AddComponent<HVRGrabbable>();
        }

        Transform screenTransform = transform.Find("Tablet_Visuals/Screen") ?? transform.Find("Screen");
        if (screenTransform != null)
        {
            _screenRenderer = screenTransform.GetComponent<Renderer>();
        }
        else
        {
            _screenRenderer = GetComponent<Renderer>();
        }

        SetupOSD();
    }

    private void SetupOSD()
    {
        Transform visualRoot = transform.Find("Tablet_Visuals") ?? transform;

        // Front OSD Text: placed 1.5mm in front of screen quad (-0.009f) to eliminate z-fighting
        GameObject osdObj = new GameObject("OSD_Text");
        osdObj.transform.SetParent(visualRoot, false);
        osdObj.transform.localPosition = new Vector3(0f, 0f, -0.0105f);
        osdObj.transform.localRotation = Quaternion.identity;
        osdObj.transform.localScale = Vector3.one;

        _osdText = osdObj.AddComponent<TextMesh>();
        _osdText.alignment = TextAlignment.Center;
        _osdText.anchor = TextAnchor.MiddleCenter;
        _osdText.fontSize = 24;
        _osdText.characterSize = 0.0013f;
        _osdText.color = Color.green;
        _osdText.lineSpacing = 1.10f;

        var mr = _osdText.GetComponent<MeshRenderer>();
        if (mr != null && mr.material != null)
        {
            mr.material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
        }
    }

    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    public void RegisterDrone(GameObject drone)
    {
        if (drone != null && !_registeredDrones.Contains(drone))
        {
            _registeredDrones.Add(drone);
            if (!_droneBatteries.ContainsKey(drone))
            {
                _droneBatteries[drone] = 25.2f;
            }
            if (_registeredDrones.Count == 1)
            {
                ConnectToDrone(0);
            }
        }
    }

    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    public void RegisterDrones(System.Collections.Generic.List<GameObject> drones)
    {
        _registeredDrones.Clear();
        if (drones != null)
        {
            for (int i = 0; i < drones.Count; i++)
            {
                var d = drones[i];
                if (d != null && !_registeredDrones.Contains(d))
                {
                    _registeredDrones.Add(d);
                    if (!_droneBatteries.ContainsKey(d))
                    {
                        _droneBatteries[d] = 25.2f;
                    }
                }
            }
        }

        if (_registeredDrones.Count > 0)
        {
            _currentDroneIndex = Mathf.Clamp(_currentDroneIndex, 0, _registeredDrones.Count - 1);
            ConnectToDrone(_currentDroneIndex);
        }
    }

    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    public void UnregisterDrone(GameObject drone)
    {
        if (drone != null && _registeredDrones.Contains(drone))
        {
            int index = _registeredDrones.IndexOf(drone);
            _registeredDrones.Remove(drone);
            _droneBatteries.Remove(drone);

            if (_pairedDronePhysics != null && _pairedDronePhysics.gameObject == drone)
            {
                _pairedDronePhysics = null;
                _pairedDroneCamera = null;
                _pairedDroneExplosive = null;
                _pairedDroneRigidbody = null;

                if (_registeredDrones.Count > 0)
                {
                    _currentDroneIndex = Mathf.Clamp(index, 0, _registeredDrones.Count - 1);
                    ConnectToDrone(_currentDroneIndex);
                }
            }
        }
    }

    public void ConnectToDrone(int index)
    {
        PruneNullDrones();
        if (_registeredDrones.Count == 0) return;
        index = (index + _registeredDrones.Count) % _registeredDrones.Count;
        _currentDroneIndex = index;

        GameObject targetDrone = _registeredDrones[_currentDroneIndex];
        if (targetDrone == null) return;

        // When switching active drones, zero out control stick inputs on the previously active drone.
        // If it was airborne, it remains armed and stays auto-hovering in place!
        if (_pairedDronePhysics != null && _pairedDronePhysics.gameObject != targetDrone)
        {
            _pairedDronePhysics.ClimbInput = 0f;
            _pairedDronePhysics.YawInput = 0f;
            _pairedDronePhysics.PitchInput = 0f;
            _pairedDronePhysics.RollInput = 0f;
        }

        if (_pairedDroneCamera != null && _pairedDroneCamera.gameObject != targetDrone)
        {
            _pairedDroneCamera.SetStreaming(false);
        }

        var phys = targetDrone.GetComponent<DronePhysics>();
        var cam = targetDrone.GetComponent<DroneCameraRig>();
        PairWithDrone(phys, cam);

        MelonLoader.MelonLogger.Msg($"[RCTablet] Connected to Drone #{_currentDroneIndex + 1}/{_registeredDrones.Count} ('{targetDrone.name}')");
    }

    public void SwitchToNextDrone()
    {
        PruneNullDrones();
        if (_registeredDrones.Count == 0) return;

        int nextIndex = -1;
        for (int i = 1; i <= _registeredDrones.Count; i++)
        {
            int candidate = (_currentDroneIndex + i) % _registeredDrones.Count;
            if (_registeredDrones[candidate] != null)
            {
                var exp = _registeredDrones[candidate].GetComponent<DroneExplosive>();
                if (exp == null || !exp.HasDetonated)
                {
                    nextIndex = candidate;
                    break;
                }
            }
        }

        // If no other live drone found, check if current drone itself is live
        if (nextIndex < 0 && _currentDroneIndex >= 0 && _currentDroneIndex < _registeredDrones.Count)
        {
            var curDrone = _registeredDrones[_currentDroneIndex];
            if (curDrone != null)
            {
                var exp = curDrone.GetComponent<DroneExplosive>();
                if (exp == null || !exp.HasDetonated)
                {
                    nextIndex = _currentDroneIndex;
                }
            }
        }

        if (nextIndex >= 0)
        {
            if (nextIndex != _currentDroneIndex || _pairedDronePhysics == null || _pairedDronePhysics.gameObject != _registeredDrones[nextIndex])
            {
                ConnectToDrone(nextIndex);
            }
        }
    }

    public void PairWithDrone(DronePhysics dronePhysics, DroneCameraRig droneCamera)
    {
        if (_pairedDroneExplosive != null)
        {
            _pairedDroneExplosive.OnDetonated -= HandleDroneDetonated;
        }

        _pairedDronePhysics = dronePhysics;
        _pairedDroneCamera = droneCamera;
        _pairedDroneRigidbody = _pairedDronePhysics != null ? _pairedDronePhysics.GetComponent<Rigidbody>() : null;
        _isDroneDetonated = false;

        if (_pairedDronePhysics != null)
        {
            _pairedDroneExplosive = _pairedDronePhysics.GetComponent<DroneExplosive>();
            if (_pairedDroneExplosive != null)
            {
                _pairedDroneExplosive.OnDetonated += HandleDroneDetonated;
            }

            // Sync per-drone battery voltage (D7)
            var key = _pairedDronePhysics.gameObject;
            if (!_droneBatteries.TryGetValue(key, out float v))
            {
                v = 25.2f;
                _droneBatteries[key] = v;
            }
            _batteryVoltage = v;

            // Read the UNIQUE individual state of this specific drone!
            _isControlMode = _pairedDronePhysics.IsArmed;
            _isWarheadArmed = _pairedDroneExplosive != null && _pairedDroneExplosive.IsArmed;
            IsPlayerFrozen = _isControlMode;
        }
        else
        {
            _isControlMode = false;
            _isWarheadArmed = false;
            IsPlayerFrozen = false;
        }

        if (_pairedDroneCamera != null)
        {
            _pairedDroneCamera.SetStreaming(true);
            if (_screenRenderer != null && _pairedDroneCamera.StreamTexture != null)
            {
                var mat = _screenRenderer.material;
                if (mat != null)
                {
                    mat.mainTexture = _pairedDroneCamera.StreamTexture;
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _pairedDroneCamera.StreamTexture);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _pairedDroneCamera.StreamTexture);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                    mat.color = Color.white;
                }
            }
        }

        MelonLoader.MelonLogger.Msg($"[RCTablet] Paired successfully with Drone! FlightArmed={_isControlMode}, WarheadArmed={_isWarheadArmed}");
    }

    private void HandleDroneDetonated()
    {
        MelonLoader.MelonLogger.Msg($"[RCTablet] Drone #{_currentDroneIndex + 1} detonated!");

        if (_screenRenderer != null && _screenRenderer.material != null)
        {
            var mat = _screenRenderer.material;
            mat.mainTexture = null;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
            Color dark = new Color(0.04f, 0.05f, 0.06f);
            mat.color = dark;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", dark);
        }

        // Check if there are other undetonated drones remaining without lambda allocation
        bool hasOtherLiveDrones = false;
        for (int i = 0; i < _registeredDrones.Count; i++)
        {
            if (_registeredDrones[i] != null && i != _currentDroneIndex)
            {
                var exp = _registeredDrones[i].GetComponent<DroneExplosive>();
                if (exp == null || !exp.HasDetonated)
                {
                    hasOtherLiveDrones = true;
                    break;
                }
            }
        }

        if (hasOtherLiveDrones)
        {
            MelonLoader.MelonCoroutines.Start(DelayedSwitchToNextDrone(0.5f));
        }
        else
        {
            _isDroneDetonated = true;
            ResetFlightState();

            // Auto respawn fresh drone at armory testing station after 2 seconds
            MelonLoader.MelonCoroutines.Start(DelayedStationRespawn(2.0f));
        }
    }

    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private System.Collections.IEnumerator DelayedSwitchToNextDrone(float delay)
    {
        yield return new WaitForSeconds(delay);
        SwitchToNextDrone();
    }

    [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
    private static System.Collections.IEnumerator DelayedStationRespawn(float delay)
    {
        yield return new WaitForSeconds(delay);
        TacticalAssaultFPV.Loadout.FPVLoadoutIntegration.RespawnStationItems();
    }

    private void TryProximityHolster()
    {
        if (_grabbable == null || _grabbable.IsHandGrabbed || _grabbable.IsSocketed) return;

        try
        {
            var sockets = UnityEngine.Object.FindObjectsOfType<Il2CppHurricaneVR.Framework.Core.Grabbers.HVRSocket>();
            if (sockets == null) return;

            Il2CppHurricaneVR.Framework.Core.Grabbers.HVRSocket bestSocket = null;
            float bestDist = 0.35f;

            Vector3 tabletPos = transform.position;
            foreach (var s in sockets)
            {
                if (s == null || s.GrabbedTarget != null) continue;
                string sName = s.gameObject.name.ToLower();
                if (sName.Contains("utility") || sName.Contains("grenade") || sName.StartsWith("utilityholstersocket"))
                {
                    float d = Vector3.Distance(tabletPos, s.transform.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestSocket = s;
                    }
                }
            }

            if (bestSocket != null)
            {
                MelonLoader.MelonLogger.Msg($"[RCTablet] Proximity holster assist: snapping tablet into vest pouch '{bestSocket.gameObject.name}' (d={bestDist:F2}m)");
                TacticalAssaultFPV.Loadout.FPVLoadoutIntegration.PrepareSocketForGrabbable(bestSocket);
                bestSocket.TryGrab(_grabbable, true, false);
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"[RCTablet] TryProximityHolster error: {ex.Message}");
        }
    }

    public void Update()
    {
        _isHeld = _grabbable != null && _grabbable.IsHandGrabbed;
        bool isSocketed = _grabbable != null && _grabbable.IsSocketed;

        // Reset flight state and player locomotion freeze immediately when stowed into pouch or dropped
        if (isSocketed || (_wasHeld && !_isHeld))
        {
            if (_isControlMode || IsPlayerFrozen)
            {
                MelonLoader.MelonLogger.Msg("[RCTablet] Tablet stowed into vest or dropped -> Resetting flight control and unfreezing player.");
                ResetFlightState();
            }

            // Proximity holster assist: if tablet was just released near a vest pouch (within 0.35m),
            // ensure it smoothly sockets back into the vest pouch instead of dropping to the floor!
            if (!isSocketed && (_wasHeld && !_isHeld))
            {
                TryProximityHolster();
            }
        }
        _wasHeld = _isHeld;

        if (_playerController == null)
        {
            _playerController = UnityEngine.Object.FindObjectOfType<HVRPlayerController>();
        }

        // Reconnect if paired drone reference is lost
        if (_pairedDronePhysics == null)
        {
            PruneNullDrones();
            if (_registeredDrones.Count > 0)
            {
                _currentDroneIndex = Mathf.Clamp(_currentDroneIndex, 0, _registeredDrones.Count - 1);
                ConnectToDrone(_currentDroneIndex);
            }
        }

        // Ensure active stream texture remains bound to screen, or display sleek dark LCD in standby
        if (_pairedDroneCamera != null && _pairedDroneCamera.StreamTexture != null && _screenRenderer != null)
        {
            var mat = _screenRenderer.material;
            if (mat != null)
            {
                if (mat.mainTexture != _pairedDroneCamera.StreamTexture)
                {
                    mat.mainTexture = _pairedDroneCamera.StreamTexture;
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _pairedDroneCamera.StreamTexture);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _pairedDroneCamera.StreamTexture);
                }
                mat.color = Color.white;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            }
        }
        else if (_screenRenderer != null && _screenRenderer.material != null)
        {
            var mat = _screenRenderer.material;
            if (mat.mainTexture != null)
            {
                mat.mainTexture = null;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
            }
            Color dark = new Color(0.04f, 0.05f, 0.06f);
            mat.color = dark;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", dark);
        }

        ProcessVRInputs();

        if (_isControlMode)
        {
            FreezePlayerMovement();
            ApplyDroneFlightInputs();
            UpdateBattery();
        }

        UpdateOSD();
    }

    private void ProcessVRInputs()
    {
        // Inputs can be triggered whenever holding the tablet, or if control mode is active
        if (!_isHeld && !_isControlMode) return;

        HVRController left = _playerController?.Inputs?.LeftController 
                          ?? HVRInputManager.Instance?.LeftController 
                          ?? HVRInputManager.Instance?.LeftXRInputController;

        HVRController right = _playerController?.Inputs?.RightController 
                           ?? HVRInputManager.Instance?.RightController 
                           ?? HVRInputManager.Instance?.RightXRInputController;

        // Left Controller: Flight Activation (X) & Warhead Armed Toggle (Y)
        bool flightControlToggle = (left != null && left.PrimaryButtonState.JustActivated);
        bool warheadToggle = (left != null && left.SecondaryButtonState.JustActivated);

        // Right Controller: Step Zoom In (A) & Step Zoom Out (B)
        bool zoomInPressed = (right != null && right.PrimaryButtonState.JustActivated);
        bool zoomOutPressed = (right != null && right.SecondaryButtonState.JustActivated);

        if (flightControlToggle && _pairedDronePhysics != null)
        {
            if (!_isControlMode)
            {
                // Resume / enter control mode. If drone was hovering/disarmed, ensure it is armed and flying!
                if (!_pairedDronePhysics.IsArmed)
                {
                    _pairedDronePhysics.SetArmed(true);
                }
                _isControlMode = true;
                IsPlayerFrozen = true;
                MelonLoader.MelonLogger.Msg($"[RCTablet] Left Button X Pressed -> Resumed Drone Flight Control (Armed=True, LocomotionFrozen=True)");
            }
            else
            {
                // Actively controlling -> disarm flight motors and exit control mode
                _pairedDronePhysics.SetArmed(false);
                _isControlMode = false;
                IsPlayerFrozen = false;
                UnfreezePlayerMovement();
                _pairedDronePhysics.ClimbInput = 0f;
                _pairedDronePhysics.YawInput = 0f;
                _pairedDronePhysics.PitchInput = 0f;
                _pairedDronePhysics.RollInput = 0f;
                MelonLoader.MelonLogger.Msg($"[RCTablet] Left Button X Pressed -> Disarmed Drone & Exited Control Mode");
            }
        }

        if (warheadToggle && _pairedDroneExplosive != null)
        {
            bool newWarheadState = !_pairedDroneExplosive.IsArmed;
            _pairedDroneExplosive.SetArmed(newWarheadState);
            _isWarheadArmed = newWarheadState;
            MelonLoader.MelonLogger.Msg($"[RCTablet] Left Button Y Pressed: Drone #{_currentDroneIndex + 1} Warhead Armed = {_isWarheadArmed}");
        }

        // Right Controller Zoom Controls (A = Zoom In, B = Zoom Out: 1.0x -> 2.0x -> 4.0x -> ...)
        if (zoomInPressed && _pairedDroneCamera != null)
        {
            _pairedDroneCamera.ZoomIn();
            MelonLoader.MelonLogger.Msg($"[RCTablet] Right Button A Pressed -> Zoom In: {_pairedDroneCamera.CurrentZoom:F1}X");
        }
        if (zoomOutPressed && _pairedDroneCamera != null)
        {
            _pairedDroneCamera.ZoomOut();
            MelonLoader.MelonLogger.Msg($"[RCTablet] Right Button B Pressed -> Zoom Out: {_pairedDroneCamera.CurrentZoom:F1}X");
        }

        // Camera Tilt Adjust (Left Trigger = Tilt Down, Right Trigger = Tilt Up)
        if (_pairedDroneCamera != null)
        {
            float lt = left != null ? left.Trigger : 0f;
            float rt = right != null ? right.Trigger : 0f;
            const float deadzone = 0.15f;
            const float tiltSpeed = 35f; // degrees per second at full trigger pull

            if (rt > deadzone)
            {
                float strength = (rt - deadzone) / (1f - deadzone);
                _pairedDroneCamera.AdjustTilt(strength * tiltSpeed * Time.deltaTime);
            }
            if (lt > deadzone)
            {
                float strength = (lt - deadzone) / (1f - deadzone);
                _pairedDroneCamera.AdjustTilt(-strength * tiltSpeed * Time.deltaTime);
            }
        }

        // Right Stick Click: Switch Active Drone (1 -> 2 -> 3 -> 1)
        bool rStickState = (right != null) && (right.JoystickButtonState.JustActivated || right.JoystickClicked);
        bool rStickClicked = rStickState && !_wasRightStickClicked;
        _wasRightStickClicked = rStickState;

        if (rStickClicked)
        {
            MelonLoader.MelonLogger.Msg("[RCTablet] Right stick clicked -> Switching active drone!");
            SwitchToNextDrone();
        }
    }

    private void FreezePlayerMovement()
    {
        if (_playerController == null) return;

        // Locomotion and rotation are cleanly blocked via Harmony prefix on HandleMovement & HandleRotation.
        // We only zero current velocity here to prevent sliding.
        _playerController.move = Vector3.zero;
        _playerController.xzVelocity = Vector3.zero;

        if (_playerController.RigidBody != null)
        {
            _playerController.RigidBody.linearVelocity = new Vector3(0f, _playerController.RigidBody.linearVelocity.y, 0f);
        }
    }

    private void UnfreezePlayerMovement()
    {
        // Locomotion unfreezes automatically via Harmony prefix when IsPlayerFrozen becomes false.
    }

    private void ApplyDroneFlightInputs()
    {
        if (_pairedDronePhysics == null) return;

        Vector2 leftStick = ReadLeftStick();
        Vector2 rightStick = ReadRightStick();

        // Left Stick: Y = Climb/Descend, X = Yaw
        _pairedDronePhysics.ClimbInput = leftStick.y;
        _pairedDronePhysics.YawInput = leftStick.x;

        // Right Stick: Y = Pitch (Forward/Back), X = Roll (Left/Right)
        _pairedDronePhysics.PitchInput = rightStick.y;
        _pairedDronePhysics.RollInput = rightStick.x;
    }

    private Vector2 ReadLeftStick()
    {
        HVRController left = _playerController?.Inputs?.LeftController 
                          ?? HVRInputManager.Instance?.LeftController 
                          ?? HVRInputManager.Instance?.LeftXRInputController;

        if (left != null)
        {
            if (left.JoystickAxis.sqrMagnitude > 0.01f) return left.JoystickAxis;
            if (left.TrackpadAxis.sqrMagnitude > 0.01f) return left.TrackpadAxis;
        }

        if (_playerController?.Inputs != null)
        {
            Vector2 move = _playerController.Inputs.MovementAxis;
            if (move.sqrMagnitude > 0.01f) return move;
            Vector2 fnMove = _playerController.Inputs.GetMovementAxis();
            if (fnMove.sqrMagnitude > 0.01f) return fnMove;
        }

        return Vector2.zero;
    }

    private Vector2 ReadRightStick()
    {
        HVRController right = _playerController?.Inputs?.RightController 
                           ?? HVRInputManager.Instance?.RightController 
                           ?? HVRInputManager.Instance?.RightXRInputController;

        if (right != null)
        {
            if (right.JoystickAxis.sqrMagnitude > 0.01f) return right.JoystickAxis;
            if (right.TrackpadAxis.sqrMagnitude > 0.01f) return right.TrackpadAxis;
        }

        if (_playerController?.Inputs != null)
        {
            Vector2 turn = _playerController.Inputs.TurnAxis;
            if (turn.sqrMagnitude > 0.01f) return turn;
            Vector2 fnTurn = _playerController.Inputs.GetTurnAxis();
            if (fnTurn.sqrMagnitude > 0.01f) return fnTurn;
        }

        return Vector2.zero;
    }

    private void UpdateBattery()
    {
        if (_pairedDronePhysics != null && _pairedDronePhysics.gameObject != null && _pairedDronePhysics.IsArmed)
        {
            var key = _pairedDronePhysics.gameObject;
            if (!_droneBatteries.TryGetValue(key, out float voltage))
            {
                voltage = 25.2f;
            }
            voltage = Mathf.Max(21.0f, voltage - (0.04f * Time.deltaTime));
            _droneBatteries[key] = voltage;
            _batteryVoltage = voltage;
        }
    }

    private void UpdateOSD()
    {
        if (_osdText == null) return;

        // Throttle OSD updates to avoid 90-120Hz GC and TextMesh vertex regeneration churn (D8)
        _osdTimer += Time.deltaTime;
        if (_osdTimer < OsdInterval) return;
        _osdTimer = 0f;

        PruneNullDrones();
        int totalDrones = Mathf.Max(1, _registeredDrones.Count);
        int currentDroneNum = _currentDroneIndex + 1;

        bool allDead = _registeredDrones.Count > 0;
        for (int i = 0; i < _registeredDrones.Count; i++)
        {
            var drone = _registeredDrones[i];
            if (drone != null)
            {
                var exp = drone.GetComponent<DroneExplosive>();
                if (exp != null && !exp.HasDetonated)
                {
                    allDead = false;
                    break;
                }
            }
        }

        if (_isDroneDetonated && allDead)
        {
            _osdText.text = "NO LINK\nALL DRONES EXPENDED";
            _osdText.color = Color.red;
            return;
        }

        if (_registeredDrones.Count == 0)
        {
            _osdText.text = "FPV STRIKE SYSTEM\n\n- STANDBY -\nGRAB DRONE & PRESS TRIGGER TO LINK";
            _osdText.color = new Color(0.2f, 0.85f, 1.0f);
            return;
        }

        if (_pairedDronePhysics == null)
        {
            _osdText.text = $"DRN [{currentDroneNum}/{totalDrones}] NO LINK\n[R-STICK] NEXT DRONE";
            _osdText.color = Color.red;
            return;
        }

        bool flightArmed = _pairedDronePhysics.IsArmed;
        bool warheadArmed = _pairedDroneExplosive != null && _pairedDroneExplosive.IsArmed;

        string flightMode = flightArmed ? "ARM [FLIGHT]" : "DISARM [SAFE]";
        string droneTag = $"DRN {currentDroneNum}/{totalDrones}";

        Color statusColor;
        if (warheadArmed)
        {
            statusColor = Color.red;
        }
        else if (flightArmed)
        {
            statusColor = Color.green;
        }
        else
        {
            statusColor = new Color(0.2f, 0.85f, 1.0f); // Soft cyan
        }
        _osdText.color = statusColor;

        float altitude = _pairedDronePhysics.transform.position.y;
        float speed = _pairedDroneRigidbody != null ? _pairedDroneRigidbody.linearVelocity.magnitude : 0f;
        float camTilt = _pairedDroneCamera?.CurrentTiltAngle ?? 28f;
        float zoom = _pairedDroneCamera?.CurrentZoom ?? 1.0f;
        float battery = (_pairedDronePhysics != null && _droneBatteries.TryGetValue(_pairedDronePhysics.gameObject, out float bat)) ? bat : _batteryVoltage;

        // Monospace-aligned Betaflight / DJI FPV style OSD layout:
        // Reticle [ + ] ALWAYS remains dead center in all flight/armed states!
        string lineTop = $"{flightMode,-15} {droneTag,8} {battery:F1}V\n";
        string lineMid = warheadArmed 
            ? "    - - [ ! WARHEAD ARMED ! ] - -\n        - -  [ + ]  - -\n\n" 
            : "\n        - -  [ + ]  - -\n\n";
        string lineBot = $"CAM {camTilt:F0}° [{zoom:F1}X]  ALT {altitude:F1}M  SPD {speed:F1}M/S    [R-STICK]";

        _osdText.text = lineTop + lineMid + lineBot;
    }

    public void OnDestroy()
    {
        ActiveTablets.Remove(this);

        if (_pairedDroneExplosive != null)
        {
            _pairedDroneExplosive.OnDetonated -= HandleDroneDetonated;
        }

        ResetFlightState();
    }
}
