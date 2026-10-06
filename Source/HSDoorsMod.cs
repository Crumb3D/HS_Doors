using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

public class HSDoorsMod : IModApi
{
    public static string ModPath;
    public static string UserDataPath;

    public void InitMod(Mod _modInstance)
    {
        ModPath = _modInstance.Path;
        try
        {
            UserDataPath = Path.Combine(GameIO.GetUserGameDataDir(), "HSDoors");
            Directory.CreateDirectory(UserDataPath);
            HSDoorsConfig.EvacuateRuntimeFilesFromModFolder();
            HSDoorsSettings.Load();
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Could not set up HSDoors save folder", e);
        }
        HSDoorsDebug.Info("Init v1.0.0; setup tool hold E; admin: hsdoors");
        HSDoorsNet.RegisterPackage();
        ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
        ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShuttingDown);
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(HSDoorsNet.OnPlayerSpawned);
        try
        {
            new Harmony("HSDoors").PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Harmony patch failed (setup tool hold E will not open)", e);
        }
    }

    static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
    {
        try
        {
            HSDoorsSettings.Load();
            HSDoorsConfig.Load();
            HSDoorsController.EnsureCreated();
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Start failed", e);
        }
    }

    static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        try
        {
            HSDoorsController.OnWorldShuttingDown();
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Shutdown handling failed", e);
        }
    }
}
