using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationCurveEditor.AnimationCurveEditor;

namespace DynamicBoneDistributionEditor
{
	public class DBDEUI : MonoBehaviour
	{
        private const int COPYBUTTONWIDTH = 55;

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

        private readonly string[] DistribKindNames = new string[5] { "Damping", "Elasticity", "Interia", "Radius", "Stiffness" };

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
            SetCurrentRightSide(0);
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
            DBDEDynamicBoneEdit Editing = DBDES[boneIndex];
            if (Editing == null) return;
            DynamicBone db = Editing.dynamicBone;
            if (db == null) return;
            currentIndex = boneIndex;
            BaseValueWrappers = new BaseValueEditWrapper[]
            {
                new BaseValueEditWrapper(db.m_Damping, (v) => { Editing.baseValues[0].value = v; Editing.ApplyBaseValues(0); }),
                new BaseValueEditWrapper(db.m_Elasticity, (v) => { Editing.baseValues[1].value = v; Editing.ApplyBaseValues(1); }),
                new BaseValueEditWrapper(db.m_Inert, (v) => { Editing.baseValues[2].value = v; Editing.ApplyBaseValues(2); }),
                new BaseValueEditWrapper(db.m_Radius, (v) => { Editing.baseValues[3].value = v; Editing.ApplyBaseValues(3); }),
                new BaseValueEditWrapper(db.m_Stiffness, (v) => { Editing.baseValues[4].value = v; Editing.ApplyBaseValues(4); })
            };
        }

