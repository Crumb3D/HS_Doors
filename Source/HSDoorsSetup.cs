using System;

public static class HSDoorsSetup
{
    public static bool HasForcedAim;
    public static Vector3i ForcedAim;

    public static void Tell(EntityPlayerLocal player, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (player != null) GameManager.ShowTooltip(player, FitTooltip(msg));
        else SdtdConsole.Instance.Output(msg);
    }

    public static string FitTooltip(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return msg;
        const int max = 38;
        var raw = msg.Replace("\r\n", "\n").Split('\n');
        var lines = new System.Collections.Generic.List<string>();
        for (int i = 0; i < raw.Length; i++)
        {
            var words = raw[i].Split(' ');
            var cur = "";
            for (int w = 0; w < words.Length; w++)
            {
                if (words[w].Length == 0) continue;
                if (cur.Length == 0) cur = words[w];
                else if (cur.Length + 1 + words[w].Length <= max) cur += " " + words[w];
                else
                {
                    lines.Add(cur);
                    cur = words[w];
                }
            }
            if (cur.Length > 0) lines.Add(cur);
        }
        if (lines.Count > 3) lines.RemoveRange(3, lines.Count - 3);
        return string.Join("\n", lines.ToArray());
    }

    public static string Execute(string sub, string arg, EntityPlayerLocal player)
    {
        if (HSDoorsNet.IsRemoteClient && sub != "status" && sub != "list")
            return HSDoorsNet.SendSetup(sub, arg, player);
        return Run(sub, arg, player);
    }

