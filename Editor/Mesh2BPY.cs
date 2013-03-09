/*
** 2012 September 25
**
** The author disclaims copyright to this source code.  In place of
** a legal notice, here is a blessing:
**    May you do good and not evil.
**    May you find forgiveness for yourself and forgive others.
**    May you share freely, never taking more than you give.
**/

using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

public class Mesh2BPY {
	
	[MenuItem("Custom/Export/Skinned mesh to Blender script")]
	public static void ExportMesh() {
   		SkinnedMeshRenderer meshRenderer = GetSelectedMesh();
		
		if (meshRenderer == null) {
			EditorUtility.DisplayDialog("Error", "Please select a skinned mesh renderer you wish to export.", "Ok");
			return;
		}

		string scriptName = meshRenderer.name + ".py";
		string scriptPath = EditorUtility.SaveFilePanel("Export to Blender script", "", scriptName, "py");
		
		if (scriptPath.Length == 0) {
			return;
		}
		
		StreamWriter w = new StreamWriter(scriptPath, false);
			
		using (w) {
			Mesh2BPY m2bpy = new Mesh2BPY(meshRenderer, w);
			m2bpy.WriteHeader();
			m2bpy.WriteMeshBuilder();
			m2bpy.WriteSkeletonBuilder();
			m2bpy.WriteSkinBuilder();
			m2bpy.WriteModelBuilder();
			m2bpy.WriteFooter();
		}
		
		EditorUtility.DisplayDialog("Mesh2BPY", "Finished writing " + scriptName, "Ok");
	}
	
	public static SkinnedMeshRenderer GetSelectedMesh() {
		Transform[] selection = Selection.GetTransforms(SelectionMode.Unfiltered);
		
		if (selection.Length == 0) {
			return null;
		}
		
		Transform meshTransform = selection[0];
		
   		return (SkinnedMeshRenderer) meshTransform.GetComponentInChildren(typeof(SkinnedMeshRenderer));
	}
	
	private const string Version = "0.6";
	private const int LineBreakLimit = 256;
	private const string S = "    "; // indent space
	
	private string modelName;
	private string scriptName;
	private string scriptPath;
	private StreamWriter w;
	
	private SkinnedMeshRenderer meshRenderer;
	private Mesh mesh;
	private Vector3[] verts;
	private int[] tris;
	private Vector2[] uvs;
	private Material[] mats;
	private Transform[] bones;
	private Transform rootBone;
	private BoneWeight[] boneWeights;
	private HashSet<Transform> registeredBones;
	private HashSet<Transform> visibleBones;
	private HashSet<Transform> looseBones;
	
	public Mesh2BPY(SkinnedMeshRenderer mr, StreamWriter sw) {
		meshRenderer = mr;
		mesh = meshRenderer.sharedMesh;
		verts = mesh.vertices;
		tris = mesh.triangles;
		uvs = mesh.uv;
		mats = meshRenderer.sharedMaterials;
		bones = meshRenderer.bones;
		boneWeights = mesh.boneWeights;
		modelName = meshRenderer.name;
		w = sw;
	}
	
	public void WriteHeader() {
		w.WriteLine("# Blender model build script for \"" + modelName + "\"");
		w.WriteLine("# Written by Mesh2BPY " + Version);
		w.WriteLine("# Verts:      " + verts.Length);
		w.WriteLine("# Tris:       " + tris.Length);
		w.WriteLine("# Bones:      " + bones.Length);
		w.WriteLine("# Materials:  " + mats.Length);
		w.WriteLine("# Sub meshes: " + mesh.subMeshCount);
		w.WriteLine();
		w.WriteLine("import bpy, mathutils, collections");
		w.WriteLine("from bpy_extras.io_utils import unpack_list, unpack_face_list");
		w.WriteLine("from bpy_extras.image_utils import load_image");
		w.WriteLine();
		w.WriteLine("model_name = '" + modelName + "'");
		w.WriteLine();
	}
	
