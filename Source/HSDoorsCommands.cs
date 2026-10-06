using System;
using System.Collections.Generic;

public class ConsoleCmdHSDoors : ConsoleCmdAbstract
{
    public override string[] getCommands()
    {
        return new[] { "hsdoors" };
    }

    public override string getDescription()
    {
        return "HSDoors admin: leftover setup and debug. Players use the Door Setup Tool (hold E).";
    }

    public override int DefaultPermissionLevel { get { return 1000; } }

    public override string getHelp()
    {
        return
            "Admin only. Players: hold the Door Setup Tool and hold E.\n" +
            "hsdoors newslide | newrevolve - start a new door\n" +
            "hsdoors c1 | c2               - slide corners, or hub / radius\n" +
            "hsdoors panel                 - register the aimed Door Panel\n" +
            "hsdoors exclude               - keep that cell still (drum leftover)\n" +
            "hsdoors list | select         - list / aim to edit that one\n" +
            "hsdoors jog | mode | forget   - preview / cycle mode / put blocks back\n" +
            "hsdoors speed | travel | crush\n" +
            "hsdoors maxcells [n]          - host moving-cell cap (0 = none)\n" +
            "hsdoors status | debug";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        try
        {
            var sub = _params != null && _params.Count > 0 ? _params[0].ToLowerInvariant() : "status";
            var arg = _params != null && _params.Count > 1 ? _params[1] : "";
            var world = GameManager.Instance.World;
            EntityPlayerLocal player = null;
            if (world != null)
            {
                player = world.GetPrimaryPlayer();
                if (player == null)
                {
                    var locals = world.GetLocalPlayers();
                    if (locals != null && locals.Count > 0) player = locals[0] as EntityPlayerLocal;
                }
            }
            Out(HSDoorsSetup.Execute(sub, arg, player));
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Command failed", e);
            Out("HSDoors command failed: " + e.Message);
        }
    }

    static void Out(string s)
    {
        SdtdConsole.Instance.Output(s);
    }
}
