using System;
using System.Collections.Generic;
using UnityEngine;

public class HSDoorsController : MonoBehaviour
{
    public HSDoorsConfigData Bound;
    readonly HSDoorsVisual visual = new HSDoorsVisual();

    static readonly List<HSDoorsController> all = new List<HSDoorsController>();

    const float PowerCheckInterval = 0.25f;
    const float SensorInterval = 0.12f;
    const float StateInterval = 0.35f;

    float nextPower;
    float nextSensor;
    float nextState;
    float holdUntil;
    float wantOpen; // 0 or 1 for slide; unused for revolve
    bool visualReady;
    bool closing;

    static HSDoorsConfigData D { get { return HSDoorsConfig.Data; } }

    public static HSDoorsController Of(HSDoorsConfigData d)
    {
        if (d == null) return null;
        return all.Find(c => c != null && c.Bound != null && c.Bound.DoorId == d.DoorId);
    }

    public static void EnsureCreated()
    {
        foreach (var d in HSDoorsConfig.Doors) Ensure(d);
    }

    public static HSDoorsController Ensure(HSDoorsConfigData d)
    {
        if (d == null) return null;
        var c = Of(d);
        if (c != null) { c.Bound = d; c.visual.Bind(d); return c; }
        var go = new GameObject("HSDoors_" + d.DoorId + "_ctrl");
        DontDestroyOnLoad(go);
        c = go.AddComponent<HSDoorsController>();
        c.Bound = d;
        c.visual.Bind(d);
        all.Add(c);
        return c;
    }

    public static void OnWorldShuttingDown()
    {
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        foreach (var c in all)
        {
            if (c == null || c.Bound == null) continue;
            if (HSDoorsNet.IsAuthority && world != null)
                HSDoorsCapture.Restore(world, c.Bound, true);
            c.visual.Destroy();
        }
        if (HSDoorsNet.IsAuthority) HSDoorsConfig.Save();
    }

    void PushCfg()
    {
        if (Bound != null) HSDoorsConfig.Use(Bound);
    }

    public string StatusText()
    {
        var d = Bound ?? D;
        if (d == null) return "No door.";
        var extra = string.IsNullOrEmpty(d.StopReason) ? "" : " stopped: " + d.StopReason;
        if (d.IsSlide) return HSDoorsConfig.Summary(d) + extra + " open " + d.Open.ToString("0.00");
        return HSDoorsConfig.Summary(d) + extra + " angle " + d.Angle.ToString("0.0");
    }

    public string Jog()
    {
        if (Bound == null || !Bound.Captured) return "Capture the moving parts first.";
        Bound.StopReason = null;
        Bound.WantedOn = true;
        Bound.Running = true;
        if (Bound.IsSlide) wantOpen = Bound.Open < 0.5f ? 1f : 0f;
        else Bound.Mode = "continuous";
        return "Jogging " + Bound.DoorId + ".";
    }

    public static string DriveCommand(Vector3i pos, string cmd)
    {
        var d = HSDoorsConfig.PanelOwner(pos);
        if (d == null) return Localization.Get("hsdoorsUnregistered");
        HSDoorsConfig.Use(d);
        var c = Ensure(d);
        return c != null ? c.ApplyPanel(cmd) : "No controller.";
    }