        private Rect windowRect = new Rect(100, 100, 550, 450);
        private Vector2 LeftSideScroll = new Vector2();
        private Vector2 RightSideScroll = new Vector2();
        private void WindowFunction(int WindowID)
        {
            #region GUI Skins
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.padding = new RectOffset(0, 0, 0, 0);
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            GUIStyle HeadStyle = new GUIStyle(GUI.skin.box);
            HeadStyle.alignment = TextAnchor.MiddleCenter;
            GUIStyle LabelStyle = new GUIStyle(GUI.skin.box);
            LabelStyle.alignment = TextAnchor.MiddleCenter;
            LabelStyle.fontSize = 15;
            Color guic = GUI.color;
            #endregion

            if (GUI.Button(new Rect(windowRect.width - 18, 2, 15, 15), "X", buttonStyle))
            {
                close();
                return;
            }

            GUI.Box(new Rect(new Vector2(5, 20), new Vector2(windowRect.width / 6 * 2 + 5, windowRect.height - 25)), "");
            GUI.Box(new Rect(new Vector2(5 + (windowRect.width / 6 * 2 + 5), 20), new Vector2(windowRect.width - (windowRect.width / 6 * 2 + 10)-5, windowRect.height - 25)), "");
            List<DBDEDynamicBoneEdit> DBDES = new List<DBDEDynamicBoneEdit>();
            try { DBDES = DBDEGetter.Invoke(); } catch(Exception) { }
            if (DBDES.IsNullOrEmpty()) close();
            GUILayout.BeginArea(new Rect(new Vector2(5, 20), windowRect.size - new Vector2(15, 22)));
            GUILayout.BeginHorizontal();
            #region Left Side
            LeftSideScroll = GUILayout.BeginScrollView(LeftSideScroll, GUILayout.Width(windowRect.width / 6 * 2 + 5)); // width 2/6
            GUILayout.BeginVertical();
            for (int i = 0; i < DBDES.Count; i++)
            {
                DBDEDynamicBoneEdit db = DBDES[i];
                GUI.color = currentIndex == i ? Color.cyan : db.IsEdited() ? Color.magenta : guic;
                if (GUILayout.Button(db.GetButtonName() + (db.IsEdited() ? "*" : "")))
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
            GUILayout.BeginVertical(); // bone settings
            GUILayout.BeginHorizontal(); // header
            GUILayout.Label(Editing.dynamicBone.m_Root.name, HeadStyle);
            if (GUILayout.Button(Editing.active ? "☑" : "☐", buttonStyle, GUILayout.Width(25), GUILayout.Height(25)))
            {
                Editing.active = !Editing.active;
            }
            GUILayout.EndHorizontal();
            RightSideScroll = GUILayout.BeginScrollView(RightSideScroll);
            for (int i = 0; i < 5; i++) // for "Damping", "Elasticity", "Interia", "Radius", "Stiffness"
            {
                int num = i;
                GUILayout.BeginHorizontal();
                GUI.color = currentEdit == num ? Color.cyan : Editing.IsEdited(num) ? Color.magenta : guic;
                GUILayout.Label(DistribKindNames[i] + (Editing.IsEdited(num) ? "*" : ""), LabelStyle,GUILayout.Width(windowRect.width / 6), GUILayout.Height(50)); // width 1/6
                GUI.color = guic;
                #region Inputs
                GUILayout.BeginVertical(); // two rows
                // top
                GUILayout.BeginHorizontal(GUILayout.Height(25));
                if (BaseValueWrappers[num].Active) // draw input field
                {
                    BaseValueWrappers[num].Text = GUILayout.TextField(BaseValueWrappers[num].Text);

                } else // draw button
                {
                    if (GUILayout.Button("Value: "+((float)Editing.baseValues[num]).ToString("0.000")))
                    {
                        BaseValueWrappers[num].Activate(Editing.baseValues[num]);
                    }
                }
                if (Clipboard != null)
                {
                    if (Clipboard.distribIndex == num && Clipboard.boneIndex == currentIndex) // draw clear button
                    {
                        if (GUILayout.Button("Clear", GUILayout.Width(COPYBUTTONWIDTH))) Clipboard = null;
                    }
                    else if (Clipboard.data is float value) // draw paste button
                    {
                        if (GUILayout.Button("Paste", GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Editing.baseValues[num].value = value;
                            Editing.ApplyBaseValues(num);
                            if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                        }
                    }
                    else // draw disabled copy button
                    {
                        GUI.enabled = false;
                        GUILayout.Button("Copy", GUILayout.Width(COPYBUTTONWIDTH));
                        GUI.enabled = true;
                    }
                }
                else // draw enabled copy button
                {
                    if (GUILayout.Button("Copy", GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, num, Editing.GetAnimationCurve((byte)num));
                    }
                }
                if (Editing.baseValues[i].IsEdited) GUI.color = Color.magenta;
                else GUI.enabled = false;
                if (GUILayout.Button("R", GUILayout.Width(25)))
                {
                    Editing.ResetBaseValues(i);
                    BaseValueWrappers[i].Value = Editing.baseValues[i].value;
                }
                GUI.enabled = true;
                GUI.color = guic;
                GUILayout.EndHorizontal();
                // bottom
                GUILayout.BeginHorizontal(GUILayout.Height(25));
                if (BaseValueWrappers[num].Active) // draw slider
                {
                    BaseValueWrappers[num].Value = GUILayout.HorizontalSlider(BaseValueWrappers[num].Value, 0f, 1f);
                    GUI.color = Color.green;
                    if (GUILayout.Button("✓", GUILayout.Width(25)))
                    {
                        BaseValueWrappers[num].Active = false;
                    }
                    GUI.color = guic;
                }
                else // draw distribution buttons
                {
                    if (GUILayout.Button("Edit Distribution"))
                    {
                        GetOrAddACE(Editing.GetAnimationCurve((byte)i), Editing, num);
                        currentEdit = num;
                    }
                    if (Clipboard != null)
                    {
                        if (Clipboard.distribIndex == num && Clipboard.boneIndex == currentIndex) // draw clear button
                        {
                            if (GUILayout.Button("Clear", GUILayout.Width(COPYBUTTONWIDTH))) Clipboard = null;
                        } else if (Clipboard.data is AnimationCurve curve) // draw paste button
                        {
                            if (GUILayout.Button("Paste", GUILayout.Width(COPYBUTTONWIDTH)))
                            {
                                Editing.SetAnimationCurve(num, curve);
                                Editing.ApplyDistribution(num);
                                if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                            }
                        }
                        else // draw disabled copy button
                        {
                            GUI.enabled = false;
                            GUILayout.Button("Copy", GUILayout.Width(COPYBUTTONWIDTH));
                            GUI.enabled = true;
                        }
                    }
                    else // draw enabled copy button
                    {
                        if (GUILayout.Button("Copy", GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Clipboard = new ClipboardEntry(currentIndex, num, Editing.GetAnimationCurve((byte)num));
                        }
                    }
                    if (Editing.distributions[i].IsEdited) GUI.color = Color.magenta;
                    else GUI.enabled = false;
                    if (GUILayout.Button("R", GUILayout.Width(25)))
                    {
                        Editing.ResetDistribution(i);
                        if (num == currentEdit)
                        {
                            AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetComponent<AnimationCurveEditor.AnimationCurveEditor>();
                            ace?.Init(Editing.GetAnimationCurve((byte)i), ace.rect, 2, 0, 0.5f);
                        }
                    }
                    GUI.enabled = true;
                    GUI.color = guic;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                #endregion
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal(GUILayout.Height(25));// bone level buttons
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
            GUILayout.EndVertical();
            #endregion

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            windowRect = KKAPI.Utilities.IMGUIUtils.DragResizeEatWindow(WindowID, windowRect);
        }

        private class BaseValueEditWrapper
        {
            private string _text;
            private float _value;
            public string Text { get => _text; set => setText(value); }
            public float Value {get => _value; set => setValue(value); }
            public bool Active;

            private readonly Action<float> _onChange;

            public BaseValueEditWrapper(float baseValue, Action<float> onChange)
            {
                _text = baseValue.ToString("0.000");
                _value = baseValue;
                Active = false;
                _onChange = onChange;
            }
            public void Activate(float baseValue)
            {
                Text = baseValue.ToString("0.000");
                Value = baseValue;
                Active = true;
            }

            private void setText(string text)
            {
                if (_text != text)
                {
                    if (float.TryParse(text, out float v) && v >= 0f && v <= 1f) Value = v;
                }
                _text = text;
            }

            private void setValue(float value)
            {
                if (_value != value)
                {
                    _text = value.ToString("0.000");
                    _onChange.Invoke(value);
                }
                _value = value;
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

