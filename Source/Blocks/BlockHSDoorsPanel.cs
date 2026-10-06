using System;

public class BlockHSDoorsPanel : BlockPowered
{
    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        try
        {
            var pos = HSDoorsCapture.ParentPos(_blockPos, _blockValue);
            var door = HSDoorsConfig.PanelOwner(pos);
            if (door == null) return new BlockActivationCommand[0];
            HSDoorsConfig.Use(door);
            string run = door.WantedOn ? "hsdoorsStop" : "hsdoorsStart";
            string rev = "hsdoorsReverse";
            string safety = door.SafetyReverse ? "hsdoorsSafetyOff" : "hsdoorsSafetyOn";
            string trap = door.TrapMode ? "hsdoorsTrapOff" : "hsdoorsTrapOn";
            string lockm = "hsdoorsLock";
            string sensor = "hsdoorsSensorFilter";
            string fail = "hsdoorsUnpowered";
            return new[]
            {
                new BlockActivationCommand(run, "lightbulb", true, false, null),
                new BlockActivationCommand(rev, "electric_switch", true, false, null),
                new BlockActivationCommand(safety, "lock", true, false, null),
                new BlockActivationCommand(trap, "map", true, false, null),
                new BlockActivationCommand(lockm, "lock", true, false, null),
                new BlockActivationCommand(sensor, "map", true, false, null),
                new BlockActivationCommand(fail, "electric_switch", true, false, null)
            };
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Panel commands failed", e);
            return new BlockActivationCommand[0];
        }
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        try
        {
            var pos = HSDoorsCapture.ParentPos(_blockPos, _blockValue);
            var door = HSDoorsConfig.PanelOwner(pos);
            if (door == null) return Localization.Get("hsdoorsUnregistered");
            if (!string.IsNullOrEmpty(door.StopReason))
                return string.Format(Localization.Get("hsdoorsStopped"), door.StopReason);
            string problem;
            if (!HSDoorsPower.IsPanelPowered(door, out problem))
                return string.Format(Localization.Get("hsdoorsNotReady"), problem);
            return Localization.Get("hsdoorsPanelHint");
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Panel text failed", e);
            return "";
        }
    }

    public override bool OnBlockActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return true;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        try
        {
            var pos = HSDoorsCapture.ParentPos(_blockPos, _blockValue);
            string cmd = CommandToNet(_commandName);
            if (string.IsNullOrEmpty(cmd)) return true;
            if (HSDoorsNet.IsRemoteClient)
            {
                HSDoorsNet.SendPanelCmd(pos, cmd);
                return true;
            }
            var msg = HSDoorsController.DriveCommand(pos, cmd);
            if (_player != null && !string.IsNullOrEmpty(msg))
                GameManager.ShowTooltip(_player, HSDoorsSetup.FitTooltip(msg));
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Panel press failed", e);
        }
        return true;
    }

    static string CommandToNet(string name)
    {
        if (name == "hsdoorsStart") return "start";
        if (name == "hsdoorsStop") return "stop";
        if (name == "hsdoorsReverse") return "reverse";
        if (name == "hsdoorsSafetyOn" || name == "hsdoorsSafetyOff") return "safety";
        if (name == "hsdoorsTrapOn" || name == "hsdoorsTrapOff") return "trap";
        if (name == "hsdoorsLock") return "lock";
        if (name == "hsdoorsSensorFilter") return "sensor";
        if (name == "hsdoorsUnpowered") return "unpowered";
        return null;
    }
}
