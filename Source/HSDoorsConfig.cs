using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class HSDoorsCell
{
    public int Dx, Dy, Dz;
    public uint Raw;
    public int Damage;
    public sbyte Density;
    public long[] Tex;
    public int Leaf; // 0 = all / rotor, 1 = left or inner, 2 = right or outer
}

public class HSDoorsConfigData
{
    public string DoorId = "door1";
    public string Kind = "slide"; // slide | revolve
    public string Mode = "bipart"; // slide: bipart, left, right, tele / revolve: continuous, demand, manual

    public int[] Corner1;
    public int[] Corner2;
    public int HubX, HubY, HubZ;
    public bool HasHub;
    public float Radius = 1.5f;
    public int Height = 3;
    public int OriginX, OriginY, OriginZ;
    public int SizeX = 1, SizeY = 1, SizeZ = 1;
    public int Axis; // 0 = X, 2 = Z
    public int AxisSign = 1;
    public float Travel = 1f;
    public int Wings = 4;

    public int PanelX, PanelY, PanelZ;
    public bool HasPanel;

    public List<int[]> Excludes = new List<int[]>();
    public List<HSDoorsCell> Cells = new List<HSDoorsCell>();
    public bool Captured;

    public float Open;
    public float Angle;
    public float Speed = 1.2f;
    public int Direction = 1;
    public bool Running;
    public bool WantedOn = true;
    public string StopReason;
    public float HoldOpen = 2f;

    public bool SafetyReverse = true;
    public float CrushDps;
    public string LockMode = "unlocked"; // unlocked, locked, in, out, zombies, players
    public bool TrapMode;
    public int SensorFilter; // 0 all, 1 players, 2 zombies, 3 none
    public string Unpowered = "closed"; // open, closed, stay
    public int SensorDepth = 2;
    public bool Debug;

    [JsonIgnore]
    public Vector3i PanelPos { get { return new Vector3i(PanelX, PanelY, PanelZ); } }

    [JsonIgnore]
    public Vector3i Origin { get { return new Vector3i(OriginX, OriginY, OriginZ); } }

    [JsonIgnore]
    public Vector3i Hub { get { return new Vector3i(HubX, HubY, HubZ); } }

    [JsonIgnore]
    public bool IsSlide { get { return Kind != "revolve"; } }

    public bool IsPanel(Vector3i pos)
    {
        return HasPanel && PanelX == pos.x && PanelY == pos.y && PanelZ == pos.z;
    }

    public void SetPanel(Vector3i p)
    {
        PanelX = p.x;
        PanelY = p.y;
        PanelZ = p.z;
        HasPanel = true;
    }

    public void ClearPanel()
    {
        HasPanel = false;
    }

    public bool IsExcluded(int x, int y, int z)
    {
        if (Excludes == null) return false;
        for (int i = 0; i < Excludes.Count; i++)
        {
            var e = Excludes[i];
            if (e != null && e.Length >= 3 && e[0] == x && e[1] == y && e[2] == z) return true;
        }
        return false;
    }

    public void ToggleExclude(Vector3i p)
    {
        if (Excludes == null) Excludes = new List<int[]>();
        for (int i = Excludes.Count - 1; i >= 0; i--)
        {
            var e = Excludes[i];
            if (e != null && e.Length >= 3 && e[0] == p.x && e[1] == p.y && e[2] == p.z)
            {
                Excludes.RemoveAt(i);
                return;
            }
        }
        Excludes.Add(new[] { p.x, p.y, p.z });
    }

    public Vector3 WorldCell(HSDoorsCell c)
    {
        return new Vector3(OriginX + c.Dx, OriginY + c.Dy, OriginZ + c.Dz);
    }

    [JsonIgnore]
    public Vector3 HubWorld
    {
        get { return new Vector3(HubX + 0.5f, HubY, HubZ + 0.5f); }
    }

