using System;
using System.IO;
using Newtonsoft.Json;

public class HSDoorsSettingsData
{
    public int MaxMovingCells;
    public float MaxSlideSpeed;
    public float MaxRevolveSpeed;
    public float MaxCrushDps;
}

public static class HSDoorsSettings
{
    public static int MaxMovingCells;
    public static float MaxSlideSpeed = 4f;
    public static float MaxRevolveSpeed = 180f;
    public static float MaxCrushDps;

    static string FileName { get { return "HSDoorsSettings.json"; } }

    static string UserFile
    {
        get
        {
            var dir = HSDoorsMod.UserDataPath;
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, FileName);
        }
    }

    static string ModFile
    {
        get
        {
            var dir = HSDoorsMod.ModPath;
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, FileName);
        }
    }

    static string SaveFile
    {
        get
        {
            try
            {
                var save = GameIO.GetSaveGameDir();
                if (!string.IsNullOrEmpty(save)) return Path.Combine(save, FileName);
            }
            catch { }
            return null;
        }
    }

    public static void Load()
    {
        if (HSDoorsNet.IsRemoteClient)
        {
            HSDoorsDebug.Info("Client: host caps come from the server, local file ignored");
            return;
        }
        MaxMovingCells = 0;
        MaxSlideSpeed = 4f;
        MaxRevolveSpeed = 180f;
        MaxCrushDps = 0f;
        TryRead(ModFile);
        TryRead(UserFile);
        TryRead(SaveFile);
        if (MaxMovingCells < 0) MaxMovingCells = 0;
        if (MaxSlideSpeed < 0.2f) MaxSlideSpeed = 0.2f;
        if (MaxRevolveSpeed < 5f) MaxRevolveSpeed = 5f;
        if (MaxCrushDps < 0f) MaxCrushDps = 0f;
        HSDoorsDebug.Info("Caps " + Describe());
    }

    public static void ApplyFromServer(HSDoorsSettingsData data)
    {
        if (data == null) return;
        MaxMovingCells = Math.Max(0, data.MaxMovingCells);
        MaxSlideSpeed = data.MaxSlideSpeed > 0.2f ? data.MaxSlideSpeed : 4f;
        MaxRevolveSpeed = data.MaxRevolveSpeed > 5f ? data.MaxRevolveSpeed : 180f;
        MaxCrushDps = Math.Max(0f, data.MaxCrushDps);
        HSDoorsDebug.Info("Caps from server: " + Describe());
    }

    public static HSDoorsSettingsData Snapshot()
    {
        return new HSDoorsSettingsData
        {
            MaxMovingCells = MaxMovingCells,
            MaxSlideSpeed = MaxSlideSpeed,
            MaxRevolveSpeed = MaxRevolveSpeed,
            MaxCrushDps = MaxCrushDps
        };
    }

    public static float ClampSlide(float s)
    {
        if (s < 0.2f) s = 0.2f;
        if (s > MaxSlideSpeed && MaxSlideSpeed > 0f) s = MaxSlideSpeed;
        return s;
    }

    public static float ClampRevolve(float s)
    {
        if (s < 5f) s = 5f;
        if (s > MaxRevolveSpeed && MaxRevolveSpeed > 0f) s = MaxRevolveSpeed;
        return s;
    }

    public static float ClampCrush(float s)
    {
        if (s < 0f) s = 0f;
        if (MaxCrushDps > 0f && s > MaxCrushDps) s = MaxCrushDps;
        if (MaxCrushDps <= 0f && s > 0f) s = 0f;
        return s;
    }

    public static string RejectIfOverCap(int cells)
    {
        if (MaxMovingCells <= 0) return null;
        if (cells <= MaxMovingCells) return null;
        return "This door would move " + cells + " blocks. This host caps moving parts at " + MaxMovingCells + " cells.";
    }

    public static string Describe()
    {
        return "cells " + (MaxMovingCells <= 0 ? "off" : MaxMovingCells.ToString())
            + ", slide " + MaxSlideSpeed.ToString("0.#") + " m/s, revolve " + MaxRevolveSpeed.ToString("0") + " deg/s, crush "
            + (MaxCrushDps <= 0f ? "off" : MaxCrushDps.ToString("0.#") + " dps");
    }

    public static string SetMaxCells(int n)
    {
        if (HSDoorsNet.IsRemoteClient) return "Host-only. Run this on the server or in single player.";
        if (n < 0) n = 0;
        MaxMovingCells = n;
        return Persist("Moving-cell cap is now " + (n <= 0 ? "off" : n.ToString()));
    }

    static string Persist(string ok)
    {
        try
        {
            var path = UserFile;
            if (string.IsNullOrEmpty(path)) path = ModFile;
            if (string.IsNullOrEmpty(path)) return "Could not write settings.";
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonConvert.SerializeObject(Snapshot(), Formatting.Indented));
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Could not save door settings", e);
            return ok + " but the file did not save: " + e.Message;
        }
        HSDoorsNet.BroadcastConfig();
        return ok + ". Host value overrides every client.";
    }

    static void TryRead(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            var data = JsonConvert.DeserializeObject<HSDoorsSettingsData>(File.ReadAllText(path));
            if (data == null) return;
            MaxMovingCells = data.MaxMovingCells;
            if (data.MaxSlideSpeed > 0f) MaxSlideSpeed = data.MaxSlideSpeed;
            if (data.MaxRevolveSpeed > 0f) MaxRevolveSpeed = data.MaxRevolveSpeed;
            MaxCrushDps = data.MaxCrushDps;
        }
        catch (Exception e)
        {
            HSDoorsDebug.Warn("Could not read " + path + ": " + e.Message);
        }
    }
}
