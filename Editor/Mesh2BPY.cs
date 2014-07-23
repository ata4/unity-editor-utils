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
	
	[MenuItem("Custom/Export Mesh to Python")]
	public static void ExportMesh() {
		Transform[] selection = Selection.GetTransforms(SelectionMode.Unfiltered);
		
		if (selection.Length == 0) {
			EditorUtility.DisplayDialog(WindowTitle, "Please select a transform that contains a SkinnedMeshRenderer you wish to export.", "Ok");
			return;
		}
		
		Transform root = selection[0];
   		SkinnedMeshRenderer mesh = (SkinnedMeshRenderer) root.GetComponentInChildren(typeof(SkinnedMeshRenderer));
		//Animation anim = (Animation) root.GetComponent(typeof(Animation));

		if (mesh == null) {
			EditorUtility.DisplayDialog(WindowTitle, "No SkinnedMeshRenderer found in the currently selected transform!", "Ok");
			return;
		}

		string scriptName = mesh.name + ".py";
		string scriptPath = EditorUtility.SaveFilePanel("Export to Python script", "", scriptName, "py");
		
		if (scriptPath.Length == 0) {
			return;
		}
		
		StreamWriter w = new StreamWriter(scriptPath, false);
			
		using (w) {
			Mesh2BPY m2bpy = new Mesh2BPY(mesh, w);
			m2bpy.WriteHeader();
			m2bpy.WriteGeometry();
			m2bpy.WriteTransforms();
			//m2bpy.WriteAnimations(anim);
		}
		
		EditorUtility.DisplayDialog(WindowTitle, "Finished writing " + scriptName, "Ok");
	}
	
	private const string Version = "0.7";
	private const string WindowTitle = "Mesh2BPY";
	private const string S = "    "; // indent space
		
	private string scriptName;
	private string scriptPath;
	private StreamWriter w;

	private Dictionary<Transform, Vector3> boneScales = new Dictionary<Transform, Vector3>();
	
	private SkinnedMeshRenderer meshRenderer;
	private Mesh mesh;
	
	public Mesh2BPY(SkinnedMeshRenderer mr, StreamWriter sw) {
		meshRenderer = mr;
		mesh = mr.sharedMesh;
		w = sw;
	}
	
	private string ValueString(string str) {
		return "'" + str + "'";
	}
	
	private string ValueVector2(UnityEngine.Vector2 vec) {
		return ValueVector2(vec.x, vec.y);
	}
	
	private string ValueVector2(float x, float y) {
		return "(" + x + ", " + y + ")";
	}
	
	private string ValueVector3(UnityEngine.Vector3 vec) {
		return ValueVector3(vec.x, vec.y, vec.z);
	}
	
	private string ValueVector3(float x, float y, float z) {
		return "(" + x + ", " + y + ", " + z + ")";
	}
	
	private string ValueQuaternion(UnityEngine.Quaternion q) {
		return ValueVector4(q.x, q.y, q.z, q.w);
	}
	
	private string ValueVector4(float x, float y, float z, float w) {
		return "(" + x + ", " + y + ", " + z + ", " + w + ")";
	}
	
	public void WriteHeader() {
		w.WriteLine("# Raw model data Python script for \"" + meshRenderer.name + "\"");
		w.WriteLine("# Written by Mesh2BPY " + Version);
		w.WriteLine();
	}
	
	public void WriteGeometry() {
		WriteMeshMeta();		
		WriteVertices();
		WriteNormals();
		WriteUV();
		WriteSubmeshes();
		WriteMaterials();
		WriteVertexGroups();
	}
	
	public void WriteMeshMeta() {
		w.WriteLine("model = {");
		w.WriteLine(S + "'name': " + ValueString(meshRenderer.name) + ",");
		w.WriteLine(S + "'pos': " + ValueVector3(meshRenderer.transform.position) + ",");
		w.WriteLine(S + "'rot': " + ValueQuaternion(meshRenderer.transform.rotation) + ",");
		w.WriteLine(S + "'scl': " + ValueVector3(meshRenderer.transform.lossyScale));
		w.WriteLine("}");
		w.WriteLine();
	}
	
	public void WriteVertices() {
		w.WriteLine("# List of vertex coordinates");
		w.Write("model['verts'] = [");
		
		Vector3[] verts = mesh.vertices;
		for (int i = 0; i < verts.Length; i++) {
			w.Write(ValueVector3(verts[i]) + ", ");
			WriteAutoLineBreak(i, 32);
		}
		
		w.WriteLine("]");
		w.WriteLine();
	}
	
	public void WriteNormals() {
		w.WriteLine("# List of normals");
		w.Write("model['normals'] = [");
		
		Vector3[] normals = mesh.normals;
		for (int i = 0; i < normals.Length; i++) {
			w.Write(ValueVector3(normals[i]) + ", ");
			WriteAutoLineBreak(i, 32);
		}
		
		w.WriteLine("]");
		w.WriteLine();
	}

	public void WriteTrianglesOld() {
		w.WriteLine("# Map of materials and face indices");
		w.WriteLine("model['tris'] = {");
		
		Material[] mats = meshRenderer.sharedMaterials;
		for (int i = 0; i < mesh.subMeshCount; i++) {
			Material material = mats[i];
			string matName = material == null ? "null" : material.name;
			
			w.Write(S + ValueString(matName) + ": [");
			
			int[] triangles = mesh.GetTriangles(i);
			
			for (int j = 0, n = 1; j < triangles.Length; j += 3, n++) {
				w.Write(ValueVector3(triangles[j], triangles[j + 1],  triangles[j + 2]) + ", ");
				WriteAutoLineBreak(j, 32);
			}
			
			w.WriteLine("],");
		}
		
		w.WriteLine("}");
		w.WriteLine();
	}

	public void WriteSubmeshes() {
		w.WriteLine("# List of triangle indices per submesh");
		w.WriteLine("model['submeshes'] = [");

		for (int i = 0; i < mesh.subMeshCount; i++) {
			w.Write(S + "[");

			int[] triangles = mesh.GetTriangles(i);

			for (int j = 0; j < triangles.Length; j++) {
				w.Write(triangles[j] + ", ");
				WriteAutoLineBreak(j, 128);
			}

			w.WriteLine("],");
		}

		w.WriteLine("]");
		w.WriteLine();
	}

	public void WriteMaterials() {
		w.WriteLine("# List of material names per submesh");
		w.Write("model['materials'] = [");

		Material[] mats = meshRenderer.sharedMaterials;
		for (int i = 0; i < mesh.subMeshCount; i++) {
			Material material = mats[i];
			string matName = material == null ? "null" : material.name;
			w.Write(ValueString(matName) + ", ");
		}

		w.WriteLine("]");
		w.WriteLine();
	}
	
	public void WriteUV() {
		w.WriteLine("# List of texture face UVs");
		w.Write("model['uv'] = [");

		int[] tris = mesh.triangles;
		Vector2[] uv = mesh.uv;
		for (int i = 0, n = 1; i < tris.Length; i += 3, n++) {
			w.Write("[");

			w.Write(ValueVector2(uv[tris[i]]) + ", ");
			w.Write(ValueVector2(uv[tris[i + 1]]) + ", ");
			w.Write(ValueVector2(uv[tris[i + 2]]));
			
			w.Write("], ");
			WriteAutoLineBreak(i, 16);
		}
		
		w.WriteLine("]");
		w.WriteLine();
	}
	
	public void WriteVertexGroups() {
		w.WriteLine("# List of vertex groups, formatted as: (vertex index, weight)");
		w.WriteLine("model['vg'] = {");
		
		Transform[] bones = meshRenderer.bones;
		BoneWeight[] boneWeights = mesh.boneWeights;
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
				w.Write(S + ValueString(bones[i].name) + ": [");
				
				int vgcount = 1;
				foreach (Dictionary<int, float> vweight in vweightList) {
					foreach (KeyValuePair<int, float> entry in vweight) {
						w.Write(ValueVector2(entry.Key, entry.Value) + ", ");
						vgcount++;
						WriteAutoLineBreak(vgcount, 32);
					}
				}
				
				w.WriteLine("],");
			} else {
				w.WriteLine("# No weights for bone " + bones[i].name);
			}
		}
		
		w.WriteLine("}");
		w.WriteLine();
	}
	
	public void WriteTransforms() {
		HashSet<Transform> bones = new HashSet<Transform>(meshRenderer.bones);

		// save all connected bones in the set
		CollectTransforms(meshRenderer.transform.parent, bones);

		try {
			// reset local scale on all bones and save the original values in the dictionary,
			// this is required to unscale Transform.position
			boneScales.Clear();
			foreach (Transform bone in bones) {
				boneScales.Add(bone, bone.localScale);
				bone.localScale = new Vector3(1, 1, 1);
			}

			w.WriteLine("model['bones'] = {");
			
			foreach (Transform bone in bones) {
				WriteTransform(bone);
			}
			
			w.WriteLine("}");
			w.WriteLine("model['root_bone'] = " + ValueString (meshRenderer.rootBone.name));
			w.WriteLine();
		} finally {
			// restore local scale
			foreach (Transform bone in bones) {
				bone.localScale = boneScales[bone];
			}
		}
	}
	
	private void WriteTransform(Transform t) {
		w.WriteLine(S + ValueString(t.name) + ": {");

		w.WriteLine(S + S + "'pos': " + ValueVector3(t.position) + ",");
		w.WriteLine(S + S + "'rot': " + ValueQuaternion(t.rotation) + ",");

		w.WriteLine(S + S + "'lpos': " + ValueVector3(t.localPosition) + ",");
		w.WriteLine(S + S + "'lrot': " + ValueQuaternion(t.localRotation) + ",");
		//w.WriteLine(S + S + "'lscl': " + ValueVector3(t.localScale) + ",");
		w.WriteLine(S + S + "'lscl': " + ValueVector3(boneScales[t]) + ",");
		
		if (t.parent != null) {
			w.WriteLine(S + S + "'parent': " + ValueString(t.parent.name) + ",");
		}
		
		if (t.childCount > 0) {
			w.Write(S + S + "'children': [");
			
			Transform lastChild = t.GetChild(t.childCount - 1);
			foreach (Transform child in t) {
				w.Write(ValueString(child.name));
				if (child != lastChild) {
					w.Write(", ");
				}
			}
			
			w.WriteLine("]");
		}
		
		w.WriteLine(S + "},");
	}
	
	private void CollectTransforms(Transform transform, HashSet<Transform> tset) {
		tset.Add(transform);
		foreach (Transform child in transform) {
			CollectTransforms(child, tset);
		}
	}
	
	private void WriteAnimations(Animation anim) {
		if (anim == null) {
			return;
		}
		
		w.WriteLine("# Map of animation keyframes for every bone");
		w.WriteLine("model['animations'] = {");
		
		AnimationClip[] clips = AnimationUtility.GetAnimationClips(anim);
		
		foreach (AnimationClip clip in clips) {
			// map curves to bones first
			AnimationClipCurveData[] curves = AnimationUtility.GetAllCurves(clip);
			Dictionary<string, HashSet<AnimationClipCurveData>> boneCurveMap = new Dictionary<string, HashSet<AnimationClipCurveData>>();
			
			foreach (AnimationClipCurveData curve in curves) {
				string boneName = Regex.Replace(curve.path, "^([^/]+/)+", "");
				
				HashSet<AnimationClipCurveData> curveSet;
				
				if (boneCurveMap.ContainsKey(boneName)) {
					curveSet = boneCurveMap[boneName];
				} else {
					curveSet = new HashSet<AnimationClipCurveData>();
					boneCurveMap.Add(boneName, curveSet);
				}
				
				curveSet.Add(curve);
			}
			
			// now write data for each bone, curve and keyframe
			w.WriteLine(S + ValueString(clip.name) + ": {");

			foreach (KeyValuePair<string, HashSet<AnimationClipCurveData>> entry in boneCurveMap) {
				w.WriteLine(S + S + ValueString(entry.Key) + ": {");
				
				foreach (AnimationClipCurveData curve in entry.Value) {
					w.Write(S + S + S + ValueString(curve.propertyName) + ": {");
					
					foreach (Keyframe kf in curve.curve.keys) {
						w.Write(ValueVector2(kf.time, kf.value) + ",");
					}
					
					w.WriteLine("},");
				}
				
				w.WriteLine(S + S + "},");
			}
			
			w.WriteLine(S + "},");
		}
		
		w.WriteLine("}");
	}
	
	private void WriteAutoLineBreak(int n, int limit) {
		if ((n + 1) % limit == 0) {
			w.WriteLine();
		}
	}
}
