# ============================================================================
# env_theme_room_layouts.py
# PURPOSE:
#   Describe useful rooms rather than scatter decorative props through a square.
#   Each recipe composes real furniture groups around reserved circulation, with
#   named transition seams and singular shrine/puzzle allocations for each biome.
# ARCHITECTURAL ROLE: Offline art authoring data · Environment; no runtime layer.
# KEY RESPONSIBILITIES:
#   - Author ten functionally distinct room recipes per building style.
#   - Declare chair/desk, ward, pew and service-equipment relationships.
#   - Specify shrine facing, interaction space, Passage pocket and puzzle lane data.
# DEPENDENCIES: FurnishedRoom builder passed by env_theme_furnished; Python stdlib.
# USAGE NOTES:
#   Metres, Unity XYZ. Coordinates are design decisions, not procedural scatter.
#   No shrine placeholder is exported. Puzzle sockets match the existing 8m lane.
# ============================================================================


def dining(r, table, seat, x, z, long=False):
    table_i=r.put(table,x,z)
    depth=r.rows[table]['size'][2]/2
    seats=[]
    for sign in (-1,1):
        yaw=180 if sign<0 else 0
        seats.append(r.put(seat,x,z+sign*(depth+.40),yaw))
    r.group('seating',seats,target=table_i)


def desk(r,x,z,board):
    table=r.put('prop_desk',x,z,180)
    chair=r.put('prop_chair',x,z-.72,180)
    r.group('desk-row',[table,chair],target=board,facing=[0,0,1])


def hospital(create):
    result=[]
    r=create('ward_long',12,10,doors=[((2,0),'S'),((2,4),'N')])
    beds=[]
    for side in ('W','E'):
        for z in (2.2,5.0,7.8):
            beds.append(r.wall('prop_bed',side,z))
            # Retracted screens sit BETWEEN beds; no giant collision cage around them.
            if z<7:
                r.put('prop_room_curtain_screen',1.60 if side=='W' else 10.40,z+1.32,90)
    r.group('ward-beds',beds)
    r.wall('prop_room_linen_shelf','S',9)
    result.append(r.finish())

    cells={(x,z) for x in range(5) for z in range(5) if x<3 or z<3}
    r=create('nurses_office',10,10,cells=cells)
    for x in (2,4):
        i=r.wall('prop_room_reception_desk','N',x)
        r.group('nurse-workstation',[i])
    r.wall('prop_cabinet','W',7)
    r.wall('prop_room_linen_shelf','E',7,line=6)
    r.wall('prop_waiting_bench','S',7)
    result.append(r.finish())

    r=create('surgery_suite',10,8)
    i=r.put('prop_gurney',6,4.8,90)
    r.group('operating-table',[i])
    r.put('prop_iv_stand',6.1,6.15)
    r.wall('prop_scrub_sink','N',2)
    r.wall('prop_cabinet','N',8)
    r.wall('prop_room_linen_shelf','W',5.8)
    r.wall('prop_xray_screen','E',6.5)
    result.append(r.finish())

    r=create('family_waiting',8,8)
    for z in (2.2,5.2): r.wall('prop_waiting_bench','W',z)
    for x in (2,4.5): r.wall('prop_waiting_bench','N',x)
    dining(r,'prop_room_side_table','prop_room_visitor_chair',5.6,5)
    r.wall('prop_room_radiator','S',6.3)
    result.append(r.finish())

    r=create('sluice_laundry',8,6)
    for x in (1,2.3): r.wall('prop_room_washer','N',x)
    r.wall('prop_scrub_sink','N',4.2)
    r.wall('prop_room_linen_shelf','N',6.4)
    r.wall('prop_cabinet','W',2.5)
    result.append(r.finish())

    r=create('gurney_gallery',6,14,doors=[((1,0),'S'),((1,6),'N')])
    for z in (3,7,11): r.wall('prop_gurney','W',z, line=0)
    for z in (4,9): r.wall('prop_waiting_bench','E',z)
    r.wall('prop_cabinet','E',12)
    result.append(r.finish())
    return result


