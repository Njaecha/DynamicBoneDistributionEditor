using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationCurveEditor.AnimationCurveEditor;
using static UnityEngine.GUI;
using KKAPI;
using static AnimationCurveEditor.AnimationCurveEditor.KeyframeEditedArgs;

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
        /// <summary>
        /// Holds information helping with editing the base values
        /// </summary>
        private BaseValueEditWrapper[] BaseValueWrappers;
        /// <summary>
        /// Clipboard used to copy settings
        /// </summary>
        private ClipboardEntry Clipboard;

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
            currentIndex = 0;
        }

        void Update()
        {
            if (KKAPI.Maker.MakerAPI.InsideAndLoaded && currentEdit.HasValue)
            {
                AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>();
                if (ace != null && ace.eatingInput && DBDE.Instance.getMakerCursorMangaer() != null && DBDE.Instance.getMakerCursorMangaer().isActiveAndEnabled == true)
                {
                    DBDE.Instance.getMakerCursorMangaer().enabled = false;
                }
                if (ace != null && !ace.eatingInput && DBDE.Instance.getMakerCursorMangaer() != null && DBDE.Instance.getMakerCursorMangaer().isActiveAndEnabled == false)
                {
                    DBDE.Instance.getMakerCursorMangaer().enabled = true;
                }

            }
        }

        private void close()
        {
            DBDEGetter = null;
            currentIndex = 0;
            currentEdit = null;
            if (KKAPI.Maker.MakerAPI.InsideAndLoaded) DBDE.toggle.Value = false;
        }

        private void OnGUI()
        {
            if (currentEdit.HasValue) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rTex);
            if (DBDEGetter != null && !DBDEGetter.Invoke().IsNullOrEmpty())
            {
                windowRect = GUI.Window(5858350, windowRect, WindowFunction, $"DynamicBoneDistributionEditor v{DBDE.Version}", KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.window);
            }
            if (currentEdit.HasValue) rCam.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>()?.OnGUI();
        }

        private Rect lastUsedAceRect = new Rect(Screen.width / 2, Screen.height / 2, 500, 300);
        private AnimationCurveEditor.AnimationCurveEditor GetOrAddACE(AnimationCurve curve, DBDEDynamicBoneEdit Editing, int num)
        {
            AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>();
            if (currentEdit.HasValue) lastUsedAceRect = ace.rect; // if ACE is already opened before this method was called.
            ace.Init(curve, lastUsedAceRect, 2, 0, 0.5f);
            ace.enabled = true;
            ace.displayName = Editing.dynamicBone.m_Root.name + " - " + DistribKindNames[num];
            ace.KeyframeEdited = new EventHandler<KeyframeEditedArgs>((object o, KeyframeEditedArgs e) =>
            {
                Editing.SetAnimationCurve(num, e.curve);
                Editing.ApplyDistribution(num);
            });
            ace.EditorClosedEvent = new EventHandler<EditorClosedArgs>((object o, EditorClosedArgs e) =>
            {
                Editing.SetAnimationCurve(num, e.curve);
                Editing.ApplyDistribution(num);
                lastUsedAceRect = e.rect;
                currentEdit = null;
            });
            return ace;
        }

        private void SetCurrentRightSide(int boneIndex)
        {
            var DBDES = DBDEGetter.Invoke();
            if (DBDES.IsNullOrEmpty()) return;
            DynamicBone db = DBDES[currentIndex].dynamicBone;
            if (db == null) return;
            currentIndex = boneIndex;
            BaseValueWrappers = new BaseValueEditWrapper[]
            {
                new BaseValueEditWrapper(db.m_Damping),
                new BaseValueEditWrapper(db.m_Elasticity),
                new BaseValueEditWrapper(db.m_Inert),
                new BaseValueEditWrapper(db.m_Radius),
                new BaseValueEditWrapper(db.m_Stiffness)
            };
        }

        private Rect windowRect = new Rect(100, 100, 400, 300);
        private Vector2 LeftSideScroll = new Vector2();
        private void WindowFunction(int WindowID)
        {
            #region GUI Skins
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.padding = new RectOffset(0, 0, 0, 0);
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            GUIStyle HeadStyle = new GUIStyle(GUI.skin.box);
            HeadStyle.alignment = TextAnchor.MiddleCenter;
            GUIStyle LabelStyle = new GUIStyle(GUI.skin.box);
            LabelStyle.alignment = TextAnchor.MiddleCenter;
            Color guic = GUI.color;
            #endregion
            if (GUI.Button(new Rect(windowRect.width - 18, 2, 15, 15), "X", buttonStyle))
            {
                close();
                return;
            }

            GUI.Box(new Rect(new Vector2(5, 20), new Vector2(windowRect.width / 7 * 3 + 5, windowRect.height - 25)), "");
            GUI.Box(new Rect(new Vector2(5 + (windowRect.width / 7 * 3 + 5), 20), new Vector2(windowRect.width - (windowRect.width / 7 * 3 + 10)-5, windowRect.height - 25)), "");
            List<DBDEDynamicBoneEdit> DBDES = new List<DBDEDynamicBoneEdit>();
            try { DBDES = DBDEGetter.Invoke(); } catch(Exception) { }
            if (DBDES.IsNullOrEmpty()) close();
            GUILayout.BeginArea(new Rect(new Vector2(5, 20), windowRect.size - new Vector2(10, 25)));
            GUILayout.BeginHorizontal();
            #region Left Side
            LeftSideScroll = GUILayout.BeginScrollView(LeftSideScroll, GUILayout.Width((windowRect.width / 7) * 3 + 5)); // width 3/7
            GUILayout.BeginVertical();
            for (int i = 0; i < DBDES.Count; i++)
            {
                DBDEDynamicBoneEdit db = DBDES[i];
                GUI.color = currentIndex == i ? Color.cyan : db.IsEdited() ? Color.magenta : guic;
                if (GUILayout.Button(db.dynamicBone.m_Root.name + (db.IsEdited() ? "*" : "")))
                {
                    SetCurrentRightSide(i);
                }
                GUI.color = guic;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            #endregion

            #region Right Side
            DBDEDynamicBoneEdit Editing = DBDES[currentIndex];
            GUILayout.Label(Editing.dynamicBone.m_Root.name, HeadStyle); // header
            GUILayout.BeginVertical(); // bone settings
            for (int i = 0; i < 5; i++) // for "Damping", "Elasticity", "Interia", "Radius", "Stiffness"
            {
                int num = i;
                GUILayout.BeginHorizontal();
                GUI.color = currentEdit == num ? Color.cyan : Editing.distributions[num].IsEdited ? Color.magenta : guic;
                GUILayout.Label(DistribKindNames[i] + (Editing.distributions[num].IsEdited ? "*" : ""), LabelStyle,GUILayout.Width((windowRect.width / 6))); // width 1/6
                GUI.color = guic;
                #region Inputs
                GUILayout.BeginVertical(); // two rows
                // top
                GUILayout.BeginHorizontal();
                if (BaseValueWrappers[num].active) // draw input field
                {
                    BaseValueWrappers[num].text = GUILayout.TextField(BaseValueWrappers[num].text, GUILayout.Width(60));

                } else // draw button
                {
                    if (GUILayout.Button(((float)Editing.baseValues[num]).ToString("0.000"), GUILayout.Width(60)))
                    {
                        BaseValueWrappers[num].Activate(Editing.baseValues[num]);
                    }
                }
                if (Clipboard != null)
                {
                    if (Clipboard.distribIndex == num && Clipboard.boneIndex == currentIndex) // draw clear button
                    {
                        if (GUILayout.Button("Clear")) Clipboard = null;
                    }
                    else if (Clipboard.data is float value) // draw paste button
                    {
                        if (GUILayout.Button("Paste"))
                        {
                            Editing.baseValues[num] = value;
                            Editing.ApplyBaseValues(num);
                            if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                        }
                    }
                    else // draw disabled copy button
                    {
                        GUI.enabled = false;
                        GUILayout.Button("Copy");
                        GUI.enabled = true;
                    }
                }
                else // draw enabled copy button
                {
                    if (GUILayout.Button("Copy"))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, num, Editing.GetAnimationCurve((byte)num));
                    }
                }
                if (GUILayout.Button("R", GUILayout.Width(25)))
                {
                    Editing.ResetBaseValues(i);
                }
                GUILayout.EndHorizontal();
                // bottom
                GUILayout.BeginHorizontal();
                if (BaseValueWrappers[num].active) // draw slider
                {
                    if (GUILayout.Button("✓", GUILayout.Width(25)))
                    {
                        Editing.baseValues[num] = BaseValueWrappers[num].value;
                        BaseValueWrappers[num].active = false;
                        Editing.ApplyBaseValues(num);
                    }
                    BaseValueWrappers[num].value = GUILayout.HorizontalSlider(BaseValueWrappers[num].value, 0f, 1f);
                }
                else // draw distribution buttons
                {
                    if (GUILayout.Button("Open")) // remaining width (about 1/4)
                    {
                        GetOrAddACE(Editing.GetAnimationCurve((byte)i), Editing, num);
                        currentEdit = num;
                    }
                    if (Clipboard != null)
                    {
                        if (Clipboard.distribIndex == num && Clipboard.boneIndex == currentIndex) // draw clear button
                        {
                            if (GUILayout.Button("Clear")) Clipboard = null;
                        } else if (Clipboard.data is AnimationCurve curve) // draw paste button
                        {
                            if (GUILayout.Button("Paste"))
                            {
                                Editing.SetAnimationCurve(num, curve);
                                Editing.ApplyDistribution(num);
                                if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                            }
                        }
                        else // draw disabled copy button
                        {
                            GUI.enabled = false;
                            GUILayout.Button("Copy");
                            GUI.enabled = true;
                        }
                    }
                    else // draw enabled copy button
                    {
                        if (GUILayout.Button("Copy"))
                        {
                            Clipboard = new ClipboardEntry(currentIndex, num, Editing.GetAnimationCurve((byte)num));
                        }
                    }
                    if (GUILayout.Button("R", GUILayout.Width(25)))
                    {
                        Editing.ResetDistribution(i);
                        if (num == currentEdit)
                        {
                            AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetComponent<AnimationCurveEditor.AnimationCurveEditor>();
                            ace?.Init(Editing.GetAnimationCurve((byte)i), ace.rect, 2, 0, 0.5f);
                        }
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                #endregion
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
            }
            GUILayout.EndVertical();
            GUILayout.BeginHorizontal(GUILayout.Height(30));// bone level buttons
            if (Clipboard != null && Clipboard.boneIndex != currentIndex && Clipboard.data is DBDEDynamicBoneEdit copied) // draw Paste button
            {
                if (GUILayout.Button("Paste All"))
                {
                    Editing.PasteData(copied);
                }
            }
            else // draw Copy button
            {
                if (GUILayout.Button("Copy All"))
                {
                    Clipboard = new ClipboardEntry(currentIndex, -1, Editing);
                }
            }
            if (GUILayout.Button("Reset All"))
            {
                Editing.ResetBaseValues();
                Editing.ResetDistribution();
            }
            if (GUILayout.Button("Clear Clipboard"))
            {
                Clipboard = null;
            }
            GUILayout.EndHorizontal();
            #endregion

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            windowRect = KKAPI.Utilities.IMGUIUtils.DragResizeEatWindow(WindowID, windowRect);
        }

        private class BaseValueEditWrapper
        {
            private string _text;
            public string text { get => _text; set => setText(value); }
            public float value;
            public bool active;

            public BaseValueEditWrapper(float baseValue)
            {
                text = baseValue.ToString("0.000");
                value = baseValue;
                active = false;
            }
            public void Activate(float baseValue)
            {
                text = baseValue.ToString("0.000");
                value = baseValue;
                active = true;
            }

            private void setText(string text)
            {
                if (_text != text)
                {
                    if (float.TryParse(text, out float v) && v >= 0f && v <= 1f) value = v;
                }
                _text = text;
            }
        }
        private class ClipboardEntry
        {
            public int boneIndex;
            public int distribIndex;
            public object data;

            public ClipboardEntry(int bone, int distrib, object data)
            {
                boneIndex = bone;
                distribIndex = distrib;
                this.data = data;
            }
        }
    }
}

