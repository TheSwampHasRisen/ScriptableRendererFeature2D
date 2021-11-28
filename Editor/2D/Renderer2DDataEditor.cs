using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;
using Object = UnityEngine.Object;
using UnityEditor.Rendering;

namespace UnityEditor.Experimental.Rendering.Universal
{
    [CustomEditor(typeof(Renderer2DData), true)]
    internal class Renderer2DDataEditor : Editor
    {
        class Styles
        {
        	public static readonly GUIContent RenderFeatures = new GUIContent("Renderer Features", "Features to include in this renderer.\nTo add or remove features, use the plus and minus at the bottom of this box.");
            public static readonly GUIContent PassNameField = new GUIContent("Name", "Render pass name. This name is the name displayed in Frame Debugger.");
            public static readonly GUIContent MissingFeature = new GUIContent("Missing RendererFeature", "Missing reference, due to compilation issues or missing files. you can attempt auto fix or choose to remove the feature.");

            public static readonly GUIContent transparencySortMode = EditorGUIUtility.TrTextContent("Transparency Sort Mode", "Default sorting mode used for transparent objects");
            public static readonly GUIContent transparencySortAxis = EditorGUIUtility.TrTextContent("Transparency Sort Axis", "Axis used for custom axis sorting mode");
            public static readonly GUIContent hdrEmulationScale = EditorGUIUtility.TrTextContent("HDR Emulation Scale", "Describes the scaling used by lighting to remap dynamic range between LDR and HDR");
            public static readonly GUIContent lightBlendStyles = EditorGUIUtility.TrTextContent("Light Blend Styles", "A Light Blend Style is a collection of properties that describe a particular way of applying lighting.");
            public static readonly GUIContent defaultMaterialType = EditorGUIUtility.TrTextContent("Default Material Type", "Material to use when adding new objects to a scene");
            public static readonly GUIContent defaultCustomMaterial = EditorGUIUtility.TrTextContent("Default Custom Material", "Material to use when adding new objects to a scene");

            public static readonly GUIContent name = EditorGUIUtility.TrTextContent("Name");
            public static readonly GUIContent maskTextureChannel = EditorGUIUtility.TrTextContent("Mask Texture Channel", "Which channel of the mask texture will affect this Light Blend Style.");
            public static readonly GUIContent renderTextureScale = EditorGUIUtility.TrTextContent("Render Texture Scale", "The resolution of the lighting buffer relative to the screen resolution. 1.0 means full screen size.");
            public static readonly GUIContent blendMode = EditorGUIUtility.TrTextContent("Blend Mode", "How the lighting should be blended with the main color of the objects.");
            public static readonly GUIContent customBlendFactors = EditorGUIUtility.TrTextContent("Custom Blend Factors");
            public static readonly GUIContent blendFactorMultiplicative = EditorGUIUtility.TrTextContent("Multiplicative");
            public static readonly GUIContent blendFactorAdditive = EditorGUIUtility.TrTextContent("Additive");
            public static readonly GUIContent useDepthStencilBuffer = EditorGUIUtility.TrTextContent("Use Depth/Stencil Buffer", "Uncheck this when you are certain you don't use any feature that requires the depth/stencil buffer (e.g. Sprite Mask). Not using the depth/stencil buffer may improve performance, especially on mobile platforms.");
            public static readonly GUIContent postProcessData = EditorGUIUtility.TrTextContent("Post-processing Data", "Resources (textures, shaders, etc.) required by post-processing effects.");
        }

        struct LightBlendStyleProps
        {
            public SerializedProperty name;
            public SerializedProperty maskTextureChannel;
            public SerializedProperty renderTextureScale;
            public SerializedProperty blendMode;
            public SerializedProperty blendFactorMultiplicative;
            public SerializedProperty blendFactorAdditive;
        }


        SerializedProperty m_TransparencySortMode;
        SerializedProperty m_TransparencySortAxis;
        SerializedProperty m_HDREmulationScale;
        SerializedProperty m_LightBlendStyles;
        LightBlendStyleProps[] m_LightBlendStylePropsArray;
        SerializedProperty m_UseDepthStencilBuffer;
        SerializedProperty m_PostProcessData;
        SerializedProperty m_DefaultMaterialType;
        SerializedProperty m_DefaultCustomMaterial;