	public void WriteMeshBuilder() {
		w.WriteLine("def buildMesh(verts, faces_map, uv):");
		w.WriteLine(S + "print('Building mesh')");
		
		Vector3 pos = meshRenderer.transform.position;
		Vector3 rot = meshRenderer.transform.eulerAngles;
		w.WriteLine(S + "# Create mesh and object");
		w.WriteLine(S + "me = bpy.data.meshes.new(model_name + '_mesh')");
		w.WriteLine(S + "ob = bpy.data.objects.new(model_name + '_mesh', me)");
		w.WriteLine(S + "ob.location = (" + -pos.x + "," + -pos.z + "," + pos.y + ")");
		w.WriteLine(S + "ob.rotation_euler = (" + rot.x * Mathf.Deg2Rad + "," + rot.y * Mathf.Deg2Rad + "," + rot.z * Mathf.Deg2Rad + ")");
		w.WriteLine();
		
		w.WriteLine(S + "# Link object to scene");
		w.WriteLine(S + "scn = bpy.context.scene");
		w.WriteLine(S + "scn.objects.link(ob)");
		w.WriteLine(S + "scn.objects.active = ob");
		w.WriteLine(S + "scn.update()");
		w.WriteLine();
		
		w.WriteLine(S + "# Load raw vertices");
		w.WriteLine(S + "me.vertices.add(len(verts))");
		w.WriteLine(S + "me.vertices.foreach_set(\"co\", unpack_list(verts))");
		w.WriteLine();
		w.WriteLine(S + "# Create materials and collect face indices");
		w.WriteLine(S + "faces_all = []");
		w.WriteLine(S + "for name, faces in faces_map.items():");		
		w.WriteLine(S + S + "material = bpy.data.materials.get(name)");
		w.WriteLine(S + S + "tex_name = name + '_diffuse'");
		w.WriteLine(S + S + "tex_file = tex_name + '.jpg'");
		w.WriteLine(S + S + "if material == None:");
		w.WriteLine(S + S + S + "material = bpy.data.materials.new(name)");

		w.WriteLine(S + S + S + "material.use_nodes = True");
		//w.WriteLine(S + S + S + "ntree = material.node_tree");
		//w.WriteLine(S + S + S + "nlinks = ntree.links");
		
		//w.WriteLine(S + S + S + "ndif = ntree.nodes.new('BSDF_DIFFUSE')");
		//w.WriteLine(S + S + S + "ndif.location = 0,470");
		//w.WriteLine(S + S + S + "nmatout = ntree.nodes.new('OUTPUT_MATERIAL')");
		//w.WriteLine(S + S + S + "nmatout.location = 200,400");
		//w.WriteLine(S + S + S + "nlinks.new(ndif.outputs[0], nmatout.inputs[0])");
		
		//w.WriteLine(S + S + S + "image = load_image(tex_file, './textures/' + model_name + '/', place_holder=True)");
		//w.WriteLine(S + S + S + "texture = bpy.data.textures.new(tex_name, 'IMAGE')");
		//w.WriteLine(S + S + S + "texture.image = image");
		//w.WriteLine(S + S + S + "slot = material.texture_slots.add()");
		//w.WriteLine(S + S + S + "slot.texture = texture");
		//w.WriteLine(S + S + S + "slot.texture_coords = 'UV'");
		//w.WriteLine(S + S + S + "slot.uv_layer = model_name + '_uv'");
		//w.WriteLine(S + S + S + "slot.use_map_color_diffuse = True");
		w.WriteLine();
		w.WriteLine(S + S + "me.materials.append(material)");
		w.WriteLine(S + S + "faces_all += faces");
		w.WriteLine();
		w.WriteLine(S + "# Load raw face indices");
		w.WriteLine(S + "me.tessfaces.add(len(faces_all))");
		w.WriteLine(S + "me.tessfaces.foreach_set(\"vertices_raw\", unpack_list(faces_all))");
		w.WriteLine(S + "me.tessfaces.foreach_set(\"use_smooth\", [True] * len(me.tessfaces))");
		w.WriteLine();
		w.WriteLine(S + "# Load UV");
		w.WriteLine(S + "uvtex = me.tessface_uv_textures.new(model_name + '_uv')");
		w.WriteLine(S + "for n, tf in enumerate(uv):");
		w.WriteLine(S + S + "texdata = uvtex.data[n]");
		w.WriteLine(S + S + "texdata.uv1 = tf[0]");
		w.WriteLine(S + S + "texdata.uv2 = tf[1]");
		w.WriteLine(S + S + "texdata.uv3 = tf[2]");
		w.WriteLine();
		w.WriteLine(S + "# Set face materials according to the mapping");
		w.WriteLine(S + "mat_index = 0");
		w.WriteLine(S + "face_index = 0");
		w.WriteLine(S + "for name, faces in faces_map.items():");
		w.WriteLine(S + S + "for i, face in enumerate(faces):");
		w.WriteLine(S + S + S + "me.tessfaces[i + face_index].material_index = mat_index");
		w.WriteLine(S + S + "mat_index += 1");
		w.WriteLine(S + S + "face_index += len(faces)");
		w.WriteLine();
		w.WriteLine(S + "# Update mesh with new data");
		w.WriteLine(S + "me.update(calc_edges=True)");
		w.WriteLine();
		w.WriteLine(S + "return ob");
		w.WriteLine();
	}
	
