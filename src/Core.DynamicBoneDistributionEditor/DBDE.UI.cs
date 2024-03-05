using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationCurveEditor.AnimationCurveEditor;
using static UnityEngine.GUI;
using KKAPI;

namespace DynamicBoneDistributionEditor
{
	public class DBDEUI : MonoBehaviour
	{
        /// <summary>
        /// Function used to get DB List with Key
        /// </summary>
		private Func<List<DBDEDynamicBoneEdit>> DBDEGetter = null;
        /// <summary>
        /// Index of DB in List
        /// </summary>
		private int  currentIndex = 0;
        /// <summary>
        /// If hasValue display ACE
        /// </summary>
        private int? currentEdit = null;

        private RenderTexture rTex;
        private Camera rCam;

        private readonly String[] DistribKindNames = new string[5] { "Damping", "Elasticity", "Interia", "Radius", "Stiffness" };

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

        /// <summary>
        /// Opens the UI. Pass a Func that returns a list of the bones that should be editable.
        /// </summary>
        /// <param name="DBDEGetter"></param>
        public void Open(Func<List<DBDEDynamicBoneEdit>> DBDEGetter)
        {
            this.DBDEGetter = DBDEGetter;
        }

        private void OnGUI()
        {
            if (currentEdit.HasValue) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rTex);
            if (DBDEGetter != null && !DBDEGetter.Invoke().IsNullOrEmpty())
            {
                windowRect = GUI.Window(5858350, windowRect, WindowFunction, "DBDE - Item", KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.window);
            }
            if (currentEdit.HasValue) rCam.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>()?.OnGUI();
        }

        private Rect windowRect = new Rect(100, 100, 400, 300);
        private Vector2 LeftSideScroll = new Vector2();

        private void WindowFunction(int WindowID)
        {
            #region GUI Skins
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.padding = new RectOffset(0, 0, 0, 0);
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            GUIStyle BoneButtonStyleEdited = new GUIStyle(GUI.skin.button);
            BoneButtonStyleEdited.normal.textColor = Color.magenta;
            GUIStyle BoneButtonStyleSelected = new GUIStyle(GUI.skin.button);
            BoneButtonStyleSelected.normal.textColor = new Color(255, 145, 0);
            GUIStyle HeadStyle = new GUIStyle(GUI.skin.box);
            HeadStyle.alignment = TextAnchor.MiddleCenter;
            GUIStyle LabelStyle = new GUIStyle(GUI.skin.box);
            LabelStyle.alignment = TextAnchor.MiddleLeft;
            GUIStyle LabelStyleEdited = new GUIStyle(LabelStyle);
            LabelStyleEdited.normal.textColor = Color.magenta;
            GUIStyle LableStyleSelected = new GUIStyle(LabelStyle);
            LabelStyleEdited.normal.textColor = new Color(255, 145, 0);
            #endregion
            if (GUI.Button(new Rect(windowRect.width - 18, 2, 15, 15), "X", buttonStyle))
            {
                DBDEGetter = null;
            }

            GUI.Box(new Rect(windowRect.position + new Vector2(5, 15), new Vector2(windowRect.width / 7 * 3 + 10, windowRect.height - 20)), "");
            GUI.Box(new Rect(windowRect.position + new Vector2(5 + (windowRect.width / 7 * 3 + 10), 15), new Vector2(windowRect.width - (windowRect.width / 7 * 3 + 10), windowRect.height - 20)), "");

            List<DBDEDynamicBoneEdit> DBDES = DBDEGetter.Invoke();

            GUILayout.BeginArea(new Rect(new Vector2(10, 20), windowRect.size - new Vector2(20, 25)));
            GUILayout.BeginHorizontal();
            #region Left Side
            LeftSideScroll = GUILayout.BeginScrollView(LeftSideScroll, GUILayout.Width((windowRect.width / 7) * 3)); // width 3/7
            GUILayout.BeginVertical();
            for (int i = 0; i < DBDES.Count; i++)
            {
                DBDEDynamicBoneEdit db = DBDES[i];
                if (GUILayout.Button(db.dynamicBone.m_Root.name + (db.IsEdited() ? "*" : ""),
                    currentIndex == i ? BoneButtonStyleSelected : db.IsEdited() ? BoneButtonStyleEdited : GUI.skin.button))
                {
                    currentIndex = i;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            #endregion
            #region Right Side
            GUILayout.BeginVertical();
            DBDEDynamicBoneEdit Editing = DBDES[currentIndex];
            //Dampening
            GUILayout.Label(Editing.dynamicBone.m_Root.name);
            for (int i = 0; i < 5; i++)
            {
                int num = i;
                GUILayout.BeginHorizontal();
                GUILayout.Label(DistribKindNames[i] + (Editing.distributions[num].HasEdits ? "*" : ""),
                    currentEdit == num ? LableStyleSelected : Editing.distributions[num].HasEdits ? LabelStyleEdited : LabelStyle,
                    GUILayout.Width((windowRect.width / 6))); // width 1/6
                if (GUILayout.Button("Open")) // remaining width (about 1/4)
                {
                    AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>();
                    Rect r = new Rect(Screen.width / 2, Screen.height / 2, 500, 300);
                    if (currentEdit.HasValue) r = ace.rect;
                    ace.Init(Editing.GetAnimationCurve((byte)i), r, 2, 0, 0.5f);
                    ace.enabled = true;
                    ace.displayName = Editing.dynamicBone.m_Root.name + " - " + DistribKindNames[num];
                    ace.KeyframeEdited = new EventHandler<KeyframeEditedArgs>((object o, KeyframeEditedArgs e) =>
                    {
                        Editing.SetDistribution(num, e.curve);
                        Editing.Apply(num);
                    });
                    ace.EditorClosedEvent = new EventHandler<EditorClosedArgs>((object o, EditorClosedArgs e) =>
                    {
                        Editing.SetDistribution(num, e.curve);
                        Editing.Apply(num);
                        currentEdit = null;
                    });
                    currentEdit = num;
                }
                if (GUILayout.Button("Reset", GUILayout.Width((windowRect.width / 7)))) // width 1/7
                {
                    Editing.Reset(i);
                    if (num == currentEdit)
                    {
                        AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetComponent<AnimationCurveEditor.AnimationCurveEditor>();
                        Rect r = ace.rect;
                        ace?.Init(Editing.GetAnimationCurve((byte)i), r, 2, 0, 0.5f);
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