    static string Run(string sub, string arg, EntityPlayerLocal player)
    {
        var d = HSDoorsConfig.Data;
        if (d == null) return "No door selected.";
        switch (sub)
        {
            case "status":
            {
                var c = HSDoorsController.Of(d);
                return c != null ? c.StatusText() : HSDoorsConfig.Summary();
            }
            case "debug":
                d.Debug = !d.Debug;
                HSDoorsDebug.Enabled = d.Debug;
                HSDoorsConfig.Save();
                return "HSDoors debug " + (d.Debug ? "ON" : "OFF");
            case "list":
                return HSDoorsConfig.ListAll();
            case "newslide":
            case "slide":
            {
                var created = HSDoorsConfig.NewDoor("slide");
                return "Started sliding " + created.DoorId + ". Set Corner 1 and Corner 2 on the leaves.";
            }
            case "newrevolve":
            case "revolve":
            {
                var created = HSDoorsConfig.NewDoor("revolve");
                return "Started revolving " + created.DoorId + ". Set Hub, then Radius / Top.";
            }
            case "select":
            {
                Vector3i p;
                var err = AimedBlock(player, out p);
                if (err != null) return err;
                return HSDoorsConfig.SelectNearest(p);
            }
            case "c1":
            case "hub":
                return SetFirst(player);
            case "c2":
            case "radius":
                return SetSecond(player);
            case "panel":
                return RegisterPanel(arg, player);
            case "mode":
            {
                var ctrl = HSDoorsController.Ensure(d);
                return ctrl != null ? ctrl.ApplyPanel("mode") : "No controller.";
            }
            case "exclude":
                return Exclude(player);
            case "jog":
            {
                var ctrl = HSDoorsController.Ensure(d);
                return ctrl != null ? ctrl.Jog() : "No controller.";
            }
            case "forget":
            case "delete":
                return Forget(d);
            case "speed":
            {
                float s;
                if (!float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s))
                    return "Usage: hsdoors speed <n>";
                d.Speed = d.IsSlide ? HSDoorsSettings.ClampSlide(s) : HSDoorsSettings.ClampRevolve(s);
                HSDoorsConfig.Save();
                return "Speed = " + d.Speed;
            }
            case "travel":
            {
                float t;
                if (!float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out t) || t < 0.25f || t > 16f)
                    return "Usage: hsdoors travel <0.25-16>";
                d.Travel = t;
                HSDoorsConfig.Save();
                return "Travel = " + t + " blocks";
            }
            case "crush":
            {
                float t;
                if (!float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out t))
                    return "Usage: hsdoors crush <dps>";
                d.CrushDps = HSDoorsSettings.ClampCrush(t);
                HSDoorsConfig.Save();
                return "Crush = " + d.CrushDps + " dps";
            }
            case "maxcells":
            {
                int n;
                if (!int.TryParse(arg, out n)) return "Usage: hsdoors maxcells <n>   (0 = no cap)";
                return HSDoorsSettings.SetMaxCells(n);
            }
        }
        return "Unknown command.";
    }

    static string SetFirst(EntityPlayerLocal player)
    {
        var d = HSDoorsConfig.Data;
        Vector3i p;
        var err = AimedBlock(player, out p);
        if (err != null) return err;
        BindAimed(p);
        d = HSDoorsConfig.Data;
        var world = GameManager.Instance.World;
        if (d.Captured)
        {
            var ctrl = HSDoorsController.Of(d);
            if (ctrl != null) ctrl.RebuildVisual();
            HSDoorsCapture.Restore(world, d);
        }
        if (d.IsSlide)
        {
            d.Corner1 = new[] { p.x, p.y, p.z };
            HSDoorsConfig.Save();
            return d.Corner2 == null ? "Corner 1 set. Mark the other leaf corner." : FinishSlide(world, d);
        }
        d.HubX = p.x;
        d.HubY = p.y;
        d.HubZ = p.z;
        d.HasHub = true;
        HSDoorsConfig.Save();
        return "Hub set at " + p + ". Aim at the outer edge / top next.";
    }

    static string SetSecond(EntityPlayerLocal player)
    {
        var d = HSDoorsConfig.Data;
        Vector3i p;
        var err = AimedBlock(player, out p);
        if (err != null) return err;
        BindAimed(p);
        d = HSDoorsConfig.Data;
        var world = GameManager.Instance.World;
        if (d.Captured)
        {
            var ctrl = HSDoorsController.Of(d);
            if (ctrl != null) ctrl.RebuildVisual();
            HSDoorsCapture.Restore(world, d);
        }
        if (d.IsSlide)
        {
            d.Corner2 = new[] { p.x, p.y, p.z };
            HSDoorsConfig.Save();
            return d.Corner1 == null ? "Corner 2 set. Mark the other leaf corner." : FinishSlide(world, d);
        }
        if (!d.HasHub) return "Set Hub first.";
        float dx = (p.x + 0.5f) - (d.HubX + 0.5f);
        float dz = (p.z + 0.5f) - (d.HubZ + 0.5f);
        d.Radius = Math.Max(0.8f, (float)Math.Sqrt(dx * dx + dz * dz) + 0.5f);
        d.Height = Math.Max(2, Math.Abs(p.y - d.HubY) + 1);
        HSDoorsConfig.Save();
        return FinishRevolve(world, d);
    }

    static string FinishSlide(World world, HSDoorsConfigData d)
    {
        var err = HSDoorsCapture.CaptureSlide(world, d);
        if (err != null) return err;
        var ctrl = HSDoorsController.Ensure(d);
        if (ctrl != null) ctrl.RebuildVisual();
        HSDoorsConfig.Save();
        return "Sliding " + d.SizeX + "x" + d.SizeY + "x" + d.SizeZ + " " + d.Mode
            + (d.HasPanel ? " ready." : ". Register the Panel.");
    }

    static string FinishRevolve(World world, HSDoorsConfigData d)
    {
        var err = HSDoorsCapture.CaptureRevolve(world, d);
        if (err != null) return err;
        var ctrl = HSDoorsController.Ensure(d);
        if (ctrl != null) ctrl.RebuildVisual();
        HSDoorsConfig.Save();
        return "Revolving R" + d.Radius.ToString("0.0") + " x" + d.Height
            + (d.HasPanel ? " ready." : ". Register the Panel.");
    }

    static string RegisterPanel(string arg, EntityPlayerLocal player)
    {
        var d = HSDoorsConfig.Data;
        if (arg == "clear")
        {
            d.ClearPanel();
            HSDoorsConfig.Save();
            return "Panel forgotten.";
        }
        Vector3i p;
        var err = AimedBlock(player, out p);
        if (err != null) return err;
        BindAimed(p);
        d = HSDoorsConfig.Data;
        var world = GameManager.Instance.World;
        var bv = world.GetBlock(p);
        p = HSDoorsCapture.ParentPos(p, bv);
        if (!(world.GetBlock(p).Block is BlockHSDoorsPanel))
            return "Aim at an HS Door Panel. That block is " + HSDoorsConfig.DisplayName(bv) + ".";
        d.SetPanel(p);
        d.StopReason = null;
        HSDoorsConfig.Save();
        string power;
        bool on = HSDoorsPower.IsPanelPowered(d, out power);
        return "Panel registered at " + p + ". " + (on ? "Powered." : power);
    }

    static string Exclude(EntityPlayerLocal player)
    {
        var d = HSDoorsConfig.Data;
        Vector3i p;
        var err = AimedBlock(player, out p);
        if (err != null) return err;
        d.ToggleExclude(p);
        HSDoorsConfig.Save();
        return d.IsExcluded(p.x, p.y, p.z)
            ? "Excluded " + p + ". Recapture with Corner/Hub to apply."
            : "No longer excluded " + p + ".";
    }

    static string Forget(HSDoorsConfigData d)
    {
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        var ctrl = HSDoorsController.Of(d);
        if (ctrl != null) ctrl.RebuildVisual();
        if (world != null) HSDoorsCapture.Restore(world, d);
        d.Corner1 = d.Corner2 = null;
        d.HasHub = false;
        d.Captured = false;
        d.ClearPanel();
        d.Cells.Clear();
        d.Running = false;
        d.StopReason = null;
        HSDoorsConfig.Save();
        return "Forgot " + d.DoorId + ". Original blocks were put back.";
    }

    static void BindAimed(Vector3i p)
    {
        var d = HSDoorsConfig.At(p);
        if (d != null) HSDoorsConfig.Use(d);
    }

    public static string AimedBlock(EntityPlayerLocal player, out Vector3i pos)
    {
        pos = Vector3i.zero;
        if (HasForcedAim)
        {
            pos = ForcedAim;
            return null;
        }
        var world = GameManager.Instance.World;
        if (player == null) player = world != null ? world.GetPrimaryPlayer() : null;
        if (player == null) return "No local player.";
        if (!ItemActionHSDoorsTool.TryAimedBuild(world, player != null ? player.HitInfo : null, player, out pos))
            return "Aim at a block first.";
        return null;
    }
}
