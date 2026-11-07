using System;
using UnityEngine;
using Il2CppHurricaneVR.Framework.ControllerInput;
using Il2CppHurricaneVR.Framework.Shared;

namespace TacticalAssaultVR.SDK;

public static class TavrInputHelper
{
    public static HVRController GetController(HVRHandSide hand)
    {
        try
        {
            var mgr = HVRInputManager.Instance;
            if (mgr == null) return null;

            if (hand == HVRHandSide.Left)
            {
                return mgr.LeftController ?? mgr.LeftXRInputController;
            }
            return mgr.RightController ?? mgr.RightXRInputController;
        }
        catch { return null; }
    }

    /// <summary>
    /// Checks if Primary Button (X on Left, A on Right) was just pressed this frame.
    /// </summary>
    public static bool GetPrimaryButtonDown(HVRHandSide hand)
    {
        var ctrl = GetController(hand);
        return ctrl != null && ctrl.PrimaryButtonState.JustActivated;
    }

    /// <summary>
    /// Checks if Primary Button (X on Left, A on Right) is currently held.
    /// </summary>
    public static bool GetPrimaryButton(HVRHandSide hand)
    {
        var ctrl = GetController(hand);
        return ctrl != null && (ctrl.PrimaryButton || ctrl.PrimaryButtonState.Active);
    }

    /// <summary>
    /// Checks if Secondary Button (Y on Left, B on Right) was just pressed this frame.
    /// </summary>
    public static bool GetSecondaryButtonDown(HVRHandSide hand)
    {
        var ctrl = GetController(hand);
        return ctrl != null && ctrl.SecondaryButtonState.JustActivated;
    }

    /// <summary>
    /// Checks if Secondary Button (Y on Left, B on Right) is currently held.
    /// </summary>
    public static bool GetSecondaryButton(HVRHandSide hand)
    {
        var ctrl = GetController(hand);
        return ctrl != null && (ctrl.SecondaryButton || ctrl.SecondaryButtonState.Active);
    }

    /// <summary>
    /// Returns 2D thumbstick / touchpad vector for the specified hand.
    /// </summary>
    public static Vector2 GetJoystick(HVRHandSide hand)
    {
        var ctrl = GetController(hand);
        if (ctrl == null) return Vector2.zero;
        if (ctrl.JoystickAxis.sqrMagnitude > 0.01f) return ctrl.JoystickAxis;
        return ctrl.TrackpadAxis;
    }

    /// <summary>
    /// Returns analog trigger pull value (0.0 to 1.0).
    /// </summary>
    public static float GetTrigger(HVRHandSide hand)
    {
        var ctrl = GetController(hand);
        return ctrl != null ? ctrl.Trigger : 0f;
    }

    /// <summary>
    /// Returns analog grip squeeze value (0.0 to 1.0).
    /// </summary>
    public static float GetGrip(HVRHandSide hand)
    {
        var ctrl = GetController(hand);
        return ctrl != null ? ctrl.Grip : 0f;
    }

    /// <summary>
    /// Triggers haptic pulse on controller.
    /// </summary>
    public static void Vibrate(HVRHandSide hand, float amplitude = 0.5f, float duration = 0.2f, float frequency = 50f)
    {
        var ctrl = GetController(hand);
        ctrl?.Vibrate(amplitude, duration, frequency);
    }
}
