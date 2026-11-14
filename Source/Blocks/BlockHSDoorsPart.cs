using UnityEngine;

/// <summary>
/// ModelEntity pieces: gun/tool hits land on the Unity mesh. Without T_Block +
/// RootTransformRefParent on every child, that is a "ting" and the voxel never
/// takes damage — so the only way to clear them is knocking out the blocks that
/// support them. Same pattern SCore / vanilla loot ModelEntities use.
/// </summary>
public class BlockHSDoorsPart : Block
{
    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        WireHit(_ebcd);
    }

    // The placement ghost uses the rotation from Place (TowardsPlacer). On the
    // click, vanilla OnBlockPlaceBefore swaps PlaceAltBlockValue to a fresh
    // BlockValue, which is rotation 0, so the mesh lands facing the player
    // instead of the ghost. Put the ghost rotation back.
    public override void OnBlockPlaceBefore(WorldBase _world, ref BlockPlacement.Result _bpResult, EntityAlive _ea, GameRandom _rnd)
    {
        byte rotation = _bpResult.blockValue.rotation;
        base.OnBlockPlaceBefore(_world, ref _bpResult, _ea, _rnd);
        if (_bpResult.blockValue.Block != null && !_bpResult.blockValue.Block.PlaceRandomRotation)
            _bpResult.blockValue.rotation = rotation;
    }

    public static void WireHit(BlockEntityData ebcd)
    {
        if (ebcd == null || ebcd.transform == null) return;
        try
        {
            SetHitTag(ebcd.transform, ebcd.transform);
        }
        catch (System.Exception e)
        {
            HSDoorsDebug.Warn("Hit wire failed: " + e.Message);
        }
    }

    static void SetHitTag(Transform root, Transform t)
    {
        t.tag = "T_Block";
        foreach (Transform child in t)
            SetHitTag(root, child);
        if (root == t) return;
        var go = t.gameObject;
        var href = go.GetComponent<RootTransformRefParent>();
        if (href == null) href = go.AddComponent<RootTransformRefParent>();
        href.RootTransform = root;
    }
}
