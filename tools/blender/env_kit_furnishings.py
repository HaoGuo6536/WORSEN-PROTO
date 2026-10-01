# ============================================================================
# env_kit_furnishings.py
# PURPOSE:
#   Add original domestic and service furniture to the four current theme kits.
#   These are measured, bottom-centred pieces, not room-sized decorative meshes;
#   the theme generators export them using their existing material/axis contracts.
# ARCHITECTURAL ROLE: Offline art generator · Environment; no Unity runtime layer.
# KEY RESPONSIBILITIES:
#   - Build recognisable, theme-specific furniture from original geometry.
#   - Adapt Unity-metre coordinates to each existing theme mesh accumulator.
#   - Declare floor versus wall support and the common local +Z furniture back.
# DEPENDENCIES: Existing theme mesh builders; Python standard library only.
# USAGE NOTES:
#   Called by env_theme_*.main; do not run the obsolete v1 combined kit builders.
#   Furniture fits a 2m planning bay; detail dimensions are authored, not snapping.
#   No shrine model is made here: niche panels only frame another owner's model.
# ============================================================================

PIECES = {
    'hospital': ('radiator', 'side_table', 'visitor_chair', 'linen_shelf',
                 'curtain_screen', 'washer', 'reception_desk', 'niche_panel'),
    'school': ('radiator', 'staff_table', 'coat_rack', 'cloak_bench',
               'piano', 'mat_stack', 'lectern', 'niche_panel'),
    'basement': ('washer', 'workbench', 'stores_shelf', 'toolboard',
                 'pipe_riser', 'service_locker', 'laundry_cart', 'niche_panel'),
    'castle': ('bookcase', 'pew', 'hearth', 'kitchen_dresser',
               'sleeping_cot', 'lectern', 'sideboard', 'niche_panel'),
}
WALL_MOUNTED = {'toolboard'}
BACK_TO_WALL = {'radiator', 'linen_shelf', 'washer', 'coat_rack', 'piano',
                'stores_shelf', 'toolboard', 'pipe_riser', 'service_locker',
                'bookcase', 'hearth', 'kitchen_dresser', 'sleeping_cot',
                'sideboard', 'niche_panel', 'reception_desk'}


def ids(theme):
    return ['prop_room_'+name for name in PIECES[theme]]


class FurnitureMesh:
    """Small coordinate adapter; preserves the theme's own mesh/material implementation."""
    def __init__(self, theme, mesh):
        self.theme, self.mesh = theme, mesh
        self.z_up = theme in ('castle', 'hospital')
        self.surfaces = {
            'hospital': dict(body='paint', edge='stainless', dark='rubber', soft='curtain',
                             accent='tile', paper='linen', wood='vinyl'),
            'school': dict(body='teal', edge='steel', dark='rubber', soft='mustard',
                           accent='fire_red', paper='paper', wood='wood'),
            'basement': dict(body='galvanised', edge='steel', dark='damp', soft='insulation',
                             accent='rust', paper='hazard', wood='insulation'),
            'castle': dict(body='wood', edge='metal', dark='soot', soft='stone_dark',
                           accent='stone', paper='mortar', wood='wood'),
        }[theme]

    def point(self, p):
        return (p[0], -p[2], p[1]) if self.z_up else p

    def box(self, p, size, surface):
        if self.z_up:
            size = (size[0], size[2], size[1])
        self.mesh.box(self.point(p), size, self.surfaces[surface])

    def rod(self, a, b, radius, surface, sides=8):
        method = self.mesh.tube if self.theme == 'hospital' else self.mesh.rod
        method(self.point(a), self.point(b), radius, self.surfaces[surface], sides)

    def legs(self, width, depth, top, surface='edge'):
        for x in (-width/2+.08, width/2-.08):
            for z in (-depth/2+.08, depth/2-.08):
                self.box((x, top/2, z), (.06, top, .06), surface)

    def shelf(self, width, depth, height, books=False):
        for x in (-width/2+.035, width/2-.035):
            self.box((x, height/2, 0), (.07, height, depth), 'body')
        self.box((0, height/2, depth/2-.015), (width-.14, height, .03), 'body')
        for row, y in enumerate((.08, .65, 1.22, 1.79)):
            self.box((0, y, 0), (width-.14, .06, depth), 'wood')
            if books:
                for i in range(9):
                    h = .30+.035*((row+i)%3)
                    x = -width/2+.18+i*(width-.36)/9
                    self.box((x, y+.03+h/2, -.02), (.10, h, depth*.64),
                             ('dark', 'paper', 'soft')[i%3])
            else:
                for x in (-width*.24, width*.24):
                    self.box((x, y+.17, -.015), (width*.35, .28, depth*.78), 'soft')
                    self.box((x, y+.20, -depth*.40), (.20, .07, .008), 'paper')


