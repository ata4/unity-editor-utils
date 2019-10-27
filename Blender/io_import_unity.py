bl_info = {
   "name":          "Unity Model Script Importer",
   "author":        "ata4",
   "version":       (0, 2, 0),
   "blender":       (2, 6, 0),
   "location":      "File > Import-Export",
   "description":   "Imports Unity model scripts exported by the Mesh2BPY script",
   "warning":       "",
   "support":       "COMMUNITY",
   "category":      "Import-Export",
 }

import bpy, bmesh, math, re

# ImportHelper is a helper class, defines filename and
# invoke() function which calls the file selector.
from bpy_extras.io_utils import ImportHelper
from bpy.props import StringProperty, BoolProperty, EnumProperty
from bpy.types import Operator
from mathutils import Vector, Quaternion

class ImportPythonModel(Operator, ImportHelper):
    """This appears in the tooltip of the operator and in the generated docs"""
    bl_idname = "import_mesh.unity"
    bl_label = "Import Unity model script"

    # ImportHelper mixin class uses this
    filename_ext = ".py"
    
    def __init__(self):
        self.model = None

    def execute(self, context):
        # compile and execute provided script
        script = open(self.filepath).read()
        script_c = compile(script, self.filepath, 'exec')
        globals = {}
        
        exec(script_c, globals)
        
        self.model = globals.get('model')
        
        # test if the "model" variable is defined
        if not self.model:
            print("No model variable found!")
            return {'CANCELLED'}
            
        # get rid of "mesh" in the model name
        pattern = re.compile("mesh", re.IGNORECASE)
        self.model['name'] = pattern.sub("", self.model['name'])
        
        # build model
        self.build_model()

        return {'FINISHED'}
        
    def build_model(self):
        print("Building %s" % self.model['name'])
        
        # create empty
        ob = bpy.data.objects.new(self.model['name'], None)
        ob.rotation_euler = (math.radians(90), 0, 0)

        # build mesh
        me = self.build_geometry()

        # create mesh object
        ob_mesh = bpy.data.objects.new(self.model['name'] + " Mesh", me)
        ob_mesh.location = self.model['pos']
        ob_mesh.rotation_quaternion = self.model['rot']
        #ob_mesh.scale = self.model['scl']
        ob_mesh.parent = ob
        
        # create armature
        amt = bpy.data.armatures.new(self.model['name'])
        amt.show_names = True
        
        # create armature object
        ob_amt = bpy.data.objects.new(self.model['name'] + " Armature", amt)
        ob_amt.show_in_front = True
        ob_amt.display_type = 'WIRE'
        ob_amt.parent = ob
        
        # Give mesh object an armature modifier, using vertex groups but
        # not envelopes
        mod = ob_mesh.modifiers.new('Armature', 'ARMATURE')
        mod.object = ob_amt
        mod.use_bone_envelopes = False
        mod.use_vertex_groups = True
        
        # link objects to scene
        bpy.context.collection.objects.link(ob)
        bpy.context.collection.objects.link(ob_mesh)
        bpy.context.collection.objects.link(ob_amt)
        
        # build armature
        bpy.context.view_layer.objects.active = ob_amt
        self.build_armature(ob_mesh, ob_amt)
        
    def build_geometry(self):
        # create mesh data and BMesh
    
        me = bpy.data.meshes.new(self.model['name'])
        bm = bmesh.new()
        
        
    
        # create vertices
        for vert in self.model['verts']:
            bm.verts.new(vert)         
        
        # to avoid slow iterator lookups later / indexing verts is slow in bmesh
        bm_verts = bm.verts[:]
        
        # set of indices of duplicate faces
        dupfaces = set()
        
        for submesh_index, submesh in enumerate(self.model['submeshes']):
            # get name of material
            mat_name = self.model['materials'][submesh_index]
        
            # create and append material
            mtl = bpy.data.materials.get(mat_name)
            if not mtl:
                mtl = bpy.data.materials.new(name = mat_name)
            me.materials.append(mtl)
            
            # create faces
            for face_index, face in enumerate(zip(submesh[0::3], submesh[1::3], submesh[2::3])):
                try:
                    bm_face = bm.faces.new((bm_verts[i] for i in face))
                    bm_face.smooth = True
                    bm_face.material_index = submesh_index
                except ValueError as e:
                    # duplicate face, save id for later
                    print("Duplicate face: %d" % face_index)
                    dupfaces.add(face_index)
                
        # create uv layers
        uv_lay = bm.loops.layers.uv.verify()
        face_index_ofs = 0
        if hasattr(bm.verts, "ensure_lookup_table"): 
            bm.verts.ensure_lookup_table()
            bm.faces.ensure_lookup_table()
        for face_index, face_uv in enumerate(self.model['uv']):
            # skip duplicate faces and correct face index
            if face_index in dupfaces:
                face_index_ofs = face_index_ofs - 1
                continue
        
            for loop_index, loop_uv in enumerate(face_uv):
                bm.faces[face_index + face_index_ofs].loops[loop_index][uv_lay].uv = loop_uv
        
        # export BMesh to mesh data
        bm.to_mesh(me)
        me.update()
        
        return me
        
    def build_armature(self, ob_mesh, ob_amt):
        bpy.ops.object.mode_set(mode='EDIT')
    
        # create vertex groups, and add verts and weights
        # first arg in assignment is a list, can assign several verts at once
        for name, vgroup in self.model['vg'].items():
            grp = ob_mesh.vertex_groups.new(name=name)
            for (v, w) in vgroup:
                grp.add([v], w, 'REPLACE')
        
        # first pass: create and position bones
        for bone_name, bone_data in self.model['bones'].items():
            scale = 1 / self.get_bone_scale(bone_name, 1)
            bone = ob_amt.data.edit_bones.new(bone_name)
            bone.head = bone_data['pos']
            bone.tail = bone.head + Vector((0, scale * -0.1, 0))
            bone.roll = 0

        # second pass: build bone hierarchy
        for bone_name, bone_data in self.model['bones'].items():
            bone = ob_amt.data.edit_bones.get(bone_name)
            bone_parent_name = bone_data.get('parent')
            if bone and bone_parent_name:
                bone_parent = ob_amt.data.edit_bones.get(bone_parent_name)
                if bone_parent:
                    bone.parent = bone_parent
                    
        bpy.ops.object.mode_set(mode='OBJECT')
        
        # create custom shape for bones
        bone_shape = bpy.data.objects.get("bone_shape")
        if not bone_shape:
            bone_shape = bpy.ops.object.empty_add(type='SPHERE')
            bone_shape = bpy.context.active_object
            bone_shape.name = "bone_shape"
            bone_shape.use_fake_user = True
            #bone_shape.empty_draw_size = 0.2
            bpy.context.collection.objects.unlink(bone_shape) # don't want the user deleting this
            bpy.context.view_layer.objects.active = ob_amt
            
        bpy.ops.object.mode_set(mode='POSE')
            
        # apply custom shape
        for bone in ob_amt.pose.bones:
            bone.custom_shape = bone_shape
            
        # third pass: apply bone scales
        for bone_name, bone_data in self.model['bones'].items():
            bone = ob_amt.pose.bones.get(bone_name)
            bone.scale = bone_data['lscl']
            
        bpy.ops.object.mode_set(mode='OBJECT')
        
    def get_bone_scale(self, bone_name, scale):
        bone_data = self.model['bones'][bone_name]
        scale *= (bone_data['lscl'][0] + bone_data['lscl'][1] + bone_data['lscl'][2]) / 3
        
        bone_parent_name = bone_data.get('parent')
        if bone_parent_name:
            scale = self.get_bone_scale(bone_parent_name, scale)
        
        return scale
                

def menu_func_import(self, context):
    self.layout.operator(ImportPythonModel.bl_idname, text="Unity Python model script (.py)")

def register():
    bpy.utils.register_class(ImportPythonModel)
    bpy.types.TOPBAR_MT_file_import.append(menu_func_import)

def unregister():
    bpy.utils.unregister_class(ImportPythonModel)
    bpy.types.TOPBAR_MT_file_import.remove(menu_func_import)

if __name__ == "__main__":
    register()

    # test call
    #bpy.ops.import_mesh.unity('INVOKE_DEFAULT')
