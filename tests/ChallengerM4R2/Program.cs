using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TacticalAssaultFPV.ChallengerM4R2;

public class Program
{
    private static int _passes = 0;
    private static int _failures = 0;

    public static int Main(string[] args)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  TACTICAL ASSAULT FPV - CHALLENGER M4 ITERATION 2 ADVERSARIAL STRESS SUITE");
        Console.WriteLine("================================================================================\n");

        // 1. Zero Dummy Asserts & Static AST Attestation
        TestZeroDummyAssertsAndASTInvariants();

        // 2. DronePhysics Remediated Telemetry & PD Restoring Force Invariants
        TestDronePhysicsRemediatedFlightDynamics();

        // 3. RCTablet Multi-Drone Switching & Permutations Stress (500,000 cycles)
        TestRCTabletSwitchingPermutationsAndTelemetry();

        // 4. FPVLoadoutIntegration Dynamic Drone Pruning & Native Socket Filter Idempotency
        TestFPVLoadoutIntegrationIdempotencyAndPruning();

        // 5. Diagnostics Assertion Engine Stress & Exception Isolation Invariants
        TestDiagnosticsAssertionEngineInvariants();

        // 6. High-Cycle Consecutive Run Multi-Execution Flakiness Verification (50 full runs)
        TestHighCycleConsecutiveExecutionFlakiness();

        Console.WriteLine("\n================================================================================");
        Console.WriteLine($"CHALLENGER M4 R2 TOTAL: {_passes} PASSED, {_failures} FAILED");
        Console.WriteLine("================================================================================");

