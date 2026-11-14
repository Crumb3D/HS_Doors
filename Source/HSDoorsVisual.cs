using System;
using System.Collections.Generic;
using UnityEngine;

public class HSDoorsVisual
{
    public GameObject Root;
    readonly List<Transform> holders = new List<Transform>();
    readonly List<HSDoorsCell> cells = new List<HSDoorsCell>();
    readonly List<Vector3> rest = new List<Vector3>();
    readonly List<GameObject> headers = new List<GameObject>();
    HSDoorsConfigData bound;

    public void Bind(HSDoorsConfigData d)
    {
        bound = d;
    }

    public bool IsBuilt { get { return Root != null; } }

    public void Rebuild(World world)
    {
        Destroy();
        if (bound == null || !bound.Captured || bound.Cells == null || bound.Cells.Count == 0) return;
        Root = new GameObject("HSDoors_" + bound.DoorId);
        var rb = Root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        if (!GameManager.IsDedicatedServer)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (!GameManager.IsDedicatedServer)
            Root.AddComponent<HSDoorsPinnedLooks>();
        Root.transform.position = UnityOrigin();
        int layer = 16;
        var pin = Root.GetComponent<HSDoorsPinnedLooks>();
        foreach (var c in bound.Cells)
        {
            var holder = new GameObject("cell");
            holder.transform.SetParent(Root.transform, false);
            holder.transform.localPosition = new Vector3(c.Dx, c.Dy, c.Dz);
            holders.Add(holder.transform);
            cells.Add(c);
            rest.Add(holder.transform.localPosition);
            if (GameManager.IsDedicatedServer) continue;
            var bv = HSDoorsConfig.BlockOf(c);
            try
            {
                var ic = ItemClass.GetForId(bv.ToItemType());
                if (ic != null)
                {
                    var worldPos = new Vector3(bound.OriginX + c.Dx, bound.OriginY + c.Dy, bound.OriginZ + c.Dz);
                    var model = HSGameApi.CloneBlockModel(ic, world, bv, worldPos, holder.transform, HSDoorsConfig.FromLongs(c.Tex));
                    if (model != null)
                    {
                        // Same spot the chunk uses for the placed block. Zeroing the clone
                        // drops a ModelEntity onto the cell corner: half a block sideways and down.
                        PlaceLikeBlock(model, bv);
                        foreach (var col in model.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                        foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                        HideFocus(model);
                        if (pin != null) pin.Keep(model);
                    }
                }
            }
            catch (Exception e)
            {
                HSDoorsDebug.Warn("Door model failed: " + e.Message);
            }
            if (bv.Block == null || !bv.Block.IsCollideMovement) continue;
            Bounds[] bounds = null;
            try { bounds = bv.Block.shape.GetBounds(bv); } catch { }
            if (bounds == null || bounds.Length == 0) bounds = new[] { new Bounds(Vector3.one * 0.5f, Vector3.one) };
            foreach (var b in bounds)
            {
                if (b.size.x < 0.001f || b.size.y < 0.001f || b.size.z < 0.001f) continue;
                var bgo = new GameObject("col");
                bgo.transform.SetParent(holder.transform, false);
                bgo.transform.localPosition = b.center;
                bgo.layer = layer;
                bgo.AddComponent<BoxCollider>().size = b.size;
            }
        }
        if (bound.IsSlide) AddHeaders();
        Apply();
        HSDoorsDebug.Verbose("Visual " + bound.DoorId + ": " + holders.Count + " cells");
    }

    // A short bar on the top of each leaf, in that leaf's own blocks. It stays
    // over the opening while the leaves slide. About a third of the housing's height.
    void AddHeaders()
    {
        if (GameManager.IsDedicatedServer || bound == null) return;
        var best = new Dictionary<int, int>();
        for (int i = 0; i < holders.Count; i++)
        {
            int along = bound.Axis == 0 ? cells[i].Dx : cells[i].Dz;
            int depth = bound.Axis == 0 ? cells[i].Dz : cells[i].Dx;
            int key = along * 64 + depth;
            int have;
            if (!best.TryGetValue(key, out have) || cells[i].Dy > cells[have].Dy)
                best[key] = i;
        }
        foreach (var kv in best)
            AttachHeader(holders[kv.Value]);
    }

    void AttachHeader(Transform holder)
    {
        Bounds leafB;
        if (!TryBounds(holder, null, out leafB)) return;
        var block = Block.GetBlockValue("hsdoorsSlideHeader");
        if (block.isair || block.Block == null) return;
        var shape = block.Block.shape as BlockShapeModelEntity;
        if (shape == null || string.IsNullOrEmpty(shape.modelName)) return;
        Color tint;
        var go = GameObjectPool.Instance.GetObjectForType(shape.modelName, out tint);
        if (go == null)
        {
            HSDoorsDebug.Warn("Door header model missing.");
            return;
        }
        var t = go.transform;
        t.SetParent(holder, false);
        t.localPosition = Vector3.zero;
        t.localScale = Vector3.one;
        go.name = "HS_DoorHeader";
        var spin = LeafSpin(holder, t);
        t.rotation = spin;
        t.localScale = new Vector3(1f, 1f / 3f, 1f);
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        Bounds headB;
        if (TryBounds(holder, t, out headB))
        {
            t.localPosition += new Vector3(
                leafB.center.x - headB.center.x,
                leafB.max.y - headB.max.y,
                leafB.center.z - headB.center.z);
        }
        t.SetParent(Root.transform, true);
        headers.Add(go);
    }

    static Quaternion LeafSpin(Transform holder, Transform header)
    {
        var renders = holder.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renders.Length; i++)
        {
            if (renders[i] == null) continue;
            if (header != null && renders[i].transform.IsChildOf(header)) continue;
            return renders[i].transform.rotation;
        }
        return holder.rotation;
    }