	public void WriteSkeletonBuilder() {
		w.WriteLine("def buildArmature():");
		w.WriteLine(S + "print('Building armature')");
		w.WriteLine(S + "# Create armature and object");
		w.WriteLine(S + "amt = bpy.data.armatures.new(model_name + '_armature')");
		w.WriteLine(S + "amt.show_names = True");
		w.WriteLine();
		
		w.WriteLine(S + "rig = bpy.data.objects.new(model_name + '_rig', amt)");
		w.WriteLine(S + "rig.show_x_ray = True");
		w.WriteLine(S + "rig.draw_type = 'WIRE'");
		w.WriteLine();
		
		w.WriteLine(S + "# Link object to scene");
		w.WriteLine(S + "scn = bpy.context.scene");
		w.WriteLine(S + "scn.objects.link(rig)");
		w.WriteLine(S + "scn.objects.active = rig");
		w.WriteLine(S + "scn.update()");
		w.WriteLine();
		
		// write bones hierarchically starting from the root bone
		registeredBones = new HashSet<Transform>(bones);
		visibleBones = new HashSet<Transform>();
		looseBones = new HashSet<Transform>(registeredBones);

		rootBone = FindRootBone(meshRenderer.transform.parent);
		
		if (rootBone != null) {
			rootBone = rootBone.parent;
		}
		
		if (rootBone == null) {
			EditorUtility.DisplayDialog("Warning", "Root bone not found! Skipped writing of skeleton data.", "Ok");
			w.WriteLine(S + "return rig");
			return;
		}
		
		// write bone info
		w.WriteLine(S + "# Bone hierarchy:");
		WriteBoneTree(rootBone, "", true);
		w.WriteLine(S + "#");
		
		w.WriteLine(S + "# Registered bones: " + registeredBones.Count);
		foreach (Transform bone in registeredBones) {
			w.WriteLine(S + "#  " + bone.name);
		}
		w.WriteLine(S + "#");
		
		looseBones.ExceptWith(visibleBones);
		
		if (looseBones.Count > 0) {
			w.WriteLine(S + "# Loose bones: " + looseBones.Count);
			foreach (Transform bone in looseBones) {
				if (bone.parent != null) {
					w.WriteLine(S + "#  " + bone.name + " (parent: " + bone.parent.name + ")");
				} else {
					w.WriteLine(S + "#  " + bone.name);
				}
			}
			w.WriteLine();
		}
		
		// write bone data
		w.WriteLine(S + "bpy.ops.object.mode_set(mode='EDIT')");
		w.WriteLine();
		w.WriteLine(S + "# Create bones");

		WriteBone(rootBone.parent, rootBone, null);
		
		w.WriteLine();
		w.WriteLine(S + "bpy.ops.object.mode_set(mode='OBJECT')");
		w.WriteLine();
		
		w.WriteLine(S + "return rig");
	}
	
	private void WritePoint(Transform point) {
		Vector3 hp = point.localPosition;
		Vector3 dir = point.localEulerAngles;
		string vname = FixBoneName(point.name);
		w.WriteLine(S + vname + " = bpy.data.objects.new('" + vname + "', None)");
		w.WriteLine(S + vname + ".empty_draw_type = 'ARROWS'");
		w.WriteLine(S + vname + ".empty_draw_size = 0.1");
		w.WriteLine(S + vname + ".location = (" + hp.x + "," + hp.y + "," + hp.z + ")");
		w.WriteLine(S + vname + ".rotation_euler = (" + dir.x * Mathf.Deg2Rad + "," + dir.y * Mathf.Deg2Rad + "," + dir.z * Mathf.Deg2Rad + ")");
		
		if (point.parent != null) {
			w.WriteLine(S + vname + ".parent = " + FixBoneName(point.parent.name));
		}
		
		w.WriteLine();
		w.WriteLine(S + "scn = bpy.context.scene");
		w.WriteLine(S + "scn.objects.link(" + vname + ")");
		w.WriteLine(S + "scn.update()");
		
		foreach (Transform child in point) {
			WritePoint(child);
		}
	}

	private Transform FindRootBone(Transform parent) {
		if (parent == null) {
			return null;
		}
		
		// find the root bone that usually isn't part of SkinnedMeshRenderer.bones
		foreach (Transform child in parent) {
			Transform bone = null;
			
			if (registeredBones.Contains(child)) {
				bone = parent;
			} else {
				bone = FindRootBone(child);
			}
			
			if (bone != null) {
				return bone;
			}
		}
		
		return null;
	}
	
