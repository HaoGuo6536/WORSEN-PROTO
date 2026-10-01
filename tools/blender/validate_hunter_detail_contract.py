# ============================================================================
# validate_hunter_detail_contract.py
# PURPOSE:
#   Enforce the owner's detailed-prototype geometry budget on actual mesh data.
#   Share one fail-closed measurement between both export validators and mutation
#   tests so metadata alone cannot claim a compliant triangle/material count.
# ARCHITECTURAL ROLE:
#   Offline art validator · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Measure triangulated faces and populated material slots across a body.
#   - Reject out-of-budget geometry, missing materials and unused slots.
#   - Exercise both boundaries and material-count regressions headlessly.
# DEPENDENCIES:
#   Blender 5.2 bpy, Python standard library; no generator imports.
# USAGE NOTES:
#   3000–8000 triangles and 2–4 unique used materials are owner acceptance gates,
#   not tunable motion minima. Run this file directly for counted mutation tests.
# ============================================================================
import json
from pathlib import Path
import bpy


def measure_budget(meshes, check):
    triangles=0
    materials=set()
    valid=bool(meshes)
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles+=len(obj.data.loop_triangles)
        used={p.material_index for p in obj.data.polygons}
        valid &= bool(used) and used==set(range(len(obj.material_slots)))
        for slot in obj.material_slots:
            if slot.material is None:
                valid=False
            else:
                materials.add(slot.material.name)
    check('detail_triangle_budget',3000<=triangles<=8000,triangles)
    check('detail_material_budget',valid and 2<=len(materials)<=4,
          {'unique_used_materials':len(materials),'all_slots_used_and_populated':bool(valid)})


def main():
    results=[]
    for triangles,slots,valid in ((2999,2,False),(3000,2,True),(8000,4,True),
                                  (8001,4,False),(4000,1,False),(4000,5,False)):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        data=bpy.data.meshes.new('BudgetBoundary')
        data.from_pydata([(0,0,0),(1,0,0),(0,1,0)],[],[(0,1,2)]*triangles)
        obj=bpy.data.objects.new('BudgetBoundary',data)
        bpy.context.collection.objects.link(obj)
        for i in range(slots):
            data.materials.append(bpy.data.materials.new('Slot'+str(i)))
        for polygon in data.polygons:
            polygon.material_index=polygon.index%slots
        checks=[]
        measure_budget([obj],lambda name,ok,detail:checks.append(bool(ok)))
        results.append({'triangles':triangles,'slots':slots,'expected_accept':valid,
                        'passed':all(checks)==valid})
    checks=[]
    measure_budget([],lambda name,ok,detail:checks.append(bool(ok)))
    results.append({'test':'empty_body_rejected','passed':not any(checks)})
    data.materials.pop(index=4)
    for polygon in data.polygons:
        polygon.material_index=polygon.index%4
    checks=[]
    measure_budget([obj],lambda name,ok,detail:checks.append(bool(ok)))
    assert all(checks), 'Missing-material mutation must start from a valid body'
    data.materials[0]=None
    checks=[]
    measure_budget([obj],lambda name,ok,detail:checks.append(bool(ok)))
    results.append({'test':'missing_slot_rejected','passed':not all(checks)})
    data.materials[0]=bpy.data.materials.new('Replacement')
    for polygon in data.polygons:
        polygon.material_index=polygon.index%3
    checks=[]
    measure_budget([obj],lambda name,ok,detail:checks.append(bool(ok)))
    results.append({'test':'unused_slot_rejected','passed':not all(checks)})
    report={'passed':sum(r['passed'] for r in results),
            'failed':sum(not r['passed'] for r in results),'tests':results}
    folder=Path(__file__).resolve().parents[2]/'Logs/AgentValidation/Art/HunterDetailPass2'
    folder.mkdir(parents=True,exist_ok=True)
    (folder/'budget-regressions.json').write_text(json.dumps(report,indent=2)+'\n')
    print('DETAIL_BUDGET_REGRESSIONS',json.dumps(report),flush=True)
    if report['failed']:
        raise RuntimeError('Budget gate accepted a regression')


if __name__=='__main__':
    main()