    public bool InFootprint(int x, int y, int z)
    {
        if (x < OriginX || x >= OriginX + SizeX) return false;
        if (y < OriginY || y >= OriginY + SizeY) return false;
        if (z < OriginZ || z >= OriginZ + SizeZ) return false;
        return true;
    }

    public bool InCylinder(int x, int y, int z)
    {
        if (y < HubY || y >= HubY + Height) return false;
        float dx = (x + 0.5f) - (HubX + 0.5f);
        float dz = (z + 0.5f) - (HubZ + 0.5f);
        return dx * dx + dz * dz <= (Radius + 0.55f) * (Radius + 0.55f);
    }
}

public class HSDoorsFile
{
    public string ActiveId;
    public bool Debug;
    public HSDoorsSettingsData Caps;
    public List<HSDoorsConfigData> Doors = new List<HSDoorsConfigData>();
}

public static class HSDoorsConfig
{
    public static HSDoorsConfigData Data = new HSDoorsConfigData();
    public static List<HSDoorsConfigData> Doors = new List<HSDoorsConfigData>();
    public static string ActiveId;

    public static string RuntimeDir
    {
        get
        {
            try
            {
                var save = GameIO.GetSaveGameDir();
                if (!string.IsNullOrEmpty(save)) return save;
            }
            catch { }
            return string.IsNullOrEmpty(HSDoorsMod.UserDataPath) ? "." : HSDoorsMod.UserDataPath;
        }
    }

    static string FilePath { get { return Path.Combine(RuntimeDir, "HSDoors.json"); } }