	private void WriteBoneTree(Transform bone, String indent, bool last) {
		visibleBones.Add(bone);
		
		w.Write(S + "# " + indent);
		
		if (last) {
		   w.Write("\\-");
		   indent += "  ";
		} else {
		   w.Write("|-");
		   indent += "| ";
		}
		
		w.WriteLine(bone.name);
		
		for (int i = 0; i < bone.childCount; i++) {
			WriteBoneTree(bone.GetChild(i), indent, i == bone.childCount - 1);
		}
	}
	
	public void WriteBone(Transform head, Transform tail, Transform parent) {
		WriteBoneRaw(head.name, parent != null ? parent.name : null, head.position, tail.position);
		
		if (tail.childCount == 0) {
			Vector3 dir;
			float dist = Math.Abs((tail.position - head.position).magnitude);
			
			// check the distance so the head isn't at the same location as the tail
			// FIXME: in Wild Skies, transforms starting with "hp" seem to have
			// incorrect rotations, use the direction of the previous bone instead
			// as a workaround
			if (dist > 0.1f && tail.name.StartsWith("hp", true, null)) {
				dir = (tail.position - head.position).normalized * 0.1f;
			} else {
				dir = tail.rotation * new Vector3(-1, 0, 0) * 0.2f;
			}
			
			WriteBoneEnd(tail, head, dir);
		} else {
			// write each child bone from the tail
			foreach (Transform child in tail) {
				WriteBone(tail, child, head);
			}
		}
	}
	
	public void WriteBoneEnd(Transform bone, Transform parent, Vector3 dir) {
		Vector3 hp = bone.position;
		Vector3 tp = bone.position + dir;
		WriteBoneRaw(bone.name, parent != null ? parent.name : null, hp, tp);
	}
	
	private void WriteBoneRaw(string name, string parentName, Vector3 hp, Vector3 tp) {
		string vname = FixBoneName(name);
		w.WriteLine(S + vname + " = amt.edit_bones.new('" + name + "')");
		w.WriteLine(S + vname + ".head = (" + -hp.x + "," + -hp.z + "," + hp.y + ")");
		w.WriteLine(S + vname + ".tail = (" + -tp.x + "," + -tp.z + "," + tp.y + ")");
		
		if (parentName != null) {
			string vparent = FixBoneName(parentName);
			w.WriteLine(S + vname + ".parent = " + vparent);
			w.WriteLine(S + vname + ".use_connect = True");
		}
		
		w.WriteLine();
	}
	
	private string FixBoneName(string boneName) {
		return Regex.Replace(boneName, "[^a-zA-Z0-9_]", "_");
	}
	
	public void WriteSkinBuilder() {
		w.WriteLine("def buildSkin(ob, rig, vgroups):");
		w.WriteLine(S + "print('Skinning mesh')");
		
		w.WriteLine(S + "# Create vertex groups, and add verts and weights");
		w.WriteLine(S + "# First arg in assignment is a list, can assign several verts at once");
		w.WriteLine(S + "for name, vgroup in vgroups.items():");
		w.WriteLine(S + S + "grp = ob.vertex_groups.new(name)");
		w.WriteLine(S + S + "for (v, w) in vgroup:");
		w.WriteLine(S + S + S +  "grp.add([v], w, 'REPLACE')");
		w.WriteLine();
		w.WriteLine(S + "# Give mesh object an armature modifier, using vertex groups but");
		w.WriteLine(S + "# not envelopes");
		w.WriteLine(S + "mod = ob.modifiers.new('Armature', 'ARMATURE')");
		w.WriteLine(S + "mod.object = rig");
		w.WriteLine(S + "mod.use_bone_envelopes = False");
		w.WriteLine(S + "mod.use_vertex_groups = True");
		w.WriteLine();
	}
	
