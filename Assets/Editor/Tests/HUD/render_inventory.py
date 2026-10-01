"""Render a pure-layout contact sheet from the compiled HUD presenter, never Unity.

Run from the worktree: python Assets/Editor/Tests/HUD/render_inventory.py <pure-run>
Uses the installed compiler/runtime and dependencies copied by Run-PureTests.
Generated program, JSON and PNG stay in that ignored offline evidence directory.
This is layout evidence, not a UI Toolkit or in-game screenshot.
"""
import json
import pathlib
import re
import subprocess
import sys
from PIL import Image, ImageDraw, ImageFont

root = pathlib.Path(__file__).resolve().parents[4]
run = root / 'Logs/AgentValidation/PLAN-002/offline-compile' / sys.argv[1]
summary = json.loads((run / 'summary.json').read_text(encoding='utf-8-sig'))
assert summary['ExitCode'] == 0
version = re.search(r'm_EditorVersion: (\S+)', (root / 'ProjectSettings/ProjectVersion.txt').read_text()).group(1)
data = pathlib.Path(f'C:/Program Files/Unity/Hub/Editor/{version}/Editor/Data')
dotnet = data / 'NetCoreRuntime/dotnet.exe'
runtime = max((data / 'NetCoreRuntime/shared/Microsoft.NETCore.App').iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
config = (root / 'Assets/Scripts/Presentation/HUD/Config/HUDDriverConfig.cs').read_text()
names = ['inventorySlotWidth', 'inventorySlotHeight', 'inventorySlotGap', 'selectedSlotScale', 'unselectedSlotOpacity', 'flashlightSlotGap', 'flashlightSlotWidth', 'readyPulseSeconds']
values = {n: float(re.search(r'_' + n + r' = ([0-9.]+)f;', config).group(1)) for n in names}
colors = {}
for name in ['panelColor', 'textColor', 'mutedColor', 'selectionColor', 'flashlightColor']:
    colors[name] = [float(s.strip().rstrip('f')) for s in re.search(r'_' + name + r' = new Color\(([^)]+)\)', config).group(1).split(',')]
source = '''using System;
using System.Text.Json;
using Worsen.Core;
using Worsen.Presentation.HUD;
class LayoutDump {
 static void Main() {
  var p = new HUDInventoryPresenter();
  var cases = new object[5];
  for(int row=0; row<5; row++) {
   var s = new HUDDriverState();
   p.SetSlots(s,new ConsumableInventorySnapshot(new[]{new ProgressionInventorySlot("gauze","Gauze",0),default(ProgressionInventorySlot),new ProgressionInventorySlot("oil-flask","Oil flask",0)},new[]{1,0,1},row%3));
   p.SetFlashlight(s,row!=0,row==1?.4f:1f,row==3?.65f:0f);
   if(row==2) p.Tick(s,PULSE/2f,PULSE);
   if(row==4) new HUDPresenter().SetChaseMode(s,true);
   var rects=new object[3];
   for(int i=0;i<3;i++) {
    bool selected=i==s.SelectedDisplaySlot;
    var r=p.SlotRect(i,selected,WIDTH,HEIGHT,GAP,SCALE);
    rects[i]=new {x=r.x,y=r.y,w=r.width,h=r.height,label=s.SlotLabels[i],selected,opacity=p.SlotOpacity(selected,DIM)};
   }
   cases[row]=new {rects,left=p.FlashlightLeft(WIDTH,GAP,SEPARATION),width=LIGHTWIDTH,height=HEIGHT,s.FlashlightText,s.FlashlightStatusText,s.FlashlightCharge,s.FlashlightAim,s.SelectedSlotText,s.ChromeVisible,pulse=p.ReadyPulse(s)};
  }
  Console.WriteLine(JsonSerializer.Serialize(cases));
 }
}'''
for token, name in {'WIDTH':'inventorySlotWidth','HEIGHT':'inventorySlotHeight','GAP':'inventorySlotGap','SCALE':'selectedSlotScale','DIM':'unselectedSlotOpacity','SEPARATION':'flashlightSlotGap','LIGHTWIDTH':'flashlightSlotWidth','PULSE':'readyPulseSeconds'}.items():
    source = re.sub(r'\b' + token + r'\b', str(values[name]) + 'f', source)
cs = run / 'InventoryLayoutDump.cs'
cs.write_text(source, encoding='utf-8')
assembly = run / 'InventoryLayoutDump.dll'
refs = [line for line in (run / 'ManagedPureRunner.rsp').read_text(encoding='utf-8-sig').splitlines() if line.startswith('-r:')]
refs += [f'-r:"{run / f}"' for f in ['Worsen.Core.dll','Worsen.Presentation.dll','UnityEngine.CoreModule.dll']]
args = ['-nologo','-nostdlib+','-target:exe',f'-out:"{assembly}"'] + refs + [f'"{cs}"']
rsp = run / 'InventoryLayoutDump.rsp'
rsp.write_text('\n'.join(args), encoding='utf-8')
subprocess.run([str(dotnet), str(data / 'DotNetSdkRoslyn/csc.dll'), '-noconfig', '@' + str(rsp)], check=True)
(run / 'InventoryLayoutDump.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net' + runtime.name.split('.')[0] + '.0','framework': {'name':'Microsoft.NETCore.App','version':runtime.name}}}), encoding='utf-8')
result = subprocess.run([str(dotnet), str(assembly)], check=True, capture_output=True, text=True)
layout = json.loads(result.stdout)
(run / 'inventory-layout.json').write_text(json.dumps({'defaults':values,'colors':colors,'cases':layout},indent=2),encoding='utf-8')