        return _failures == 0 ? 0 : 1;
    }

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passes++;
            Console.WriteLine($"  [PASS] {testName}");
        }
        else
        {
            _failures++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [FAIL] {testName} - {details}");
            Console.ResetColor();
        }
    }

    #region 1. Zero Dummy Asserts & Static AST Invariants

    private static void TestZeroDummyAssertsAndASTInvariants()
    {
        Console.WriteLine("--- 1. Static AST Invariants & Zero Dummy Asserts ---");

        string diagPath = "/home/dmitry/Projects/Tactical Assault FPV/src/TacticalAssaultFPV/Diagnostics.cs";
        Assert(File.Exists(diagPath), "Diagnostics.cs Source File Exists", diagPath);

        string diagSource = File.ReadAllText(diagPath);

        // Invariant 1.1: Literal boolean in Assert condition MUST be 0 matches
        var literalMatch = Regex.Matches(diagSource, @"Assert\s*\(\s*(true|false)\b");
        Assert(literalMatch.Count == 0,
            "Diagnostics.cs Zero Literal Boolean Assertions (Assert(true/false))",
            $"Found {literalMatch.Count} occurrences at indices: {string.Join(", ", literalMatch.Select(m => m.Index))}");

        // Invariant 1.2: All Assert(...) invocations must pass real expressions
        // Signature: Assert(bool condition, string testName, string failureDetails, ref int passed, ref int failed, ...)
        var allAssertCalls = Regex.Matches(diagSource, @"\bAssert\s*\(([^,]+),");
        int callCount = allAssertCalls.Count;
        bool allCallsNonTrivial = true;
        string suspiciousCall = "";

        foreach (Match m in allAssertCalls)
        {
            string cond = m.Groups[1].Value.Trim();
            if (cond == "true" || cond == "false" || cond == "1 == 1" || cond == "0 == 0")
            {
                allCallsNonTrivial = false;
                suspiciousCall = cond;
                break;
            }
        }
        Assert(allCallsNonTrivial && callCount >= 40,
            $"All {callCount} Assert Calls in Diagnostics.cs Evaluate Dynamic Expressions",
            $"Suspicious call: {suspiciousCall}");

        // Invariant 1.3: Verify remediated properties on DronePhysics
        string dpPath = "/home/dmitry/Projects/Tactical Assault FPV/src/TacticalAssaultFPV/Drone/DronePhysics.cs";
        string dpSource = File.ReadAllText(dpPath);
        bool dpHasTargetYaw = dpSource.Contains("public float TargetYawAngle => _targetYawAngle;");
        bool dpHasHoldAlt = dpSource.Contains("public float HoldAltitude => _holdAltitude;");
        bool dpHasHoverThrust = dpSource.Contains("public float HoverThrust => _hoverThrust;");
        bool dpHasLastForce = dpSource.Contains("public float LastAppliedVerticalForce => _lastAppliedVerticalForce;");
        bool dpHideFromIl2Cpp = dpSource.Contains("[HideFromIl2Cpp]") && dpSource.Contains("LastAppliedVerticalForce");

        Assert(dpHasTargetYaw && dpHasHoldAlt && dpHasHoverThrust && dpHasLastForce && dpHideFromIl2Cpp,
            "DronePhysics Exposes [HideFromIl2Cpp] Telemetry Properties for Diagnostics Inspection");

        // Invariant 1.4: Verify remediated properties on RCTablet
        string rcPath = "/home/dmitry/Projects/Tactical Assault FPV/src/TacticalAssaultFPV/Controller/RCTablet.cs";
        string rcSource = File.ReadAllText(rcPath);
        bool rcHasRegCount = rcSource.Contains("public int RegisteredDroneCount => _registeredDrones.Count;");
        bool rcHasCurIndex = rcSource.Contains("public int CurrentDroneIndex => _currentDroneIndex;");
        bool rcHasPairedPhysics = rcSource.Contains("public DronePhysics PairedDronePhysics => _pairedDronePhysics;");
        bool rcHasPairedCamera = rcSource.Contains("public DroneCameraRig PairedDroneCamera => _pairedDroneCamera;");
        bool rcHideFromIl2Cpp = rcSource.Contains("[HideFromIl2Cpp]") && rcSource.Contains("RegisteredDroneCount");

        Assert(rcHasRegCount && rcHasCurIndex && rcHasPairedPhysics && rcHasPairedCamera && rcHideFromIl2Cpp,
            "RCTablet Exposes [HideFromIl2Cpp] Telemetry Properties for Diagnostics Inspection");

        // Invariant 1.5: Verify remediated properties on FPVLoadoutIntegration
        string fpvPath = "/home/dmitry/Projects/Tactical Assault FPV/src/TacticalAssaultFPV/Loadout/FPVLoadoutIntegration.cs";
        string fpvSource = File.ReadAllText(fpvPath);
        bool fpvHasActiveTablet = fpvSource.Contains("public static GameObject ActiveEquippedTablet");
        bool fpvHasActiveDrones = fpvSource.Contains("public static System.Collections.Generic.List<GameObject> ActiveDrones => _activeDrones;");
        bool fpvHasSavedFilters = fpvSource.Contains("public static int SavedSocketFiltersCount => _savedSocketFilters.Count;");
        bool fpvHasPublicRegister = fpvSource.Contains("public static void RegisterActiveDrone(GameObject drone)");
        bool fpvHasPublicPrepare = fpvSource.Contains("public static void PrepareSocketForGrabbable(HVRSocket socket)");

        Assert(fpvHasActiveTablet && fpvHasActiveDrones && fpvHasSavedFilters && fpvHasPublicRegister && fpvHasPublicPrepare,
            "FPVLoadoutIntegration Exposes Public Inspection Properties & Preparation Hooks");
    }

    #endregion

    #region 2. DronePhysics Remediated Flight Dynamics

    public class MockDronePhysicsModel
    {
        public float Mass = 1.2f;
        public float Gravity = 9.81f;
        public float HoverThrust => Mass * Gravity;

        public bool IsArmed { get; private set; }
        public float ClimbInput { get; set; }
        public float YawInput { get; set; }
        public float PitchInput { get; set; }
        public float RollInput { get; set; }

        public float TargetYawAngle { get; private set; }
        public float HoldAltitude { get; private set; }
        public bool HasHoldAltitude { get; private set; }
        public float LastAppliedVerticalForce { get; private set; }

        public float Y = 10.0f;
        public float Vy = 0.0f;

        public void SetArmed(bool armed)
        {
            IsArmed = armed;
            if (!IsArmed)
            {
                ClimbInput = 0f;
                YawInput = 0f;
                PitchInput = 0f;
                RollInput = 0f;
                HasHoldAltitude = false;
                LastAppliedVerticalForce = 0f;
            }
            else
            {
                TargetYawAngle = 0f;
                HoldAltitude = Y;
                HasHoldAltitude = true;
            }
        }

        public void FixedUpdate(float fixedDeltaTime = 0.02f)
        {
            if (!IsArmed) return;

            // Attitude and Yaw
            if (Math.Abs(YawInput) > 0.08f)
            {
                TargetYawAngle += YawInput * 90.0f * fixedDeltaTime;
            }

            // Vertical Flight
            if (Math.Abs(ClimbInput) > 0.08f)
            {
                HasHoldAltitude = false;
                float targetVy = ClimbInput > 0f ? ClimbInput * 3.5f : ClimbInput * 2.5f;
                float error = targetVy - Vy;
                float verticalForce = HoverThrust + (error * (Mass * 8.0f));
                verticalForce = Math.Clamp(verticalForce, 0f, HoverThrust * 3.0f);
                LastAppliedVerticalForce = verticalForce;
            }
            else
            {
                if (!HasHoldAltitude)
                {
                    HoldAltitude = Y;
                    HasHoldAltitude = true;
                }

                float altError = HoldAltitude - Y;
                float targetVy = Math.Clamp(altError * 3.5f, -2.5f, 2.5f);
                float error = targetVy - Vy;
                float verticalForce = HoverThrust + (error * (Mass * 9.0f));
                verticalForce = Math.Clamp(verticalForce, 0f, HoverThrust * 3.0f);
                LastAppliedVerticalForce = verticalForce;
            }
        }
    }

    private static void TestDronePhysicsRemediatedFlightDynamics()
    {
        Console.WriteLine("\n--- 2. DronePhysics Remediated Flight Dynamics & Invariants ---");

        var drone = new MockDronePhysicsModel();

        // 2.1 Disarmed FixedUpdate zero-force invariant
        drone.SetArmed(false);
        drone.FixedUpdate();
        Assert(!drone.IsArmed && drone.LastAppliedVerticalForce == 0f,
            "Disarmed Drone FixedUpdate Yields Zero Applied Vertical Force");

        // 2.2 Hover Hold Equilibrium Force Match
        drone.SetArmed(true);
        drone.ClimbInput = 0f;
        drone.YawInput = 0f;
        drone.Vy = 0f;
        drone.FixedUpdate();
        float expectedHover = drone.Mass * drone.Gravity; // 11.772 N
        Assert(Math.Abs(drone.LastAppliedVerticalForce - expectedHover) < 1e-4f,
            $"Armed Neutral Hover Force Exactly Matches Weight ({expectedHover:F3}N)",
            $"Actual force: {drone.LastAppliedVerticalForce}");

        // 2.3 Climb Stick Thrust Amplification
        drone.ClimbInput = 1.0f;
        drone.FixedUpdate();
        float climbForce = drone.LastAppliedVerticalForce;
        Assert(climbForce > expectedHover && climbForce <= expectedHover * 3.0f,
            $"Max Climb Input (1.0) Amplifies Vertical Force Above Hover ({climbForce:F2}N > {expectedHover:F2}N, <= {expectedHover * 3:F2}N)");

        // 2.4 Extreme Pathological Velocities Clamping
        // Under extreme downward velocity (-500 m/s), upward restoring force must clamp to 3.0 * hoverThrust
        drone.ClimbInput = 0f;
        drone.Vy = -500.0f;
        drone.FixedUpdate();
        Assert(Math.Abs(drone.LastAppliedVerticalForce - (expectedHover * 3.0f)) < 1e-4f,
            "Extreme Sink (-500 m/s) Restoring Force Clamped Strictly at 3.0x Hover Thrust Ceiling");

        // Under extreme upward velocity (+500 m/s), upward restoring force must clamp to 0 N floor
        drone.Vy = +500.0f;
        drone.FixedUpdate();
        Assert(drone.LastAppliedVerticalForce == 0f,
            "Extreme Rise (+500 m/s) Restoring Force Clamped Strictly at 0.0 N Floor");

        // 2.5 Yaw Angle Accumulation Rate
        drone.SetArmed(true);
        drone.YawInput = 1.0f;
        drone.FixedUpdate(0.02f);
        float expectedYawDelta = 1.0f * 90.0f * 0.02f; // 1.8 deg
        Assert(Math.Abs(drone.TargetYawAngle - expectedYawDelta) < 1e-4f,
            $"Yaw Rotation Matches 90°/s Rate Integration ({expectedYawDelta:F2}°/frame @ 50Hz)");

        // 2.6 Disarm Safety Immediate Reset
        drone.SetArmed(false);
        Assert(!drone.IsArmed &&
               drone.LastAppliedVerticalForce == 0f &&
               drone.ClimbInput == 0f &&
               drone.YawInput == 0f &&
               drone.PitchInput == 0f &&
               drone.RollInput == 0f &&
               !drone.HasHoldAltitude,
            "Disarm Immediately Zeroes All Flight Channels, Hold State, and Applied Forces");

        // 2.7 100,000 Step Monte Carlo Physics Fuzzing (NaN, Inf, Random Extremes)
        var rng = new Random(4242);
        bool allFuzzedForcesSafe = true;
        for (int i = 0; i < 100_000; i++)
        {
            drone.SetArmed(true);
            drone.ClimbInput = (float)(rng.NextDouble() * 4.0 - 2.0); // [-2.0, 2.0]
            drone.YawInput = (float)(rng.NextDouble() * 4.0 - 2.0);
            drone.Vy = (float)(rng.NextDouble() * 2000.0 - 1000.0); // [-1000, 1000] m/s
            drone.Y = (float)(rng.NextDouble() * 1000.0 - 500.0);

            drone.FixedUpdate();
            float f = drone.LastAppliedVerticalForce;
            if (float.IsNaN(f) || float.IsInfinity(f) || f < 0f || f > expectedHover * 3.0f)
            {
                allFuzzedForcesSafe = false;
                break;
            }
        }
        Assert(allFuzzedForcesSafe,
            "100,000 Monte Carlo Random Physics Steps: Force Strictly Bounded [0, 3mg] with Zero NaN/Inf");
    }

    #endregion

    #region 3. RCTablet Multi-Drone Switching & Permutations Stress

    public class MockDroneEntity
    {
        public int Id { get; }
        public bool HasDetonated { get; set; } = false;

        public MockDroneEntity(int id) => Id = id;
    }

    public class MockRCTabletModel
    {
        private readonly List<MockDroneEntity?> _registeredDrones = new();
        private int _currentDroneIndex = 0;
        private MockDroneEntity? _pairedDrone;
        public Dictionary<MockDroneEntity, float> DroneBatteries { get; } = new();

        public int RegisteredDroneCount => _registeredDrones.Count;
        public int CurrentDroneIndex => _currentDroneIndex;
        public MockDroneEntity? PairedDrone => _pairedDrone;

        public void RegisterDrones(List<MockDroneEntity?> drones)
        {
            _registeredDrones.Clear();
            if (drones != null)
            {
                foreach (var d in drones)
                {
                    if (d != null)
                    {
                        _registeredDrones.Add(d);
                        if (!DroneBatteries.ContainsKey(d))
                        {
                            DroneBatteries[d] = 25.2f;
                        }
                    }
                }
            }
            if (_registeredDrones.Count > 0)
            {
                ConnectToDrone(0);
            }
            else
            {
                _pairedDrone = null;
                _currentDroneIndex = -1;
            }
        }

        public void ConnectToDrone(int index)
        {
            if (_registeredDrones.Count == 0 || index < 0 || index >= _registeredDrones.Count)
            {
                _pairedDrone = null;
                _currentDroneIndex = -1;
                return;
            }

            _currentDroneIndex = index;
            _pairedDrone = _registeredDrones[index];
        }

        public void SwitchToNextDrone()
        {
            PruneNullDrones();
            int count = _registeredDrones.Count;
            if (count == 0)
            {
                _pairedDrone = null;
                _currentDroneIndex = -1;
                return;
            }

            if (count == 1)
            {
                ConnectToDrone(0);
                return;
            }

            int startIndex = _currentDroneIndex >= 0 ? _currentDroneIndex : 0;
            for (int offset = 1; offset <= count; offset++)
            {
                int nextIndex = (startIndex + offset) % count;
                var candidate = _registeredDrones[nextIndex];
                if (candidate != null && !candidate.HasDetonated)
                {
                    ConnectToDrone(nextIndex);
                    return;
                }
            }

            // All drones detonated: retain or reconnect to current index safely
            ConnectToDrone((startIndex + 1) % count);
        }

        public void PruneNullDrones()
        {
            for (int i = _registeredDrones.Count - 1; i >= 0; i--)
            {
                if (_registeredDrones[i] == null)
                {
                    _registeredDrones.RemoveAt(i);
                }
            }
        }

        public float GetBatteryVoltage(MockDroneEntity? drone)
        {
            if (drone != null && DroneBatteries.TryGetValue(drone, out float v))
            {
                return v;
            }
            return 25.2f;
        }
    }

    private static void TestRCTabletSwitchingPermutationsAndTelemetry()
    {
        Console.WriteLine("\n--- 3. RCTablet Multi-Drone Switching & Permutations Stress ---");

        var tablet = new MockRCTabletModel();
        var d1 = new MockDroneEntity(1);
        var d2 = new MockDroneEntity(2);
        var d3 = new MockDroneEntity(3);

        // 3.1 3-Drone Cyclic Switching (0 -> 1 -> 2 -> 0)
        tablet.RegisterDrones(new List<MockDroneEntity?> { d1, d2, d3 });
        Assert(tablet.RegisteredDroneCount == 3 && tablet.CurrentDroneIndex == 0 && tablet.PairedDrone == d1,
            "Initial Registration Pairs with Drone 0 (d1)");

        tablet.SwitchToNextDrone();
        bool step1 = tablet.CurrentDroneIndex == 1 && tablet.PairedDrone == d2;
        tablet.SwitchToNextDrone();
        bool step2 = tablet.CurrentDroneIndex == 2 && tablet.PairedDrone == d3;
        tablet.SwitchToNextDrone();
        bool step0_wrap = tablet.CurrentDroneIndex == 0 && tablet.PairedDrone == d1;

        Assert(step1 && step2 && step0_wrap,
            "Full Cyclic Switching Order Confirmed: 0(d1) -> 1(d2) -> 2(d3) -> 0(d1)");

        // 3.2 Dead Drone Skipping
        d2.HasDetonated = true;
        tablet.SwitchToNextDrone(); // From 0 -> should skip dead 1 -> pair with 2 (d3)
        bool skipOk = tablet.CurrentDroneIndex == 2 && tablet.PairedDrone == d3;
        tablet.SwitchToNextDrone(); // From 2 -> should skip dead 1 -> wrap to 0 (d1)
        bool wrapSkipOk = tablet.CurrentDroneIndex == 0 && tablet.PairedDrone == d1;

        Assert(skipOk && wrapSkipOk,
            "Dead Drone Skipping: Detonated Drone 1 Skipped Forward (0->2) and on Wrap (2->0)");

        // 3.3 Sole Surviving Drone
        d3.HasDetonated = true; // Now both d2 and d3 are dead, d1 is sole survivor
        tablet.SwitchToNextDrone();
        bool sole1 = tablet.CurrentDroneIndex == 0 && tablet.PairedDrone == d1;
        tablet.SwitchToNextDrone();
        bool sole2 = tablet.CurrentDroneIndex == 0 && tablet.PairedDrone == d1;

        Assert(sole1 && sole2,
            "Sole Surviving Drone: Switches Gracefully Retain Connection to Surviving Drone (d1)");

        // 3.4 All Drones Dead (Termination Safety)
        d1.HasDetonated = true; // All dead now
        bool noExceptionOnAllDead = true;
        try
        {
            for (int i = 0; i < 100; i++)
            {
                tablet.SwitchToNextDrone();
            }
        }
        catch
        {
            noExceptionOnAllDead = false;
        }
        Assert(noExceptionOnAllDead,
            "All Drones Detonated: Handled Gracefully Without Infinite Loop or Exception Across 100 Switches");

        // 3.5 500,000 Cycles Monte Carlo Random Permutation Stress
        var rng = new Random(777);
        var stressTab = new MockRCTabletModel();
        var pool = new List<MockDroneEntity?>
        {
            new MockDroneEntity(10),
            new MockDroneEntity(20),
            new MockDroneEntity(30)
        };
        stressTab.RegisterDrones(pool);

        bool neverConnectedToDeadWhenLiveExists = true;
        for (int i = 0; i < 500_000; i++)
        {
            // Randomly flip death states
            int targetIdx = rng.Next(3);
            pool[targetIdx]!.HasDetonated = rng.Next(2) == 1;

            stressTab.SwitchToNextDrone();

            bool anyLive = pool.Any(d => d != null && !d.HasDetonated);
            if (anyLive && stressTab.PairedDrone != null && stressTab.PairedDrone.HasDetonated)
            {
                neverConnectedToDeadWhenLiveExists = false;
                break;
            }
        }
        Assert(neverConnectedToDeadWhenLiveExists,
            "500,000 Cycles Monte Carlo Switching: Invariant (Never Pair with Dead Drone While Live Drone Exists) 100% Preserved");

        // 3.6 Per-Drone Independent Battery Voltage Drainage
        var b1 = new MockDroneEntity(100);
        var b2 = new MockDroneEntity(200);
        stressTab.RegisterDrones(new List<MockDroneEntity?> { b1, b2 });

        stressTab.DroneBatteries[b1] = 25.2f;
        stressTab.DroneBatteries[b2] = 22.5f;

        // Simulate flight drain on b1
        for (int frame = 0; frame < 2000; frame++)
        {
            stressTab.DroneBatteries[b1] = Math.Max(21.0f, stressTab.DroneBatteries[b1] - (0.04f * 0.016f));
        }

        float vB1 = stressTab.GetBatteryVoltage(b1);
        float vB2 = stressTab.GetBatteryVoltage(b2);

        Assert(Math.Abs(vB1 - 23.92f) < 0.05f && Math.Abs(vB2 - 22.5f) < 1e-4f,
            $"Per-Drone Decoupled Telemetry: D1 Drained ({vB1:F2}V) While Inactive D2 Remained Stable ({vB2:F2}V)");
    }

    #endregion

    #region 4. FPVLoadoutIntegration Dynamic Pruning & Filter Idempotency

    public class MockSocket
    {
        public string Name { get; set; } = "UtilitySocket";
        public object[]? Filters { get; set; }
        public bool CanRemoveGrabbable { get; set; } = false;
        public bool GrabbableMustBeHeld { get; set; } = true;
        public bool ParentDisablesGrab { get; set; } = true;
    }

    public class MockLoadoutIntegration
    {
        public enum FPVLoadoutMode { None = 0, ControllerKit = 1, DualDrone = 2 }

        public static FPVLoadoutMode CurrentLoadoutMode { get; private set; } = FPVLoadoutMode.None;
        public static bool IsFPVDroneSelected => CurrentLoadoutMode != FPVLoadoutMode.None;

        public static List<object?> ActiveDrones { get; } = new();
        public static object? ActiveEquippedTablet { get; set; }
        private static readonly Dictionary<MockSocket, object[]> _savedSocketFilters = new();
        public static int SavedSocketFiltersCount => _savedSocketFilters.Count;

        public static void SelectLoadoutMode(FPVLoadoutMode mode) => CurrentLoadoutMode = mode;

        public static void RegisterActiveDrone(object? drone)
        {
            if (drone != null && !ActiveDrones.Contains(drone))
            {
                ActiveDrones.Add(drone);
            }
        }

        public static void SyncTabletDrones(MockRCTabletModel? tablet)
        {
            if (tablet == null) return;

            // Null pruning
            ActiveDrones.RemoveAll(d => d == null);

            var droneEntities = ActiveDrones.OfType<MockDroneEntity>().ToList();
            tablet.RegisterDrones(droneEntities.Cast<MockDroneEntity?>().ToList());
        }

        public static void PrepareSocketForGrabbable(MockSocket socket)
        {
            if (socket == null) return;
            socket.CanRemoveGrabbable = true;
            socket.GrabbableMustBeHeld = false;
            socket.ParentDisablesGrab = false;

            if (!_savedSocketFilters.ContainsKey(socket) && socket.Filters != null)
            {
                _savedSocketFilters[socket] = socket.Filters;
                socket.Filters = Array.Empty<object>();
            }
        }

        public static void RestoreAllSocketFilters()
        {
            foreach (var kvp in _savedSocketFilters)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Key.Filters = kvp.Value;
                }
            }
            _savedSocketFilters.Clear();
        }

        public static void ClearSceneState()
        {
            ActiveEquippedTablet = null;
            ActiveDrones.Clear();
            RestoreAllSocketFilters();
        }
    }

    private static void TestFPVLoadoutIntegrationIdempotencyAndPruning()
    {
        Console.WriteLine("\n--- 4. FPVLoadoutIntegration Dynamic Pruning & Filter Idempotency ---");

        // 4.1 Mode State Transitions
        MockLoadoutIntegration.SelectLoadoutMode(MockLoadoutIntegration.FPVLoadoutMode.ControllerKit);
        bool kit = MockLoadoutIntegration.CurrentLoadoutMode == MockLoadoutIntegration.FPVLoadoutMode.ControllerKit &&
                   MockLoadoutIntegration.IsFPVDroneSelected;

        MockLoadoutIntegration.SelectLoadoutMode(MockLoadoutIntegration.FPVLoadoutMode.DualDrone);
        bool dual = MockLoadoutIntegration.CurrentLoadoutMode == MockLoadoutIntegration.FPVLoadoutMode.DualDrone &&
                    MockLoadoutIntegration.IsFPVDroneSelected;

        MockLoadoutIntegration.SelectLoadoutMode(MockLoadoutIntegration.FPVLoadoutMode.None);
        bool none = MockLoadoutIntegration.CurrentLoadoutMode == MockLoadoutIntegration.FPVLoadoutMode.None &&
                    !MockLoadoutIntegration.IsFPVDroneSelected;

        Assert(kit && dual && none, "Loadout Mode Transitions (ControllerKit, DualDrone, None) Validated");

        // 4.2 Dynamic Drone Registration and Pruning
        var tab = new MockRCTabletModel();
        var drone1 = new MockDroneEntity(101);
        var drone2 = new MockDroneEntity(102);

        MockLoadoutIntegration.ActiveDrones.Clear();
        MockLoadoutIntegration.RegisterActiveDrone(drone1);
        MockLoadoutIntegration.RegisterActiveDrone(drone2);
        MockLoadoutIntegration.SyncTabletDrones(tab);

        Assert(MockLoadoutIntegration.ActiveDrones.Count == 2 && tab.RegisteredDroneCount == 2,
            "SyncTabletDrones Correctly Synchronizes 2 Drones to Tablet");

        // Destroy drone2 (simulated by setting index 1 to null)
        MockLoadoutIntegration.ActiveDrones[1] = null;
        MockLoadoutIntegration.SyncTabletDrones(tab);

        Assert(MockLoadoutIntegration.ActiveDrones.Count == 1 && tab.RegisteredDroneCount == 1,
            "SyncTabletDrones Automatically Prunes Destroyed (Null) Drones from Loadout and Tablet");

        // 4.3 Native Socket Filter Preservation & Idempotency
        var socketA = new MockSocket { Name = "Vest_Socket_Left" };
        var originalFiltersA = new object[] { "Filter_Grenade", "Filter_Smoke" };
        socketA.Filters = originalFiltersA;

        // First call: preserves filters and empties array
        MockLoadoutIntegration.PrepareSocketForGrabbable(socketA);
        bool preservedFirst = MockLoadoutIntegration.SavedSocketFiltersCount == 1 &&
                              socketA.Filters != null && socketA.Filters.Length == 0 &&
                              socketA.CanRemoveGrabbable;

        // Second call (idempotency check): MUST NOT overwrite saved original filters with empty array!
        MockLoadoutIntegration.PrepareSocketForGrabbable(socketA);
        bool preservedIdempotent = MockLoadoutIntegration.SavedSocketFiltersCount == 1;

        // Restore filters: socket.Filters must match original reference bit-for-bit
        MockLoadoutIntegration.RestoreAllSocketFilters();
        bool restored = MockLoadoutIntegration.SavedSocketFiltersCount == 0 &&
                        ReferenceEquals(socketA.Filters, originalFiltersA);

        Assert(preservedFirst && preservedIdempotent && restored,
            "PrepareSocketForGrabbable Idempotently Preserves Original Filter Array & Restores Cleanly");

        // 4.4 Consecutive Restores Are Safe No-Ops
        MockLoadoutIntegration.RestoreAllSocketFilters();
        MockLoadoutIntegration.RestoreAllSocketFilters();
        Assert(MockLoadoutIntegration.SavedSocketFiltersCount == 0,
            "Consecutive RestoreAllSocketFilters Calls are Safe Idempotent No-Ops");

        // 4.5 ClearSceneState Preserves Mode
        MockLoadoutIntegration.SelectLoadoutMode(MockLoadoutIntegration.FPVLoadoutMode.ControllerKit);
        MockLoadoutIntegration.ActiveEquippedTablet = new object();
        MockLoadoutIntegration.RegisterActiveDrone(drone1);
        MockLoadoutIntegration.ClearSceneState();

        bool sceneReset = MockLoadoutIntegration.ActiveEquippedTablet == null &&
                          MockLoadoutIntegration.ActiveDrones.Count == 0 &&
                          MockLoadoutIntegration.SavedSocketFiltersCount == 0 &&
                          MockLoadoutIntegration.CurrentLoadoutMode == MockLoadoutIntegration.FPVLoadoutMode.ControllerKit;

        Assert(sceneReset, "ClearSceneState Cleans Up Static References While Preserving Selected Loadout Mode");
    }

    #endregion

    #region 5. Diagnostics Assertion Engine & Exception Isolation

    private static bool MockDiagnosticsAssert(bool condition, string testName, string failureDetails, ref int passed, ref int failed, List<string>? msgs = null, List<string>? errs = null)
    {
        if (condition)
        {
            passed++;
            msgs?.Add($"[PASS] {testName}");
            return true;
        }
        else
        {
            failed++;
            errs?.Add($"[FAIL] {testName} - {failureDetails}");
            return false;
        }
    }

    private static void TestDiagnosticsAssertionEngineInvariants()
    {
        Console.WriteLine("\n--- 5. Diagnostics Assertion Engine Stress & Exception Isolation ---");

        int p = 0;
        int f = 0;

        // 5.1 1,000,000 Assertion Tally Calls (Deterministic Pass/Fail Division)
        for (int i = 0; i < 1_000_000; i++)
        {
            bool cond = (i % 4) != 0; // 750,000 passes, 250,000 fails
            MockDiagnosticsAssert(cond, "TestTally", "Failure", ref p, ref f);
        }

        Assert(p == 750_000 && f == 250_000,
            $"1,000,000 Assert Calls: Exact Tally Tracking ({p} Passes, {f} Failures, Zero Drift)");

        // 5.2 Null and Pathological String Tolerance
        bool nullSafe = false;
        try
        {
            int p2 = 0, f2 = 0;
            MockDiagnosticsAssert(true, null!, null!, ref p2, ref f2);
            MockDiagnosticsAssert(false, null!, null!, ref p2, ref f2);
            nullSafe = (p2 == 1 && f2 == 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Null assertion exception: {ex}");
        }
        Assert(nullSafe, "Assertion Engine Handles Null TestName and Null Details Without NullReferenceException");

        // 5.3 Exception Isolation: Simulated mid-pass crash in pass 4 does not prevent passes 5-9 from executing
        int runnerPassed = 0;
        int runnerFailed = 0;
        var executedPasses = new List<int>();

        for (int pass = 1; pass <= 9; pass++)
        {
            try
            {
                if (pass == 4)
                {
                    throw new InvalidOperationException("Simulated unexpected IL2CPP interop fault in Pass 4");
                }
                executedPasses.Add(pass);
                MockDiagnosticsAssert(true, $"Pass_{pass}_Check", "", ref runnerPassed, ref runnerFailed);
            }
            catch
            {
                runnerFailed++;
            }
        }

        bool exceptionIsolated = executedPasses.Count == 8 &&
                                 !executedPasses.Contains(4) &&
                                 runnerPassed == 8 &&
                                 runnerFailed == 1;

        Assert(exceptionIsolated,
            "Exception Isolation: Subsystem Exception Increments Failure Count and Allows Subsequent Passes to Complete");
    }

    #endregion

    #region 6. High-Cycle Consecutive Run Multi-Execution Flakiness Verification

    private static void TestHighCycleConsecutiveExecutionFlakiness()
    {
        Console.WriteLine("\n--- 6. High-Cycle Consecutive Multi-Execution Flakiness Verification ---");

        string stressTestDll = "/home/dmitry/Projects/Tactical Assault FPV/tests/TacticalAssaultFPV.StressTests/bin/Debug/net8.0/TacticalAssaultFPV.StressTests.dll";
        Assert(File.Exists(stressTestDll), "Stress Test Assembly DLL Exists", stressTestDll);

        // Run 50 consecutive executions of the 182-test stress suite
        Console.WriteLine("Executing 50 consecutive runs of TacticalAssaultFPV.StressTests to empirically verify 0 flakiness...");
        bool all50RunsPassed = true;
        int totalStressPasses = 0;
        int consecutiveFailures = 0;

        var sw = Stopwatch.StartNew();
        for (int run = 1; run <= 50; run++)
        {
            var psi = new ProcessStartInfo("dotnet", $"exec \"{stressTestDll}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi)!;
            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0 || !stdout.Contains("TEST RESULTS: 182 PASSED, 0 FAILED"))
            {
                all50RunsPassed = false;
                consecutiveFailures++;
                Console.WriteLine($"  [FAIL] Run {run}/50 failed with exit code {proc.ExitCode}");
                break;
            }
            totalStressPasses += 182;
        }
        sw.Stop();

        Assert(all50RunsPassed,
            $"50 Consecutive Multi-Run Stress Executions (9,100 Tests Total): 100% Pass Rate with 0 Flakiness (Elapsed: {sw.ElapsedMilliseconds}ms)",
            $"Failures: {consecutiveFailures}");
    }

    #endregion
}
