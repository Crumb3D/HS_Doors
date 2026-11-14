using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paintable cap in the header cell. The metal operator is drawn with it and
/// stretched on its thin axis so the bar is the same depth as that cap.
/// Full walls use a ceiling plate (rotation 6). Thinner walls use a panel,
/// which is as thin as the bar is allowed to go.
/// </summary>
public class BlockHSDoorsLintel : Block
{
    public const byte ModelNone = 0;
    public const byte ModelHeader = 1;
    public const byte ModelSensor = 2;
    public const byte ModelEnd = 3;
    public const byte ModelWide = 4;

    static readonly Dictionary<Vector3i, Transform> Live = new Dictionary<Vector3i, Transform>();

    public static string ModelBlockName(byte kind)
    {
        switch (kind)
        {
            case ModelHeader: return "hsdoorsSlideHeader";
            case ModelSensor: return "hsdoorsSlideHeaderSensor";
            case ModelEnd: return "hsdoorsSlideHeaderEnd";
            case ModelWide: return "hsdoorsSlideHeader2";
            default: return null;
        }
    }

    public override void OnBlockAdded(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(_world, _chunk, _blockPos, _blockValue, _addedByPlayer);
        Show(_chunk, _blockPos, _blockValue);
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
        var chunk = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetChunkFromWorldPos(_blockPos) as Chunk
            : null;
        Show(chunk, _blockPos, _blockValue);
    }

    public override void OnBlockUnloaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        Hide(_blockPos);
        base.OnBlockUnloaded(_world, _blockPos, _blockValue);
    }

    public override void OnBlockRemoved(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        Hide(_blockPos);
        base.OnBlockRemoved(_world, _chunk, _blockPos, _blockValue);
    }

    static void Show(Chunk chunk, Vector3i pos, BlockValue bv)
    {
        if (chunk == null || bv.meta == ModelNone) return;
        if (GameManager.IsDedicatedServer) return;
        Transform existing;
        if (Live.TryGetValue(pos, out existing) && existing != null) return;

        string blockName = ModelBlockName(bv.meta);
        if (string.IsNullOrEmpty(blockName)) return;
        var header = Block.GetBlockValue(blockName);
        if (header.isair || header.Block == null) return;
        var modelShape = header.Block.shape as BlockShapeModelEntity;
        if (modelShape == null || string.IsNullOrEmpty(modelShape.modelName)) return;

        Color tint;
        var go = GameObjectPool.Instance.GetObjectForType(modelShape.modelName, out tint);
        if (go == null) return;

        byte yaw = (byte)(bv.meta2 & 3);
        var rotation = BlockShapeNew.GetRotationStatic(yaw);
        var offset = modelShape.GetRotatedOffset(header.Block, rotation);
        offset.x += 0.5f;
        offset.z += 0.5f;
        var world = pos.ToVector3() + offset;

        var t = go.transform;
        t.SetParent(null, false);
        t.localScale = Vector3.one;
        t.SetPositionAndRotation(world - Origin.position, rotation);

        bool thinIsX;
        float thin = LocalThin(t, out thinIsX);
        float target = TargetDepth(bv);
        if (thin > 0.02f && Mathf.Abs(target / thin - 1f) > 0.08f)
        {
            var scale = t.localScale;
            float k = target / thin;
            if (thinIsX) scale.x = k;
            else scale.z = k;
            t.localScale = scale;
        }

        go.SetActive(true);
        AlignToCap(t, pos, bv, thinIsX, target < 0.75f);
        int layer;
        if (ChunkCluster.LayerMappingTable.TryGetValue("terraincollision", out layer))
            Utils.SetColliderLayerRecursively(go, layer);

        var ebcd = new BlockEntityData(bv, pos);
        ebcd.transform = t;
        ebcd.bHasTransform = true;
        ebcd.bRenderingOn = true;
        chunk.AddEntityBlockStub(ebcd);
        BlockHSDoorsPart.WireHit(ebcd);
        Origin.Add(t, -1);
        Live[pos] = t;
    }

    static float TargetDepth(BlockValue bv)
    {
        return (bv.meta2 & 4) != 0 ? 1f : 0.25f;
    }

    static float LocalThin(Transform root, out bool axisIsX)
    {
        bool any = false;
        var min = Vector3.zero;
        var max = Vector3.zero;
        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            var mf = renderers[r].GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f));
                var local = root.InverseTransformPoint(renderers[r].transform.TransformPoint(corner));
                if (!any) { min = max = local; any = true; }
                else
                {
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
        }
        var size = max - min;
        axisIsX = size.x < size.z;
        return any ? (axisIsX ? size.x : size.z) : 0f;
    }

    static void AlignToCap(Transform t, Vector3i pos, BlockValue bv, bool thinIsX, bool proud)
    {
        Bounds cap;
        try
        {
            var bounds = bv.Block.shape.GetBounds(bv);
            if (bounds == null || bounds.Length == 0) return;
            cap = bounds[0];
        }
        catch { return; }

        var renderers = t.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        var box = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);

        var axis = thinIsX ? t.right : t.forward;
        if (axis.sqrMagnitude < 0.01f) return;
        axis.Normalize();
        var shapeWorld = pos.ToVector3() + new Vector3(cap.center.x, 0f, cap.center.z);
        var modelWorld = box.center + Origin.position;
        t.position += Vector3.Project(shapeWorld - modelWorld, axis);
        if (!proud) return;
        var outDir = shapeWorld - (pos.ToVector3() + new Vector3(0.5f, 0f, 0.5f));
        outDir.y = 0f;
        if (outDir.sqrMagnitude > 0.0001f)
            t.position += outDir.normalized * 0.012f;
    }

    static void Hide(Vector3i pos)
    {
        Transform mine;
        if (!Live.TryGetValue(pos, out mine)) mine = null;
        Live.Remove(pos);

        Chunk chunk = null;
        if (GameManager.Instance != null && GameManager.Instance.World != null)
            chunk = GameManager.Instance.World.GetChunkFromWorldPos(pos) as Chunk;
        BlockEntityData ebcd = chunk != null ? chunk.GetBlockEntity(pos) : null;
        bool chunkAlreadyPooled = ebcd == null || !ebcd.bHasTransform || ebcd.transform == null;

        if (mine != null)
        {
            Origin.Remove(mine);
            mine.localScale = Vector3.one;
            if (!chunkAlreadyPooled)
                GameObjectPool.Instance.PoolObject(mine.gameObject);
        }
        if (ebcd != null && chunk != null)
        {
            ebcd.bHasTransform = false;
            ebcd.transform = null;
            chunk.RemoveEntityBlockStub(pos);
        }
    }
}