    public string ApplyPanel(string cmd)
    {
        if (Bound == null) return "No door.";
        if (cmd == "start")
        {
            Bound.WantedOn = true;
            Bound.StopReason = null;
            HSDoorsConfig.Save();
            return Bound.DoorId + " enabled.";
        }
        if (cmd == "stop")
        {
            Bound.WantedOn = false;
            HSDoorsConfig.Save();
            return Bound.DoorId + " disabled.";
        }
        if (cmd == "reverse")
        {
            Bound.Direction = Bound.Direction < 0 ? 1 : -1;
            HSDoorsConfig.Save();
            HSDoorsNet.BroadcastState(Bound);
            return Bound.DoorId + " direction " + (Bound.Direction > 0 ? "forward" : "reverse") + ".";
        }
        if (cmd == "safety")
        {
            Bound.SafetyReverse = !Bound.SafetyReverse;
            HSDoorsConfig.Save();
            return "Safety reverse " + (Bound.SafetyReverse ? "ON" : "OFF") + ".";
        }
        if (cmd == "trap")
        {
            Bound.TrapMode = !Bound.TrapMode;
            HSDoorsConfig.Save();
            return "Trap mode " + (Bound.TrapMode ? "ON" : "OFF") + ".";
        }
        if (cmd == "lock")
        {
            Bound.LockMode = NextLock(Bound.LockMode);
            HSDoorsConfig.Save();
            return "Lock: " + Bound.LockMode + ".";
        }
        if (cmd == "sensor")
        {
            Bound.SensorFilter = (Bound.SensorFilter + 1) % 4;
            HSDoorsConfig.Save();
            return "Sensor: " + SensorName(Bound.SensorFilter) + ".";
        }
        if (cmd == "unpowered")
        {
            Bound.Unpowered = Bound.Unpowered == "closed" ? "open" : (Bound.Unpowered == "open" ? "stay" : "closed");
            HSDoorsConfig.Save();
            return "Unpowered: fail " + Bound.Unpowered + ".";
        }
        if (cmd == "mode")
        {
            Bound.Mode = NextMode(Bound);
            HSDoorsConfig.Save();
            var ctrl = Ensure(Bound);
            if (ctrl != null) ctrl.RebuildVisual();
            return Bound.DoorId + " mode " + Bound.Mode + ".";
        }
        return "Unknown panel command.";
    }

    static string NextLock(string cur)
    {
        if (cur == "unlocked") return "locked";
        if (cur == "locked") return "players";
        if (cur == "players") return "zombies";
        if (cur == "zombies") return "in";
        if (cur == "in") return "out";
        return "unlocked";
    }

    static string SensorName(int f)
    {
        if (f == 1) return "players";
        if (f == 2) return "zombies";
        if (f == 3) return "nobody";
        return "everyone";
    }

    static string NextMode(HSDoorsConfigData d)
    {
        if (d.IsSlide)
        {
            if (d.Mode == "bipart") return "left";
            if (d.Mode == "left") return "right";
            if (d.Mode == "right") return "tele";
            return "bipart";
        }
        if (d.Mode == "continuous") return "demand";
        if (d.Mode == "demand") return "manual";
        return "continuous";
    }

    public void ApplyRemoteState(float open, float angle, bool running, string reason)
    {
        if (Bound == null) return;
        Bound.Open = open;
        Bound.Angle = angle;
        Bound.Running = running;
        Bound.StopReason = reason;
    }

    public void RebuildVisual()
    {
        visualReady = false;
        visual.Bind(Bound);
        visual.Destroy();
    }

    void LateUpdate()
    {
        try { Tick(); }
        catch (Exception e) { HSDoorsDebug.Error("Controller tick failed", e); }
    }

    void Tick()
    {
        if (Bound == null) return;
        PushCfg();
        visual.Bind(Bound);
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;

        if (Bound.Captured)
        {
            if (HSDoorsNet.IsAuthority)
                HSDoorsCapture.EnsureCapturedRemoved(world, Bound);
            if (!visual.IsBuilt)
            {
                visual.Rebuild(world);
                visualReady = true;
            }
        }
        else if (visual.IsBuilt)
        {
            visual.Destroy();
            visualReady = false;
        }
        if (!Bound.Captured) return;

        float prevOpen = Bound.Open;
        float prevAngle = Bound.Angle;

        if (HSDoorsNet.IsAuthority)
        {
            if (Time.unscaledTime >= nextPower)
            {
                nextPower = Time.unscaledTime + PowerCheckInterval;
                TickPower();
            }
            if (Time.unscaledTime >= nextSensor)
            {
                nextSensor = Time.unscaledTime + SensorInterval;
                TickSensor(world);
            }
            StepMotion(Time.deltaTime, world);
            if (Time.unscaledTime >= nextState)
            {
                nextState = Time.unscaledTime + StateInterval;
                HSDoorsNet.BroadcastState(Bound);
            }
        }
        else
            StepMotion(Time.deltaTime, world);

        visual.Apply();
        HSDoorsPush.Tick(world, Bound, Bound.Open - prevOpen, Bound.Angle - prevAngle, Time.deltaTime);
    }

