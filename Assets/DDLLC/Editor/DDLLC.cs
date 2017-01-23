using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.IO;
using Microsoft.CSharp;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Linq;

namespace DDLLC {
	public class DDLLC : ScriptableObject {

		[SerializeField]
		string packageName = "Unnamed";

		[SerializeField]
		MonoScript[] scripts;
		[SerializeField]
		MonoScript[] editorScripts;

		[SerializeField]
		string[] dependencies;

		static DDLLC _instance;
		public static DDLLC instance {
			get {
				if (!_instance) {
					var found = Resources.FindObjectsOfTypeAll<DDLLC>();
					_instance = found.Length > 0 ? found[0] : null;
					if (!_instance) {
						_instance = ScriptableObject.CreateInstance<DDLLC>();
						_instance.hideFlags = HideFlags.DontSaveInBuild;
						Directory.CreateDirectory("Assets/DDLLC");
						AssetDatabase.CreateAsset(_instance, "Assets/CompilerSettings.asset");
						AssetDatabase.SaveAssets();
					}
				}
				return _instance;
			}
		}

		public static string[] scriptFilenames {
			get {
				return instance.scripts.Select(s => AssetDatabase.GetAssetPath(s)).ToArray();
			}
		}

		public static string[] editorScriptFilenames {
			get {
				return instance.editorScripts.Select(s => AssetDatabase.GetAssetPath(s)).ToArray();
			}
		}

		public static string[] dependencyFilenames {
			get {
				return instance.dependencies.ToArray();
			}
		}

		public static string pkgName {
			get {
				return instance.packageName;
			}
		}


		void Reset() {
			packageName = PlayerSettings.productName.Replace(" ", "");
		}

		string GetProjectName() {
			string[] s = Application.dataPath.Split('/');
			string projectName = s[s.Length - 2];
			return projectName;
		}
	}



	[CustomEditor(typeof(DDLLC))]
	public class DDLLCEditor : Editor {

		ReorderableList listScripts, listEditorScripts, listDependencies;
		SerializedProperty scripts, editorScripts, dependencies, packageName;

		void OnEnable() {
			scripts = serializedObject.FindProperty("scripts");
			editorScripts = serializedObject.FindProperty("editorScripts");
			dependencies = serializedObject.FindProperty("dependencies");
			packageName = serializedObject.FindProperty("packageName");

			listScripts = new ReorderableList(serializedObject, scripts);
			listScripts.drawElementCallback += (rekt, index, isActive, isFocused) => {
				rekt.height = 16; rekt.y += 2;
				EditorGUI.PropertyField(rekt, scripts.GetArrayElementAtIndex(index));
			};
			listScripts.drawHeaderCallback += (rekt) => { GUI.Label(rekt, "Scripts"); };
			listScripts.onRemoveCallback += (list) => {
				scripts.GetArrayElementAtIndex(list.index).objectReferenceValue = null;
				scripts.DeleteArrayElementAtIndex(list.index);
			};


			listEditorScripts = new ReorderableList(serializedObject, editorScripts);
			listEditorScripts.drawElementCallback += (rekt, index, isActive, isFocused) => {
				rekt.height = 16; rekt.y += 2;
				EditorGUI.PropertyField(rekt, editorScripts.GetArrayElementAtIndex(index));
			};
			listEditorScripts.drawHeaderCallback += (rekt) => { GUI.Label(rekt, "Editor Scripts"); };
			listEditorScripts.onRemoveCallback += (list) => {
				editorScripts.GetArrayElementAtIndex(list.index).objectReferenceValue = null;
				editorScripts.DeleteArrayElementAtIndex(list.index);
			};


			listDependencies = new ReorderableList(serializedObject, dependencies);
			listDependencies.drawElementCallback += (rekt, index, isActive, isFocused) => {
				rekt.height = 16; rekt.y += 2;
				EditorGUI.PropertyField(rekt, dependencies.GetArrayElementAtIndex(index));
			};
			listDependencies.drawHeaderCallback += (rekt) => { GUI.Label(rekt, "Additional Dependencies"); };
			listDependencies.onRemoveCallback += (list) => {
				dependencies.DeleteArrayElementAtIndex(list.index);
			};
		}


