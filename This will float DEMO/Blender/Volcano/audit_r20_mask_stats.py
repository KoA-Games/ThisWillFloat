import json
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parent
BLEND = ROOT / "Volcano_Hero_02_TropicalLayerBalance_R19.blend"
OUT = Path("/tmp/volcano_r20_mask_stats.json")

bpy.ops.wm.open_mainfile(filepath=str(BLEND))
obj = bpy.data.objects["Volcano_Hero_02"]
mesh = obj.data
attr = mesh.color_attributes["VolcanoNaturalZoneWeightsRGBA"]

values = [tuple(item.color) for item in attr.data]
count = len(values)
mins = [min(v[i] for v in values) for i in range(4)]
maxs = [max(v[i] for v in values) for i in range(4)]
means = [sum(v[i] for v in values) / count for i in range(4)]
dominant = [0, 0, 0, 0]
for v in values:
    dominant[max(range(4), key=lambda i: v[i])] += 1

# CORNER-domain height statistics, matched to loop vertex indices.
zs = [obj.matrix_world @ vert.co for vert in mesh.vertices]
zmin = min(v.z for v in zs)
zmax = max(v.z for v in zs)
bands = []
for lo, hi in ((0.0, .2), (.2, .35), (.35, .55), (.55, .75), (.75, 1.01)):
    samples = []
    if attr.domain == 'CORNER':
        for loop, col in zip(mesh.loops, values):
            zn = (zs[loop.vertex_index].z - zmin) / max(zmax-zmin, 1e-9)
            if lo <= zn < hi:
                samples.append(col)
    else:
        for i, col in enumerate(values):
            zn = (zs[i].z - zmin) / max(zmax-zmin, 1e-9)
            if lo <= zn < hi:
                samples.append(col)
    bands.append({
        'range': [lo, hi],
        'count': len(samples),
        'mean_rgba': [sum(v[i] for v in samples)/max(len(samples), 1) for i in range(4)],
    })

mat = obj.active_material
names = [
    "VEG_R11_ColorFactor", "VEG_R11_VegetationMask", "VEG_R12_Density",
    "VEG_R14_MidTransitionMask", "VEG_R14_BaseHeightPatch", "VEG_R14_AllVegetationUnion",
    "VEG_R15_BareSoilStrength", "VEG_R18_LowerGuaranteeStrength", "VEG_R18_RightLowerStrength",
    "VEG_R18_VisibleGreenPalette", "VEG_R18_RightShadowGreenPalette",
]
nodes = {}
for name in names:
    n = mat.node_tree.nodes.get(name)
    if not n:
        nodes[name] = None
        continue
    nodes[name] = {
        'type': n.bl_idname,
        'label': n.label,
        'inputs': [
            {
                'name': s.name,
                'default': list(s.default_value) if hasattr(s.default_value, '__len__') and not isinstance(s.default_value, str) else getattr(s, 'default_value', None),
                'from': [(l.from_node.name, l.from_socket.name) for l in s.links],
            }
            for s in n.inputs
        ],
    }

report = {
    'attribute': {'name': attr.name, 'domain': attr.domain, 'type': attr.data_type, 'count': count},
    'bounds_world': {'zmin': zmin, 'zmax': zmax},
    'min_rgba': mins,
    'max_rgba': maxs,
    'mean_rgba': means,
    'dominant_pct': [100.0*x/count for x in dominant],
    'height_bands': bands,
    'nodes': nodes,
}
OUT.write_text(json.dumps(report, indent=2), encoding='utf-8')
print(f"R20_MASK_STATS_COMPLETE {OUT}")