	public void WriteModelBuilder() {
		w.WriteLine("def build():");
		w.WriteLine(S + "print('Building model ' + model_name)");
		w.WriteLine(S + "# List of vertex coordinates");
		w.WriteLine(S + "verts = [");
		w.Write(S + S);
		
		for (int i = 0; i < verts.Length; i++) {
			//Vector3 vert = meshRenderer.transform.TransformPoint(verts[i]);
			Vector3 vert = verts[i];
			
			//This is sort of ugly - inverting x-component since we're in
			//a different coordinate system than "everyone" is "used to".
			w.Write("(" + -vert.x + "," + -vert.z + "," + vert.y + "),");
			
			WriteAutoLineBreak(i + 1);
		}
		
		w.WriteLine();
		w.WriteLine(S + "]");
		w.WriteLine();

		w.WriteLine(S + "# Map of materials and faces");
		w.WriteLine(S + "faces = collections.OrderedDict()");
		
		for (int i = 0; i < mesh.subMeshCount; i++) {
			Material material = mats[i];
			String matName = material == null ? "null" : material.name;
			
			w.WriteLine(S + "faces['" + matName + "'] = [");
			w.Write(S + S);
			
			int[] triangles = mesh.GetTriangles(i);
			
			for (int j = 0, n = 1; j < triangles.Length; j += 3, n++) {
				w.Write("(" + triangles[j] + "," + triangles[j + 1] + "," + triangles[j + 2] + ",0),");

				WriteAutoLineBreak(n);
			}
			
			w.WriteLine();
			w.WriteLine(S + "]");
			w.WriteLine();
		}
		
		w.WriteLine();
		
		w.WriteLine(S + "# List of texture face UVs");
		w.WriteLine(S + "uv = [");
		w.Write(S + S);
		
		for (int i = 0, n = 1; i < tris.Length; i += 3, n++) {
			w.Write("[");
			w.Write("(" + uvs[tris[i]].x + "," + uvs[tris[i]].y + "),");
			w.Write("(" + uvs[tris[i + 1]].x + "," + uvs[tris[i + 1]].y + "),");
			w.Write("(" + uvs[tris[i + 2]].x + "," + uvs[tris[i + 2]].y + ")");
			w.Write("],");
			
			WriteAutoLineBreak(n);
		}
		
		w.WriteLine();
		w.WriteLine(S + "]");
		w.WriteLine();
		
		w.WriteLine(S + "# List of vertex groups, in the form (vertex, weight)");
		w.WriteLine(S + "vg = collections.OrderedDict()");
		
		for (int i = 0; i < bones.Length; i++) {
			List<Dictionary<int, float>> vweightList = new List<Dictionary<int, float>>();
			
			for (int j = 0; j < boneWeights.Length; j++) {
				Dictionary<int, float> vweight = new Dictionary<int, float>();
				
				if (boneWeights[j].boneIndex0 == i && boneWeights[j].weight0 != 0) {
					vweight.Add(j, boneWeights[j].weight0);
				}
				
				if (boneWeights[j].boneIndex1 == i && boneWeights[j].weight1 != 0) {
					vweight.Add(j, boneWeights[j].weight1);
				}
				
				if (boneWeights[j].boneIndex2 == i && boneWeights[j].weight2 != 0) {
					vweight.Add(j, boneWeights[j].weight2);
				}
				
				if (boneWeights[j].boneIndex3 == i && boneWeights[j].weight3 != 0) {
					vweight.Add(j, boneWeights[j].weight3);
				}
				
				if (vweight.Count > 0) {
					vweightList.Add(vweight);
				}
			}
			
			if (vweightList.Count > 0) {
				w.Write(S + "vg['" + bones[i].name + "'] = [");
				
				foreach (Dictionary<int, float> vweight in vweightList) {
					foreach(KeyValuePair<int, float> entry in vweight) {
						w.Write("(" + entry.Key + "," + entry.Value + "),");
					}
				}
				
				w.WriteLine("]");
			} else {
				w.WriteLine(S + "# No weights for bone " + bones[i].name);
			}
		}
		
		w.WriteLine();
		
		w.WriteLine(S + "origin = bpy.data.objects.new(model_name, None)");
		w.WriteLine(S + "mesh = buildMesh(verts, faces, uv)");
		w.WriteLine(S + "rig = buildArmature()");
		w.WriteLine(S + "buildSkin(mesh, rig, vg)");
		w.WriteLine();
		w.WriteLine(S + "# Connect objects");
		w.WriteLine(S + "mesh.parent = origin");
		w.WriteLine(S + "rig.parent = origin");
		w.WriteLine();
		w.WriteLine(S + "scn = bpy.context.scene");
		w.WriteLine(S + "scn.objects.link(origin)");
		w.WriteLine(S + "scn.objects.active = origin");
		w.WriteLine(S + "scn.update()");
		w.WriteLine();

		w.WriteLine();
	}
	
	public void WriteFooter() {
		w.WriteLine("if __name__ == \"__main__\":");
		w.WriteLine(S + "build()");
	}
	
	private void WriteAutoLineBreak(int n) {
		if (n % LineBreakLimit == 0) {
			w.WriteLine();
			w.Write(S + S);
		}
	}
}
