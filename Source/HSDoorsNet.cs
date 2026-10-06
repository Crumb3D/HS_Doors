using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

public static class HSDoorsNet
{
    public const byte Config = 1;
    public const byte Setup = 2;
    public const byte State = 3;
    public const byte Tip = 4;
    public const byte Panel = 5;

    public static bool IsAuthority
    {
        get
        {
            try
            {
                if (GameManager.IsDedicatedServer) return true;
                var cm = ConnectionManager.Instance;
                if (cm != null) return cm.IsServer;
            }
            catch { }
            return true;
        }
    }

    public static bool IsRemoteClient
    {
        get
        {
            try
            {
                var cm = ConnectionManager.Instance;
                return cm != null && cm.IsClient && !cm.IsServer;
            }
            catch { }
            return false;
        }
    }

    public static void RegisterPackage()
    {
        try
        {
            var t = typeof(NetPackageHSDoors);
            var f = typeof(NetPackageManager).GetField("knownPackageTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) return;
            var dict = f.GetValue(null) as IDictionary;
            if (dict == null) return;
            var args = f.FieldType.GetGenericArguments();
            if (args != null && args.Length >= 1 && args[0] == typeof(string))
            {
                if (!dict.Contains(t.Name)) dict[t.Name] = t;
            }
            else if (args != null && args.Length >= 1 && args[0] == typeof(Type))
            {
                if (!dict.Contains(t)) dict[t] = t.Name;
            }
            else if (!dict.Contains(t.Name)) dict[t.Name] = t;
            HSDoorsDebug.Verbose("Registered NetPackageHSDoors");
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Net package register failed", e);
        }
    }

    static NetPackageHSDoors Pkg()
    {
        return NetPackageManager.GetPackage<NetPackageHSDoors>();
    }

    static void ToServer(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null) return;
        cm.SendToClientsOrServer(pkg);
    }

    static void ToClients(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null || !cm.IsServer) return;
        cm.SendPackage(pkg);
    }

    static void ToClient(ClientInfo ci, NetPackage pkg)
    {
        if (ci != null) ci.SendPackage(pkg);
        else ToClients(pkg);
    }

    public static string SendSetup(string sub, string arg, EntityPlayerLocal player)
    {
        Vector3i aim = Vector3i.zero;
        bool hasAim = HSDoorsSetup.AimedBlock(player, out aim) == null;
        ToServer(Pkg().SetupCmd(Setup, sub ?? "", arg ?? "", "", 0f, 0f, false, hasAim, aim));
        return null;
    }

    public static void SendPanelCmd(Vector3i pos, string cmd)
    {
        ToServer(Pkg().SetupCmd(Panel, cmd ?? "", "", "", 0f, 0f, false, true, pos));
    }

    public static void BroadcastConfig()
    {
        if (!IsAuthority) return;
        try
        {
            ToClients(Pkg().SetupCmd(Config, HSDoorsConfig.ToSyncJson(), "", "", 0f, 0f, false, false, Vector3i.zero));
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Broadcast door list failed", e);
        }
    }

    public static void SendConfigTo(ClientInfo ci)
    {
        if (!IsAuthority || ci == null) return;
        try
        {
            ToClient(ci, Pkg().SetupCmd(Config, HSDoorsConfig.ToSyncJson(), "", "", 0f, 0f, false, false, Vector3i.zero));
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Send door list failed", e);
        }
    }

    public static void BroadcastState(HSDoorsConfigData d)
    {
        if (!IsAuthority || d == null) return;
        ToClients(Pkg().SetupCmd(State, d.StopReason ?? "", "", d.DoorId ?? "", d.Open, d.Angle, d.Running, false, Vector3i.zero));
    }

    public static void SendStateTo(ClientInfo ci, HSDoorsConfigData d)
    {
        if (!IsAuthority || ci == null || d == null) return;
        ToClient(ci, Pkg().SetupCmd(State, d.StopReason ?? "", "", d.DoorId ?? "", d.Open, d.Angle, d.Running, false, Vector3i.zero));
    }

    public static void ReplyTip(ClientInfo ci, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (ci != null)
        {
            ToClient(ci, Pkg().SetupCmd(Tip, msg, "", "", 0f, 0f, false, false, Vector3i.zero));
            return;
        }
        TellLocal(msg);
    }

    public static void TellLocal(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var locals = world != null ? world.GetLocalPlayers() : null;
            if (locals == null) return;
            for (int i = 0; i < locals.Count; i++)
            {
                var p = locals[i] as EntityPlayerLocal;
                if (p != null) GameManager.ShowTooltip(p, HSDoorsSetup.FitTooltip(msg));
            }
        }
        catch (Exception e)
        {
            HSDoorsDebug.Warn("Local tip failed: " + e.Message);
        }
    }

    public static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!IsAuthority || data.ClientInfo == null) return;
        SendConfigTo(data.ClientInfo);
        foreach (var d in HSDoorsConfig.Doors)
            SendStateTo(data.ClientInfo, d);
    }
}

