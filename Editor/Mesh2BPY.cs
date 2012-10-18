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

public class Mesh2BPY
{
	[MenuItem("Custom/Export/Skinned mesh to Blender script")]
	public static void ExportMesh()
	{
   		SkinnedMeshRenderer meshRenderer = GetSelectedMesh();
		
		if (meshRenderer == null)
		{
			EditorUtility.DisplayDialog("Error", "Please select a skinned mesh renderer you wish to export.", "Ok");
			return;
		}

		string scriptName = meshRenderer.name + ".py";
		string scriptPath = EditorUtility.SaveFilePanel("Export to Blender script", "", scriptName, "py");
		
		using (StreamWriter w = new StreamWriter(scriptPath, false))
		{
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
	
	public static SkinnedMeshRenderer GetSelectedMesh()
	{
		Transform[] selection = Selection.GetTransforms(SelectionMode.Unfiltered);
		
		if (selection.Length == 0)
			return null;
		
		Transform meshTransform = selection[0];
		
   		return (SkinnedMeshRenderer) meshTransform.GetComponentInChildren(typeof(SkinnedMeshRenderer));
	}
	
	private const string Version = "0.5";
	private const int LineBreakLimit = 256;
	private const string S = "    "; // indent space
	
	private string meshName;
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
	private HashSet<Transform> bonesSet;
	
	public Mesh2BPY(SkinnedMeshRenderer mr, StreamWriter sw)
	{
		meshRenderer = mr;
		mesh = meshRenderer.sharedMesh;
		verts = mesh.vertices;
		tris = mesh.triangles;
		uvs = mesh.uv;
		mats = meshRenderer.sharedMaterials;
		bones = meshRenderer.bones;
		boneWeights = mesh.boneWeights;
		meshName = meshRenderer.name;
		w = sw;
	}
	
	public void WriteHeader()
	{
		w.WriteLine("# Blender model build script for \"" + meshName + "\"");
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
	}
	
	public void WriteMeshBuilder()
	{
		w.WriteLine("def buildMesh(verts, faces_map, uv):");
		w.WriteLine(S + "print('Building mesh')");
		w.WriteLine(S + "# Create mesh and object");
		w.WriteLine(S + "me = bpy.data.meshes.new('" + meshName + "_mesh')");
		w.WriteLine(S + "ob = bpy.data.objects.new('" + meshName + "', me)");
		Vector3 pos = meshRenderer.transform.position;
		w.WriteLine(S + "ob.location = (" + -pos.x + "," + -pos.z + "," + pos.y + ")");
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
		w.WriteLine(S + S + S + "image = load_image(tex_file, './textures/', place_holder=True)");
		w.WriteLine(S + S + S + "texture = bpy.data.textures.new(tex_name, 'IMAGE')");
		w.WriteLine(S + S + S + "texture.image = image");
		w.WriteLine(S + S + S + "slot = material.texture_slots.add()");
		w.WriteLine(S + S + S + "slot.texture = texture");
		w.WriteLine(S + S + S + "slot.texture_coords = 'UV'");
		w.WriteLine(S + S + S + "slot.uv_layer = '" + meshName + "_uv'");
		w.WriteLine(S + S + S + "slot.use_map_color_diffuse = True");
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
		w.WriteLine(S + "uvtex = me.tessface_uv_textures.new('" + meshName + "_uv')");
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
	
	public void WriteSkeletonBuilder()
	{
		w.WriteLine("def buildArmature():");
		w.WriteLine(S + "print('Building armature')");
		w.WriteLine(S + "# Create armature and object");
		w.WriteLine(S + "amt = bpy.data.armatures.new('" + meshName + "_armature')");
		w.WriteLine(S + "amt.show_names = True");
		w.WriteLine();
		w.WriteLine(S + "rig = bpy.data.objects.new('" + meshName + "_rig', amt)");
		w.WriteLine(S + "rig.show_x_ray = True");
		w.WriteLine(S + "rig.draw_type = 'WIRE'");
		w.WriteLine();
		w.WriteLine(S + "# Link object to scene");
		w.WriteLine(S + "scn = bpy.context.scene");
		w.WriteLine(S + "scn.objects.link(rig)");
		w.WriteLine(S + "scn.objects.active = rig");
		w.WriteLine(S + "scn.update()");
		w.WriteLine(S + "rig.select = True");
		w.WriteLine();
		
		// write bones hierarchically starting from the root bone
		bonesSet = new HashSet<Transform>(bones);
		rootBone = FindRootBone(meshRenderer.transform.parent);
		
		if (rootBone != null)
		{
			w.WriteLine(S + "bpy.ops.object.mode_set(mode='EDIT')");
			w.WriteLine();
			w.WriteLine(S + "# Create bones");
			
			WriteBone(rootBone.parent, rootBone, null);
			
			w.WriteLine();
			w.WriteLine(S + "bpy.ops.object.mode_set(mode='OBJECT')");
			w.WriteLine();
		}
		else
		{
			EditorUtility.DisplayDialog("Warning", "Root bone not found! Skipped writing of skeleton data.", "Ok");
		}
		
		w.WriteLine(S + "return rig");
	}
	
	private Transform FindRootBone(Transform parent)
	{	
		if (parent == null)
			return null;
		
		// find the root bone that usually isn't part of SkinnedMeshRenderer.bones
		foreach (Transform child in parent)
		{
			Transform bone = null;
			
			if (bonesSet.Contains(child))
				bone = parent;
			else 
				bone = FindRootBone(child);
			
			if (bone != null)
				return bone;
		}
		
		return null;
	}
	
	public void WriteBone(Transform head, Transform tail, Transform parent)
	{
		Vector3 hp = head.position;
		Vector3 tp = tail.position;

		WriteBoneRaw(head.name, parent != null ? parent.name : null, hp, tp);
		
		if (tail.childCount == 0)
		{
			Vector3 dir;
			float dist = Math.Abs((tail.position - head.position).magnitude);
			
			// check the distance so the head isn't at the same location as the tail
			// FIXME: in Wild Skies, transforms starting with "hp" seem to have
			// incorrect rotations, use the direction of the previous bone instead
			// as a workaround
			if (dist > 0.1f && tail.name.StartsWith("hp", true, null))
				dir = (tail.position - head.position).normalized * 0.1f;
			else
				dir = tail.rotation * new Vector3(-1, 0, 0) * 0.2f;

			// write the tail as terminating bone with a fixed length
			WriteBone(tail, head, dir);
		}
		else
		{
			// write each child bone from the tail
			foreach (Transform child in tail)
			{
				WriteBone(tail, child, head);
			}
		}
	}
	
	public void WriteBone(Transform bone, Transform parent, Vector3 dir)
	{
		Vector3 hp = bone.position;
		Vector3 tp = bone.position + dir;
		WriteBoneRaw(bone.name, parent != null ? parent.name : null, hp, tp);
	}
	
	private void WriteBoneRaw(string name, string parentName, Vector3 hp, Vector3 tp)
	{
		string vname = FixBoneName(name);
		w.WriteLine(S + vname + " = amt.edit_bones.new('" + name + "')");
		w.WriteLine(S + vname + ".head = (" + -hp.x + "," + -hp.z + "," + hp.y + ")");
		w.WriteLine(S + vname + ".tail = (" + -tp.x + "," + -tp.z + "," + tp.y + ")");
		
		if (parentName != null)
		{
			string vparent = FixBoneName(parentName);
			w.WriteLine(S + vname + ".parent = " + vparent);
			w.WriteLine(S + vname + ".use_connect = True");
		}
		
		w.WriteLine();
	}
	
	private string FixBoneName(string boneName)
	{
		return Regex.Replace(boneName, "[^a-zA-Z0-9_]", "_");
	}
	
	public void WriteSkinBuilder()
	{
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
		w.WriteLine(S + "ob.parent = rig");
		w.WriteLine();
	}
	
	public void WriteModelBuilder() 
	{
		w.WriteLine("def build():");
		w.WriteLine(S + "print('Building model " + meshName + "')");
		w.WriteLine(S + "# List of vertex coordinates");
		w.WriteLine(S + "verts = [");
		w.Write(S + S);
		
		for (int i = 0; i < verts.Length; i++)
		{
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
		
		for (int i = 0; i < mesh.subMeshCount; i++)
		{
			Material material = mats[i];
			String matName = material == null ? "null" : material.name;
			
			w.WriteLine(S + "faces['" + matName + "'] = [");
			w.Write(S + S);
			
			int[] triangles = mesh.GetTriangles(i);
			
			for (int j = 0, n = 1; j < triangles.Length; j += 3, n++) 
			{
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
		
		for (int i = 0, n = 1; i < tris.Length; i += 3, n++) 
		{
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
		
		for (int i = 0; i < bones.Length; i++)
		{
			List<Dictionary<int, float>> vweightList = new List<Dictionary<int, float>>();
			
			for (int j = 0; j < boneWeights.Length; j++)
			{
				Dictionary<int, float> vweight = new Dictionary<int, float>();
				
				if (boneWeights[j].boneIndex0 == i && boneWeights[j].weight0 != 0)
					vweight.Add(j, boneWeights[j].weight0);
				
				if (boneWeights[j].boneIndex1 == i && boneWeights[j].weight1 != 0)
					vweight.Add(j, boneWeights[j].weight1);
				
				if (boneWeights[j].boneIndex2 == i && boneWeights[j].weight2 != 0)
					vweight.Add(j, boneWeights[j].weight2);
				
				if (boneWeights[j].boneIndex3 == i && boneWeights[j].weight3 != 0)
					vweight.Add(j, boneWeights[j].weight3);
				
				if (vweight.Count > 0)
					vweightList.Add(vweight);
			}
			
			if (vweightList.Count > 0)
			{
				w.Write(S + "vg['" + bones[i].name + "'] = [");
				
				foreach (Dictionary<int, float> vweight in vweightList)
					foreach(KeyValuePair<int, float> entry in vweight)
						w.Write("(" + entry.Key + "," + entry.Value + "),");
				
				w.WriteLine("]");
			}
			else
			{
				w.WriteLine(S + "# No weights for bone " + bones[i].name);
			}
		}
		
		w.WriteLine();
		
		w.WriteLine(S + "ob = buildMesh(verts, faces, uv)");
		w.WriteLine(S + "rig = buildArmature()");
		w.WriteLine(S + "buildSkin(ob, rig, vg)");
		w.WriteLine();
	}
	
	public void WriteFooter()
	{
		w.WriteLine("if __name__ == \"__main__\":");
		w.WriteLine(S + "build()");
	}
	
	private void WriteAutoLineBreak(int n)
	{
		if (n % LineBreakLimit == 0)
		{
			w.WriteLine();
			w.Write(S + S);
		}
	}
}

