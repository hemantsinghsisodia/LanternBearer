# This program is free software; you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation; either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful, but
# WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTIBILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
# General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program. If not, see <http://www.gnu.org/licenses/>.

bl_info = {
    "name" : "Water_Shader_Addon_Free",
    "author" : "chuck cg", 
    "description" : "Water shader material for blender",
    "blender" : (3, 6, 0),
    "version" : (2, 1, 2),
    "location" : "",
    "warning" : "",
    "doc_url": "", 
    "tracker_url": "", 
    "category" : "Material" 
}


import bpy
import bpy.utils.previews
import os
import webbrowser


addon_keymaps = {}
_icons = None
class SNA_PT_WATER_SHADER_34B70(bpy.types.Panel):
    bl_label = 'Water Shader'
    bl_idname = 'SNA_PT_WATER_SHADER_34B70'
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_context = ''
    bl_category = 'WaterShader'
    bl_order = 0
    bl_ui_units_x=0

    @classmethod
    def poll(cls, context):
        return not (False)

    def draw_header(self, context):
        layout = self.layout

    def draw(self, context):
        layout = self.layout
        layout.template_icon(icon_value=_icons['cover.jpg'].icon_id, scale=9.647000312805176)
        row_3ABF1 = layout.row(heading='', align=True)
        row_3ABF1.alert = False
        row_3ABF1.enabled = True
        row_3ABF1.active = True
        row_3ABF1.use_property_split = False
        row_3ABF1.use_property_decorate = False
        row_3ABF1.scale_x = 1.4500000476837158
        row_3ABF1.scale_y = 2.5
        row_3ABF1.alignment = 'Expand'.upper()
        row_3ABF1.operator_context = "INVOKE_DEFAULT" if True else "EXEC_DEFAULT"
        op = row_3ABF1.operator('sna.import_water_cube_47fd5', text='Add Water Cube', icon_value=455, emboss=True, depress=False)
        row_B9E72 = layout.row(heading='terrain gen', align=False)
        row_B9E72.alert = False
        row_B9E72.enabled = True
        row_B9E72.active = True
        row_B9E72.use_property_split = False
        row_B9E72.use_property_decorate = False
        row_B9E72.scale_x = 1.4259999990463257
        row_B9E72.scale_y = 2.299999952316284
        row_B9E72.alignment = 'Expand'.upper()
        row_B9E72.operator_context = "INVOKE_DEFAULT" if True else "EXEC_DEFAULT"
        op = row_B9E72.operator('sna.terrain_generator_7fa03', text='Terrain Generator', icon_value=555, emboss=True, depress=False)


class SNA_OT_Import_Water_Cube_47Fd5(bpy.types.Operator):
    bl_idname = "sna.import_water_cube_47fd5"
    bl_label = "Import Water Cube"
    bl_description = "Adds a cube with the water material"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        if bpy.app.version >= (3, 0, 0) and True:
            cls.poll_message_set('')
        return not False

    def execute(self, context):
        before_data = list(bpy.data.objects)
        bpy.ops.wm.append(directory=os.path.join(os.path.dirname(__file__), 'assets', 'WaterShader4_3.blend') + r'\Object', filename='water_shader', link=False)
        new_data = list(filter(lambda d: not d in before_data, list(bpy.data.objects)))
        appended_C68A7 = None if not new_data else new_data[0]
        return {"FINISHED"}

    def invoke(self, context, event):
        return self.execute(context)


class SNA_OT_Terrain_Generator_7Fa03(bpy.types.Operator):
    bl_idname = "sna.terrain_generator_7fa03"
    bl_label = "terrain generator"
    bl_description = "Adds the procedural terrain generator"
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        if bpy.app.version >= (3, 0, 0) and True:
            cls.poll_message_set('')
        return not False

    def execute(self, context):
        before_data = list(bpy.data.objects)
        bpy.ops.wm.append(directory=os.path.join(os.path.dirname(__file__), 'assets', 'Terrain Generator 1 .blend') + r'\Object', filename='terrain', link=False)
        new_data = list(filter(lambda d: not d in before_data, list(bpy.data.objects)))
        appended_B26D8 = None if not new_data else new_data[0]
        return {"FINISHED"}

    def invoke(self, context, event):
        return self.execute(context)


class SNA_PT_LINKS_E24E0(bpy.types.Panel):
    bl_label = 'Links'
    bl_idname = 'SNA_PT_LINKS_E24E0'
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_context = ''
    bl_category = 'WaterShader'
    bl_order = 0
    bl_ui_units_x=0

    @classmethod
    def poll(cls, context):
        return not (False)

    def draw_header(self, context):
        layout = self.layout

    def draw(self, context):
        layout = self.layout
        op = layout.operator('sna.youtube_8fdb7', text='Tutorials/Youtube', icon_value=0, emboss=True, depress=False)
        op = layout.operator('sna.gumroad_389b9', text='3D Assets', icon_value=0, emboss=True, depress=False)
        layout.label(text='Created by ChuckCG', icon_value=0)


class SNA_OT_Youtube_8Fdb7(bpy.types.Operator):
    bl_idname = "sna.youtube_8fdb7"
    bl_label = "youtube"
    bl_description = ""
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        if bpy.app.version >= (3, 0, 0) and True:
            cls.poll_message_set('')
        return not False

    def execute(self, context):
        webbrowser.open("https://www.youtube.com/@chuckcg")
        return {"FINISHED"}

    def invoke(self, context, event):
        return self.execute(context)


class SNA_OT_Gumroad_389B9(bpy.types.Operator):
    bl_idname = "sna.gumroad_389b9"
    bl_label = "gumroad"
    bl_description = ""
    bl_options = {"REGISTER", "UNDO"}

    @classmethod
    def poll(cls, context):
        if bpy.app.version >= (3, 0, 0) and True:
            cls.poll_message_set('')
        return not False

    def execute(self, context):
        webbrowser.open("https://chuckcg.gumroad.com/")
        return {"FINISHED"}

    def invoke(self, context, event):
        return self.execute(context)


def register():
    global _icons
    _icons = bpy.utils.previews.new()
    bpy.utils.register_class(SNA_PT_WATER_SHADER_34B70)
    if not 'cover.jpg' in _icons: _icons.load('cover.jpg', os.path.join(os.path.dirname(__file__), 'icons', 'cover.jpg'), "IMAGE")
    bpy.utils.register_class(SNA_OT_Import_Water_Cube_47Fd5)
    bpy.utils.register_class(SNA_OT_Terrain_Generator_7Fa03)
    bpy.utils.register_class(SNA_PT_LINKS_E24E0)
    bpy.utils.register_class(SNA_OT_Youtube_8Fdb7)
    bpy.utils.register_class(SNA_OT_Gumroad_389B9)


def unregister():
    global _icons
    bpy.utils.previews.remove(_icons)
    wm = bpy.context.window_manager
    kc = wm.keyconfigs.addon
    for km, kmi in addon_keymaps.values():
        km.keymap_items.remove(kmi)
    addon_keymaps.clear()
    bpy.utils.unregister_class(SNA_PT_WATER_SHADER_34B70)
    bpy.utils.unregister_class(SNA_OT_Import_Water_Cube_47Fd5)
    bpy.utils.unregister_class(SNA_OT_Terrain_Generator_7Fa03)
    bpy.utils.unregister_class(SNA_PT_LINKS_E24E0)
    bpy.utils.unregister_class(SNA_OT_Youtube_8Fdb7)
    bpy.utils.unregister_class(SNA_OT_Gumroad_389B9)
