using System.Collections.Generic;
using UnityEngine;

public static class HSDoorsSensor
{
    static readonly List<Entity> buf = new List<Entity>();

    public static bool Occupied(World world, HSDoorsConfigData d)
    {
        string dummy;
        return Occupied(world, d, out dummy);
    }

    public static bool Occupied(World world, HSDoorsConfigData d, out string why)
    {
        why = null;
        if (world == null || d == null) return false;
        if (d.SensorFilter == 3) return false;
        buf.Clear();
        world.GetEntitiesInBounds(typeof(EntityAlive), SensorBounds(d), buf);
        for (int i = 0; i < buf.Count; i++)
        {
            var e = buf[i] as EntityAlive;
            if (e == null || e.IsDead()) continue;
            if (!FilterOk(d, e)) continue;
            if (d.LockMode == "locked") continue;
            if (d.LockMode == "players" && !(e is EntityPlayer)) continue;
            if (d.LockMode == "zombies" && !(e is EntityEnemy)) continue;
            why = e is EntityPlayer ? "player" : (e is EntityEnemy ? "zombie" : "npc");
            return true;
        }
        return false;
    }

    public static bool FilterOk(HSDoorsConfigData d, EntityAlive e)
    {
        if (e == null) return false;
        if (d.SensorFilter == 1) return e is EntityPlayer;
        if (d.SensorFilter == 2) return e is EntityEnemy;
        if (d.SensorFilter == 3) return false;
        return true;
    }

    public static Bounds SensorBounds(HSDoorsConfigData d)
    {
        int depth = Mathf.Clamp(d.SensorDepth, 1, 6);
        if (d.IsSlide)
        {
            float extra = depth;
            var min = new Vector3(d.OriginX - extra, d.OriginY, d.OriginZ - extra);
            var max = new Vector3(d.OriginX + d.SizeX + extra, d.OriginY + d.SizeY, d.OriginZ + d.SizeZ + extra);
            if (d.Axis == 0)
            {
                min.z = d.OriginZ - extra;
                max.z = d.OriginZ + d.SizeZ + extra;
                min.x = d.OriginX - 0.2f;
                max.x = d.OriginX + d.SizeX + 0.2f;
            }
            else
            {
                min.x = d.OriginX - extra;
                max.x = d.OriginX + d.SizeX + extra;
                min.z = d.OriginZ - 0.2f;
                max.z = d.OriginZ + d.SizeZ + 0.2f;
            }
            var c = (min + max) * 0.5f;
            return new Bounds(c, max - min);
        }
        var hub = d.HubWorld;
        float r = d.Radius + depth;
        return new Bounds(new Vector3(hub.x, d.HubY + d.Height * 0.5f, hub.z), new Vector3(r * 2f, d.Height + 1f, r * 2f));
    }

    public static bool InOpening(World world, HSDoorsConfigData d)
    {
        if (world == null || d == null || !d.IsSlide) return false;
        buf.Clear();
        var b = new Bounds(
            new Vector3(d.OriginX + d.SizeX * 0.5f, d.OriginY + d.SizeY * 0.5f, d.OriginZ + d.SizeZ * 0.5f),
            new Vector3(d.SizeX + 0.2f, d.SizeY + 0.2f, d.SizeZ + 0.2f));
        world.GetEntitiesInBounds(typeof(EntityAlive), b, buf);
        for (int i = 0; i < buf.Count; i++)
        {
            var e = buf[i] as EntityAlive;
            if (e != null && !e.IsDead()) return true;
        }
        return false;
    }
}
