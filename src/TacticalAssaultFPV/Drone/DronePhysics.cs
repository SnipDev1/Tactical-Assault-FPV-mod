using System;
using UnityEngine;
using Il2CppHurricaneVR.Framework.Core;
using Il2CppInterop.Runtime.Attributes;
using TacticalAssaultFPV.Config;

namespace TacticalAssaultFPV.Drone;

public class DronePhysics : MonoBehaviour
{
    public DronePhysics(IntPtr ptr) : base(ptr) { }

    private const float Deg2Rad = 0.0174532925f;

    private Rigidbody _rb;
    private HVRGrabbable _grabbable;
    private bool _isArmed;
    private bool _isPoweredOn;

    public bool IsArmed => _isArmed;
    public bool IsPoweredOn => _isPoweredOn;

    [HideFromIl2Cpp]
    internal GameObject StatusLED { get; set; }

    // Simplified arcade control inputs:
    public float ClimbInput { get; set; } = 0f;    // Left stick Y: Up / Down altitude
    public float YawInput { get; set; } = 0f;      // Left stick X: Rotate heading
    public float PitchInput { get; set; } = 0f;    // Right stick Y: Tilt forward / backward
    public float RollInput { get; set; } = 0f;     // Right stick X: Tilt / strafe left / right

    [HideFromIl2Cpp]
    internal Transform[] Propellers { get; set; } = Array.Empty<Transform>();
    [HideFromIl2Cpp]
    internal AudioSource MotorAudio { get; set; }

    private float _targetYawAngle;
    private float _gravity;
    private float _hoverThrust;
    private float _holdAltitude;
    private bool _hasHoldAltitude;
    private float _lastAppliedVerticalForce;

    [HideFromIl2Cpp]
    public float TargetYawAngle => _targetYawAngle;

    [HideFromIl2Cpp]
    public float HoldAltitude => _holdAltitude;

    [HideFromIl2Cpp]
    public bool HasHoldAltitude => _hasHoldAltitude;

    [HideFromIl2Cpp]
    public float HoverThrust => _hoverThrust;

    [HideFromIl2Cpp]
    public float LastAppliedVerticalForce => _lastAppliedVerticalForce;