def school(create):
    result=[]
    r=create('classroom_double',12,10)
    board=r.wall('chalk_rail_board_2m','N',6,1.30)
    for x in (1.6,4.1,6.6,9.1):
        for z in (4.6,6.3,8.0): desk(r,x,z,board)
    r.wall('prop_teacher_desk','N',9.5)
    for z in (2,5): r.wall('prop_room_radiator','W',z)
    result.append(r.finish())

    r=create('reading_library',10,10)
    for x in (1.3,3.5,5.7,7.9): r.wall('prop_bookcase','N',x)
    for z in (2,4.4,6.8): r.wall('prop_bookcase','W',z)
    for x in (3.2,6.6):
        for z in (3.8,6.8):
            dining(r,'prop_room_staff_table','prop_chair',x,z)
    result.append(r.finish())

    r=create('refectory',12,12)
    for x in (2.0,5.8,9.6):
        for z in (6,9.3):
            i=r.put('prop_cafeteria_table',x,z)
            r.group('integrated-dining',[i])
    r.wall('drinking_fountain','W',2)
    r.wall('prop_room_radiator','N',5)
    result.append(r.finish())

    r=create('staff_common_room',8,8)
    dining(r,'prop_room_staff_table','prop_chair',4.9,5.0)
    r.wall('prop_bookcase','N',2)
    r.wall('prop_room_piano','W',4.8)
    r.wall('prop_room_radiator','S',6.4)
    r.wall('bulletin_board','N',5.4,1.4)
    result.append(r.finish())

    r=create('gym_equipment_hall',12,14)
    for x in (2,4.2,6.4,8.6): r.wall('prop_bleacher','N',x)
    for z in (6,9): r.wall('prop_room_mat_stack','E',z)
    r.wall('prop_basketball_hoop','W',8,2.2)
    r.wall('prop_room_cloak_bench','W',4.5)
    result.append(r.finish())

    r=create('cloakroom',8,10,doors=[((1,0),'S'),((1,4),'N')])
    for side in ('W','E'):
        for z in (2.4,5.0,7.6):
            r.wall('prop_room_coat_rack' if z==5 else 'locker_bank_2m',side,z)
    for z in (4,7): r.put('prop_room_cloak_bench',4,z,90)
    result.append(r.finish())
    return result


def basement(create):
    result=[]
    r=create('twin_boilers',12,10)
    for x in (2.1,5.4,8.7):
        boiler=r.wall('prop_boiler','N',x)
        pump=r.put('prop_pump',x,6.4)
        r.group('boiler-train',[boiler,pump],facing=[0,0,-1])
    r.wall('prop_electrical_cabinet','W',5)
    r.wall('prop_room_pipe_riser','E',7.5)
    result.append(r.finish())

    r=create('pump_gallery',10,8)
    for x in (2,4.5,7):
        r.wall('prop_pump','N',x)
        r.wall('prop_gauge_panel','N',x,1.3)
    for z in (2.2,5): r.wall('prop_room_pipe_riser','W',z)
    r.wall('prop_room_service_locker','E',6)
    result.append(r.finish())

    r=create('caged_stores',10,10)
    for z in (2,4.5,7): r.wall('prop_storage_cage','W',z)
    for x in (2.2,4.6,7): r.wall('prop_room_stores_shelf','N',x)
    r.wall('prop_room_service_locker','E',6.5)
    result.append(r.finish())

    r=create('laundry',10,8)
    for x in (1.1,2.4,3.7,5): r.wall('prop_room_washer','N',x)
    r.wall('prop_room_stores_shelf','N',7.8)
    r.put('prop_room_laundry_cart',7.8,5.5)
    r.wall('prop_room_workbench','W',4.6)
    result.append(r.finish())

    r=create('coal_store',8,8)
    for x in (1.3,3.6,5.9): r.wall('prop_fuel_bunker','N',x)
    for z in (2,4.7): r.wall('prop_room_pipe_riser','W',z)
    r.wall('prop_room_service_locker','E',6.4)
    result.append(r.finish())

    r=create('repair_workshop',10,10)
    for x in (2,4.5,7):
        bench=r.wall('prop_room_workbench','N',x)
        tools=r.wall('prop_room_toolboard','N',x,1.30)
        r.group('workbench-tools',[bench,tools])
    for z in (2.5,5.2): r.wall('prop_room_stores_shelf','W',z)
    r.wall('prop_room_service_locker','E',7)
    result.append(r.finish())
    return result


