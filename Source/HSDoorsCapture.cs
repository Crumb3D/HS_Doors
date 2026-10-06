using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public static class HSDoorsCapture
{
    public static bool IsDrumName(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        var c = n.ToLowerInvariant();
        return c.IndexOf("drumside") >= 0
            || c.IndexOf("drumwall") >= 0
            || c.IndexOf("canopy") >= 0
            || c.IndexOf("floormat") >= 0
            || c.IndexOf("slideheader") >= 0
            || c.IndexOf("hsdoorspanel") >= 0;
    }

    public static bool IsRotor3(string n)
    {
        return !string.IsNullOrEmpty(n) && n.IndexOf("Rotor3", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static int DetectWings(string n)
    {
        if (string.IsNullOrEmpty(n)) return 0;
        if (n.IndexOf("Rotor3", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
        if (n.IndexOf("Post4", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
        if (n.IndexOf("Post2", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
        return 0;
    }

    public static Vector3i ParentPos(Vector3i pos, BlockValue bv)
    {
        try
        {
            if (bv.ischild && bv.Block != null)
                return bv.Block.multiBlockPos.GetParentPos(pos, bv);
        }
        catch { }
        return pos;
    }

    public static string CaptureSlide(World world, HSDoorsConfigData d)
    {
        if (world == null || d == null) return "no world";
        if (d.Corner1 == null || d.Corner2 == null) return "Set Corner 1 and Corner 2 on the leaves.";
        int x0 = Math.Min(d.Corner1[0], d.Corner2[0]);
        int y0 = Math.Min(d.Corner1[1], d.Corner2[1]);
        int z0 = Math.Min(d.Corner1[2], d.Corner2[2]);
        int x1 = Math.Max(d.Corner1[0], d.Corner2[0]);
        int y1 = Math.Max(d.Corner1[1], d.Corner2[1]);
        int z1 = Math.Max(d.Corner1[2], d.Corner2[2]);
        d.OriginX = x0;
        d.OriginY = y0;
        d.OriginZ = z0;
        d.SizeX = x1 - x0 + 1;
        d.SizeY = y1 - y0 + 1;
        d.SizeZ = z1 - z0 + 1;
        if (d.SizeX >= d.SizeZ)
        {
            d.Axis = 0;
            d.AxisSign = d.Corner2[0] >= d.Corner1[0] ? 1 : -1;
            if (d.Travel < 0.5f) d.Travel = d.SizeX * 0.5f;
        }
        else
        {
            d.Axis = 2;
            d.AxisSign = d.Corner2[2] >= d.Corner1[2] ? 1 : -1;
            if (d.Travel < 0.5f) d.Travel = d.SizeZ * 0.5f;
        }
        d.Height = d.SizeY;
        return CaptureBox(world, d, true);
    }

    public static string CaptureRevolve(World world, HSDoorsConfigData d)
    {
        if (world == null || d == null) return "no world";
        if (!d.HasHub) return "Set Hub on the centre post.";
        if (d.Radius < 0.6f) d.Radius = 1.5f;
        if (d.Height < 2) d.Height = 3;
        int r = Mathf.CeilToInt(d.Radius + 0.2f);
        d.OriginX = d.HubX - r;
        d.OriginY = d.HubY;
        d.OriginZ = d.HubZ - r;
        d.SizeX = r * 2 + 1;
        d.SizeY = d.Height;
        d.SizeZ = r * 2 + 1;
        d.Wings = 4;
        return CaptureBox(world, d, false);
    }

    static string CaptureBox(World world, HSDoorsConfigData d, bool slide)
    {
        Restore(world, d);
        var cells = new List<HSDoorsCell>();
        var seen = new HashSet<Vector3i>();
        for (int y = 0; y < d.SizeY; y++)
        for (int x = 0; x < d.SizeX; x++)
        for (int z = 0; z < d.SizeZ; z++)
        {
            var pos = new Vector3i(d.OriginX + x, d.OriginY + y, d.OriginZ + z);
            if (!slide && !d.InCylinder(pos.x, pos.y, pos.z)) continue;
            if (d.IsExcluded(pos.x, pos.y, pos.z)) continue;
            if (world.GetChunkFromWorldPos(pos) == null) return "Chunk not loaded at " + pos + ".";
            var bv = world.GetBlock(pos);
            if (bv.isair) continue;
            var parent = ParentPos(pos, bv);
            if (!seen.Add(parent)) continue;
            var pbv = world.GetBlock(parent);
            if (pbv.isair || pbv.Block == null) continue;
            try { if (pbv.Block.shape != null && pbv.Block.shape.IsTerrain()) continue; } catch { }
            if (pbv.Block is BlockHSDoorsPanel) continue;
            var name = pbv.Block.GetBlockName() ?? "";
            if (IsDrumName(name) && !slide) continue;
            int w = DetectWings(name);
            if (w > 0) d.Wings = w;
            var chunk = world.GetChunkFromWorldPos(parent) as Chunk;
            int lx = World.toBlockXZ(parent.x), ly = World.toBlockY(parent.y), lz = World.toBlockXZ(parent.z);
            var cell = new HSDoorsCell
            {
                Dx = parent.x - d.OriginX,
                Dy = parent.y - d.OriginY,
                Dz = parent.z - d.OriginZ,
                Raw = pbv.rawData,
                Damage = pbv.damage,
                Density = chunk != null ? chunk.GetDensity(lx, ly, lz) : (sbyte)0,
                Tex = HSDoorsConfig.ToLongs(chunk != null ? chunk.GetTextureFullArray(lx, ly, lz) : TextureFullArray.Default)
            };
            cell.Leaf = AssignLeaf(d, cell, slide);
            cells.Add(cell);
        }
        if (cells.Count == 0) return "No moving blocks in that area. Build the leaves / wings, then mark them again.";
        var cap = HSDoorsSettings.RejectIfOverCap(cells.Count);
        if (cap != null) return cap;
        var changes = new List<BlockChangeInfo>();
        foreach (var c in cells)
        {
            var pos = new Vector3i(d.OriginX + c.Dx, d.OriginY + c.Dy, d.OriginZ + c.Dz);
            var bv = HSDoorsConfig.BlockOf(c);
            changes.Add(new BlockChangeInfo(pos, BlockValue.Air, MarchingCubes.DensityAir));
            if (bv.Block != null && bv.Block.isMultiBlock)
            {
                for (int i = 0; i < bv.Block.multiBlockPos.Length; i++)
                {
                    var cp = pos + bv.Block.multiBlockPos.Get(i, bv.type, bv.rotation);
                    if (cp != pos) changes.Add(new BlockChangeInfo(cp, BlockValue.Air, MarchingCubes.DensityAir));
                }
            }
        }
        world.SetBlocksRPC(changes);
        d.Cells = cells;
        d.Captured = true;
        d.Open = 0f;
        d.Angle = 0f;
        d.StopReason = null;
        WriteJournal(d);
        HSDoorsDebug.Info("Captured " + cells.Count + " moving block(s) for " + d.DoorId + " (" + d.Wings + "-wing)");
        return null;
    }

    static int AssignLeaf(HSDoorsConfigData d, HSDoorsCell c, bool slide)
    {
        if (!slide) return 0;
        if (d.Mode == "left") return 1;
        if (d.Mode == "right") return 2;
        if (d.Axis == 0)
        {
            float mid = (d.SizeX - 1) * 0.5f;
            return c.Dx + 0.01f < mid ? 1 : 2;
        }
        else
        {
            float mid = (d.SizeZ - 1) * 0.5f;
            return c.Dz + 0.01f < mid ? 1 : 2;
        }
    }

    public static void Restore(World world, HSDoorsConfigData d, bool keepCaptured = false)
    {
        if (world == null || d == null || d.Cells == null || d.Cells.Count == 0) return;
        if (!d.Captured && !keepCaptured) return;
        var changes = new List<BlockChangeInfo>();
        foreach (var s in d.Cells)
        {
            var pos = new Vector3i(d.OriginX + s.Dx, d.OriginY + s.Dy, d.OriginZ + s.Dz);
            if (world.GetChunkFromWorldPos(pos) == null) continue;
            if (!world.GetBlock(pos).isair) continue;
            var bv = new BlockValue(s.Raw, s.Damage);
            changes.Add(new BlockChangeInfo(pos, bv, s.Density, HSDoorsConfig.FromLongs(s.Tex)));
        }
        if (changes.Count > 0) world.SetBlocksRPC(changes);
        if (!keepCaptured)
        {
            d.Captured = false;
            ClearJournal(d);
        }
        HSDoorsDebug.Info("Restored " + changes.Count + " block(s) for " + d.DoorId);
    }

    public static void EnsureCapturedRemoved(World world, HSDoorsConfigData d)
    {
        if (world == null || d == null || !d.Captured || d.Cells == null) return;
        var changes = new List<BlockChangeInfo>();
        foreach (var s in d.Cells)
        {
            var pos = new Vector3i(d.OriginX + s.Dx, d.OriginY + s.Dy, d.OriginZ + s.Dz);
            if (world.GetChunkFromWorldPos(pos) == null) continue;
            var bv = world.GetBlock(pos);
            if (bv.isair) continue;
            if (bv.rawData == s.Raw) changes.Add(new BlockChangeInfo(pos, BlockValue.Air, MarchingCubes.DensityAir));
        }
        if (changes.Count > 0) world.SetBlocksRPC(changes);
    }

    static string JournalPath(HSDoorsConfigData d)
    {
        var safe = d == null || string.IsNullOrEmpty(d.DoorId) ? "door1" : d.DoorId;
        foreach (var c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');
        return Path.Combine(HSDoorsConfig.RuntimeDir, "HSDoors." + safe + ".journal.json");
    }

    public static void WriteJournal(HSDoorsConfigData d)
    {
        if (d == null || HSDoorsNet.IsRemoteClient) return;
        try
        {
            var dir = HSDoorsConfig.RuntimeDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(JournalPath(d), JsonConvert.SerializeObject(d, Formatting.Indented));
        }
        catch (Exception e)
        {
            HSDoorsDebug.Warn("Journal write failed: " + e.Message);
        }
    }

    public static void ClearJournal(HSDoorsConfigData d)
    {
        try
        {
            var p = JournalPath(d);
            if (File.Exists(p)) File.Delete(p);
        }
        catch { }
    }
}
