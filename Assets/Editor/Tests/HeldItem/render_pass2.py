"""Render compiled HUD/held-item data without Unity. Usage: render_pass2.py <pure-run>.

Writes a managed data probe and PNG evidence inside the ignored pure-run directory.
Software projection is visual evidence of geometry/layout, not a Unity screenshot.
No assets, imports, Library writes, network requests or external packages installed.
"""
import json
import math
import pathlib
import re
import subprocess
import sys
from PIL import Image, ImageDraw, ImageFont

ROOT = pathlib.Path(__file__).resolve().parents[4]
RUN = ROOT / 'Logs/AgentValidation/PLAN-002/offline-compile' / sys.argv[1]
assert json.loads((RUN / 'summary.json').read_text(encoding='utf-8-sig'))['ExitCode'] == 0
version = re.search(r'm_EditorVersion: (\S+)', (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text()).group(1)
data = pathlib.Path(f'C:/Program Files/Unity/Hub/Editor/{version}/Editor/Data')
dotnet = data / 'NetCoreRuntime/dotnet.exe'
runtime = max((data / 'NetCoreRuntime/shared/Microsoft.NETCore.App').iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
held_config = (ROOT / 'Assets/Scripts/Presentation/HeldItem/Config/HeldItemDriverConfig.cs').read_text()
hud_config = (ROOT / 'Assets/Scripts/Presentation/HUD/Config/HUDDriverConfig.cs').read_text()

def scalar(text, name):
    return float(re.search(r'_' + name + r' = ([0-9.]+)f?;', text).group(1))

def vector(text, name, kind):
    return [float(v.strip().rstrip('f')) for v in re.search(r'_' + name + r' = new ' + kind + r'\(([^)]+)\)', text).group(1).split(',')]

held = {k: scalar(held_config, k) for k in ['scale', 'transitionSeconds', 'lowerDistance', 'swayAmplitude', 'swayPeriod']}
held.update({k: vector(held_config, k, 'Vector3') for k in ['position', 'euler']})
held.update({k: vector(held_config, k, 'Color') for k in ['bodyColor', 'detailColor']})
hud = {k: scalar(hud_config, k) for k in ['safeInset', 'inventorySlotWidth', 'inventorySlotHeight', 'inventorySlotGap', 'selectedSlotScale', 'flashlightSlotGap', 'flashlightSlotWidth', 'compassSize', 'counterArrowGap', 'countFontSize', 'fontSize', 'smallFontSize', 'healthWidth', 'healthBarHeight']}
source = r'''using System;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.HUD;
using Worsen.Presentation.HeldItem;
class PassTwoDump {
 static float[] V(Vector3 v) => new[]{v.x,v.y,v.z};
 static float[] R(Rect r) => new[]{r.x,r.y,r.width,r.height};
 static ConsumableInventorySnapshot Slots(int index) => new ConsumableInventorySnapshot(new[]{
  new ProgressionInventorySlot("gauze","Gauze",0), default,
  new ProgressionInventorySlot("glass-vial","Glass vial",0)},new[]{1,0,2},index);
 static void Main() {
  var gp=new HeldItemGeometryPresenter(); var hp=new HeldItemPresenter(); var hs=new HeldItemDriverState();
  var shapes=new Dictionary<string,object>();
  foreach(var id in new[]{"gauze","glass-vial","oil-flask","firecracker","smelling-salts","wax-ward","doorstop","adrenaline"}) {
   var parts=new List<object>(); foreach(var part in gp.Build(id)) parts.Add(new {shape=part.Shape.ToString(),position=V(part.Position),scale=V(part.Scale),detail=part.Detail});
   shapes.Add(id,parts);
  }
  var frames=new List<object>();
  void Capture(string caption) { frames.Add(new {caption,id=hs.DisplayedId,raise=hs.Raise,position=V(hp.ViewPosition(hp.Position(hs,new Vector3(PX,PY,PZ),LOWER,SWAY),60f,420f/236f))}); }
  hp.SetSelection(hs,Slots(0)); Capture("Selected / start");
  hp.Tick(hs,TRANSITION*.5f,TRANSITION,PERIOD); Capture("Raise / halfway");
  hp.Tick(hs,TRANSITION*.5f,TRANSITION,PERIOD); Capture("Raised / before click");
  hp.Tick(hs,PERIOD*.25f,TRANSITION,PERIOD); Capture("Idle sway");
  hp.SetSelection(hs,default); hp.Tick(hs,TRANSITION*.5f,TRANSITION,PERIOD); Capture("Consumed / lowering");
  hp.Tick(hs,TRANSITION*.5f,TRANSITION,PERIOD); Capture("Consumed / empty");
  hp.SetSelection(hs,Slots(2)); hp.Tick(hs,TRANSITION,TRANSITION,PERIOD); Capture("Vial selected");
  hp.SetSelection(hs,Slots(0)); hp.Tick(hs,TRANSITION*.5f,TRANSITION,PERIOD); Capture("Switch / vial lowering");
  hp.Tick(hs,TRANSITION*.5f,TRANSITION,PERIOD); Capture("Switch / below view");
  hp.Tick(hs,TRANSITION*.5f,TRANSITION,PERIOD); Capture("Switch / gauze raising");
  hp.Tick(hs,TRANSITION*.5f,TRANSITION,PERIOD); Capture("Switch / gauze raised");
  var layouts=new List<object>(); var lp=new HUDLayoutPresenter(); var ip=new HUDInventoryPresenter(); var guidance=new HUDGuidancePresenter();
  foreach(var dim in new[]{new[]{1280,720},new[]{1920,1080},new[]{2560,1080},new[]{3440,1440}}) {
   // UI Toolkit Expand uses the smaller reference-resolution scale.
   float scale=Math.Min(dim[0]/1920f,dim[1]/1080f), w=dim[0]/scale,h=dim[1]/scale;
   var safe=lp.SafeRect(w,h,INSET); float rowWidth=ip.FlashlightLeft(SLOTW,SLOTG,LIGHTG)+LIGHTW;
   var slots=new List<object>(); var state=new HUDDriverState(); ip.SetSlots(state,Slots(2)); ip.SetFlashlight(state,true,1f,.5f);
   new HUDPresenter().SetHealth(state,72f,100f); new HUDPresenter().SetCount(state,1,4); new HUDPresenter().SetDirection(state,Vector3.forward,true);
   for(int i=0;i<3;i++) slots.Add(new {rect=R(ip.SlotRect(i,i==2,SLOTW,SLOTH,SLOTG,SELECT)),label=state.SlotLabels[i],selected=i==2});
   layouts.Add(new {dim,scale,heldPosition=V(hp.ViewPosition(new Vector3(PX,PY,PZ),60f,(float)dim[0]/dim[1])),safe=R(safe),arrow=R(lp.Arrow(safe,ARROW)),countClearance=guidance.CounterBottom(ARROW,COUNTG),
    inventory=R(lp.Inventory(safe,rowWidth,SLOTH,ARROW,guidance.CounterBottom(ARROW,COUNTG),COUNTF,SLOTG)),
    caption=R(lp.Caption(safe,rowWidth,FONT*1.5f,ARROW)),health=R(lp.Health(safe,HEALTHW,FONT*1.5f+HEALTHH)),
    slots,lightLeft=ip.FlashlightLeft(SLOTW,SLOTG,LIGHTG),lightWidth=LIGHTW,state.HealthText,state.HealthFraction,state.CountText,
    state.SelectedSlotText,state.FlashlightText,state.FlashlightStatusText,state.FlashlightCharge,state.FlashlightAim});
  }
  var arrowVertices=new List<float[]>(); foreach(var v in new HUDGeometryPresenter().Arrow(new Rect(0,0,1,1))) arrowVertices.Add(new[]{v.x,v.y});
  Console.WriteLine(JsonSerializer.Serialize(new {shapes,frames,layouts,arrowVertices}));
 }
}'''
tokens = {'PX': held['position'][0], 'PY': held['position'][1], 'PZ': held['position'][2], 'LOWER': held['lowerDistance'], 'SWAY': held['swayAmplitude'], 'TRANSITION': held['transitionSeconds'], 'PERIOD': held['swayPeriod']}
tokens.update({token: hud[name] for token, name in {'INSET': 'safeInset', 'SLOTW': 'inventorySlotWidth', 'SLOTH': 'inventorySlotHeight', 'SLOTG': 'inventorySlotGap', 'LIGHTG': 'flashlightSlotGap', 'LIGHTW': 'flashlightSlotWidth', 'SELECT': 'selectedSlotScale', 'ARROW': 'compassSize', 'COUNTG': 'counterArrowGap', 'COUNTF': 'countFontSize', 'FONT': 'fontSize', 'HEALTHW': 'healthWidth', 'HEALTHH': 'healthBarHeight'}.items()})
for token, value in tokens.items():
    source = re.sub(r'\b' + token + r'\b', str(value) + 'f', source)
cs = RUN / 'PassTwoDump.cs'
cs.write_text(source, encoding='utf-8')
assembly = RUN / 'PassTwoDump.dll'
refs = [line for line in (RUN / 'ManagedPureRunner.rsp').read_text(encoding='utf-8-sig').splitlines() if line.startswith('-r:')]
refs += [f'-r:"{RUN / f}"' for f in ['Worsen.Core.dll', 'Worsen.Presentation.dll', 'UnityEngine.CoreModule.dll']]
rsp = RUN / 'PassTwoDump.rsp'
rsp.write_text('\n'.join(['-nologo', '-nostdlib+', '-target:exe', f'-out:"{assembly}"'] + refs + [f'"{cs}"']), encoding='utf-8')
subprocess.run([str(dotnet), str(data / 'DotNetSdkRoslyn/csc.dll'), '-noconfig', '@' + str(rsp)], check=True)
(RUN / 'PassTwoDump.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net' + runtime.name.split('.')[0] + '.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}), encoding='utf-8')
result = subprocess.run([str(dotnet), str(assembly)], check=True, capture_output=True, text=True)
values = json.loads(result.stdout)
(RUN / 'pass2-data.json').write_text(json.dumps({'held': held, 'hud': hud, **values}, indent=2), encoding='utf-8')
FONT = 'C:/Windows/Fonts/segoeui.ttf'
def font(size):
    return ImageFont.truetype(FONT, max(8, round(size)))

def primitive(part):
    """Generate Unity primitive-sized faces for software inspection."""
    if part['shape'] == 'Cube':
        vertices = [(x, y, z) for z in [-.5, .5] for y in [-.5, .5] for x in [-.5, .5]]
        faces = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]]
    else:
        n = 24
        vertices, faces = [], []
        rings = 12 if part['shape'] == 'Sphere' else 1
        for j in range(rings + 1):
            phi = -math.pi / 2 + math.pi * j / rings
            y = .5 * math.sin(phi) if rings > 1 else (j * 2 - 1)
            radius = .5 * math.cos(phi) if rings > 1 else .5
            vertices += [(radius * math.cos(i * math.tau / n), y, radius * math.sin(i * math.tau / n)) for i in range(n)]
        for j in range(rings):
            for i in range(n):
                faces.append([j*n+i, j*n+(i+1)%n, (j+1)*n+(i+1)%n, (j+1)*n+i])
        if rings == 1:
            faces += [list(range(n)), list(range(n, 2*n))]
    return [[tuple(vertices[i][axis] * part['scale'][axis] + part['position'][axis] for axis in range(3)) for i in face] for face in faces]

def rotate(v):
    x, y, z = v
    rx, ry, rz = [math.radians(a) for a in held['euler']]
    # Unity Euler order Z, X, Y.
    x, y = x*math.cos(rz)-y*math.sin(rz), x*math.sin(rz)+y*math.cos(rz)
    y, z = y*math.cos(rx)-z*math.sin(rx), y*math.sin(rx)+z*math.cos(rx)
    x, z = x*math.cos(ry)+z*math.sin(ry), -x*math.sin(ry)+z*math.cos(ry)
    return x, y, z

def model(image, item_id, position, item_scale=None):
    if not item_id:
        return
    w, h = image.size
    focal = h / (2 * math.tan(math.radians(60) / 2))
    polygons = []
    for part in values['shapes'][item_id]:
        for face in primitive(part):
            verts = [tuple(a * (item_scale or held['scale']) + b for a, b in zip(rotate(v), position)) for v in face]
            if min(v[2] for v in verts) <= .01:
                continue
            pts = [(w/2+v[0]/v[2]*focal, h/2-v[1]/v[2]*focal) for v in verts]
            a, b, c = verts[:3]
            u, v = [b[i]-a[i] for i in range(3)], [c[i]-a[i] for i in range(3)]
            normal = [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]
            length = math.sqrt(sum(t*t for t in normal)) or 1
            shade = .65 + .35 * abs(sum(normal[i]*[.3,.7,-.6][i] for i in range(3))/length)
            color = tuple(round(255*c*shade) for c in held['detailColor' if part['detail'] else 'bodyColor'][:3])
            polygons.append((sum(v[2] for v in verts)/len(verts), pts, color))
    draw = ImageDraw.Draw(image)
    for _, pts, color in sorted(polygons, reverse=True, key=lambda p: p[0]):
        draw.polygon(pts, fill=color)

# Item-specific geometry contact sheet, with the same rotations and scales as runtime.
sheet = Image.new('RGB', (1280, 640), (22, 24, 28))
draw = ImageDraw.Draw(sheet)
for i, item_id in enumerate(values['shapes']):
    tile = Image.new('RGB', (320, 280), (28, 30, 35))
    model(tile, item_id, [0, 0, .45], .20)
    ImageDraw.Draw(tile).text((12, 245), item_id, font=font(19), fill='white')
    sheet.paste(tile, ((i % 4)*320, 55+(i//4)*285))
draw.text((16, 12), 'Compiled primitive silhouettes / software projection, not Unity', font=font(21), fill='white')
sheet.save(RUN / 'held-item-shapes.png')

strip = Image.new('RGB', (1280, 65 + math.ceil(len(values['frames']) / 3) * 252), (18, 20, 24))
for i, frame in enumerate(values['frames']):
    tile = Image.new('RGB', (420, 236), (28, 30, 35))
    if frame['raise'] > 0:
        model(tile, frame['id'], frame['position'])
    td = ImageDraw.Draw(tile)
    td.text((12, 12), frame['caption'], font=font(17), fill='white')
    td.text((12, 36), f"raise={frame['raise']:.2f}", font=font(14), fill=(170,180,185))
    strip.paste(tile, ((i % 3)*426, 65+(i//3)*252))
ImageDraw.Draw(strip).text((16, 14), 'Compiled raise / idle / consumption-lower frames; 60-degree vertical FOV', font=font(21), fill='white')
strip.save(RUN / 'held-item-motion.png')

previews = []
for layout in values['layouts']:
    w, h = layout['dim']
    image = Image.new('RGB', (w, h), (27, 29, 34))
    model(image, 'glass-vial', layout['heldPosition'])
    d = ImageDraw.Draw(image)
    scale = layout['scale']
    def rect(r):
        x, y, rw, rh = [v*scale for v in r]
        return x, y, x+rw, y+rh
    def text(pos, value, size, color=(220,213,192), anchor=None):
        d.text(tuple(v*scale for v in pos), value, font=font(size*scale), fill=color, anchor=anchor)
    # Dark rounded camcorder edge approximates only the safe-area constraint.
    d.rounded_rectangle((10, 10, w-10, h-10), radius=round(h*.07), outline=(8,9,11), width=round(h*.025))
    ix, iy, _, _ = layout['inventory']
    for i, slot in enumerate(layout['slots']):
        x, y, rw, rh = slot['rect']
        color = (255,230,140) if slot['selected'] else (138,132,120)
        d.rectangle(rect([ix+x, iy+y, rw, rh]), fill=(12,13,15), outline=color, width=max(1,round(scale*(3 if slot['selected'] else 1.5))))
        text([ix+x+rw/2, iy+y+8], str(i+1), hud['fontSize'], color, 'mt')
        label = slot['label']
        label_font = font(hud['fontSize']*scale)
        if d.textlength(label, font=label_font) > (rw-8)*scale:
            while label and d.textlength(label+'…', font=label_font) > (rw-8)*scale:
                label = label[:-1]
            label += '…'
        text([ix+x+rw/2, iy+y+36], label, hud['fontSize'], color, 'mt')
    lx = ix + layout['lightLeft']
    d.rectangle(rect([lx, iy, layout['lightWidth'], hud['inventorySlotHeight']]), fill=(12,13,15), outline=(89,230,255), width=max(1,round(scale*2)))
    text([lx+layout['lightWidth']/2, iy+6], layout['FlashlightText'], hud['fontSize'], (89,230,255), 'mt')
    text([lx+layout['lightWidth']/2, iy+29], layout['FlashlightStatusText'], hud['fontSize'], anchor='mt')
    d.rectangle(rect([lx, iy+56, layout['lightWidth'], 4.5]), fill=(89,230,255))
    d.rectangle(rect([lx, iy+62, layout['lightWidth']*.5, 4.5]), fill=(255,230,140))
    text(layout['caption'][:2], layout['SelectedSlotText'], min(hud['smallFontSize'],hud['fontSize']))
    hx, hy, hw, hh = layout['health']
    text([hx, hy], layout['HealthText'], min(hud['smallFontSize'],hud['fontSize']))
    d.rectangle(rect([hx,hy+hh-hud['healthBarHeight'],hw,hud['healthBarHeight']]), fill=(10,11,12))
    d.rectangle(rect([hx,hy+hh-hud['healthBarHeight'],hw*layout['HealthFraction'],hud['healthBarHeight']]), fill=(166,209,153))
    ax, ay, aw, ah = layout['arrow']
    arrow_pts = values['arrowVertices']
    d.polygon([((ax+x*aw)*scale,(ay+y*ah)*scale) for x,y in arrow_pts],fill='white')
    text([ax+aw*.5, ay+ah-layout['countClearance']-hud['countFontSize']], layout['CountText'], hud['countFontSize'], (255,255,255), 'mt')
    d.text((round(w*.4),round(h*.12)),f'{w} x {h} / Expand\nSoftware layout, not Unity',font=font(18*scale),fill=(150,160,170))
    image.save(RUN / f'hud-{w}x{h}.png')
    image.thumbnail((960,540))
    previews.append(image)
contact = Image.new('RGB',(1920,1120),(18,20,24))
for i,image in enumerate(previews):
    contact.paste(image,((i%2)*960,40+(i//2)*540))
ImageDraw.Draw(contact).text((16,8),'Compiled HUD geometry / proportional safe inset and arm-free held vial',font=font(21),fill='white')
contact.save(RUN / 'hud-resolution-sheet.png')
for name in ['held-item-shapes.png','held-item-motion.png','hud-resolution-sheet.png']:
    print(RUN / name)