		public override void OnInspectorGUI() {
			serializedObject.Update();

			GUILayout.Space(4);
			EditorGUILayout.PropertyField(packageName);
			GUILayout.Space(12);
			listScripts.DoLayoutList();
			GUILayout.Space(12);
			listEditorScripts.DoLayoutList();
			GUILayout.Space(12);
			listDependencies.DoLayoutList();

			if (serializedObject.ApplyModifiedProperties()) {
				// just to be sure, wtf unity5
				EditorUtility.SetDirty(target);
			}

			GUILayout.Space(12);
			EditorGUILayout.HelpBox("Upon compilation, every occurence of PLACEHOLDER will be replaced with the name specified above.\nIntended for use in namespaces, to avoid conflicts between dll and script code.", MessageType.Info);

			if (GUILayout.Button("Compile")) {
				Compiler.Compile();
			}
		}

		[MenuItem("Compiler/Configure Compiler")]
		static void Config() {
			Selection.activeObject = DDLLC.instance;
		}


		protected override void OnHeaderGUI() {
			GUILayout.Space(12);
			GUILayout.Label("  Compiler Setup", EditorStyles.centeredGreyMiniLabel);
			GUILayout.Space(8);
		}
	}




	/// <summary>
	/// This is balls.
	/// </summary>
	class Compiler {
		public static void Compile() {

			var opts = new Dictionary<string, string>();
			opts.Add("CompilerVersion", "v3.5");

			string firstpass = null;
			string secondpass = null;
			var basepath = @"Assets\Plugins\" + DDLLC.pkgName + @"\";


			if (DDLLC.scriptFilenames.Any()) {
				Directory.CreateDirectory(basepath);
				using (var provider = new CSharpCodeProvider(opts)) {
					CompilerParameters parameters = new CompilerParameters();
					parameters.GenerateExecutable = false;
					parameters.OutputAssembly = basepath + DDLLC.pkgName + @".dll";

					// references
					parameters.ReferencedAssemblies.Add(InternalEditorUtility.GetEngineAssemblyPath());
					if (DDLLC.dependencyFilenames.Any()) {
						parameters.ReferencedAssemblies.AddRange(DDLLC.dependencyFilenames);
					}

					// sources
					var files = DDLLC.scriptFilenames;
					var sources = files.Select(f => File.ReadAllText(f).Replace("PLACEHOLDER", DDLLC.pkgName)).ToArray();

					var _ = provider.CompileAssemblyFromSource(parameters, sources);
					firstpass = _.PathToAssembly;

					Debug.Log("FINISHED: " + _.PathToAssembly);
				}
			}

			basepath += @"Editor\";

			if (DDLLC.editorScriptFilenames.Any()) {
				Directory.CreateDirectory(basepath);
				using (var provider = new CSharpCodeProvider(opts)) {
					CompilerParameters parameters = new CompilerParameters();
					parameters.GenerateExecutable = false;
					parameters.OutputAssembly = basepath + DDLLC.pkgName + ".Editor.dll";

					// references
					if (!string.IsNullOrEmpty(firstpass))
						parameters.ReferencedAssemblies.Add(firstpass);
					parameters.ReferencedAssemblies.Add(InternalEditorUtility.GetEngineAssemblyPath());
					parameters.ReferencedAssemblies.Add(InternalEditorUtility.GetEditorAssemblyPath());
					if (DDLLC.dependencyFilenames.Any()) {
						parameters.ReferencedAssemblies.AddRange(DDLLC.dependencyFilenames);
					}

					// sources
					var files = DDLLC.editorScriptFilenames;
					var sources = files.Select(f => File.ReadAllText(f).Replace("PLACEHOLDER", DDLLC.pkgName)).ToArray();

					var _ = provider.CompileAssemblyFromSource(parameters, sources);
					secondpass = _.PathToAssembly;

					Debug.Log("FINISHED: " + _.PathToAssembly);
				}
			}

			AssetDatabase.Refresh();

			if (firstpass != null && secondpass != null)
				EditorApplication.delayCall += () => AssetDatabase.ExportPackage(new string[] { firstpass, secondpass }.Where(s => !string.IsNullOrEmpty(s)).ToArray(), "../" + DDLLC.pkgName + ".unitypackage");
		}
	}
}