public class NetPackageHSDoors : NetPackage
{
    byte kind;
    string text;
    string arg;
    string id;
    float open;
    float angle;
    bool running;
    bool hasPos;
    Vector3i pos;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.Both; } }

    public NetPackageHSDoors SetupCmd(byte k, string t, string a, string i, float o, float ang, bool run, bool has, Vector3i p)
    {
        kind = k;
        text = t ?? "";
        arg = a ?? "";
        id = i ?? "";
        open = o;
        angle = ang;
        running = run;
        hasPos = has;
        pos = p;
        return this;
    }

    public override void read(PooledBinaryReader br)
    {
        kind = br.ReadByte();
        text = br.ReadString();
        arg = br.ReadString();
        id = br.ReadString();
        open = br.ReadSingle();
        angle = br.ReadSingle();
        running = br.ReadBoolean();
        hasPos = br.ReadBoolean();
        pos = new Vector3i(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
    }

    public override void write(PooledBinaryWriter bw)
    {
        base.write(bw);
        bw.Write(kind);
        bw.Write(text ?? "");
        bw.Write(arg ?? "");
        bw.Write(id ?? "");
        bw.Write(open);
        bw.Write(angle);
        bw.Write(running);
        bw.Write(hasPos);
        bw.Write(pos.x);
        bw.Write(pos.y);
        bw.Write(pos.z);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        try
        {
            if (world == null) return;
            switch (kind)
            {
                case HSDoorsNet.Config:
                    if (HSDoorsNet.IsAuthority) return;
                    HSDoorsConfig.ApplyFromServer(text);
                    break;
                case HSDoorsNet.Setup:
                    if (!HSDoorsNet.IsAuthority) return;
                    HSDoorsSetup.HasForcedAim = hasPos;
                    HSDoorsSetup.ForcedAim = pos;
                    string result;
                    try { result = HSDoorsSetup.Execute(text, arg, null); }
                    finally { HSDoorsSetup.HasForcedAim = false; }
                    HSDoorsNet.ReplyTip(Sender, result);
                    break;
                case HSDoorsNet.State:
                    if (HSDoorsNet.IsAuthority) return;
                    var d = HSDoorsConfig.ById(id);
                    if (d == null) return;
                    var ctrl = HSDoorsController.Ensure(d);
                    if (ctrl != null) ctrl.ApplyRemoteState(open, angle, running, text);
                    break;
                case HSDoorsNet.Tip:
                    HSDoorsNet.TellLocal(text);
                    break;
                case HSDoorsNet.Panel:
                    if (!HSDoorsNet.IsAuthority) return;
                    HSDoorsNet.ReplyTip(Sender, HSDoorsController.DriveCommand(pos, string.IsNullOrEmpty(text) ? "start" : text));
                    break;
            }
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Net package failed (" + kind + ")", e);
        }
    }

    public override int GetLength()
    {
        return 48 + (text != null ? text.Length : 0) + (id != null ? id.Length : 0);
    }
}

[HarmonyPatch(typeof(NetPackageManager), "SetupBaseMapping")]
public static class HSDoorsNetRegister
{
    static void Postfix()
    {
        HSDoorsNet.RegisterPackage();
    }
}
