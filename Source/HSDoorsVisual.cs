using System;
using System.Collections.Generic;
using UnityEngine;

public class HSDoorsVisual
{
    public GameObject Root;
    readonly List<Transform> holders = new List<Transform>();
    readonly List<HSDoorsCell> cells = new List<HSDoorsCell>();
    readonly List<Vector3> rest = new List<Vector3>();
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
                    var model = ic.CloneModel(world, bv.ToItemValue(), worldPos, holder.transform, _textureFullArray: HSDoorsConfig.FromLongs(c.Tex));
                    if (model != null)
                    {
                        model.localPosition = Vector3.zero;
                        model.localRotation = bv.Block.shape.GetRotation(bv);
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
        Apply();
        HSDoorsDebug.Verbose("Visual " + bound.DoorId + ": " + holders.Count + " cells");
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
