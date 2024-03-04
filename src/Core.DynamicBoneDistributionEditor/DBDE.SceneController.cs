using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using KKAPI;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using Studio;
using UnityEngine;
using ExtensibleSaveFormat;
using MessagePack;
using AnimationCurveEditor;
using static AnimationCurveEditor.AnimationCurveEditor;

namespace DynamicBoneDistributionEditor
{
    public class DBDESceneController : SceneCustomFunctionController
    {
        Dictionary<int, List<DBDEDynamicBoneEdit>> DistributionEdits = new Dictionary<int, List<DBDEDynamicBoneEdit>>();

        private RenderTexture rTex;
        private Camera rCam;

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            if (operation == SceneOperationKind.Load || operation == SceneOperationKind.Clear) DistributionEdits.Clear();

            PluginData data = GetExtendedData();
            if (data.data.TryGetValue("Edits", out var bytes) && bytes != null)
            {
                Dictionary<int, List<byte[]>> intermediate = MessagePackSerializer.Deserialize<Dictionary<int, List<byte[]>>>((byte[])bytes);

                foreach(int oldKey in intermediate.Keys)
                {
                    if (loadedItems[oldKey] is OCIItem oci)
                    {
                        if (!DistributionEdits.ContainsKey(oci.itemInfo.dicKey)) DistributionEdits.Add(oci.itemInfo.dicKey, new List<DBDEDynamicBoneEdit>());
                        for (int i = 0; i < oci.dynamicBones.Length; i++)
                        {
                            DistributionEdits[oci.itemInfo.dicKey].Add(new DBDEDynamicBoneEdit(oci.dynamicBones[i], intermediate[oldKey][i]));
                        }
                    }
                }
            }
            // Apply Edits on all DBs 
            DistributionEdits.Values.ToList().ForEach(x => x.ForEach(y => y.Apply()));
        }

        void Start()
        {
            rTex = new RenderTexture(Screen.width, Screen.height, 32);
            // Create a new camera
            GameObject renderCameraObject = new GameObject("DBDE_UI_Camera");
            renderCameraObject.transform.SetParent(this.transform);
            rCam = renderCameraObject.AddComponent<Camera>();
            rCam.targetTexture = rTex;
            rCam.clearFlags = CameraClearFlags.SolidColor;
            rCam.backgroundColor = Color.clear;
            rCam.cullingMask = 0;
        }

        protected override void OnSceneSave()
        {
            PluginData data = new PluginData();

            data.data.Add("Edits", MessagePackSerializer.Serialize(DistributionEdits.Select(e => new KeyValuePair<int, List<byte[]>>(e.Key, e.Value.Select(de => de.Sersialise()).ToList())).ToDictionary(x => x.Key, x => x.Value)));
            SetExtendedData(data);
        }

        internal void OpenDBDE(int dictKey)
        {
            ObjectCtrlInfo objectCtrlInfo = Studio.Studio.GetCtrlInfo(dictKey);
            if (objectCtrlInfo is OCIItem oci)
            {
                OpenDBDE(oci);
            }
        }

        public void OpenDBDE(OCIItem oci)
        {
            if (!DistributionEdits.ContainsKey(oci.itemInfo.dicKey))
            {
                DistributionEdits.Add(oci.itemInfo.dicKey, new List<DBDEDynamicBoneEdit>());
                for(int i = 0; i < oci.dynamicBones.Length; i++)
                {
                    DynamicBone db = oci.dynamicBones[i];
                    DBDE.Logger.LogInfo($"Adding DynamicBone: Key={oci.itemInfo.dicKey} | DB={db.name} | {oci.dynamicBones.Length}");
                    DistributionEdits[oci.itemInfo.dicKey].Add(new DBDEDynamicBoneEdit(db));
                }
            }
            currentlyDisplayedObject = oci.itemInfo.dicKey;
            currentDisplayedDBIndex = 0;
            currentDisplayedDistributionKind = -1;
        }


