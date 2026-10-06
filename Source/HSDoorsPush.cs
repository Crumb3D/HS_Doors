using System.Collections.Generic;
using UnityEngine;

public static class HSDoorsPush
{
    static readonly List<Entity> buf = new List<Entity>();

    public static void Tick(World world, HSDoorsConfigData d, float dOpen, float dAngle, float dt)
    {
        if (world == null || d == null || dt <= 0f) return;
        if (d.IsSlide) TickSlide(world, d, dOpen, dt);
        else TickRevolve(world, d, dAngle, dt);
    }

    static void TickSlide(World world, HSDoorsConfigData d, float dOpen, float dt)
    {
        if (Mathf.Abs(dOpen) < 0.0001f) return;
        var axis = HSDoorsSliding.SlideAxis(d);
        buf.Clear();
        var door = new Bounds(
            new Vector3(d.OriginX + d.SizeX * 0.5f, d.OriginY + d.SizeY * 0.5f, d.OriginZ + d.SizeZ * 0.5f),
            new Vector3(d.SizeX + d.Travel * 2f + 1f, d.SizeY + 1f, d.SizeZ + d.Travel * 2f + 1f));
        world.GetEntitiesInBounds(typeof(EntityAlive), door, buf);
        for (int i = 0; i < buf.Count; i++)
        {
            var e = buf[i] as EntityAlive;
            if (e == null || e.IsDead()) continue;
            int leaf = NearestLeaf(d, e.position);
            var box = HSDoorsSliding.LeafWorldBounds(d, leaf);
            box.Expand(0.15f);
            if (!box.Contains(e.position + Vector3.up * 0.6f) && !box.Contains(e.position)) continue;
            float sign = leaf == 1 ? -1f : 1f;
            var move = axis * (sign * dOpen * Mathf.Max(0.25f, d.Travel));
            ApplyMove(e, move);
            MaybeCrush(e, d, dt, dOpen < 0f);
        }
    }

    static int NearestLeaf(HSDoorsConfigData d, Vector3 p)
    {
        if (d.Mode == "left") return 1;
        if (d.Mode == "right") return 2;
        if (d.Axis == 0)
            return p.x < d.OriginX + d.SizeX * 0.5f ? 1 : 2;
        return p.z < d.OriginZ + d.SizeZ * 0.5f ? 1 : 2;
    }

    static void TickRevolve(World world, HSDoorsConfigData d, float dAngle, float dt)
    {
        if (Mathf.Abs(dAngle) < 0.001f && d.CrushDps <= 0f) return;
        buf.Clear();
        world.GetEntitiesInBounds(typeof(EntityAlive), HSDoorsSensor.SensorBounds(d), buf);
        for (int i = 0; i < buf.Count; i++)
        {
            var e = buf[i] as EntityAlive;
            if (e == null || e.IsDead()) continue;
            if (!HSDoorsRevolving.InsideDrum(d, e.position)) continue;
            if (Mathf.Abs(dAngle) >= 0.001f)
            {
                var next = HSDoorsRevolving.RotateAroundHub(d, e.position, dAngle);
                ApplyMove(e, next - e.position);
            }
            float dist = HSDoorsRevolving.DistToWing(d, e.position);
            if (dist < 0.18f) MaybeCrush(e, d, dt, true);
        }
    }

    static void ApplyMove(EntityAlive e, Vector3 delta)
    {
        if (delta.sqrMagnitude < 0.0000001f) return;
        var player = e as EntityPlayerLocal;
        if (player != null && player.vp_FPController != null)
        {
            player.vp_FPController.SetPosition(player.vp_FPController.Transform.position + delta);
            return;
        }
        e.SetPosition(e.position + delta);
    }

    static void MaybeCrush(EntityAlive e, HSDoorsConfigData d, float dt, bool closing)
    {
        if (!closing || d.CrushDps <= 0f || e == null) return;
        float dmg = HSDoorsSettings.ClampCrush(d.CrushDps) * dt;
        if (dmg <= 0f) return;
        try
        {
            e.DamageEntity(new DamageSource(EnumDamageSource.Internal, EnumDamageTypes.Crushing), (int)Mathf.Max(1f, dmg), false);
        }
        catch { }
    }
}
