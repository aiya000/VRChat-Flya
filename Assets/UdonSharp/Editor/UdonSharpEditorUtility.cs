﻿
using JetBrains.Annotations;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharp.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;

namespace UdonSharpEditor
{
    public static class UdonSharpEditorUtility
    {
        /// <summary>
        /// Creates a new UdonAssemblyProgramAsset from an UdonSharpProgramAsset for the sake of portability. Most info used for the inspector gets stripped so this isn't a great solution for remotely complex assets.
        /// </summary>
        /// <param name="udonSharpProgramAsset">The source program asset</param>
        /// <param name="savePath">The save path for the asset file. Save path is only needed here because Udon needs a GUID for saving the serialized program asset and it'd be a pain to break that requirement at the moment</param>
        /// <returns>The exported UdonAssemblyProgramAsset</returns>
        [PublicAPI]
        public static UdonAssemblyProgramAsset UdonSharpProgramToAssemblyProgram(UdonSharpProgramAsset udonSharpProgramAsset, string savePath)
        {
            if (EditorApplication.isPlaying)
                throw new System.NotSupportedException("USharpEditorUtility.UdonSharpProgramToAssemblyProgram() cannot be called in play mode");

            UdonAssemblyProgramAsset newProgramAsset = ScriptableObject.CreateInstance<UdonAssemblyProgramAsset>();
            AssetDatabase.CreateAsset(newProgramAsset, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            newProgramAsset = AssetDatabase.LoadAssetAtPath<UdonAssemblyProgramAsset>(savePath);

            udonSharpProgramAsset.CompileCsProgram();

            FieldInfo assemblyField = typeof(UdonAssemblyProgramAsset).GetField("udonAssembly", BindingFlags.NonPublic | BindingFlags.Instance);
            assemblyField.SetValue(newProgramAsset, UdonSharpEditorCache.Instance.GetUASMStr(udonSharpProgramAsset));

            MethodInfo assembleMethod = typeof(UdonAssemblyProgramAsset).GetMethod("AssembleProgram", BindingFlags.NonPublic | BindingFlags.Instance);
            assembleMethod.Invoke(newProgramAsset, new object[] { });

            IUdonProgram uSharpProgram = udonSharpProgramAsset.GetRealProgram();
            FieldInfo assemblyProgramGetter = typeof(UdonProgramAsset).GetField("program", BindingFlags.NonPublic | BindingFlags.Instance);
            IUdonProgram assemblyProgram = (IUdonProgram)assemblyProgramGetter.GetValue(newProgramAsset);

            if (uSharpProgram == null || assemblyProgram == null)
                return null;

            string[] symbols = uSharpProgram.SymbolTable.GetSymbols();

            foreach (string symbol in symbols)
            {
                uint symbolAddress = uSharpProgram.SymbolTable.GetAddressFromSymbol(symbol);
                System.Type symbolType = uSharpProgram.Heap.GetHeapVariableType(symbolAddress);
                object symbolValue = uSharpProgram.Heap.GetHeapVariable(symbolAddress);
                
                assemblyProgram.Heap.SetHeapVariable(assemblyProgram.SymbolTable.GetAddressFromSymbol(symbol), symbolValue, symbolType);
            }

            EditorUtility.SetDirty(newProgramAsset);

            newProgramAsset.SerializedProgramAsset.StoreProgram(assemblyProgram);
            EditorUtility.SetDirty(newProgramAsset.SerializedProgramAsset);

            AssetDatabase.SaveAssets();

            // This doesn't work unfortunately due to how Udon tries to locate the serialized asset when importing an assembly
            //string serializedAssetPath = $"{Path.GetDirectoryName(savePath)}/{Path.GetFileNameWithoutExtension(savePath)}_serialized.asset";
            
            //AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(newProgramAsset.SerializedProgramAsset), serializedAssetPath);
            //AssetDatabase.SaveAssets();

            return newProgramAsset;
        }

        /// <summary>
        /// Deletes an UdonSharp program asset and the serialized program asset associated with it
        /// </summary>
        /// <param name="programAsset"></param>
        [PublicAPI]
        public static void DeleteProgramAsset(UdonSharpProgramAsset programAsset)
        {
            if (programAsset == null)
                return;

            AbstractSerializedUdonProgramAsset serializedAsset = programAsset.GetSerializedUdonProgramAsset();

            if (serializedAsset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(serializedAsset);
                serializedAsset = AssetDatabase.LoadAssetAtPath<AbstractSerializedUdonProgramAsset>(assetPath);

                if (serializedAsset != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }

            string programAssetPath = AssetDatabase.GetAssetPath(programAsset);

            programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programAssetPath);

            if (programAsset != null)
                AssetDatabase.DeleteAsset(programAssetPath);
        }

        /// <summary>
        /// Converts a set of UdonSharpBehaviour components to their equivalent UdonBehaviour components
        /// </summary>
        /// <param name="components"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonBehaviour[] ConvertToUdonBehaviours(UdonSharpBehaviour[] components)
        {
            return ConvertToUdonBehavioursInternal(components, false, false);
        }

        /// <summary>
        /// Converts a set of UdonSharpBehaviour components to their equivalent UdonBehaviour components
        /// Registers an Undo operation for the conversion
        /// </summary>
        /// <param name="components"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonBehaviour[] ConvertToUdonBehavioursWithUndo(UdonSharpBehaviour[] components)
        {
            return ConvertToUdonBehavioursInternal(components, true, false);
        }

        static internal Dictionary<MonoScript, UdonSharpProgramAsset> _programAssetLookup;
        static internal Dictionary<System.Type, UdonSharpProgramAsset> _programAssetTypeLookup;
        private static void InitTypeLookups()
        {
            if (_programAssetLookup == null)
            {
                _programAssetLookup = new Dictionary<MonoScript, UdonSharpProgramAsset>();
                _programAssetTypeLookup = new Dictionary<System.Type, UdonSharpProgramAsset>();

                UdonSharpProgramAsset[] udonSharpProgramAssets = UdonSharpProgramAsset.GetAllUdonSharpPrograms();

                foreach (UdonSharpProgramAsset programAsset in udonSharpProgramAssets)
                {
                    if (programAsset && programAsset.sourceCsScript != null && !_programAssetLookup.ContainsKey(programAsset.sourceCsScript))
                    {
                        _programAssetLookup.Add(programAsset.sourceCsScript, programAsset);
                        if (programAsset.sourceCsScript.GetClass() != null)
                            _programAssetTypeLookup.Add(programAsset.sourceCsScript.GetClass(), programAsset);
                    }
                }
            }
        }

        private static UdonSharpProgramAsset GetUdonSharpProgramAsset(MonoScript programScript)
        {
            InitTypeLookups();

            _programAssetLookup.TryGetValue(programScript, out UdonSharpProgramAsset foundProgramAsset);

            return foundProgramAsset;
        }

        /// <summary>
        /// Gets the UdonSharpProgramAsset that represents the program for the given UdonSharpBehaviour
        /// </summary>
        /// <param name="udonSharpBehaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonSharpProgramAsset GetUdonSharpProgramAsset(UdonSharpBehaviour udonSharpBehaviour)
        {
            return GetUdonSharpProgramAsset(MonoScript.FromMonoBehaviour(udonSharpBehaviour));
        }

        [PublicAPI]
        public static UdonSharpProgramAsset GetUdonSharpProgramAsset(System.Type type)
        {
            InitTypeLookups();

            _programAssetTypeLookup.TryGetValue(type, out UdonSharpProgramAsset foundProgramAsset);

            return foundProgramAsset;
        }

        private static readonly FieldInfo _backingBehaviourField = typeof(UdonSharpBehaviour).GetField("_backingUdonBehaviour", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Gets the backing UdonBehaviour for a proxy
        /// </summary>
        /// <param name="behaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonBehaviour GetBackingUdonBehaviour(UdonSharpBehaviour behaviour)
        {
            return (UdonBehaviour)_backingBehaviourField.GetValue(behaviour);
        }

        internal static void SetBackingUdonBehaviour(UdonSharpBehaviour behaviour, UdonBehaviour backingBehaviour)
        {
            _backingBehaviourField.SetValue(behaviour, backingBehaviour);
        }

        /// <summary>
        /// Returns true if the given behaviour is a proxy behaviour that's linked to an UdonBehaviour.
        /// </summary>
        /// <param name="behaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static bool IsProxyBehaviour(UdonSharpBehaviour behaviour)
        {
            return GetBackingUdonBehaviour(behaviour) != null;
        }

        static Dictionary<UdonBehaviour, UdonSharpBehaviour> _proxyBehaviourLookup = new Dictionary<UdonBehaviour, UdonSharpBehaviour>();

        /// <summary>
        /// Finds an existing proxy behaviour, if none exists returns null
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonSharpBehaviour FindProxyBehaviour(UdonBehaviour udonBehaviour)
        {
            return FindProxyBehaviour(udonBehaviour, ProxySerializationPolicy.Default);
        }

        /// <summary>
        /// Finds an existing proxy behaviour, if none exists returns null
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <param name="proxySerializationPolicy"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonSharpBehaviour FindProxyBehaviour(UdonBehaviour udonBehaviour, ProxySerializationPolicy proxySerializationPolicy)
        {
            if (_proxyBehaviourLookup.TryGetValue(udonBehaviour, out UdonSharpBehaviour proxyBehaviour))
            {
                if (proxyBehaviour != null)
                {
                    CopyUdonToProxy(proxyBehaviour, proxySerializationPolicy);

                    proxyBehaviour.enabled = false;

                    return proxyBehaviour;
                }
                else
                {
                    _proxyBehaviourLookup.Remove(udonBehaviour);
                }
            }

            UdonSharpBehaviour[] behaviours = udonBehaviour.GetComponents<UdonSharpBehaviour>();

            foreach (UdonSharpBehaviour udonSharpBehaviour in behaviours)
            {
                IUdonBehaviour backingBehaviour = GetBackingUdonBehaviour(udonSharpBehaviour);
                if (backingBehaviour != null && ReferenceEquals(backingBehaviour, udonBehaviour))
                {
                    _proxyBehaviourLookup.Add(udonBehaviour, udonSharpBehaviour);

                    CopyUdonToProxy(udonSharpBehaviour, proxySerializationPolicy);

                    udonSharpBehaviour.enabled = false;

                    return udonSharpBehaviour;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the C# version of an UdonSharpBehaviour that proxies an UdonBehaviour with the program asset for the matching UdonSharpBehaviour type
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonSharpBehaviour GetProxyBehaviour(UdonBehaviour udonBehaviour)
        {
            return GetProxyBehaviour(udonBehaviour, ProxySerializationPolicy.Default);
        }

        /// <summary>
        /// Returns if the given UdonBehaviour is an UdonSharpBehaviour
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static bool IsUdonSharpBehaviour(UdonBehaviour udonBehaviour)
        {
            return udonBehaviour.programSource != null && 
                   udonBehaviour.programSource is UdonSharpProgramAsset programAsset && 
                   programAsset.sourceCsScript != null;
        }

        /// <summary>
        /// Gets the UdonSharpBehaviour type from the given behaviour.
        /// If the behaviour is not an UdonSharpBehaviour, returns null.
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static System.Type GetUdonSharpBehaviourType(UdonBehaviour udonBehaviour)
        {
            if (!IsUdonSharpBehaviour(udonBehaviour))
                return null;

            return ((UdonSharpProgramAsset)udonBehaviour.programSource).sourceCsScript.GetClass();
        }

        /// <summary>
        /// Gets the C# version of an UdonSharpBehaviour that proxies an UdonBehaviour with the program asset for the matching UdonSharpBehaviour type
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <param name="proxySerializationPolicy"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonSharpBehaviour GetProxyBehaviour(UdonBehaviour udonBehaviour, ProxySerializationPolicy proxySerializationPolicy)
        {
            if (udonBehaviour == null)
                throw new System.ArgumentNullException("Source Udon Behaviour cannot be null");

            if (udonBehaviour.programSource == null)
                throw new System.ArgumentNullException("Program source on UdonBehaviour cannot be null");

            UdonSharpProgramAsset udonSharpProgram = udonBehaviour.programSource as UdonSharpProgramAsset;

            if (udonSharpProgram == null)
                throw new System.ArgumentException("UdonBehaviour must be using an UdonSharp program");

            UdonSharpBehaviour proxyBehaviour = FindProxyBehaviour(udonBehaviour, proxySerializationPolicy);

            if (proxyBehaviour)
                return proxyBehaviour;

            // We've failed to find an existing proxy behaviour so we need to create one
            System.Type scriptType = udonSharpProgram.sourceCsScript.GetClass();

            if (scriptType == null)
                return null;

            proxyBehaviour = (UdonSharpBehaviour)udonBehaviour.gameObject.AddComponent(scriptType);
            proxyBehaviour.hideFlags = HideFlags.DontSaveInBuild |
#if !UDONSHARP_DEBUG
                                       HideFlags.HideInInspector |
#endif
                                       HideFlags.DontSaveInEditor;
            proxyBehaviour.enabled = false;

            SetBackingUdonBehaviour(proxyBehaviour, udonBehaviour);

            _proxyBehaviourLookup.Add(udonBehaviour, proxyBehaviour);
            
            CopyUdonToProxy(proxyBehaviour, proxySerializationPolicy);

            return proxyBehaviour;
        }

        /// <summary>
        /// Copies the state of the proxy to its backing UdonBehaviour
        /// </summary>
        /// <param name="proxy"></param>
        [PublicAPI]
        public static void CopyProxyToUdon(UdonSharpBehaviour proxy)
        {
            CopyProxyToUdon(proxy, ProxySerializationPolicy.Default);
        }

        /// <summary>
        /// Copies the state of the UdonBehaviour to its proxy object
        /// </summary>
        /// <param name="proxy"></param>
        [PublicAPI]
        public static void CopyUdonToProxy(UdonSharpBehaviour proxy)
        {
            CopyUdonToProxy(proxy, ProxySerializationPolicy.Default);
        }

        /// <summary>
        /// Copies the state of the proxy to its backing UdonBehaviour
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="serializationPolicy"></param>
        [PublicAPI]
        public static void CopyProxyToUdon(UdonSharpBehaviour proxy, ProxySerializationPolicy serializationPolicy)
        {
            if (serializationPolicy.MaxSerializationDepth == 0)
                return;

            Profiler.BeginSample("CopyProxyToUdon");

            SimpleValueStorage<UdonBehaviour> udonBehaviourStorage = new SimpleValueStorage<UdonBehaviour>(GetBackingUdonBehaviour(proxy));

            ProxySerializationPolicy lastPolicy = USBSerializationContext.currentPolicy;
            USBSerializationContext.currentPolicy = serializationPolicy;

            Serializer.CreatePooled(proxy.GetType()).WriteWeak(udonBehaviourStorage, proxy);

            USBSerializationContext.currentPolicy = lastPolicy;

            Profiler.EndSample();
        }

        /// <summary>
        /// Copies the state of the UdonBehaviour to its proxy object
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="serializationPolicy"></param>
        [PublicAPI]
        public static void CopyUdonToProxy(UdonSharpBehaviour proxy, ProxySerializationPolicy serializationPolicy)
        {
            if (serializationPolicy.MaxSerializationDepth == 0)
                return;

            Profiler.BeginSample("CopyUdonToProxy");

            SimpleValueStorage<UdonBehaviour> udonBehaviourStorage = new SimpleValueStorage<UdonBehaviour>(GetBackingUdonBehaviour(proxy));

            ProxySerializationPolicy lastPolicy = USBSerializationContext.currentPolicy;
            USBSerializationContext.currentPolicy = serializationPolicy;

            object proxyObj = proxy;
            Serializer.CreatePooled(proxy.GetType()).ReadWeak(ref proxyObj, udonBehaviourStorage);

            USBSerializationContext.currentPolicy = lastPolicy;

            Profiler.EndSample();
        }

        [PublicAPI]
        public static UdonBehaviour CreateBehavourForProxy(UdonSharpBehaviour udonSharpBehaviour)
        {
            UdonBehaviour backingBehaviour = GetBackingUdonBehaviour(udonSharpBehaviour);

            if (backingBehaviour == null)
            {
                backingBehaviour = udonSharpBehaviour.gameObject.AddComponent<UdonBehaviour>();
                backingBehaviour.programSource = GetUdonSharpProgramAsset(udonSharpBehaviour);
            }

            CopyProxyToUdon(udonSharpBehaviour);

            return backingBehaviour;
        }

        /// <summary>
        /// Destroys an UdonSharpBehaviour proxy and its underlying UdonBehaviour
        /// </summary>
        /// <param name="behaviour"></param>
        [PublicAPI]
        public static void DestroyImmediate(UdonSharpBehaviour behaviour)
        {
            UdonBehaviour backingBehaviour = GetBackingUdonBehaviour(behaviour);
            
            Object.DestroyImmediate(behaviour);

            if (backingBehaviour)
            {
                _proxyBehaviourLookup.Remove(backingBehaviour);
                Object.DestroyImmediate(backingBehaviour);
            }
        }

        #region Internal utilities
        internal static void CollectUdonSharpBehaviourReferencesInternal(object rootObject, HashSet<UdonSharpBehaviour> gatheredSet, HashSet<object> visitedSet = null)
        {
            if (gatheredSet == null)
                gatheredSet = new HashSet<UdonSharpBehaviour>();

            if (visitedSet == null)
                visitedSet = new HashSet<object>(new VRC.Udon.Serialization.OdinSerializer.Utilities.ReferenceEqualityComparer<object>());

            if (rootObject == null)
                return;

            if (visitedSet.Contains(rootObject))
                return;

            System.Type objectType = rootObject.GetType();

            if (objectType.IsValueType)
                return;

            if (VRC.Udon.Serialization.OdinSerializer.FormatterUtilities.IsPrimitiveType(objectType))
                return;

            visitedSet.Add(rootObject);

            if (objectType == typeof(UdonSharpBehaviour) ||
                objectType.IsSubclassOf(typeof(UdonSharpBehaviour)))
            {
                gatheredSet.Add((UdonSharpBehaviour)rootObject);
            }

            if (objectType.IsArray)
            {
                foreach (object arrayElement in (System.Array)rootObject)
                {
                    CollectUdonSharpBehaviourReferencesInternal(arrayElement, gatheredSet, visitedSet);
                }
            }
            else
            {
                FieldInfo[] objectFields = objectType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                foreach (FieldInfo fieldInfo in objectFields)
                {
                    object fieldValue = fieldInfo.GetValue(rootObject);

                    CollectUdonSharpBehaviourReferencesInternal(fieldValue, gatheredSet, visitedSet);
                }
            }
        }

        internal static UdonBehaviour[] ConvertToUdonBehavioursInternal(UdonSharpBehaviour[] components, bool shouldUndo, bool showPrompts)
        {
            components = components.Distinct().ToArray();

            bool convertChildren = true;

            if (showPrompts)
            {
                HashSet<UdonSharpBehaviour> allReferencedBehaviours = new HashSet<UdonSharpBehaviour>();

                // Check if any of these need child component conversion
                foreach (UdonSharpBehaviour targetObject in components)
                {
                    HashSet<UdonSharpBehaviour> referencedBehaviours = new HashSet<UdonSharpBehaviour>();

                    CollectUdonSharpBehaviourReferencesInternal(targetObject, referencedBehaviours);

                    if (referencedBehaviours.Count > 1)
                    {
                        foreach (UdonSharpBehaviour referencedBehaviour in referencedBehaviours)
                        {
                            if (referencedBehaviour != targetObject)
                                allReferencedBehaviours.Add(referencedBehaviour);
                        }
                    }
                }

                if (allReferencedBehaviours.Count > 0)
                {
                    // This is an absolute mess, it should probably just be simplified to counting the number of affected behaviours
                    string referencedBehaviourStr;
                    if (allReferencedBehaviours.Count <= 2)
                        referencedBehaviourStr = string.Join(", ", allReferencedBehaviours.Select(e => $"'{e.ToString()}'"));
                    else
                        referencedBehaviourStr = $"{allReferencedBehaviours.Count} behaviours";

                    string rootBehaviourStr;

                    if (components.Length <= 2)
                        rootBehaviourStr = $"{string.Join(", ", components.Select(e => $"'{e.ToString()}'"))} reference{(components.Length == 1 ? "s" : "")} ";
                    else
                        rootBehaviourStr = $"{components.Length} behaviours to convert reference ";

                    string messageStr = $"{rootBehaviourStr}{referencedBehaviourStr}. Do you want to convert all referenced behaviours as well? If no, references to these behaviours will be set to null.";

                    int result = EditorUtility.DisplayDialogComplex("Dependent behaviours found", messageStr, "Yes", "Cancel", "No");

                    if (result == 2) // No
                        convertChildren = false;
                    else if (result == 1) // Cancel
                        return null;
                }
            }

            if (shouldUndo)
                Undo.RegisterCompleteObjectUndo(components, "Convert to UdonBehaviour");

            List<UdonBehaviour> createdComponents = new List<UdonBehaviour>();
            foreach (UdonSharpBehaviour targetObject in components)
            {
                MonoScript behaviourScript = MonoScript.FromMonoBehaviour(targetObject);
                UdonSharpProgramAsset programAsset = GetUdonSharpProgramAsset(behaviourScript);

                if (programAsset == null)
                {
                    if (showPrompts)
                    {
                        string scriptPath = AssetDatabase.GetAssetPath(behaviourScript);
                        string scriptDirectory = Path.GetDirectoryName(scriptPath);
                        string scriptFileName = Path.GetFileNameWithoutExtension(scriptPath);

                        string assetPath = Path.Combine(scriptDirectory, $"{scriptFileName}.asset").Replace('\\', '/');

                        if (EditorUtility.DisplayDialog("No linked program asset", $"There was no UdonSharpProgramAsset found for '{behaviourScript.GetClass()}', do you want to create one?", "Ok", "Cancel"))
                        {
                            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) != null)
                            {
                                if (!EditorUtility.DisplayDialog("Existing file found", $"Asset file {assetPath} already exists, do you want to overwrite it?", "Ok", "Cancel"))
                                    continue;
                            }
                        }
                        else
                            continue;

                        programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                        programAsset.sourceCsScript = behaviourScript;
                        AssetDatabase.CreateAsset(programAsset, assetPath);
                        AssetDatabase.SaveAssets();

                        UdonSharpProgramAsset.ClearProgramAssetCache();

                        programAsset.CompileCsProgram();

                        AssetDatabase.SaveAssets();

                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }
                    else
                    {
                        Debug.LogWarning($"Could not convert U# behaviour '{behaviourScript.GetClass()}' on '{targetObject.gameObject}' because it does not have a corresponding UdonSharpProgramAsset");
                        continue;
                    }
                }

                GameObject targetGameObject = targetObject.gameObject;

                UdonBehaviour udonBehaviour = null;

                if (shouldUndo)
                    udonBehaviour = Undo.AddComponent<UdonBehaviour>(targetGameObject);
                else
                    udonBehaviour = targetGameObject.AddComponent<UdonBehaviour>();

                udonBehaviour.programSource = programAsset;

                //if (shouldUndo)
                //    Undo.RegisterCompleteObjectUndo(targetObject, "Convert C# to U# behaviour");

                UdonSharpEditorUtility.SetBackingUdonBehaviour(targetObject, udonBehaviour);

                try
                {
                    if (convertChildren)
                        UdonSharpEditorUtility.CopyProxyToUdon(targetObject, shouldUndo ? ProxySerializationPolicy.AllWithCreateUndo : ProxySerializationPolicy.All);
                    else
                        UdonSharpEditorUtility.CopyProxyToUdon(targetObject, ProxySerializationPolicy.RootOnly);
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }

                UdonSharpEditorUtility.SetBackingUdonBehaviour(targetObject, null);

                System.Type behaviourType = targetObject.GetType();

                UdonSharpBehaviour newProxy;

                if (shouldUndo)
                    newProxy = (UdonSharpBehaviour)Undo.AddComponent(targetObject.gameObject, behaviourType);
                else
                    newProxy = (UdonSharpBehaviour)targetObject.gameObject.AddComponent(behaviourType);

                UdonSharpEditorUtility.SetBackingUdonBehaviour(newProxy, udonBehaviour);
                try
                {
                    UdonSharpEditorUtility.CopyUdonToProxy(newProxy);
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
                
                if (shouldUndo)
                    Undo.DestroyObjectImmediate(targetObject);
                else
                    Object.DestroyImmediate(targetObject);

                newProxy.hideFlags = HideFlags.DontSaveInBuild |
#if !UDONSHARP_DEBUG
                                     HideFlags.HideInInspector |
#endif
                                     HideFlags.DontSaveInEditor;

                newProxy.enabled = false;

                createdComponents.Add(udonBehaviour);
            }

            return createdComponents.ToArray();
        }
        #endregion
    }
}
