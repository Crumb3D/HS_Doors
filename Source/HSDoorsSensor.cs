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
        if (d.LockMode == "locked") return false;

        var zone = SensorBounds(d);
        if (AnyPlayerIn(world, d, zone, out why)) return true;

        buf.Clear();
        world.GetEntitiesInBounds(typeof(Entity), zone, buf);
        for (int i = 0; i < buf.Count; i++)
        {
            var e = buf[i] as EntityAlive;
            if (e == null || e.IsDead()) continue;
            if (e is EntityPlayer) continue;
            if (!FilterOk(d, e)) continue;
            if (!LockOk(d, e)) continue;
            if (!EntityInZone(zone, e)) continue;
            why = e is EntityEnemy ? "zombie" : "npc";
            return true;
        }
        return false;
    }

    static bool AnyPlayerIn(World world, HSDoorsConfigData d, Bounds zone, out string why)
    {
        why = null;
        try
        {
            var players = world.Players;
            if (players != null && players.list != null)
            {
                for (int i = 0; i < players.list.Count; i++)
                {
                    var e = players.list[i];
                    if (!PlayerTriggers(d, zone, e)) continue;
                    why = "player";
                    return true;
                }
            }
        }
        catch { }

        try
        {
            var locals = world.GetLocalPlayers();
            if (locals != null)
            {
                for (int i = 0; i < locals.Count; i++)
                {
                    var e = locals[i] as EntityPlayer;
                    if (!PlayerTriggers(d, zone, e)) continue;
                    why = "player";
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    static bool PlayerTriggers(HSDoorsConfigData d, Bounds zone, EntityPlayer e)
    {
        if (e == null || e.IsDead()) return false;
        if (!FilterOk(d, e)) return false;
        if (!LockOk(d, e)) return false;
        return EntityInZone(zone, e);
    }

    static bool LockOk(HSDoorsConfigData d, EntityAlive e)
    {
        if (d.LockMode == "players" && !(e is EntityPlayer)) return false;
        if (d.LockMode == "zombies" && !(e is EntityEnemy)) return false;
        return true;
    }

    static bool EntityInZone(Bounds zone, Entity e)
    {
        if (e == null) return false;
        var feet = e.position;
        if (InAabb(zone, feet) || InAabb(zone, feet + Vector3.up * 0.9f)) return true;
        try { return zone.Intersects(e.boundingBox); }
        catch { return false; }
    }

    static bool InAabb(Bounds b, Vector3 p)
    {
        var min = b.min;
        var max = b.max;
        return p.x >= min.x && p.x <= max.x
            && p.y >= min.y && p.y <= max.y
            && p.z >= min.z && p.z <= max.z;
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
        var b = new Bounds();
        if (d.IsSlide)
        {
            float x0 = d.OriginX - 0.5f;
            float x1 = d.OriginX + d.SizeX + 0.5f;
            float y0 = d.OriginY - 0.5f;
            float y1 = d.OriginY + d.SizeY + 1f;
            float z0 = d.OriginZ - 0.5f;
            float z1 = d.OriginZ + d.SizeZ + 0.5f;
            if (d.Axis == 0)
            {
                z0 = d.OriginZ - depth;
                z1 = d.OriginZ + d.SizeZ + depth;
            }
            else
            {
                x0 = d.OriginX - depth;
                x1 = d.OriginX + d.SizeX + depth;
            }
            b.SetMinMax(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));
            return b;
        }
        var hub = d.HubWorld;
        float r = d.Radius + depth;
        b.SetMinMax(
            new Vector3(hub.x - r, d.HubY - 0.5f, hub.z - r),
            new Vector3(hub.x + r, d.HubY + d.Height + 1f, hub.z + r));
        return b;
    }

    public static bool InOpening(World world, HSDoorsConfigData d)
    {
        if (world == null || d == null || !d.IsSlide) return false;
        var b = new Bounds();
        b.SetMinMax(
            new Vector3(d.OriginX - 0.25f, d.OriginY - 0.5f, d.OriginZ - 0.25f),
            new Vector3(d.OriginX + d.SizeX + 0.25f, d.OriginY + d.SizeY + 1f, d.OriginZ + d.SizeZ + 0.25f));
        try
        {
            var players = world.Players;
            if (players != null && players.list != null)
            {
                for (int i = 0; i < players.list.Count; i++)
                {
                    var e = players.list[i];
                    if (e != null && !e.IsDead() && EntityInZone(b, e)) return true;
                }
            }
        }
        catch { }
        try
        {
            var locals = world.GetLocalPlayers();
            if (locals != null)
            {
                for (int i = 0; i < locals.Count; i++)
                {
                    var e = locals[i] as EntityAlive;
                    if (e != null && !e.IsDead() && EntityInZone(b, e)) return true;
                }
            }
        }
        catch { }
        buf.Clear();
        world.GetEntitiesInBounds(typeof(Entity), b, buf);
        for (int i = 0; i < buf.Count; i++)
        {
            var e = buf[i] as EntityAlive;
            if (e == null || e.IsDead()) continue;
            if (e is EntityPlayer) continue;
            if (EntityInZone(b, e)) return true;
        }
        return false;
    }
}