def castle(create):
    result=[]
    r=create('household_refectory',12,14)
    for x in (2.4,8.8):
        for z in (5.8,9.8): dining(r,'prop_trestle_table','prop_bench',x,z)
    r.wall('prop_room_sideboard','N',5.6)
    r.wall('prop_room_hearth','W',2)
    result.append(r.finish())

    r=create('household_kitchen',10,10)
    for x in (2,4.6): r.wall('prop_room_kitchen_dresser','N',x)
    r.wall('prop_room_hearth','N',7.8)
    r.wall('prop_room_sideboard','W',6)
    i=r.put('prop_trestle_table',5.6,5.5)
    r.group('preparation-table',[i])
    for z in (1.5,3.0): r.put('prop_barrel',.65,z)
    result.append(r.finish())

    r=create('armoury_long',10,8)
    for x in (1.4,3.7,6,8.3): r.wall('prop_weapon_rack','N',x)
    for z in (2,4.5): r.wall('prop_weapon_rack','W',z)
    r.put('prop_bench',5.5,5)
    r.wall('prop_room_sideboard','E',6)
    result.append(r.finish())

    r=create('scriptorium',10,10)
    for x in (1.3,3.5,5.7,7.9): r.wall('prop_room_bookcase','N',x)
    for z in (2,4.5,7): r.wall('prop_room_bookcase','W',z)
    for x in (3.3,6.9):
        for z in (3.8,6.8): dining(r,'prop_trestle_table','prop_bench',x,z)
    result.append(r.finish())

    r=create('guard_mess',8,8)
    dining(r,'prop_trestle_table','prop_bench',4.7,4.8)
    for x in (1.3,3): r.wall('prop_room_sleeping_cot','N',x)
    r.wall('prop_weapon_rack','E',6.5)
    result.append(r.finish())

    r=create('pew_chapel',10,12)
    altar=r.wall('prop_altar','N',5)
    pews=[]
    for x in (2.1,7.6):
        for z in (5.5,7.5,9.5): pews.append(r.put('prop_room_pew',x,z,180))
    r.group('pews',pews,target=altar,facing=[0,0,1])
    r.wall('prop_room_lectern','W',2.5)
    result.append(r.finish())
    return result


TRANSITIONS = {
    'castle': ('service_gate','infirmary_bend','prop_room_sideboard','prop_bench'),
    'hospital': ('admissions_lobby','service_airlock','prop_room_linen_shelf','prop_waiting_bench'),
    'school': ('administration_lobby','plant_annex','prop_room_coat_rack','prop_room_cloak_bench'),
    'basement': ('utility_lobby','loading_bend','prop_room_service_locker','prop_room_workbench'),
}


def transition_rooms(theme,create):
    names=TRANSITIONS[theme]
    result=[]
    for index in (0,1):
        cells=None if index==0 else {(x,z) for x in range(4) for z in range(4) if x<2 or z<2}
        doors=[((1,0),'S'),((3,1),'E')] if index==0 else [((1,0),'S'),((0,2),'W')]
        r=create(names[index],8,6 if index==0 else 8,cells=cells,doors=doors)
        if index==0:
            r.wall(names[2],'N',2)
            r.wall(names[3],'N',5.3)
        else:
            r.wall(names[2],'N',2)
            r.wall(names[3],'S',6.3)
        r.t['transition']={'purpose':'service-to-public threshold' if index==0 else 'older wing / plant annex',
                          'compatibleThemes':[s for s in TRANSITIONS if s!=theme],
                          'doors':[dict(index=i,clearWidth=3.2,clearHeight=2.8,floorY=0,
                                        seam='retain own jamb; neighbour owns its shell height') for i in range(len(doors))]}
        result.append(r.finish())
    return result