    public void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
        }
        _grabbable = GetComponent<HVRGrabbable>();

        _rb.mass = 1.2f;
        _rb.linearDamping = 1.2f;   // Damps lateral drift smoothly
        _rb.angularDamping = 6.0f;  // Prevents spinning out or violent flips
        _rb.maxAngularVelocity = 12f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        _gravity = Mathf.Abs(Physics.gravity.y);
        if (_gravity < 0.01f) _gravity = 9.81f;
        _hoverThrust = _rb.mass * _gravity;
        _targetYawAngle = transform.eulerAngles.y;
    }

    public void SetArmed(bool armed)
    {
        if (armed && !_isPoweredOn)
        {
            MelonLoader.MelonLogger.Warning("[DronePhysics] Cannot arm flight motors: Drone is not powered on! Pull trigger in hand first.");
            return;
        }

        _isArmed = armed;
        if (!_isArmed)
        {
            ClimbInput = 0f;
            YawInput = 0f;
            PitchInput = 0f;
            RollInput = 0f;
            _hasHoldAltitude = false;
            _lastAppliedVerticalForce = 0f;
            if (MotorAudio != null)
            {
                MotorAudio.Stop();
            }
        }
        else
        {
            _targetYawAngle = transform.eulerAngles.y;
            _holdAltitude = transform.position.y;
            _hasHoldAltitude = true;
            if (MotorAudio != null && !MotorAudio.isPlaying)
            {
                MotorAudio.Play();
            }
        }
    }

    public void PowerOn()
    {
        if (_isPoweredOn) return;
        _isPoweredOn = true;

        MelonLoader.MelonLogger.Msg($"[DronePhysics] Drone '{gameObject.name}' POWERED ON via pilot trigger! Binding to RC Tablet...");

        // 1. Play authentic ESC startup chimes
        DroneMotorAudio.PlayPowerOnSound(transform.position);

        // 2. Light up status LED (neon green)
        if (StatusLED != null)
        {
            var r = StatusLED.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                m.color = new Color(0.1f, 1.0f, 0.35f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.1f, 1.0f, 0.35f));
                r.material = m;
            }
        }

        // 3. Activate camera stream
        var camRig = GetComponent<DroneCameraRig>();
        if (camRig != null)
        {
            camRig.SetStreaming(true);
        }

        // 4. Register with RC Tablet
        for (int i = 0; i < Controller.RCTablet.ActiveTablets.Count; i++)
        {
            var tab = Controller.RCTablet.ActiveTablets[i];
            if (tab != null)
            {
                tab.RegisterDrone(gameObject);
            }
        }
        if (Loadout.FPVLoadoutIntegration.ActiveEquippedTablet != null)
        {
            var tab = Loadout.FPVLoadoutIntegration.ActiveEquippedTablet.GetComponent<Controller.RCTablet>();
            if (tab != null)
            {
                tab.RegisterDrone(gameObject);
            }
        }

        // 5. Controller haptic pulse on grabbing hand
        try
        {
            var left = Il2CppHurricaneVR.Framework.ControllerInput.HVRInputManager.Instance?.LeftController;
            var right = Il2CppHurricaneVR.Framework.ControllerInput.HVRInputManager.Instance?.RightController;
            var handGrabber = _grabbable?.PrimaryGrabber?.TryCast<Il2CppHurricaneVR.Framework.Core.Grabbers.HVRHandGrabber>();
            if (handGrabber != null && handGrabber.HandSide == Il2CppHurricaneVR.Framework.Shared.HVRHandSide.Left)
            {
                left?.Vibrate(0.7f, 0.25f, 50f);
            }
            else if (handGrabber != null && handGrabber.HandSide == Il2CppHurricaneVR.Framework.Shared.HVRHandSide.Right)
            {
                right?.Vibrate(0.7f, 0.25f, 50f);
            }
        }
        catch { }
    }

    public void PowerOff()
    {
        if (!_isPoweredOn) return;
        _isPoweredOn = false;
        _isArmed = false;

        MelonLoader.MelonLogger.Msg($"[DronePhysics] Drone '{gameObject.name}' returned to vest pouch -> Powered OFF & Disarmed.");

        // 1. Turn off status LED
        if (StatusLED != null)
        {
            var r = StatusLED.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                Color dark = new Color(0.12f, 0.12f, 0.12f);
                m.color = dark;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", dark);
                r.material = m;
            }
        }

        // 2. Stop motor audio
        if (MotorAudio != null && MotorAudio.isPlaying)
        {
            MotorAudio.Stop();
        }

        // 3. Pause camera stream
        var camRig = GetComponent<DroneCameraRig>();
        if (camRig != null)
        {
            camRig.SetStreaming(false);
        }

        // 4. Unregister from RC Tablets
        for (int i = 0; i < Controller.RCTablet.ActiveTablets.Count; i++)
        {
            var tab = Controller.RCTablet.ActiveTablets[i];
            if (tab != null)
            {
                tab.UnregisterDrone(gameObject);
            }
        }
    }

    private bool _wasSocketed;

    private void CheckSocketState()
    {
        if (_grabbable == null)
        {
            _grabbable = GetComponent<HVRGrabbable>();
        }
        if (_grabbable == null) return;

        bool isSocketed = _grabbable.IsSocketed;
        if (isSocketed && !_wasSocketed)
        {
            if (_isPoweredOn)
            {
                PowerOff();
            }
        }
        _wasSocketed = isSocketed;
    }

    private void CheckPowerOnInput()
    {
        if (_isPoweredOn) return;

        if (_grabbable == null)
        {
            _grabbable = GetComponent<HVRGrabbable>();
        }

        // Must be held in the player's VR hand to power on
        if (_grabbable == null || !_grabbable.IsHandGrabbed) return;

        bool triggerPressed = false;

        var left = Il2CppHurricaneVR.Framework.ControllerInput.HVRInputManager.Instance?.LeftController
                ?? Il2CppHurricaneVR.Framework.ControllerInput.HVRInputManager.Instance?.LeftXRInputController;
        var right = Il2CppHurricaneVR.Framework.ControllerInput.HVRInputManager.Instance?.RightController
                 ?? Il2CppHurricaneVR.Framework.ControllerInput.HVRInputManager.Instance?.RightXRInputController;

        var handGrabber = _grabbable.PrimaryGrabber?.TryCast<Il2CppHurricaneVR.Framework.Core.Grabbers.HVRHandGrabber>();
        if (handGrabber != null)
        {
            if (handGrabber.HandSide == Il2CppHurricaneVR.Framework.Shared.HVRHandSide.Left && left != null)
            {
                if (left.TriggerButtonState.Active || left.TriggerButtonState.JustActivated || left.Trigger > 0.35f) triggerPressed = true;
            }
            else if (handGrabber.HandSide == Il2CppHurricaneVR.Framework.Shared.HVRHandSide.Right && right != null)
            {
                if (right.TriggerButtonState.Active || right.TriggerButtonState.JustActivated || right.Trigger > 0.35f) triggerPressed = true;
            }
        }

        // Fallback: if hand grabber side not determined, any trigger pull while holding drone activates it
        if (!triggerPressed)
        {
            if (left != null && (left.TriggerButtonState.Active || left.TriggerButtonState.JustActivated || left.Trigger > 0.35f))
            {
                triggerPressed = true;
            }
            else if (right != null && (right.TriggerButtonState.Active || right.TriggerButtonState.JustActivated || right.Trigger > 0.35f))
            {
                triggerPressed = true;
            }
        }

        if (triggerPressed)
        {
            PowerOn();
        }
    }

    public void Update()
    {
        CheckSocketState();
        CheckPowerOnInput();
        AnimatePropellers();
        UpdateMotorAudio();
    }

    public void FixedUpdate()
    {
        if (!_isArmed || _rb == null) return;

        if (_grabbable == null)
        {
            _grabbable = GetComponent<HVRGrabbable>();
        }

        if (_grabbable != null && (_grabbable.IsHandGrabbed || _grabbable.IsSocketed))
        {
            _holdAltitude = transform.position.y;
            _targetYawAngle = transform.eulerAngles.y;
            return;
        }

        ApplyVerticalFlight();
        ApplyAttitudeAndYaw();
        ApplyHorizontalFlight();
    }

    private void ApplyVerticalFlight()
    {
        float vy = _rb.linearVelocity.y;
        float y = transform.position.y;

        if (Mathf.Abs(ClimbInput) > 0.08f)
        {
            _hasHoldAltitude = false;
            float targetVy = ClimbInput > 0f ? ClimbInput * 3.5f : ClimbInput * 2.5f;
            float error = targetVy - vy;
            float verticalForce = _hoverThrust + (error * (_rb.mass * 8.0f));
            verticalForce = Mathf.Clamp(verticalForce, 0f, _hoverThrust * 3.0f);
            _lastAppliedVerticalForce = verticalForce;
            _rb.AddForce(Vector3.up * verticalForce, ForceMode.Force);
        }
        else
        {
            if (!_hasHoldAltitude)
            {
                _holdAltitude = y;
                _hasHoldAltitude = true;
            }

            // Altitude Hold (Loiter): active PD controller prevents altitude loss/gain when hovering
            float altError = _holdAltitude - y;
            float targetVy = Mathf.Clamp(altError * 3.5f, -2.5f, 2.5f);
            float error = targetVy - vy;
            float verticalForce = _hoverThrust + (error * (_rb.mass * 9.0f));
            verticalForce = Mathf.Clamp(verticalForce, 0f, _hoverThrust * 3.0f);
            _lastAppliedVerticalForce = verticalForce;
            _rb.AddForce(Vector3.up * verticalForce, ForceMode.Force);
        }
    }

    private void ApplyAttitudeAndYaw()
    {
        if (Mathf.Abs(YawInput) > 0.08f)
        {
            _targetYawAngle += YawInput * 90.0f * Time.fixedDeltaTime;
        }

        // Pitch & Roll target angles (arcade self-leveling)
        float targetPitch = PitchInput * 30.0f;
        float targetRoll = -RollInput * 25.0f;

        Quaternion targetRot = Quaternion.Euler(targetPitch, _targetYawAngle, targetRoll);

        // PD torque to orient towards targetRot smoothly
        Quaternion deltaRot = targetRot * Quaternion.Inverse(transform.rotation);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;

        Vector3 torque = (axis * (angleDeg * Deg2Rad * 40.0f)) - (_rb.angularVelocity * 8.0f);
        _rb.AddTorque(torque, ForceMode.Acceleration);
    }

    private void ApplyHorizontalFlight()
    {
        Vector3 forwardXZ = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        forwardXZ = forwardXZ.sqrMagnitude > 0.001f ? forwardXZ.normalized : Vector3.forward;
        Vector3 rightXZ = Vector3.Cross(Vector3.up, forwardXZ);

        if (Mathf.Abs(PitchInput) > 0.08f)
        {
            Vector3 fwdForce = forwardXZ * (PitchInput * 11.0f * _rb.mass);
            _rb.AddForce(fwdForce, ForceMode.Force);
        }
        else
        {
            // Damping forward drift when stick is released
            float fwdSpeed = Vector3.Dot(_rb.linearVelocity, forwardXZ);
            _rb.AddForce(-forwardXZ * (fwdSpeed * 2.5f * _rb.mass), ForceMode.Force);
        }

        if (Mathf.Abs(RollInput) > 0.08f)
        {
            Vector3 rightForce = rightXZ * (RollInput * 9.0f * _rb.mass);
            _rb.AddForce(rightForce, ForceMode.Force);
        }
        else
        {
            // Damping lateral drift when stick is released
            float sideSpeed = Vector3.Dot(_rb.linearVelocity, rightXZ);
            _rb.AddForce(-rightXZ * (sideSpeed * 2.5f * _rb.mass), ForceMode.Force);
        }
    }

    private void AnimatePropellers()
    {
        if (!_isArmed || Propellers == null) return;

        float spinSpeed = _isArmed ? 3200f : 0f;
        float deltaAngle = spinSpeed * Time.deltaTime;

        for (int i = 0; i < Propellers.Length; i++)
        {
            if (Propellers[i] != null)
            {
                float dir = (i % 2 == 0) ? 1f : -1f;
                Propellers[i].Rotate(Vector3.up, deltaAngle * dir, Space.Self);
            }
        }
    }

    private void UpdateMotorAudio()
    {
        if (MotorAudio == null) return;

        if (_isArmed)
        {
            if (!MotorAudio.isPlaying)
            {
                MotorAudio.Play();
            }

            float stickActivity = Mathf.Max(Mathf.Abs(ClimbInput), Mathf.Abs(PitchInput), Mathf.Abs(RollInput), Mathf.Abs(YawInput));
            float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            float throttleLoad = Mathf.Clamp01(0.15f + 0.85f * (stickActivity * 0.70f + (speed / 18.0f)));

            // Smooth realistic RPM pitch: idle 0.90x, punchout 1.45x (pleasant, warm, no harsh screech)
            MotorAudio.pitch = Mathf.Lerp(0.90f, 1.45f, throttleLoad);
            MotorAudio.volume = Mathf.Lerp(0.35f, 0.65f, throttleLoad);
        }
        else
        {
            MotorAudio.volume = 0f;
            if (MotorAudio.isPlaying)
            {
                MotorAudio.Stop();
            }
        }
    }
}