        private void OnGUI()
        {
            if (currentDisplayedDistributionKind > -1) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rTex);
            if (currentlyDisplayedObject.HasValue && DistributionEdits.ContainsKey(currentlyDisplayedObject.Value))
            {
                windowRect = GUI.Window(5858350, windowRect, WindowFunction, "DBDE - Item", KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.window);
            }
            if (currentDisplayedDistributionKind > -1) rCam.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>()?.OnGUI();
        }

        private int? currentlyDisplayedObject = null;
        private Vector2 LeftSideScroll = new Vector2();
        private int currentDisplayedDBIndex = 0;
        private int currentDisplayedDistributionKind = -1;
        private Rect windowRect = new Rect(100, 100, 400, 300);

        private String[] names = new string[5] { "Damping", "Elasticity", "Interia", "Radius", "Stiffness"};

        private void WindowFunction(int WindowID)
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.padding = new RectOffset(0, 0, 0, 0);
            buttonStyle.alignment = TextAnchor.MiddleCenter;

            if (GUI.Button(new Rect(windowRect.width - 18, 2, 15, 15), "X", buttonStyle))
            {
                currentlyDisplayedObject = null;
            }

            GUILayout.BeginArea(new Rect(new Vector2(10,20), windowRect.size - new Vector2(20, 25)));
            GUILayout.BeginHorizontal();
            #region Left Side
            LeftSideScroll = GUILayout.BeginScrollView(LeftSideScroll, GUILayout.Width((windowRect.width / 6) * 2));
            GUILayout.BeginVertical();
            for(int i = 0; i < DistributionEdits[currentlyDisplayedObject.Value].Count; i++)
            {
                DBDEDynamicBoneEdit db = DistributionEdits[currentlyDisplayedObject.Value][i];
                if (GUILayout.Button(db.dynamicBone.m_Root.name))
                {
                    currentDisplayedDBIndex = i;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            #endregion
            #region Right Side
            GUILayout.BeginVertical();
            DBDEDynamicBoneEdit Editing = DistributionEdits[currentlyDisplayedObject.Value][currentDisplayedDBIndex];
            //Dampening
            GUILayout.Label(Editing.dynamicBone.m_Root.name);
            for (int i = 0; i < 5; i++)
            {
                int num = i;
                GUILayout.BeginHorizontal();
                GUILayout.Label(names[i], GUILayout.Width((windowRect.width / 6)));
                if (GUILayout.Button("Open"))
                {
                    AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>();
                    Rect r = new Rect(Screen.width / 2, Screen.height / 2, 500, 300);
                    if (currentDisplayedDistributionKind > -1) r = ace.rect;
                    ace.Init(Editing.GetAnimationCurve((byte)i), r, 2, 0, 0.5f);
                    ace.enabled = true;
                    ace.displayName = Editing.dynamicBone.m_Root.name + " - " + names[num];
                    ace.KeyframeEdited = new EventHandler<KeyframeEditedArgs>((object o, KeyframeEditedArgs e) => 
                    {
                        Editing.SetDistribution(num, e.curve);
                        Editing.Apply();
                    });
                    ace.EditorClosedEvent = new EventHandler<EditorClosedArgs>((object o, EditorClosedArgs e) =>
                    {
                        Editing.SetDistribution(num, e.curve);
                        Editing.Apply();
                        currentDisplayedDistributionKind = -1;
                    });
                    currentDisplayedDistributionKind = num;
                }
                if (GUILayout.Button("Reset", GUILayout.Width((windowRect.width / 6))))
                {
                    Editing.Reset(i);
                    if (num == currentDisplayedDistributionKind)
                    {
                        AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetComponent<AnimationCurveEditor.AnimationCurveEditor>();
                        Rect r = ace.rect;
                        if (ace != null)
                        {
                            ace.Init(Editing.GetAnimationCurve((byte)i), r, 2, 0, 0.5f);
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            #endregion
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            windowRect = KKAPI.Utilities.IMGUIUtils.DragResizeEatWindow(WindowID, windowRect);
        }
    }
}