image = Image.new('RGB',(1280,1050),(18,19,23))
draw = ImageDraw.Draw(image)
font_path = 'C:/Windows/Fonts/segoeui.ttf'
font = ImageFont.truetype(font_path, 18)
small = ImageFont.truetype(font_path, 15)
heading = ImageFont.truetype(font_path, 22)
def tint(name, opacity=1):
    return tuple(round((18 + (v*255-18)*opacity)) for v in colors[name][:3])

draw.text((28,20),'Inventory: compiled pure-layout contact sheet',font=heading,fill='white')
draw.text((28,52),'Not a Unity render. Rectangles, labels, aim/charge and pulse are actual Presenter outputs.',font=small,fill=(175,179,189))
for row, case in enumerate(layout):
    y = 140 + row*177
    draw.text((28,y-38),['OFF / charged','ON / recharging','ON / ready pulse low','ON / held face aim','CHASE / chrome suppressed'][row],font=small,fill=(183,187,198))
    if case['ChromeVisible']:
        for i,r in enumerate(case['rects']):
            x = 30 + r['x']
            top = y + r['y']
            color = tint('selectionColor' if r['selected'] else 'mutedColor',r['opacity'])
            draw.rectangle((x,top,x+r['w'],top+r['h']),fill=tint('panelColor'),outline=color,width=3 if r['selected'] else 1)
            draw.text((x+r['w']/2,top+9),str(i+1)+(' SELECTED' if r['selected'] else ''),anchor='mt',font=small,fill=color)
            draw.text((x+r['w']/2,top+38),r['label'],anchor='mt',font=font,fill=tint('textColor',r['opacity']))
        x = 30 + case['left']
        draw.rectangle((x,y,x+case['width'],y+case['height']),fill=tint('panelColor'),outline=tint('flashlightColor'),width=round(1.5*(1+case['pulse'])))
        draw.text((x+case['width']/2,y+6),case['FlashlightText'],anchor='mt',font=font,fill=tint('flashlightColor'))
        draw.text((x+case['width']/2,y+29),case['FlashlightStatusText'],anchor='mt',font=font,fill=tint('textColor'))
        if case['FlashlightCharge']:
            draw.rectangle((x,y+56,x+case['width']*case['FlashlightCharge'],y+60),fill=tint('flashlightColor'))
        if case['FlashlightAim']:
            draw.rectangle((x,y+62,x+case['width']*case['FlashlightAim'],y+66),fill=tint('selectionColor'))
        draw.text((30,y+90),case['SelectedSlotText'],font=font,fill=tint('textColor'))
        draw.text((680,y+10),'3 physical slots, no compaction\nFlashlight is separate, never selectable',font=font,fill=(164,169,181))
    else:
        draw.text((680,y+10),'Inventory and flashlight hidden;\nobjective guidance remains independent.',font=font,fill=(164,169,181))
# Enlarged source geometry is unnecessary: keep pixel scale close to the UI defaults.
path = run / 'inventory-layout.png'
image.save(path)
print(path)
