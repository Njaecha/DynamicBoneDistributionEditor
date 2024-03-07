using System;
using System.Collections.Generic;
using ADV.Commands.Base;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;
using static ActionData.PersonalityMoveData;
using static AnimationCurveEditor.AnimationCurveEditor;
using static Illusion.Utils;

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

        private Vector3EditWrapper gravityWrapper;
        private Vector3EditWrapper forceWrapper;

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
            gravityWrapper = new Vector3EditWrapper(db.m_Gravity, (v) => { Editing.gravity.value = v; Editing.ApplyGravity(); });
            forceWrapper = new Vector3EditWrapper(db.m_Force, (v) => { Editing.force.value = v; Editing.ApplyForce(); });
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

            if (GUI.Button(new Rect(windowRect.width - 18, 2, 15, 15), new GUIContent("X"), buttonStyle))
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
            #region Right Side - Header
            GUILayout.BeginHorizontal(); // header
            GUILayout.Label(new GUIContent(Editing.GetButtonName()), HeadStyle);
            if (GUILayout.Button(new GUIContent(Editing.active ? "☑" : "☐", "Toggle DynamicBone"), buttonStyle, GUILayout.Width(25), GUILayout.Height(25)))
            {
                Editing.active = !Editing.active;
            }
            GUILayout.EndHorizontal();
            #endregion
            #region Right Side - BaseValue/Distribution
            RightSideScroll = GUILayout.BeginScrollView(RightSideScroll);
            for (int i = 0; i < 5; i++) // for "Damping", "Elasticity", "Interia", "Radius", "Stiffness"
            {
                int num = i;
                GUILayout.BeginHorizontal();
                GUI.color = currentEdit == num ? Color.cyan : Editing.IsEdited(num) ? Color.magenta : guic;
                GUILayout.Label(DistribKindNames[i] + (Editing.IsEdited(num) ? "*" : ""), LabelStyle,GUILayout.Width(windowRect.width / 6), GUILayout.Height(50)); // width 1/6
                GUI.color = guic;
                #region BaseValue / Distribution Controls
                GUILayout.BeginVertical(); // two rows
                // top
                GUILayout.BeginHorizontal(GUILayout.Height(25));
                if (BaseValueWrappers[num].Active) // draw input field
                {
                    BaseValueWrappers[num].Text = GUILayout.TextField(BaseValueWrappers[num].Text);

                } else // draw button
                {
                    if (GUILayout.Button(new GUIContent("Value: "+((float)Editing.baseValues[num]).ToString("0.000"), $"{DistribKindNames[num]} Base Value. Click to edit!")))
                    {
                        BaseValueWrappers[num].Activate(Editing.baseValues[num]);
                    }
                }
                if (Clipboard != null)
                {
                    if (Clipboard.data is float value) // draw paste button
                    {
                        string tooltipValue = value.ToString("0.000");
                        if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Editing.baseValues[num].value = value;
                            Editing.ApplyBaseValues(num);
                            if (BaseValueWrappers[num].Active) BaseValueWrappers[num].Value = value;
                            if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                        }
                    }
                    else DrawDisabledCopyButton();
                }
                else // draw enabled copy button
                {
                    if (GUILayout.Button(new GUIContent("Copy", $"Copy {DistribKindNames[num]} Base Value"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, num, Editing.baseValues[num].value);
                    }
                }
                if (Editing.baseValues[i].IsEdited) GUI.color = Color.magenta;
                else GUI.enabled = false;
                if (GUILayout.Button(new GUIContent("R", $"Reset {DistribKindNames[num]} Base Value"), GUILayout.Width(25)))
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
                    if (GUILayout.Button(new GUIContent("✓","Accept Value"), GUILayout.Width(25)))
                    {
                        BaseValueWrappers[num].Active = false;
                    }
                    GUI.color = guic;
                }
                else // draw distribution buttons
                {
                    if (GUILayout.Button(new GUIContent("Edit Distribution","Opens Curve Editor for this Distribution Curve")))
                    {
                        GetOrAddACE(Editing.GetAnimationCurve((byte)i), Editing, num);
                        currentEdit = num;
                    }
                    if (Clipboard != null)
                    {
                        if (Clipboard.data is AnimationCurve curve) // draw paste button
                        {
                            if (GUILayout.Button(new GUIContent("Paste", "Paste Distribution Curve"), GUILayout.Width(COPYBUTTONWIDTH)))
                            {
                                Editing.SetAnimationCurve(num, curve);
                                Editing.ApplyDistribution(num);
                                if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                            }
                        }
                        else DrawDisabledCopyButton();
                    }
                    else // draw enabled copy button
                    {
                        if (GUILayout.Button(new GUIContent("Copy",$"Copy {DistribKindNames[num]} Distribution Curve"), GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Clipboard = new ClipboardEntry(currentIndex, num, Editing.GetAnimationCurve((byte)num));
                        }
                    }
                    if (Editing.distributions[i].IsEdited) GUI.color = Color.magenta;
                    else GUI.enabled = false;
                    if (GUILayout.Button(new GUIContent("R",$"Reset {DistribKindNames[num]} Distribution Curve"), GUILayout.Width(25)))
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
            #endregion
            #region Right Side - Gravity
            GUILayout.BeginHorizontal();
            // == top row ==
            GUILayout.Label("Gravity" + (Editing.gravity.IsEdited ? "*" : ""), LabelStyle , GUILayout.Width(windowRect.width / 6));
            if (gravityWrapper.Active) // draw accept button
            {
                GUI.color = Color.green;
                if (GUILayout.Button(new GUIContent("✓", "Accept Values")))
                {
                    gravityWrapper.Active = false;
                }
                GUI.color = guic;
            }
            else // draw value button
            {
                string vX = Editing.gravity.value.x.ToString("0.000");
                string vY = Editing.gravity.value.y.ToString("0.000");
                string vZ = Editing.gravity.value.z.ToString("0.000");
                if (GUILayout.Button(new GUIContent($"X={vX} | Y={vY} | Z={vZ}", "Gravity Vector. Click to edit!")))
                {
                    gravityWrapper.Activte(Editing.gravity.value);
                }
            }

            if (Clipboard != null)
            {
                if (Clipboard.data is Vector3 value) // draw paste button
                {
                    string tooltipValue = value.ToString("F3");
                    if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Editing.gravity.value = value;
                        Editing.ApplyGravity();
                        if (gravityWrapper.Active) gravityWrapper.Value = value;
                        if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                    }
                }
                else DrawDisabledCopyButton();
            }
            else // draw enabled copy button
            {
                if (GUILayout.Button(new GUIContent("Copy", $"Copy Gravity Vector"), GUILayout.Width(COPYBUTTONWIDTH)))
                {
                    Clipboard = new ClipboardEntry(currentIndex, 0, Editing.gravity.value);
                }
            }
            if (Editing.gravity.IsEdited) GUI.color = Color.magenta;
            else GUI.enabled = false;
            if (GUILayout.Button(new GUIContent("R", $"Reset Gravity"), GUILayout.Width(25)))
            {
                Editing.ResetGravity();
                gravityWrapper.Value = Editing.gravity.value;
            }
            GUI.enabled = true;
            GUI.color = guic;
            GUILayout.EndHorizontal();
            // == sliders ==
            if (gravityWrapper.Active)
            {
                GUILayout.BeginHorizontal();
                #region Gravity Sliders X
                GUILayout.Label("X:", LabelStyle, GUILayout.Width(25));
                gravityWrapper.ValueX = GUILayout.HorizontalSlider(gravityWrapper.ValueX,
                    gravityWrapper.ValueX < -5 ? gravityWrapper.ValueX : -5,
                    gravityWrapper.ValueX > 5 ? gravityWrapper.ValueX : 5);
                if (Clipboard != null)
                {
                    if (Clipboard.data is float value && Clipboard.distribIndex == -2) // draw paste button
                    {
                        string tooltipValue = value.ToString("0.000");
                        if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Editing.gravity.value = new Vector3(value, Editing.gravity.value.y, Editing.gravity.value.z);
                            Editing.ApplyGravity();
                            if (gravityWrapper.Active) gravityWrapper.ValueX = value;
                            if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                        }
                    }
                    else DrawDisabledCopyButton();
                }
                else // draw copy button
                {
                    if (GUILayout.Button(new GUIContent("Copy", $"Copy X Value"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, -2, gravityWrapper.ValueX);
                    }
                }
                if (GUILayout.Button(new GUIContent("R", $"Reset X"), GUILayout.Width(25)))
                {
                    gravityWrapper.ValueX = Editing.gravity.initialValue.x;
                }
                #endregion
                #region Gravity Sliders Y
                GUILayout.BeginHorizontal();
                GUILayout.Label("Y:", LabelStyle, GUILayout.Width(25));
                gravityWrapper.ValueY = GUILayout.HorizontalSlider(gravityWrapper.ValueY,
                    gravityWrapper.ValueY < -5 ? gravityWrapper.ValueY : -5,
                    gravityWrapper.ValueY > 5 ? gravityWrapper.ValueY : 5);
                if (Clipboard != null)
                {
                    if (Clipboard.data is float value && Clipboard.distribIndex == -2) // draw paste button
                    {
                        string tooltipValue = value.ToString("0.000");
                        if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Editing.gravity.value = new Vector3(Editing.gravity.value.x, value, Editing.gravity.value.z);
                            Editing.ApplyGravity();
                            if (gravityWrapper.Active) gravityWrapper.ValueY = value;
                            if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                        }
                    }
                    else DrawDisabledCopyButton();
                }
                else // draw copy button
                {
                    if (GUILayout.Button(new GUIContent("Copy", $"Copy Y Value"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, -2, gravityWrapper.ValueY);
                    }
                }
                if (GUILayout.Button(new GUIContent("R", $"Reset Y"), GUILayout.Width(25)))
                {
                    gravityWrapper.ValueY = Editing.gravity.initialValue.y;
                }
                #endregion
                #region Gravity Sliders Z
                GUILayout.BeginHorizontal();
                GUILayout.Label("Z:", LabelStyle, GUILayout.Width(25));
                gravityWrapper.ValueZ = GUILayout.HorizontalSlider(gravityWrapper.ValueZ,
                    gravityWrapper.ValueZ < -5 ? gravityWrapper.ValueZ : -5,
                    gravityWrapper.ValueZ > 5 ? gravityWrapper.ValueZ : 5);
                if (Clipboard != null)
                {
                    if (Clipboard.data is float value && Clipboard.distribIndex == -2) // draw paste button
                    {
                        string tooltipValue = value.ToString("0.000");
                        if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Editing.gravity.value = new Vector3(Editing.gravity.value.x, Editing.gravity.value.y, value);
                            Editing.ApplyGravity();
                            if (gravityWrapper.Active) gravityWrapper.ValueZ = value;
                            if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                        }
                    }
                    else DrawDisabledCopyButton();
                }
                else // draw copy button
                {
                    if (GUILayout.Button(new GUIContent("Copy", $"Copy Z Value"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, -2, gravityWrapper.ValueZ);
                    }
                }
                if (GUILayout.Button(new GUIContent("R", $"Reset Z"), GUILayout.Width(25)))
                {
                    gravityWrapper.ValueZ = Editing.gravity.initialValue.z;
                }
                #endregion
                GUILayout.EndHorizontal();
            }
            #endregion
            #region Right Side - Force
            GUILayout.BeginHorizontal();
            // == top row ==
            GUILayout.Label("Force" + (Editing.force.IsEdited ? "*" : ""), LabelStyle, GUILayout.Width(windowRect.width / 6));
            if (forceWrapper.Active) // draw accept button
            {
                GUI.color = Color.green;
                if (GUILayout.Button(new GUIContent("✓", "Accept Values")))
                {
                    forceWrapper.Active = false;
                }
                GUI.color = guic;
            }
            else // draw value button
            {
                string vX = Editing.force.value.x.ToString("0.000");
                string vY = Editing.force.value.y.ToString("0.000");
                string vZ = Editing.force.value.z.ToString("0.000");
                if (GUILayout.Button(new GUIContent($"X={vX} | Y={vY} | Z={vZ}", "Force Vector. Click to edit!")))
                {
                    forceWrapper.Activte(Editing.force.value);
                }
            }

            if (Clipboard != null)
            {
                if (Clipboard.data is Vector3 value) // draw paste button
                {
                    string tooltipValue = value.ToString("F3");
                    if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Editing.force.value = value;
                        Editing.ApplyForce();
                        if (forceWrapper.Active) forceWrapper.Value = value;
                        if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                    }
                }
                else DrawDisabledCopyButton();
            }
            else // draw enabled copy button
            {
                if (GUILayout.Button(new GUIContent("Copy", $"Copy Force Vector"), GUILayout.Width(COPYBUTTONWIDTH)))
                {
                    Clipboard = new ClipboardEntry(currentIndex, 0, Editing.force.value);
                }
            }
            if (Editing.force.IsEdited) GUI.color = Color.magenta;
            else GUI.enabled = false;
            if (GUILayout.Button(new GUIContent("R", $"Reset Force"), GUILayout.Width(25)))
            {
                Editing.ResetForce();
                forceWrapper.Value = Editing.force.value;
            }
            GUI.enabled = true;
            GUI.color = guic;
            GUILayout.EndHorizontal();
            // == sliders ==
            if (forceWrapper.Active)
            {
                GUILayout.BeginHorizontal();
                #region Force Sliders X
                GUILayout.Label("X:", LabelStyle, GUILayout.Width(25));
                forceWrapper.ValueX = GUILayout.HorizontalSlider(forceWrapper.ValueX,
                    forceWrapper.ValueX < -5 ? forceWrapper.ValueX : -5,
                    forceWrapper.ValueX > 5 ? forceWrapper.ValueX : 5);
                if (Clipboard != null)
                {
                    if (Clipboard.data is float value && Clipboard.distribIndex == -2) // draw paste button
                    {
                        string tooltipValue = value.ToString("0.000");
                        if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Editing.force.value = new Vector3(value, Editing.force.value.y, Editing.force.value.z);
                            Editing.ApplyForce();
                            if (forceWrapper.Active) forceWrapper.ValueX = value;
                            if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                        }
                    }
                    else DrawDisabledCopyButton();
                }
                else // draw copy button
                {
                    if (GUILayout.Button(new GUIContent("Copy", $"Copy X Value"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, -2, forceWrapper.ValueX);
                    }
                }
                if (GUILayout.Button(new GUIContent("R", $"Reset X"), GUILayout.Width(25)))
                {
                    forceWrapper.ValueX = Editing.force.initialValue.x;
                }
                #endregion
                #region Force Sliders Y
                GUILayout.BeginHorizontal();
                GUILayout.Label("Y:", LabelStyle, GUILayout.Width(25));
                forceWrapper.ValueY = GUILayout.HorizontalSlider(forceWrapper.ValueY,
                    forceWrapper.ValueY < -5 ? forceWrapper.ValueY : -5,
                    forceWrapper.ValueY > 5 ? forceWrapper.ValueY : 5);
                if (Clipboard != null)
                {
                    if (Clipboard.data is float value && Clipboard.distribIndex == -2) // draw paste button
                    {
                        string tooltipValue = value.ToString("0.000");
                        if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Editing.force.value = new Vector3(Editing.force.value.x, value, Editing.force.value.z);
                            Editing.ApplyForce();
                            if (forceWrapper.Active) forceWrapper.ValueY = value;
                            if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                        }
                    }
                    else DrawDisabledCopyButton();
                }
                else // draw copy button
                {
                    if (GUILayout.Button(new GUIContent("Copy", $"Copy Y Value"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, -2, forceWrapper.ValueY);
                    }
                }
                if (GUILayout.Button(new GUIContent("R", $"Reset Y"), GUILayout.Width(25)))
                {
                    forceWrapper.ValueY = Editing.force.initialValue.y;
                }
                #endregion
                #region Force Sliders Z
                GUILayout.BeginHorizontal();
                GUILayout.Label("Z:", LabelStyle, GUILayout.Width(25));
                forceWrapper.ValueZ = GUILayout.HorizontalSlider(forceWrapper.ValueZ,
                    forceWrapper.ValueZ < -5 ? forceWrapper.ValueZ : -5,
                    forceWrapper.ValueZ > 5 ? forceWrapper.ValueZ : 5);
                if (Clipboard != null)
                {
                    if (Clipboard.data is float value && Clipboard.distribIndex == -2) // draw paste button
                    {
                        string tooltipValue = value.ToString("0.000");
                        if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Editing.force.value = new Vector3(Editing.force.value.x, Editing.force.value.y, value);
                            Editing.ApplyForce();
                            if (forceWrapper.Active) forceWrapper.ValueZ = value;
                            if (Input.GetKey(KeyCode.LeftShift)) Clipboard = null;
                        }
                    }
                    else DrawDisabledCopyButton();
                }
                else // draw copy button
                {
                    if (GUILayout.Button(new GUIContent("Copy", $"Copy Z Value"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, -2, forceWrapper.ValueZ);
                    }
                }
                if (GUILayout.Button(new GUIContent("R", $"Reset Z"), GUILayout.Width(25)))
                {
                    forceWrapper.ValueZ = Editing.force.initialValue.z;
                }
                #endregion
                GUILayout.EndHorizontal();
            }
            #endregion
            #region Right Side - Footer
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
            #endregion
            GUILayout.EndVertical();
            #endregion

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            windowRect = KKAPI.Utilities.IMGUIUtils.DragResizeEatWindow(WindowID, windowRect);
        }

        private static void DrawDisabledCopyButton()
        {
            GUI.enabled = false;
            GUILayout.Button("Copy", GUILayout.Width(COPYBUTTONWIDTH));
            GUI.enabled = true;
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

        private class Vector3EditWrapper
        {
            private string _textX;
            private string _textY;
            private string _textZ;
            private float _valueX;
            private float _valueY;
            private float _valueZ;

            public string TextX { get => _textX; set => setText(ref _textX, (v) => setValue(ref _valueX, ref _textX, v), value); }
            public float ValueX { get => _valueX; set => setValue(ref _valueX, ref _textX, value); }
            public string TextY { get => _textY; set => setText(ref _textY, (v) => setValue(ref _valueY, ref _textY, v), value); }
            public float ValueY { get => _valueY; set => setValue(ref _valueY, ref _textY, value); }
            public string TextZ { get => _textZ; set => setText(ref _textZ, (v) => setValue(ref _valueZ, ref _textZ, v), value); }
            public float ValueZ { get => _valueZ; set => setValue(ref _valueZ, ref _textZ, value); }

            private readonly Action<Vector3> _onChange;

            public Vector3 Value { get => new Vector3(ValueX, ValueY, ValueZ); set => SetForVector(value); }

            public bool Active;

            public Vector3EditWrapper(Vector3 vector, Action<Vector3>onChange)
            {
                _textX = vector.x.ToString("0.000");
                _textY = vector.y.ToString("0.000");
                _textZ = vector.z.ToString("0.000");
                _valueX = vector.x;
                _valueY = vector.y;
                _valueZ = vector.z;
                this._onChange = onChange;
            }

            private void SetForVector(Vector3 vector)
            {
                TextX = vector.x.ToString("0.000");
                TextY = vector.y.ToString("0.000");
                TextZ = vector.z.ToString("0.000");
                ValueX = vector.x;
                ValueY = vector.y;
                ValueZ = vector.z;
            }

            public void Activte(Vector3 vector)
            {
                SetForVector(vector);
                Active = true;
            }

            private void setText(ref string bTextField, Action<float> ValueSetter, string text)
            {
                if (bTextField != text)
                {
                    if (float.TryParse(text, out float v) && v >= 0f && v <= 1f) ValueSetter.Invoke(v);
                }
                bTextField = text;
            }

            private void setValue(ref float bValueField, ref string bTextField, float value)
            {
                if (bValueField != value)
                {
                    bTextField = value.ToString("0.000");
                    _onChange.Invoke(new Vector3(_valueX, _valueY, _valueZ));
                }
                bValueField = value;
            }
        }
    }
}

