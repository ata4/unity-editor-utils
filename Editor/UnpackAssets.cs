/*
** 2013 March 9
**
** The author disclaims copyright to this source code.  In place of
** a legal notice, here is a blessing:
**	May you do good and not evil.
**	May you find forgiveness for yourself and forgive others.
**	May you share freely, never taking more than you give.
**/

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

public class UnpackAssets : MonoBehaviour {
	
	private static string selectedDir = Application.dataPath;
	private static Dictionary<string, HashSet<string>> assetList = new Dictionary<string, HashSet<string>>();
	private const char SEPARATOR = ':';
	
	[MenuItem("Custom/Assets/Create asset bundle list")]
	public static void List() {
		string assetPackPath = EditorUtility.OpenFilePanel("Select asset bundle", selectedDir, "assets");
		
		if (assetPackPath.Length == 0) {
			return;
		}
		
		string listName = Path.GetFileNameWithoutExtension(assetPackPath) + ".txt";
		string listPath = EditorUtility.SaveFilePanel("Save asset bundle list", "", selectedDir + "/" + listName, "txt");
		
		if (listPath.Length == 0) {
			return;
		}
		
		TextWriter tw = new StreamWriter(listPath);
		
		using (tw) {
			UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPackPath);
			
			foreach (UnityEngine.Object asset in assets) {
				if (asset == null) {
					continue;
				}
				
				string assetName = GetFixedAssetName(asset);
				string assetType = asset.GetType().Name;
				
				if (assetName.Length == 0) {
					continue;
				}
				
				tw.WriteLine(assetType + SEPARATOR + assetName);
			}
		}
		
		EditorUtility.DisplayDialog("UnpackAssets", "Finished writing " + listPath, "Ok");
	}

	[MenuItem("Custom/Assets/Unpack asset bundle")]
	public static void Unpack() {
		string assetPackPath = EditorUtility.OpenFilePanel("Select assets file to unpack", selectedDir, "assets");
		
		if (assetPackPath.Length == 0) {
			return;
		}
		
		selectedDir = Path.GetDirectoryName(assetPackPath);
		
		string assetListPath = EditorUtility.OpenFilePanel("Select list of assets to unpack", selectedDir, "txt");
		
		if (assetListPath.Length == 0) {
			return;
		}
		
		LoadAssetList(assetListPath);
		UnpackAssetBundle(assetPackPath);
	}
	
	private static void LoadAssetList(string assetListPath) {
		StreamReader reader = new StreamReader(assetListPath, Encoding.Default);
		
		using (reader) {
			for (string line; (line = reader.ReadLine()) != null;) {
				string[] parts = line.Split(SEPARATOR);
				
				if (parts.Length < 2) {
					continue;
				}
				
				string type = parts[0];
				string name = parts[1];
				
				HashSet<string> value;
				
				if (assetList.TryGetValue(type, out value)) {
					value.Add(name);
				} else {
					assetList[type] = new HashSet<string>();
				}
			}
		}
	}
	
	private static void UnpackAssetBundle(string assetPackPath) {
		string assetPackName = Path.GetFileNameWithoutExtension(assetPackPath);
		string importDir = Application.dataPath + "/" + assetPackName;
		
		if (!Directory.Exists(importDir)) {
			Directory.CreateDirectory(importDir);
		}
		
		UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPackPath);

		foreach (UnityEngine.Object asset in assets) {
			if (asset == null) {
				continue;
			}
			
			string assetName = GetFixedAssetName(asset);
			string assetType = asset.GetType().Name;
			
			HashSet<string> value;
			
			if (assetList.TryGetValue(assetType, out value)) {
				if (!value.Contains(assetName)) {
					continue;
				}
			} else {
				continue;
			}
			
			string assetBasePath = importDir + "/" + assetType;
			
			if (!Directory.Exists(assetBasePath)) {
				Directory.CreateDirectory(assetBasePath);
			}
			
			string assetExt = "asset";
			string assetPath = null;
			
			if (IsGameObject(asset)) {
				assetExt = "prefab";
			}
			
			for (int i = 0; assetPath == null || File.Exists(assetPath); i++) {
				assetPath = "Assets/" + assetPackName + "/" + assetType + "/" + assetName + (i == 0 ? "" : "_" + i) + "." + assetExt;
			}
			
			UnpackAsset(asset, assetPath);
		}
		
		EditorUtility.DisplayDialog("UnpackAssets", "Finished unpacking " + assetPackName, "Ok");
	}
	
	private static void UnpackAsset(Object asset, string assetPath) {
		if (IsGameObject(asset)) {
			GameObject gobj = (GameObject) asset;
			Object prefab = PrefabUtility.CreateEmptyPrefab(assetPath);
			PrefabUtility.ReplacePrefab(gobj, prefab);
		} else {
			Object assetInstance = asset;
			
			try {
				assetInstance = Object.Instantiate(asset);
			} catch {
				assetInstance = asset;
			}
							
			if (assetInstance != null) {
				AssetDatabase.CreateAsset(assetInstance, assetPath);
			} else {
				Debug.LogWarning("Can't unpack " + GetFixedAssetName(asset));
			}
		}
		AssetDatabase.Refresh();
	}
	
	private static bool IsGameObject(Object asset) {
		return asset.GetType() == typeof(GameObject) || asset.GetType().IsSubclassOf(typeof(GameObject));
	}
	
	private static bool IsSubstanceArchive(Object asset) {
		return asset.GetType() == typeof(SubstanceArchive);
	}
	
	private static string GetFixedAssetName(Object asset) {
		return Regex.Replace(asset.name, "[^a-zA-Z0-9.-_ ]", "");
	}
}