    static bool TryBounds(Transform space, Transform only, out Bounds local)
    {
        local = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        var renders = (only != null ? only : space).GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renders.Length; i++)
        {
            var r = renders[i];
            if (r == null) continue;
            if (only == null && r.transform.name == "HS_DoorHeader") continue;
            if (only == null && r.transform.IsChildOf(space) && HasHeaderParent(r.transform, space)) continue;
            var ext = r.bounds.extents;
            var c = r.bounds.center;
            for (int k = 0; k < 8; k++)
            {
                var corner = c + Vector3.Scale(ext, new Vector3(
                    (k & 1) == 0 ? -1f : 1f,
                    (k & 2) == 0 ? -1f : 1f,
                    (k & 4) == 0 ? -1f : 1f));
                var p = space.InverseTransformPoint(corner);
                if (!any) { local = new Bounds(p, Vector3.zero); any = true; }
                else local.Encapsulate(p);
            }
        }
        return any;
    }

    static bool HasHeaderParent(Transform t, Transform stop)
    {
        while (t != null && t != stop)
        {
            if (t.name == "HS_DoorHeader") return true;
            t = t.parent;
        }
        return false;
    }

    public void Apply()
    {
        if (bound == null || Root == null) return;
        Root.transform.position = UnityOrigin();
        Root.transform.rotation = Quaternion.identity;
        if (bound.IsSlide)
        {
            for (int i = 0; i < holders.Count; i++)
            {
                var off = HSDoorsSliding.LeafOffset(bound, cells[i]);
                holders[i].localPosition = rest[i] + off;
                holders[i].localRotation = Quaternion.identity;
            }
        }
        else
        {
            Root.transform.position = UnityHub();
            Root.transform.rotation = Quaternion.AngleAxis(bound.Angle, Vector3.up);
            var localHub = new Vector3(bound.HubX - bound.OriginX + 0.5f, bound.HubY - bound.OriginY, bound.HubZ - bound.OriginZ + 0.5f);
            for (int i = 0; i < holders.Count; i++)
            {
                holders[i].localPosition = rest[i] - localHub;
                holders[i].localRotation = Quaternion.identity;
            }
        }
    }

    Vector3 UnityOrigin()
    {
        return new Vector3(bound.OriginX, bound.OriginY, bound.OriginZ) - Origin.position;
    }

    Vector3 UnityHub()
    {
        return new Vector3(bound.HubX + 0.5f, bound.HubY, bound.HubZ + 0.5f) - Origin.position;
    }

    static void PlaceLikeBlock(Transform model, BlockValue bv)
    {
        var shape = bv.Block != null ? bv.Block.shape : null;
        var rot = shape != null ? shape.GetRotation(bv) : Quaternion.identity;
        var entity = shape as BlockShapeModelEntity;
        if (entity == null)
        {
            model.localPosition = new Vector3(0.5f, 0.5f, 0.5f);
            model.localRotation = rot;
            return;
        }
        var off = entity.GetRotatedOffset(bv.Block, rot);
        off.x += 0.5f;
        off.z += 0.5f;
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        var visual = model.childCount > 0 ? model.GetChild(0) : model;
        visual.localPosition = off;
        visual.localRotation = rot;
    }

    static void HideFocus(Transform model)
    {
        if (model == null) return;
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == model) continue;
            var n = t.name;
            if (n.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("highlight", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("focus", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("interact", StringComparison.OrdinalIgnoreCase) >= 0)
                t.gameObject.SetActive(false);
        }
    }

    public void Destroy()
    {
        for (int i = 0; i < headers.Count; i++)
        {
            var go = headers[i];
            if (go == null) continue;
            go.transform.SetParent(null, false);
            go.transform.localScale = Vector3.one;
            go.transform.localRotation = Quaternion.identity;
            GameObjectPool.Instance.PoolObject(go);
        }
        headers.Clear();
        holders.Clear();
        cells.Clear();
        rest.Clear();
        if (Root != null) UnityEngine.Object.Destroy(Root);
        Root = null;
    }
}

public class HSDoorsPinnedLooks : MonoBehaviour
{
    readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

    public void Keep(Transform model)
    {
        if (model == null) return;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var shared = r.sharedMaterials;
            if (shared == null || shared.Length == 0) continue;
            var copies = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] == null) continue;
                var m = new Material(shared[i]) { hideFlags = HideFlags.DontUnloadUnusedAsset };
                owned.Add(m);
                copies[i] = m;
            }
            r.materials = copies;
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < owned.Count; i++)
            if (owned[i] != null) Destroy(owned[i]);
        owned.Clear();
    }
}