def shrine_room(theme,create):
    name={'castle':'reliquary_nook','hospital':'ward_memorial','school':'remembrance_room','basement':'boiler_shrine_nook'}[theme]
    r=create(name,10,10,kind='shrine')
    r.wall('prop_room_niche_panel','W',7)
    # One model socket, facing the declared Passage edge. No duplicate shrine prop.
    r.t['shrineSockets']=[dict(id='shrine',position=[1.8,0,7],facing=[1,0,0],
                              modelEnvelope=[1.2,2.4,1.2],interactionCenter=[3.6,0,7],
                              interactionSize=[2,2.4,2],modelOwner='Assets/Art/Shrine/<Kind>/')]
    r.reserve('shrine-model',[1.8,1.2,7],[1.2,2.4,1.2])
    r.reserve('shrine-interaction',[3.6,1.2,7],[2,2.4,2])
    r.reserve('passage-approach',[6.5,1,7],[7,2,2.4])
    r.t['passageGap']=dict(side='E',edgeCell=[4,3],edge=[10,0,7],landing=[14,0,7],
                          width=2.4,gapLength=4,sealedUntilActivated=True,
                          pocketTemplateId=theme+'_'+TRANSITIONS[theme][0],
                          pocketOffset=[7,2],pocketTurns=0,optionalOnly=True)
    if theme=='castle':
        for x in (2,5,8): r.put('prop_room_pew',x,4.5,90)
        r.wall('prop_room_bookcase','N',5)
    elif theme=='hospital':
        r.wall('prop_room_reception_desk','N',5)
        for x in (2,5): r.put('prop_waiting_bench',x,4.4)
        r.wall('prop_room_radiator','N',8)
    elif theme=='school':
        r.wall('bulletin_board','N',5,1.4)
        for x in (2,5): r.put('prop_room_cloak_bench',x,4.5)
        r.wall('prop_bookcase','N',8)
    else:
        r.wall('prop_boiler','N',7.5)
        r.wall('prop_room_service_locker','N',4.5)
        r.wall('prop_room_workbench','W',3)
    return r.finish()


def puzzle_room(theme,create):
    name={'castle':'trial_gallery','hospital':'rehabilitation_lane','school':'movement_hall','basement':'pressure_lane'}[theme]
    r=create(name,10,14,kind='puzzle',doors=[((1,0),'S'),((1,6),'N')])
    r.t.update(gimmick='puzzle',minRound=3,weight=.65)
    r.t['puzzleSockets']=dict(origin=[7,0,7],axis=[0,0,1],laneLength=8,laneWidth=1.6,
                             cageHeight=2.5,panelThickness=.1,clearance=.8,
                             steps=[[7,0,4],[7,0,6],[7,0,8]],reward=[7,0,10],
                             supportedKinds=['OrderedPlates','DimmingPath','MovingDoor','TimedVaults'])
    r.reserve('puzzle-envelope',[7,1.35,7],[3.2,2.5,9.6])
    if theme=='castle':
        for z in (3,6,9,12): r.wall('prop_weapon_rack','W',z)
        r.wall('prop_room_bookcase','N',7)
    elif theme=='hospital':
        for z in (3,6,9,12): r.wall('prop_waiting_bench','W',z)
        r.wall('prop_cabinet','N',7)
    elif theme=='school':
        for z in (3,6,9,12): r.wall('locker_bank_2m','W',z)
        r.wall('chalk_rail_board_2m','N',7,1.4)
    else:
        for z in (3,6,9,12): r.wall('prop_room_pipe_riser','W',z)
        r.wall('prop_gauge_panel','N',7,1.4)
    return r.finish()


def layout_rooms(theme,create):
    result={'castle':castle,'hospital':hospital,'school':school,'basement':basement}[theme](create)
    result+=transition_rooms(theme,create)
    result.append(shrine_room(theme,create))
    result.append(puzzle_room(theme,create))
    return result