        Analytics.Renderer2DAnalytics m_Analytics = Analytics.Renderer2DAnalytics.instance;
        Renderer2DData m_Renderer2DData;
        bool m_WasModified;

        private SerializedProperty m_RendererFeatures;
        private SerializedProperty m_RendererFeaturesMap;
        private SerializedProperty m_FalseBool;
        [SerializeField] private bool falseBool = false;
        List<Editor> m_Editors = new List<Editor>();

        void SendModifiedAnalytics(Analytics.IAnalytics analytics)
        {
            if (m_WasModified)
            {
                Analytics.RendererAssetData modifiedData = new Analytics.RendererAssetData();
                modifiedData.instance_id = m_Renderer2DData.GetInstanceID();
                modifiedData.was_create_event = false;
                modifiedData.blending_layers_count = 0;
                modifiedData.blending_modes_used = 0;
                analytics.SendData(Analytics.AnalyticsDataTypes.k_Renderer2DDataString, modifiedData);
            }
        }

        void OnEnable()
        {
            m_WasModified = false;
            m_Renderer2DData = (Renderer2DData)serializedObject.targetObject;

            m_TransparencySortMode = serializedObject.FindProperty("m_TransparencySortMode");
            m_TransparencySortAxis = serializedObject.FindProperty("m_TransparencySortAxis");
            m_HDREmulationScale = serializedObject.FindProperty("m_HDREmulationScale");
            m_LightBlendStyles = serializedObject.FindProperty("m_LightBlendStyles");

            int numBlendStyles = m_LightBlendStyles.arraySize;
            m_LightBlendStylePropsArray = new LightBlendStyleProps[numBlendStyles];

            for (int i = 0; i < numBlendStyles; ++i)
            {
                SerializedProperty blendStyleProp = m_LightBlendStyles.GetArrayElementAtIndex(i);
                ref LightBlendStyleProps props = ref m_LightBlendStylePropsArray[i];

                props.name = blendStyleProp.FindPropertyRelative("name");
                props.maskTextureChannel = blendStyleProp.FindPropertyRelative("maskTextureChannel");
                props.renderTextureScale = blendStyleProp.FindPropertyRelative("renderTextureScale");
                props.blendMode = blendStyleProp.FindPropertyRelative("blendMode");
                props.blendFactorMultiplicative = blendStyleProp.FindPropertyRelative("customBlendFactors.multiplicative");
                props.blendFactorAdditive = blendStyleProp.FindPropertyRelative("customBlendFactors.additive");

                if (props.blendFactorMultiplicative == null)
                    props.blendFactorMultiplicative = blendStyleProp.FindPropertyRelative("customBlendFactors.modulate");
                if (props.blendFactorAdditive == null)
                    props.blendFactorAdditive = blendStyleProp.FindPropertyRelative("customBlendFactors.additve");
            }

            m_UseDepthStencilBuffer = serializedObject.FindProperty("m_UseDepthStencilBuffer");
            m_PostProcessData = serializedObject.FindProperty("m_PostProcessData");
            m_DefaultMaterialType = serializedObject.FindProperty("m_DefaultMaterialType");
            m_DefaultCustomMaterial = serializedObject.FindProperty("m_DefaultCustomMaterial");
            
            m_RendererFeatures = serializedObject.FindProperty(nameof(ScriptableRendererData.m_RendererFeatures));
            m_RendererFeaturesMap = serializedObject.FindProperty(nameof(ScriptableRendererData.m_RendererFeatureMap));
            var editorObj = new SerializedObject(this);
            m_FalseBool = editorObj.FindProperty(nameof(falseBool));
            UpdateEditorList();
        }