    public static void EvacuateRuntimeFilesFromModFolder()
    {
        var mod = HSDoorsMod.ModPath;
        var dest = HSDoorsMod.UserDataPath;
        if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(dest) || !Directory.Exists(mod)) return;
        Directory.CreateDirectory(dest);
        var from = Path.Combine(mod, "HSDoors.json");
        var to = Path.Combine(dest, "HSDoors.json");
        if (!File.Exists(from)) return;
        try
        {
            if (!File.Exists(to)) File.Copy(from, to);
            File.Delete(from);
            HSDoorsDebug.Info("Moved HSDoors.json out of Mods so server and clients keep the same folder.");
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Could not move save out of the mod folder", e);
        }
    }

    static void AdoptPendingSaveIfNeeded()
    {
        var pending = string.IsNullOrEmpty(HSDoorsMod.UserDataPath) ? null : Path.Combine(HSDoorsMod.UserDataPath, "HSDoors.json");
        if (string.IsNullOrEmpty(pending) || !File.Exists(pending)) return;
        if (File.Exists(FilePath)) return;
        var dir = RuntimeDir;
        if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(HSDoorsMod.UserDataPath), StringComparison.OrdinalIgnoreCase)) return;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.Copy(pending, FilePath);
        HSDoorsDebug.Info("Copied door list into this world save.");
    }

    public static void Use(HSDoorsConfigData d)
    {
        if (d == null) return;
        Data = d;
        ActiveId = d.DoorId;
    }

    public static string ToSyncJson()
    {
        var file = new HSDoorsFile
        {
            ActiveId = ActiveId,
            Debug = HSDoorsDebug.Enabled,
            Caps = HSDoorsSettings.Snapshot(),
            Doors = Doors
        };
        return JsonConvert.SerializeObject(file);
    }

    public static void ApplyFromServer(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            var file = JsonConvert.DeserializeObject<HSDoorsFile>(json, settings) ?? new HSDoorsFile();
            Doors = file.Doors != null ? file.Doors : new List<HSDoorsConfigData>();
            ActiveId = file.ActiveId;
            if (Doors.Count == 0) Doors.Add(new HSDoorsConfigData());
            foreach (var d in Doors) Normalize(d);
            Use(ById(ActiveId) ?? Doors[0]);
            HSDoorsDebug.Enabled = file.Debug;
            HSDoorsSettings.ApplyFromServer(file.Caps);
            foreach (var d in Doors)
            {
                if (d.Debug) HSDoorsDebug.Enabled = true;
                var ctrl = HSDoorsController.Ensure(d);
                if (ctrl != null) ctrl.RebuildVisual();
            }
            HSDoorsDebug.Info("Got " + Doors.Count + " door(s) from server. Editing " + Data.DoorId);
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Could not apply door list from server", e);
        }
    }

    public static void Load()
    {
        if (HSDoorsNet.IsRemoteClient)
        {
            Doors = new List<HSDoorsConfigData>();
            Doors.Add(new HSDoorsConfigData());
            Use(Doors[0]);
            HSDoorsDebug.Info("Client: waiting for the server door list");
            return;
        }
        Doors = new List<HSDoorsConfigData>();
        try
        {
            AdoptPendingSaveIfNeeded();
            if (File.Exists(FilePath))
            {
                var raw = File.ReadAllText(FilePath);
                var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
                var file = JsonConvert.DeserializeObject<HSDoorsFile>(raw, settings) ?? new HSDoorsFile();
                if (file.Doors != null) Doors.AddRange(file.Doors);
                ActiveId = file.ActiveId;
                HSDoorsDebug.Enabled = file.Debug;
            }
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Config load failed, using defaults", e);
            Doors.Clear();
        }
        if (Doors.Count == 0) Doors.Add(new HSDoorsConfigData());
        foreach (var d in Doors) Normalize(d);
        Use(ById(ActiveId) ?? Doors[0]);
        foreach (var d in Doors)
            if (d.Debug) HSDoorsDebug.Enabled = true;
        Save();
        HSDoorsDebug.Info("Config loaded: " + Doors.Count + " door(s), active " + Data.DoorId);
    }

    static void Normalize(HSDoorsConfigData d)
    {
        if (d == null) return;
        if (d.Cells == null) d.Cells = new List<HSDoorsCell>();
        if (d.Excludes == null) d.Excludes = new List<int[]>();
        if (string.IsNullOrEmpty(d.DoorId)) d.DoorId = "door1";
        if (d.Kind != "revolve") d.Kind = "slide";
        if (d.IsSlide)
        {
            if (d.Mode != "left" && d.Mode != "right" && d.Mode != "tele") d.Mode = "bipart";
            d.Speed = HSDoorsSettings.ClampSlide(d.Speed);
        }
        else
        {
            if (d.Mode != "demand" && d.Mode != "manual") d.Mode = "continuous";
            d.Speed = HSDoorsSettings.ClampRevolve(d.Speed);
        }
        if (d.Direction != -1 && d.Direction != 1) d.Direction = 1;
        if (d.Axis != 0 && d.Axis != 2) d.Axis = 0;
        if (d.AxisSign == 0) d.AxisSign = 1;
        if (d.Wings < 2) d.Wings = 4;
        if (d.Height < 1) d.Height = 3;
        if (d.HoldOpen < 0f) d.HoldOpen = 2f;
        if (string.IsNullOrEmpty(d.LockMode)) d.LockMode = "unlocked";
        if (string.IsNullOrEmpty(d.Unpowered)) d.Unpowered = "closed";
        d.CrushDps = HSDoorsSettings.ClampCrush(d.CrushDps);
        if (d.SizeX < 1) d.SizeX = 1;
        if (d.SizeY < 1) d.SizeY = 1;
        if (d.SizeZ < 1) d.SizeZ = 1;
        if (d.Travel < 0.25f) d.Travel = 1f;
        if (d.Cells.Count > 0 && !d.Captured) d.Captured = true;
    }

    public static void Save()
    {
        try
        {
            if (Data != null)
            {
                var i = Doors.FindIndex(e => e.DoorId == Data.DoorId);
                if (i >= 0) Doors[i] = Data;
                else if (!Doors.Contains(Data)) Doors.Add(Data);
                ActiveId = Data.DoorId;
            }
            if (HSDoorsNet.IsRemoteClient) return;
            var dir = RuntimeDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var file = new HSDoorsFile
            {
                ActiveId = ActiveId,
                Debug = HSDoorsDebug.Enabled,
                Caps = HSDoorsSettings.Snapshot(),
                Doors = Doors
            };
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(file, Formatting.Indented));
            HSDoorsNet.BroadcastConfig();
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Config save failed", e);
        }
    }

    public static HSDoorsConfigData ById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return Doors.Find(e => string.Equals(e.DoorId, id, StringComparison.OrdinalIgnoreCase));
    }

    public static HSDoorsConfigData NewDoor(string kind)
    {
        int n = 1;
        while (ById("door" + n) != null) n++;
        var d = new HSDoorsConfigData
        {
            DoorId = "door" + n,
            Kind = kind == "revolve" ? "revolve" : "slide",
            Mode = kind == "revolve" ? "continuous" : "bipart",
            Speed = kind == "revolve" ? 45f : 1.2f
        };
        Doors.Add(d);
        Use(d);
        HSDoorsController.Ensure(d);
        Save();
        return d;
    }

    public static HSDoorsConfigData PanelOwner(Vector3i pos)
    {
        foreach (var d in Doors)
            if (d.IsPanel(pos)) return d;
        return null;
    }

    public static HSDoorsConfigData At(Vector3i pos)
    {
        var panel = PanelOwner(pos);
        if (panel != null) return panel;
        foreach (var d in Doors)
        {
            if (d.IsSlide && d.InFootprint(pos.x, pos.y, pos.z)) return d;
            if (!d.IsSlide && d.HasHub && d.InCylinder(pos.x, pos.y, pos.z)) return d;
        }
        return null;
    }

    public static string SelectNearest(Vector3i pos)
    {
        var d = At(pos);
        if (d == null) return "No door at this block. New Sliding or New Revolving, then mark the parts.";
        Use(d);
        Save();
        return "Now editing " + d.DoorId + ". " + Summary(d);
    }

    public static string ListAll()
    {
        if (Doors.Count == 0) return "No doors.";
        var parts = new List<string>();
        foreach (var d in Doors)
        {
            var mark = d.DoorId == ActiveId ? "* " : "  ";
            parts.Add(mark + Summary(d));
        }
        return "Doors:\n" + string.Join("\n", parts.ToArray());
    }

    public static string Summary(HSDoorsConfigData d)
    {
        if (d == null) return "none";
        var kind = d.IsSlide ? "slide " + d.Mode : "revolve " + d.Mode;
        var size = d.IsSlide
            ? d.SizeX + "x" + d.SizeY + "x" + d.SizeZ
            : ("R" + d.Radius.ToString("0.0") + " x" + d.Height);
        return d.DoorId + ": " + kind + " " + size
            + (d.HasPanel ? "" : " (no panel)")
            + (d.Captured ? " captured" : "")
            + (d.Running ? " running" : "");
    }

    public static string Summary()
    {
        return Summary(Data);
    }

    public static int TexChannels
    {
        get { return System.Runtime.InteropServices.Marshal.SizeOf(typeof(TextureFullArray)) / 8; }
    }

    public static long[] ToLongs(TextureFullArray tex)
    {
        int n = TexChannels;
        var a = new long[n];
        for (int i = 0; i < n; i++) a[i] = tex[i];
        return a;
    }

    public static TextureFullArray FromLongs(long[] a)
    {
        var tex = TextureFullArray.Default;
        if (a == null) return tex;
        for (int i = 0; i < a.Length && i < TexChannels; i++) tex[i] = a[i];
        return tex;
    }

    public static BlockValue BlockOf(HSDoorsCell s)
    {
        return new BlockValue(s.Raw, s.Damage);
    }

    public static string DisplayName(BlockValue bv)
    {
        try
        {
            var n = bv.Block.GetLocalizedBlockName();
            if (!string.IsNullOrEmpty(n)) return n;
        }
        catch { }
        return bv.Block != null ? bv.Block.GetBlockName() : "block";
    }
}