def build_furnishing(theme, builder, piece):
    m = FurnitureMesh(theme, builder)
    n = piece.removeprefix('prop_room_')
    if n in ('linen_shelf', 'stores_shelf', 'bookcase'):
        m.shelf(1.8, .48 if n != 'stores_shelf' else .70, 2.2, n == 'bookcase')
    elif n == 'radiator':
        for x in (-.67, .67):
            m.box((x, .08, 0), (.1, .16, .22), 'edge')
        for i in range(12):
            m.box((-.715+i*.13, .5, 0), (.09, .78, .18), 'body')
        for y in (.19, .81):
            m.rod((-.79, y, .055), (.79, y, .055), .035, 'edge')
        m.rod((.78, .74, 0), (.90, .74, 0), .025, 'edge')
        m.rod((.9, .70, 0), (.9, .81, 0), .05, 'dark')
    elif n in ('side_table', 'staff_table', 'workbench', 'reception_desk'):
        w, d, h = {'side_table':(.6,.55,.62), 'staff_table':(1.8,.9,.76),
                  'workbench':(1.9,.8,.92), 'reception_desk':(1.96,.75,1.10)}[n]
        m.box((0, h-.045, 0), (w, .09, d), 'wood')
        m.legs(w, d, h-.09)
        if n == 'side_table':
            m.box((0, .40, 0), (.49, .20, .47), 'body')
            m.box((0, .40, -.247), (.17, .025, .025), 'edge')
        elif n == 'workbench':
            m.box((0, .23, 0), (w-.20, .07, d-.15), 'accent')
            for x in (-.65, -.1, .45):
                m.box((x, .4, 0), (.42, .27, .5), 'dark')
            m.box((-.65, h+.08, -.14), (.3, .16, .25), 'edge')
            m.rod((-.93,h+.11,-.14),(-.4,h+.11,-.14),.025,'body')
        elif n == 'reception_desk':
            m.box((0,.56,-.29),(1.8,.92,.07),'accent')
            for x in (-.56,0,.56):
                m.box((x,.6,-.333),(.48,.72,.02),'body')
    elif n == 'visitor_chair':
        m.legs(.52,.54,.45)
        m.box((0,.47,0),(.52,.10,.54),'soft')
        m.box((0,.76,.235),(.52,.50,.07),'soft')
        for x in (-.285,.285):
            m.box((x,.64,0),(.055,.05,.52),'wood')
            m.rod((x,.45,.20),(x,.63,.20),.023,'edge')
    elif n == 'curtain_screen':
        # A folded privacy screen: not an opaque collision box surrounding a bed.
        for x in (-.57,.57):
            m.box((x,.025,0),(.14,.05,.34),'edge')
            m.rod((x,.05,0),(x,1.85,0),.018,'edge')
        m.rod((-.57,1.84,0),(.57,1.84,0),.02,'edge')
        for i in range(12):
            m.box((-.50+i*.09,1.0,.018 if i%2 else -.018),(.095,1.55,.025),'soft')
    elif n == 'washer':
        m.box((0,.06,0),(.80,.12,.72),'edge')
        m.box((0,.53,0),(.86,.94,.78),'body')
        m.box((0,1.02,0),(.88,.06,.80),'soft')
        m.rod((0,.51,-.398),(0,.51,-.448),.29,'edge',16)
        m.rod((0,.51,-.45),(0,.51,-.457),.22,'dark',16)
        m.box((0,.90,-.399),(.72,.14,.018),'dark')
        for x in (-.24,.24):
            m.rod((x,.9,-.413),(x,.9,-.443),.042,'paper')
    elif n == 'coat_rack':
        m.box((0,.99,.19),(1.94,1.98,.05),'wood')
        for x in (-.85,.85):
            m.box((x,.1,0),(.1,.2,.43),'edge')
        m.box((0,1.95,0),(1.98,.06,.44),'body')
        for x in (-.72,-.24,.24,.72):
            m.rod((x,1.58,.15),(x,1.58,-.15),.025,'edge')
            m.rod((x,1.58,-.15),(x,1.66,-.15),.025,'edge')
        for x in (-.72,.24):
            m.box((x,1.15,-.02),(.32,.80,.10),'soft')
    elif n in ('cloak_bench', 'pew'):
        w = 1.8
        m.legs(w,.48,.43,'wood')
        m.box((0,.47,0),(w,.08,.48),'wood')
        if n == 'pew':
            m.box((0,.80,.21),(w,.60,.09),'wood')
            for x in (-.89,.89):
                m.box((x,.57,0),(.10,1.14,.58),'body')
                m.box((x,1.16,0),(.14,.05,.62),'wood')
            m.box((0,.14,-.13),(1.7,.045,.27),'wood')
    elif n == 'piano':
        m.box((0,.55,.11),(1.60,1.10,.45),'wood')
        m.box((0,.68,-.20),(1.65,.15,.43),'dark')
        for x in (-.72,.72):
            m.box((x,.32,-.31),(.09,.64,.10),'wood')
        for i in range(24):
            m.box((-.69+i*.06,.763,-.29),(.056,.015,.22),'paper')
            if i%7 not in (2,6):
                m.box((-.66+i*.06,.780,-.23),(.024,.03,.11),'dark')
        m.box((0,1.14,.09),(1.7,.07,.52),'wood')
    elif n == 'mat_stack':
        m.box((0,.025,0),(1.92,.05,1.18),'wood')
        for i in range(4):
            m.box((0,.12+i*.16,0),(1.9,.15,1.16),'soft' if i%2 else 'body')
            for x in (-.65,.65):
                m.box((x,.11+i*.16,-.584),(.18,.035,.015),'paper')
    elif n == 'lectern':
        m.box((0,.04,0),(.66,.08,.56),'body')
        m.box((0,.65,.10),(.18,1.22,.18),'wood')
        m.box((0,1.25,0),(.78,.08,.55),'wood')
        m.box((0,1.32,-.18),(.64,.06,.08),'edge')
        m.box((0,1.308,.02),(.48,.036,.34),'paper')
    elif n == 'toolboard':
        m.box((0,.55,0),(1.8,1.1,.06),'body')
        for i in range(7):
            x=-.70+i*.23
            m.box((x,.55,-.07),(.035,.48,.045),'edge')
            m.box((x,.80,-.07),(.16,.07,.055),'accent')
        m.box((0,.04,-.09),(1.8,.06,.23),'edge')
    elif n == 'pipe_riser':
        m.box((0,.025,0),(.55,.05,.42),'edge')
        for x in (-.16,.16):
            m.rod((x,.05,0),(x,2.85,0),.10,'accent',12)
            for y in (.3,1.4,2.6):
                m.rod((x,y-.035,0),(x,y+.035,0),.145,'edge',12)
        for y in (.3,1.4,2.6):
            m.box((0,y,.13),(.49,.07,.16),'edge')
    elif n == 'service_locker':
        m.box((0,1.0,0),(1.35,2,.5),'body')
        for x in (-.44,0,.44):
            m.box((x,1,-.255),(.415,1.87,.02),'accent')
            m.box((x+.12,1.0,-.28),(.025,.19,.03),'edge')
            for y in (1.5,1.58,1.66):
                m.box((x,y,-.27),(.24,.022,.012),'dark')
    elif n == 'laundry_cart':
        for x in (-.42,.42):
            for z in (-.29,.29):
                m.rod((x-.025,.08,z),(x+.025,.08,z),.08,'edge')
        m.box((0,.52,0),(.92,.74,.70),'body')
        m.box((0,.90,0),(.85,.04,.63),'soft')
        for x in (-.42,.42):
            m.rod((x,.75,.29),(x,1.13,.29),.023,'edge')
        m.rod((-.42,1.13,.29),(.42,1.13,.29),.023,'edge')
    elif n == 'hearth':
        for x in (-.71,.71):
            m.box((x,.88,0),(.28,1.76,.65),'accent')
        m.box((0,1.76,0),(1.86,.22,.77),'soft')
        m.box((0,.06,0),(1.86,.12,.82),'accent')
        m.box((0,.74,.27),(1.14,1.36,.10),'dark')
        for x in (-.32,.32):
            m.box((x,.24,-.10),(.55,.17,.22),'wood')
        for x in (-.4,0,.4):
            m.rod((x,.12,-.29),(x,.41,-.29),.018,'edge')
    elif n in ('kitchen_dresser','sideboard'):
        h= .90
        m.box((0,.44,0),(1.8,.88,.66),'body')
        m.box((0,.92,0),(1.94,.08,.74),'wood')
        for x in (-.44,.44):
            m.box((x,.47,-.342),(.79,.71,.025),'wood')
            m.rod((x,.52,-.36),(x,.52,-.39),.035,'edge')
        if n == 'kitchen_dresser':
            for x in (-.87,.87):
                m.box((x,1.45,.25),(.10,1.05,.16),'wood')
            for y in (1.17,1.91):
                m.box((0,y,.17),(1.84,.07,.32),'wood')
            for x in (-.6,-.2,.2,.6):
                m.rod((x,1.21,.15),(x,1.44,.15),.12,'soft',8)
    elif n == 'sleeping_cot':
        m.legs(.95,1.98,.40,'wood')
        m.box((0,.4,0),(.96,.14,1.99),'wood')
        m.box((0,.53,0),(.88,.12,1.93),'paper')
        m.box((0,.63,.66),(.70,.08,.42),'soft')
        m.box((0,.78,.96),(.98,.72,.08),'wood')
        m.box((0,.56,-.96),(.98,.38,.08),'wood')
    elif n == 'niche_panel':
        # Thin backboard, deliberately no pedestal or fake shrine in the socket.
        m.box((0,1.42,.01),(1.78,2.84,.08),'soft')
        for x in (-.9,.9):
            m.box((x,1.46,-.025),(.12,2.92,.16),'accent')
        for y in (.09,2.84):
            m.box((0,y,-.035),(1.72,.10,.18),'edge')
        if theme=='castle':
            for x in (-.5,0,.5):
                m.box((x,2.59,-.05),(.10,.28,.08),'accent')
        elif theme=='hospital':
            m.box((0,2.5,-.05),(.42,.035,.012),'paper')
        elif theme=='school':
            for x in (-.5,0,.5):
                m.box((x,2.52,-.05),(.24,.16,.012),'paper')
        else:
            for x in (-.62,.62):
                m.box((x,2.5,-.05),(.18,.18,.015),'paper')
    else:
        raise ValueError((theme, piece))