        private void OnDestroy()
        {
            SendModifiedAnalytics(m_Analytics);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            
            EditorGUILayout.PropertyField(m_TransparencySortMode, Styles.transparencySortMode);
            if(m_TransparencySortMode.intValue == (int)TransparencySortMode.CustomAxis)
                EditorGUILayout.PropertyField(m_TransparencySortAxis, Styles.transparencySortAxis);

            EditorGUILayout.PropertyField(m_HDREmulationScale, Styles.hdrEmulationScale);
            if (EditorGUI.EndChangeCheck() && m_HDREmulationScale.floatValue < 1.0f)
                m_HDREmulationScale.floatValue = 1.0f;

            EditorGUILayout.LabelField(Styles.lightBlendStyles);
            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            int numBlendStyles = m_LightBlendStyles.arraySize;
            for (int i = 0; i < numBlendStyles; ++i)
            {
                SerializedProperty blendStyleProp = m_LightBlendStyles.GetArrayElementAtIndex(i);
                ref LightBlendStyleProps props = ref m_LightBlendStylePropsArray[i];
                
                EditorGUILayout.BeginHorizontal();
                blendStyleProp.isExpanded = EditorGUILayout.Foldout(blendStyleProp.isExpanded, props.name.stringValue, true);
                EditorGUILayout.EndHorizontal();

                if (blendStyleProp.isExpanded)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(props.name, Styles.name);
                    EditorGUILayout.PropertyField(props.maskTextureChannel, Styles.maskTextureChannel);
                    EditorGUILayout.PropertyField(props.renderTextureScale, Styles.renderTextureScale);
                    EditorGUILayout.PropertyField(props.blendMode, Styles.blendMode);

                    if (props.blendMode.intValue == (int)Light2DBlendStyle.BlendMode.Custom)
                    {
                        EditorGUILayout.BeginHorizontal();

                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField(Styles.customBlendFactors, GUILayout.MaxWidth(200.0f));
                        EditorGUI.indentLevel--;

                        int oldIndentLevel = EditorGUI.indentLevel;
                        EditorGUI.indentLevel = 0;

                        EditorGUIUtility.labelWidth = 80.0f;
                        EditorGUILayout.PropertyField(props.blendFactorMultiplicative, Styles.blendFactorMultiplicative, GUILayout.MinWidth(110.0f));

                        GUILayout.Space(10.0f);

                        EditorGUIUtility.labelWidth = 50.0f;
                        EditorGUILayout.PropertyField(props.blendFactorAdditive, Styles.blendFactorAdditive, GUILayout.MinWidth(90.0f));

                        EditorGUIUtility.labelWidth = 0.0f;
                        EditorGUI.indentLevel = oldIndentLevel;
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUI.indentLevel--;
                }
            }

            
            EditorGUI.indentLevel--;
            EditorGUILayout.PropertyField(m_UseDepthStencilBuffer, Styles.useDepthStencilBuffer);
            EditorGUILayout.PropertyField(m_PostProcessData, Styles.postProcessData);

            EditorGUILayout.PropertyField(m_DefaultMaterialType, Styles.defaultMaterialType);
            if(m_DefaultMaterialType.intValue == (int)Renderer2DData.Renderer2DDefaultMaterialType.Custom)
                EditorGUILayout.PropertyField(m_DefaultCustomMaterial, Styles.defaultCustomMaterial);

            m_WasModified |= serializedObject.hasModifiedProperties;
            serializedObject.ApplyModifiedProperties();

            if (m_RendererFeatures == null)
                OnEnable();
            else if (m_RendererFeatures.arraySize != m_Editors.Count)
                UpdateEditorList();
            
            serializedObject.Update();
            DrawRendererFeatureList();
        }

        private void UpdateEditorList()
        {
            ClearEditorsList();
            for (int i = 0; i < m_RendererFeatures.arraySize; i++)
            {
                m_Editors.Add(CreateEditor(m_RendererFeatures.GetArrayElementAtIndex(i).objectReferenceValue));
            }
        }

        //To avoid leaking memory we destroy editors when we clear editors list
        private void ClearEditorsList()
        {
            for (int i = m_Editors.Count - 1; i >= 0; --i)
            {
                DestroyImmediate(m_Editors[i]);
            }

            m_Editors.Clear();
        }

