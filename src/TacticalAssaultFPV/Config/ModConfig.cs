namespace TacticalAssaultFPV.Config;

public static class ModConfig
{
    // Rates & Sensitivities (Acro/Rate Mode)
    public static float PitchRateDegPerSec { get; set; } = 670f;
    public static float RollRateDegPerSec { get; set; } = 670f;
    public static float YawRateDegPerSec { get; set; } = 540f;
    public static float StickExpo { get; set; } = 0.25f;

    // Throttle & Thrust
    public static float HoverThrottle { get; set; } = 0.42f; // Center stick (~42% throttle)
    public static float ThrustToWeightRatio { get; set; } = 4.5f; // Power punch
    public static float DroneMassKg { get; set; } = 0.65f; // 7-inch quad with payload

    // Camera
    public static float CameraTiltAngleDeg { get; set; } = 28f; // Upward tilt angle
    public static int RenderTextureResolution { get; set; } = 512;

    // Payload & Detonation
    public static float ExplosionRadius { get; set; } = 6.5f;
    public static float ExplosionDamage { get; set; } = 250f;
    public static float MinDetonationVelocity { get; set; } = 3.5f; // m/s required to detonate on contact
    public static bool ArmOnTakeoff { get; set; } = true;

    // Armory Station / Test Pedestal Toggle
    public static bool EnableArmoryTerminalStation { get; set; } = false; // Disabled by default per user request, easily toggled anytime
}
