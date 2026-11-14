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
            || c.IndexOf("lintel") >= 0
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
        if (d.Height < 2) d.Height = 2;
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
            if (name.IndexOf("slideheader", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (name.IndexOf("lintel", StringComparison.OrdinalIgnoreCase) >= 0) continue;
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

    // One header block per leaf column, in the cell directly above the leaves, same facing.
    public static int PlaceSlideHeader(World world, HSDoorsConfigData d)
    {
        if (world == null || d == null || d.Cells == null || d.Cells.Count == 0) return 0;
        int top = d.OriginY;
        byte rot = 0;
        var depthCount = new Dictionary<int, int>();
        var cols = new List<int>();
        foreach (var c in d.Cells)
        {
            var bv = HSDoorsConfig.BlockOf(c);
            int h = 1;
            if (bv.Block != null && bv.Block.isMultiBlock && bv.Block.multiBlockPos != null)
                h = Math.Max(1, bv.Block.multiBlockPos.dim.y);
            int cellTop = d.OriginY + c.Dy + h;
            if (cellTop > top) top = cellTop;
            rot = bv.rotation;
            int along = d.Axis == 0 ? d.OriginX + c.Dx : d.OriginZ + c.Dz;
            int depth = d.Axis == 0 ? d.OriginZ + c.Dz : d.OriginX + c.Dx;
            int n;
            depthCount.TryGetValue(depth, out n);
            depthCount[depth] = n + 1;
            if (!cols.Contains(along)) cols.Add(along);
        }
        if (cols.Count == 0 || depthCount.Count == 0) return 0;
        cols.Sort();
        int depthPick = d.Axis == 0 ? d.OriginZ : d.OriginX;
        int best = -1;
        foreach (var kv in depthCount)
        {
            if (kv.Value > best) { best = kv.Value; depthPick = kv.Key; }
        }
        int mid = cols[cols.Count / 2];
        byte yaw = (byte)(rot & 3);
        var above = new List<Vector3i>();
        for (int i = 0; i < cols.Count; i++)
        {
            int along = cols[i];
            above.Add(d.Axis == 0
                ? new Vector3i(along, top, depthPick)
                : new Vector3i(depthPick, top, along));
        }
        // Depth comes from the blocks sitting above the leaves. Those cells are
        // replaced. Nothing beside the opening is touched.
        bool full = DepthOf(world, above);
        if (!full && !AnyMeasured(world, above))
        {
            BlockValue side;
            Vector3i sidePos;
            bool hasSide = FindSideWall(world, d, top, depthPick, cols, out side, out sidePos);
            full = !hasSide || Thickness(side) >= 0.6f;
        }
        if (above.Count == 2)
        {
            Vector3i parent;
            byte modelYaw;
            if (PickWideRotation(yaw, above[0], above[1], out modelYaw, out parent))
            {
                var other = parent == above[0] ? above[1] : above[0];
                world.SetBlockRPC(parent, Lintel(BlockHSDoorsLintel.ModelWide, modelYaw, full));
                world.SetBlockRPC(other, Lintel(BlockHSDoorsLintel.ModelNone, modelYaw, full));
                HSDoorsDebug.Info("Header replaced " + parent + " and " + other + (full ? " full" : " panel"));
                return 2;
            }
        }
        int placed = 0;
        for (int i = 0; i < above.Count; i++)
        {
            int along = cols[i];
            byte kind = along == mid ? BlockHSDoorsLintel.ModelSensor : BlockHSDoorsLintel.ModelHeader;
            world.SetBlockRPC(above[i], Lintel(kind, yaw, full));
            placed++;
        }
        if (placed > 0)
            HSDoorsDebug.Info("Header replaced " + placed + " cell(s) above the leaves" + (full ? " full" : " panel"));
        return placed;
    }

    // True when the blocks above the door are at least a full wall deep.
    static bool DepthOf(World world, List<Vector3i> cells)
    {
        bool any = false;
        bool full = false;
        for (int i = 0; i < cells.Count; i++)
        {
            float t;
            if (!Measure(world, cells[i], out t)) continue;
            any = true;
            if (t >= 0.6f) full = true;
        }
        return any && full;
    }

    static bool AnyMeasured(World world, List<Vector3i> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            float t;
            if (Measure(world, cells[i], out t)) return true;
        }
        return false;
    }

    static bool Measure(World world, Vector3i pos, out float thickness)
    {
        thickness = 1f;
        if (world.GetChunkFromWorldPos(pos) == null) return false;
        var b = world.GetBlock(pos);
        if (b.ischild)
        {
            pos = ParentPos(pos, b);
            if (world.GetChunkFromWorldPos(pos) == null) return false;
            b = world.GetBlock(pos);
        }
        if (b.isair || b.Block == null) return false;
        var n = b.Block.GetBlockName() ?? "";
        if (n.IndexOf("lintel", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (n.IndexOf("slideheader", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        thickness = Thickness(b);
        return true;
    }

    // Rotation 6 stands the plate on the ceiling. meta is which housing to draw
    // in the bottom half. meta2 low bits are that housing's yaw; bit 2 is full depth.
    static BlockValue Lintel(byte model, byte yaw, bool full)
    {
        var bv = Block.GetBlockValue("hsdoorsLintelFull");
        if (bv.isair) return bv;
        bv.rotation = 6;
        bv.meta = model;
        bv.meta2 = (byte)((yaw & 3) | (full ? 4 : 0));
        return bv;
    }

    static float Thickness(BlockValue bv)
    {
        try
        {
            if (bv.isair || bv.Block == null || bv.Block.shape == null) return 1f;
            var bounds = bv.Block.shape.GetBounds(bv);
            if (bounds == null || bounds.Length == 0) return 1f;
            float t = Mathf.Min(bounds[0].size.x, bounds[0].size.z);
            return t < 0.02f ? 1f : t;
        }
        catch { return 1f; }
    }

    static bool FindSideWall(World world, HSDoorsConfigData d, int top, int depth, List<int> cols, out BlockValue bv, out Vector3i pos)
    {
        bv = BlockValue.Air;
        pos = Vector3i.zero;
        if (cols == null || cols.Count == 0) return false;
        int[] alongs = { cols[0] - 1, cols[cols.Count - 1] + 1 };
        if (ScanSide(world, d, top, depth, alongs, 0, 0, new[] { 0 }, out bv, out pos)) return true;
        if (ScanSide(world, d, top, depth, alongs, -2, -1, new[] { 0 }, out bv, out pos)) return true;
        return ScanSide(world, d, top, depth, alongs, -2, 0, new[] { 1, -1 }, out bv, out pos);
    }

    static bool ScanSide(World world, HSDoorsConfigData d, int top, int depth, int[] alongs, int y0, int y1, int[] depthOff, out BlockValue best, out Vector3i bestPos)
    {
        best = BlockValue.Air;
        bestPos = Vector3i.zero;
        float bestT = -1f;
        bool found = false;
        for (int i = 0; i < alongs.Length; i++)
        for (int y = y0; y <= y1; y++)
        for (int di = 0; di < depthOff.Length; di++)
        {
            int dep = depth + depthOff[di];
            var p = d.Axis == 0
                ? new Vector3i(alongs[i], top + y, dep)
                : new Vector3i(dep, top + y, alongs[i]);
            BlockValue b;
            if (!SideBlock(world, ref p, out b)) continue;
            float t = Thickness(b);
            if (!found || t > bestT)
            {
                found = true;
                bestT = t;
                best = b;
                bestPos = p;
            }
        }
        return found;
    }

    static bool SideBlock(World world, ref Vector3i p, out BlockValue b)
    {
        b = BlockValue.Air;
        if (world.GetChunkFromWorldPos(p) == null) return false;
        b = world.GetBlock(p);
        if (b.isair || b.Block == null) return false;
        if (b.ischild)
        {
            p = ParentPos(p, b);
            b = world.GetBlock(p);
            if (b.isair || b.Block == null) return false;
        }
        var n = b.Block.GetBlockName() ?? "";
        if (n.IndexOf("hsdoors", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return true;
    }

    static bool PickWideRotation(byte leafRot, Vector3i a, Vector3i b, out byte rotation, out Vector3i parent)
    {
        rotation = leafRot;
        parent = a;
        if (Connects(leafRot, a, b, out parent)) return true;
        var leafFwd = BlockShapeNew.GetRotationStatic(leafRot) * Vector3.forward;
        float best = -2f;
        bool found = false;
        for (byte r = 0; r < 4; r++)
        {
            Vector3i p;
            if (!Connects(r, a, b, out p)) continue;
            float dot = Vector3.Dot(BlockShapeNew.GetRotationStatic(r) * Vector3.forward, leafFwd);
            if (!found || dot > best)
            {
                best = dot;
                rotation = r;
                parent = p;
                found = true;
            }
        }
        return found;
    }

    static bool Connects(byte rotation, Vector3i a, Vector3i b, out Vector3i parent)
    {
        parent = a;
        int sx, sz;
        if (!HeaderStep(rotation, out sx, out sz)) return false;
        if (a.x + sx == b.x && a.z + sz == b.z) { parent = a; return true; }
        if (b.x + sx == a.x && b.z + sz == a.z) { parent = b; return true; }
        return false;
    }

    static bool HeaderStep(byte rotation, out int sx, out int sz)
    {
        var v = BlockShapeNew.GetRotationStatic(rotation) * Vector3.right;
        sx = Mathf.RoundToInt(v.x);
        sz = Mathf.RoundToInt(v.z);
        return sx * sx + sz * sz == 1;
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