        private void DrawRendererFeatureList()
        {
            EditorGUILayout.LabelField(Styles.RenderFeatures, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (m_RendererFeatures.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No Renderer Features added", MessageType.Info);
            }
            else
            {
                //Draw List
                CoreEditorUtils.DrawSplitter();
                for (int i = 0; i < m_RendererFeatures.arraySize; i++)
                {
                    SerializedProperty renderFeaturesProperty = m_RendererFeatures.GetArrayElementAtIndex(i);
                    DrawRendererFeature(i, ref renderFeaturesProperty);
                    CoreEditorUtils.DrawSplitter();
                }
            }

            EditorGUILayout.Space();

            //Add renderer
            if (GUILayout.Button("Add Renderer Feature", EditorStyles.miniButton))
            {
                AddPassMenu();
            }
        }
        
        private void AddComponent(object type)
        {
            serializedObject.Update();

            ScriptableObject component = CreateInstance((string)type);
            component.name = $"New{(string)type}";
            Undo.RegisterCreatedObjectUndo(component, "Add Renderer Feature");

            // Store this new effect as a sub-asset so we can reference it safely afterwards
            // Only when we're not dealing with an instantiated asset
            if (EditorUtility.IsPersistent(target))
            {
                AssetDatabase.AddObjectToAsset(component, target);
            }
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out var guid, out long localId);

            // Grow the list first, then add - that's how serialized lists work in Unity
            m_RendererFeatures.arraySize++;
            SerializedProperty componentProp = m_RendererFeatures.GetArrayElementAtIndex(m_RendererFeatures.arraySize - 1);
            componentProp.objectReferenceValue = component;

            // Update GUID Map
            m_RendererFeaturesMap.arraySize++;
            SerializedProperty guidProp = m_RendererFeaturesMap.GetArrayElementAtIndex(m_RendererFeaturesMap.arraySize - 1);
            guidProp.longValue = localId;
            UpdateEditorList();
            serializedObject.ApplyModifiedProperties();

            // Force save / refresh
            if (EditorUtility.IsPersistent(target))
            {
                ForceSave();
            }
            serializedObject.ApplyModifiedProperties();
        }
        
        private void ForceSave()
        {
            EditorUtility.SetDirty(target);
        }
        
        private void AddPassMenu()
        {
            GenericMenu menu = new GenericMenu();
            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<ScriptableRendererFeature>();
            foreach (Type type in types)
            {
                var data = target as ScriptableRendererData;
                if (data.DuplicateFeatureCheck(type))
                {
                    continue;
                }

                string path = GetMenuNameFromType(type);
                menu.AddItem(new GUIContent(path), false, AddComponent, type.Name);
            }
            menu.ShowAsContext();
        }
        
        private string GetMenuNameFromType(Type type)
        {
            var path = type.Name;
            if (type.Namespace != null)
            {
                if (type.Namespace.Contains("Experimental"))
                    path += " (Experimental)";
            }

            // Inserts blank space in between camel case strings
            return Regex.Replace(Regex.Replace(path, "([a-z])([A-Z])", "$1 $2", RegexOptions.Compiled),
                "([A-Z])([A-Z][a-z])", "$1 $2", RegexOptions.Compiled);
        }
        
        private string ValidateName(string name)
        {
            name = Regex.Replace(name, @"[^a-zA-Z0-9 ]", "");
            return name;
        }
        
        private void RemoveComponent(int id)
        {
            SerializedProperty property = m_RendererFeatures.GetArrayElementAtIndex(id);
            Object component = property.objectReferenceValue;
            property.objectReferenceValue = null;

            Undo.SetCurrentGroupName(component == null ? "Remove Renderer Feature" : $"Remove {component.name}");

            // remove the array index itself from the list
            m_RendererFeatures.DeleteArrayElementAtIndex(id);
            m_RendererFeaturesMap.DeleteArrayElementAtIndex(id);
            UpdateEditorList();
            serializedObject.ApplyModifiedProperties();

            // Destroy the setting object after ApplyModifiedProperties(). If we do it before, redo
            // actions will be in the wrong order and the reference to the setting object in the
            // list will be lost.
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }

