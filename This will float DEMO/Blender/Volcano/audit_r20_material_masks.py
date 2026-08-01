import json
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parent
BLEND = ROOT / "Volcano_Hero_02_TropicalLayerBalance_R19.blend"
OUT = Path("/tmp/volcano_r20_material_audit.json")


def value(v):
    if hasattr(v, "to_list"):
        return v.to_list()
    if isinstance(v, (str, int, float, bool)) or v is None:
        return v
    try:
        return list(v)
    except Exception:
        return str(v)


def socket_info(socket):
    entry = {
        "name": socket.name,
        "identifier": socket.identifier,
        "linked": bool(socket.is_linked),
    }
    if hasattr(socket, "default_value"):
        entry["default"] = value(socket.default_value)
    if socket.is_linked:
        entry["links"] = [
            {
                "from_node": link.from_node.name,
                "from_socket": link.from_socket.name,
            }
            for link in socket.links
        ]
    return entry


def node_info(node):
    entry = {
        "name": node.name,
        "label": node.label,
        "type": node.bl_idname,
        "inputs": [socket_info(s) for s in node.inputs],
        "outputs": [{"name": s.name, "identifier": s.identifier} for s in node.outputs],
    }
    for attr in ("operation", "blend_type", "data_type", "mapping", "interpolation_type", "normalize"):
        if hasattr(node, attr):
            entry[attr] = value(getattr(node, attr))
    if node.bl_idname == "ShaderNodeValToRGB":
        entry["ramp"] = [
            {"position": e.position, "color": list(e.color)}
            for e in node.color_ramp.elements
        ]
        entry["ramp_interpolation"] = node.color_ramp.interpolation
    if node.bl_idname == "ShaderNodeTexImage":
        entry["image"] = node.image.name if node.image else None
        entry["image_path"] = bpy.path.abspath(node.image.filepath) if node.image else None
    return entry


bpy.ops.wm.open_mainfile(filepath=str(BLEND))
hero = bpy.data.objects["Volcano_Hero_02"]
mesh = hero.data
mat = hero.active_material
report = {
    "blend": str(BLEND),
    "object": hero.name,
    "material": mat.name,
    "vertex_count": len(mesh.vertices),
    "edge_count": len(mesh.edges),
    "polygon_count": len(mesh.polygons),
    "uv_layers": [{"name": uv.name, "active": uv.active, "active_render": uv.active_render} for uv in mesh.uv_layers],
    "color_attributes": [
        {"name": a.name, "domain": a.domain, "data_type": a.data_type}
        for a in mesh.color_attributes
    ],
    "modifiers": [{"name": m.name, "type": m.type, "show_render": m.show_render} for m in hero.modifiers],
    "nodes": [node_info(n) for n in mat.node_tree.nodes],
    "links": [
        {
            "from_node": l.from_node.name,
            "from_socket": l.from_socket.name,
            "to_node": l.to_node.name,
            "to_socket": l.to_socket.name,
        }
        for l in mat.node_tree.links
    ],
}
OUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(f"R20_AUDIT_COMPLETE {OUT} nodes={len(report['nodes'])} links={len(report['links'])}")
