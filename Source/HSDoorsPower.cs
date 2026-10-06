using System;

public static class HSDoorsPower
{
    public static bool IsPanelPowered(HSDoorsConfigData d, out string problem)
    {
        if (d == null || !d.HasPanel)
        {
            problem = "needs an HS Door Panel registered";
            return false;
        }
        try
        {
            var world = GameManager.Instance.World;
            if (world == null)
            {
                problem = "no world";
                return false;
            }
            var pos = d.PanelPos;
            if (!(world.GetBlock(pos).Block is BlockHSDoorsPanel))
            {
                problem = "panel is missing (re-register it)";
                return false;
            }
            var te = world.GetTileEntity(pos) as TileEntityPowered;
            if (te != null && te.IsPowered)
            {
                problem = null;
                return true;
            }
            if (PowerManager.HasInstance)
            {
                var item = PowerManager.Instance.GetPowerItemByWorldPos(pos);
                if (item != null && item.IsPowered)
                {
                    problem = null;
                    return true;
                }
            }
            problem = "no power: wire a generator or battery bank to the Door Panel";
            return false;
        }
        catch (Exception e)
        {
            HSDoorsDebug.Error("Power check failed", e);
            problem = "power check failed";
            return false;
        }
    }
}