            // Force save / refresh
            ForceSave();
        }

        private void MoveComponent(int id, int offset)
        {
            Undo.SetCurrentGroupName("Move Render Feature");
            serializedObject.Update();
            m_RendererFeatures.MoveArrayElement(id, id + offset);
            m_RendererFeaturesMap.MoveArrayElement(id, id + offset);
            UpdateEditorList();
            serializedObject.ApplyModifiedProperties();

            // Force save / refresh
            ForceSave();
        }
        
        private void OnContextClick(Vector2 position, int id)
        {
            var menu = new GenericMenu();

            if (id == 0)
                menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Up"));
            else
                menu.AddItem(EditorGUIUtility.TrTextContent("Move Up"), false, () => MoveComponent(id, -1));

            if (id == m_RendererFeatures.arraySize - 1)
                menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Down"));
            else
                menu.AddItem(EditorGUIUtility.TrTextContent("Move Down"), false, () => MoveComponent(id, 1));

            menu.AddSeparator(string.Empty);
            menu.AddItem(EditorGUIUtility.TrTextContent("Remove"), false, () => RemoveComponent(id));

            menu.DropDown(new Rect(position, Vector2.zero));
        }
        
        private void DrawRendererFeature(int index, ref SerializedProperty renderFeatureProperty)
        {
            Object rendererFeatureObjRef = renderFeatureProperty.objectReferenceValue;
            if (rendererFeatureObjRef != null)
            {
                bool hasChangedProperties = false;
                string title = ObjectNames.GetInspectorTitle(rendererFeatureObjRef);

                // Get the serialized object for the editor script & update it
                Editor rendererFeatureEditor = m_Editors[index];
                SerializedObject serializedRendererFeaturesEditor = rendererFeatureEditor.serializedObject;
                serializedRendererFeaturesEditor.Update();

                // Foldout header
                EditorGUI.BeginChangeCheck();
                SerializedProperty activeProperty = serializedRendererFeaturesEditor.FindProperty("m_Active");
                bool displayContent = CoreEditorUtils.DrawHeaderToggle(title, renderFeatureProperty, activeProperty, pos => OnContextClick(pos, index));
                hasChangedProperties |= EditorGUI.EndChangeCheck();

                // ObjectEditor
                if (displayContent)
                {
                    EditorGUI.BeginChangeCheck();
                    SerializedProperty nameProperty = serializedRendererFeaturesEditor.FindProperty("m_Name");
                    nameProperty.stringValue = ValidateName(EditorGUILayout.DelayedTextField(Styles.PassNameField, nameProperty.stringValue));
                    if (EditorGUI.EndChangeCheck())
                    {
                        hasChangedProperties = true;

                        // We need to update sub-asset name
                        rendererFeatureObjRef.name = nameProperty.stringValue;
                        AssetDatabase.SaveAssets();

                        // Triggers update for sub-asset name change
                        ProjectWindowUtil.ShowCreatedAsset(target);
                    }

                    EditorGUI.BeginChangeCheck();
                    rendererFeatureEditor.OnInspectorGUI();
                    hasChangedProperties |= EditorGUI.EndChangeCheck();

                    EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                }

                // Apply changes and save if the user has modified any settings
                if (hasChangedProperties)
                {
                    serializedRendererFeaturesEditor.ApplyModifiedProperties();
                    serializedObject.ApplyModifiedProperties();
                    ForceSave();
                }
            }
            else
            {
                CoreEditorUtils.DrawHeaderToggle(Styles.MissingFeature,renderFeatureProperty, m_FalseBool,pos => OnContextClick(pos, index));
                m_FalseBool.boolValue = false; // always make sure false bool is false
                EditorGUILayout.HelpBox(Styles.MissingFeature.tooltip, MessageType.Error);
                if (GUILayout.Button("Attempt Fix", EditorStyles.miniButton))
                {
                    ScriptableRendererData data = target as ScriptableRendererData;
                    data.ValidateRendererFeatures();
                }
            }
        }
    }
}