    void TickPower()
    {
        string problem;
        bool powered = HSDoorsPower.IsPanelPowered(Bound, out problem);
        if (!powered)
        {
            Bound.Running = false;
            Bound.StopReason = problem;
            if (Bound.Unpowered == "open") wantOpen = 1f;
            else if (Bound.Unpowered == "closed") wantOpen = 0f;
            return;
        }
        if (!Bound.WantedOn)
        {
            Bound.Running = false;
            wantOpen = 0f;
            return;
        }
        Bound.StopReason = null;
        Bound.Running = true;
    }

    void TickSensor(World world)
    {
        if (!Bound.Running) return;
        bool occ = HSDoorsSensor.Occupied(world, Bound);
        if (Bound.IsSlide)
        {
            if (occ)
            {
                wantOpen = 1f;
                holdUntil = Time.unscaledTime + Bound.HoldOpen;
                closing = false;
            }
            else if (Time.unscaledTime >= holdUntil)
            {
                if (Bound.SafetyReverse && HSDoorsSensor.InOpening(world, Bound))
                {
                    wantOpen = 1f;
                    holdUntil = Time.unscaledTime + Bound.HoldOpen;
                }
                else
                {
                    wantOpen = 0f;
                    closing = true;
                }
            }
        }
        else
        {
            if (Bound.TrapMode && occ && HSDoorsRevolving.InsideDrum(Bound, PrimaryFeet(world)))
                Bound.Running = false;
        }
    }

    static Vector3 PrimaryFeet(World world)
    {
        var p = world.GetPrimaryPlayer();
        return p != null ? p.position : Vector3.zero;
    }

    void StepMotion(float dt, World world)
    {
        if (dt <= 0f) return;
        if (Bound.IsSlide)
        {
            if (!Bound.Running && Bound.Unpowered == "stay") return;
            float target = wantOpen;
            float speed = Mathf.Max(0.2f, Bound.Speed);
            float travel = Mathf.Max(0.25f, Bound.Travel);
            float step = (speed / travel) * dt;
            if (Bound.Open < target) Bound.Open = Mathf.Min(target, Bound.Open + step);
            else if (Bound.Open > target) Bound.Open = Mathf.Max(target, Bound.Open - step);
            Bound.Open = Mathf.Clamp01(Bound.Open);
        }
        else
        {
            bool run = Bound.Running && Bound.WantedOn;
            if (Bound.Mode == "demand")
                run = run && HSDoorsSensor.Occupied(world, Bound);
            if (Bound.Mode == "manual")
                run = run && Bound.WantedOn && Bound.Running;
            if (!run)
            {
                if (Bound.Mode == "demand" && !HSDoorsRevolving.NearSnap(Bound, Bound.Angle, 1.5f))
                {
                    float snap = HSDoorsRevolving.SnapAngle(Bound, Bound.Angle);
                    float a = Mathf.MoveTowardsAngle(Bound.Angle, snap, Bound.Speed * dt);
                    Bound.Angle = a;
                }
                return;
            }
            Bound.Angle += Bound.Direction * Bound.Speed * dt;
            if (Bound.Angle >= 360f) Bound.Angle -= 360f;
            if (Bound.Angle < 0f) Bound.Angle += 360f;
        }
    }
}
