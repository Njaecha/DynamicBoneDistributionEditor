using System;
using System.Collections.Generic;
using ADV.Commands.Base;
using AnimationCurveEditor;
using KKAPI.Maker;
using KKAPI;
using UnityEngine;
using static AnimationCurveEditor.AnimationCurveEditor;
using static AnimationCurveEditor.AnimationCurveEditor.KeyframeEditedArgs;
using static Illusion.Utils;
using System.Linq;

namespace DynamicBoneDistributionEditor
{
	public class DBDEUI : MonoBehaviour
	{
        private const int COPYBUTTONWIDTH = 55;
        private const float VSLIDERMIN = -0.03f;
        private const float VSLIDERMAX = 0.03f;

        #region fields for DynamicBones
        private readonly string[] DistribKindNames = new string[] { "Damping", "Elasticity", "Inertia", "Radius", "Stiffness" };
        private readonly string[] axisNames = new string[] { "None", "X", "Y", "Z" };
        
        /// <summary>
        /// Function used to get DB List with Key
        /// </summary>
        private Func<List<DBDEDynamicBoneEdit>> DBDEGetter = null;

        /// <summary>
        /// Function called when user presses the "Refresh Bonelist" button
        /// </summary>
        private Action RefreshBoneList;

        /// <summary>
        /// Holds information helping with editing the base values
        /// </summary>
        private BaseValueEditWrapper[] BaseValueWrappers;

        private Vector3EditWrapper gravityWrapper;
        private Vector3EditWrapper forceWrapper;
        private Vector3EditWrapper endOffsetWrapper;
        
        private BaseValueEditWrapper weightWrapper;
        #endregion

        #region fields for DynamicBoneColliders

        private Func<List<DBDEDynamicBoneColliderEdit>> ColliderGetter = null;
        private Action RefreshColliderList;

        private BaseValueEditWrapper radiusWrapper;
        private BaseValueEditWrapper lengthWrapper;

        private Vector3EditWrapper offsetWrapper;

        #endregion

        /// <summary>
        /// Index of DB in List
        /// </summary>
		private int  currentIndex = 0;

        /// <summary>
        /// If hasValue display ACE
        /// </summary>
        private int? currentEdit = null;

        /// <summary>
        /// Clipboard used to copy settings
        /// </summary>
        private ClipboardEntry Clipboard;

        private RenderTexture rTex;
        private Camera rCam;


        private Rect windowRect = new Rect(100, 100, 650, 450);
        private Vector2 LeftSideScroll = new Vector2();
        private Vector2 RightSideScroll = new Vector2();

        private string filterText = string.Empty;

        public string TitleAppendix = "";
        public DBDECharaController referencedChara = null;

        public bool UpdateUIWhileOpen = true;

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

            Camera.main.GetOrAddComponent<DBDEGizmoController>();
        }

        /// <summary>
        /// Opens the UI. Pass a Func that returns a list of the bones that should be editable.
        /// </summary>
        /// <param name="DBDEGetter"></param>
        public void Open(Func<List<DBDEDynamicBoneEdit>> DBDEGetter, Action RefreshBoneList)
        {
            this.DBDEGetter = DBDEGetter;
            this.RefreshBoneList = RefreshBoneList;
            SetCurrentRightSide(0);
        }

        void Update()
        {
            if (KKAPI.Maker.MakerAPI.InsideAndLoaded && currentEdit.HasValue)
            {
                AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>();
                if (ace && ace.eatingInput && DBDE.Instance.getMakerCursorMangaer() != null && DBDE.Instance.getMakerCursorMangaer().isActiveAndEnabled == true)
                {
                    DBDE.Instance.getMakerCursorMangaer().enabled = false;
                }
                if (ace && !ace.eatingInput && DBDE.Instance.getMakerCursorMangaer() != null && DBDE.Instance.getMakerCursorMangaer().isActiveAndEnabled == false)
                {
                    DBDE.Instance.getMakerCursorMangaer().enabled = true;
                }

            }
        }

        public void Close()
        {
            TitleAppendix = "";
            DBDEGetter = null;
            RefreshBoneList = null;
            currentIndex = 0;
            currentEdit = null;
            referencedChara = null;
            if (KKAPI.Maker.MakerAPI.InsideAndLoaded) DBDE.toggle?.SetValue(false);
            AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetComponent<AnimationCurveEditor.AnimationCurveEditor>();
            ace?.close();
            DBDEGizmoController gizmo = Camera.main.GetOrAddComponent<DBDEGizmoController>();
            if (gizmo) gizmo.Editing = null;
        }

        private bool CanShow()
        {
            if (!MakerAPI.InsideMaker) return false;
            if (MakerAPI.InsideMaker && !MakerAPI.IsInterfaceVisible()) return false;

            if (SceneApi.GetAddSceneName() == "Config") return false;
            if (SceneApi.GetIsOverlap()) return false;
            if (SceneApi.GetIsNowLoadingFade()) return false;

            return true;
        }

        int _beenHiddenForFrames = 0;

        private void OnGUI()
        {
            AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetComponent<AnimationCurveEditor.AnimationCurveEditor>();
            if (KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.Maker && !CanShow())
            {
                if (ace) ace.enabled = false;
                return;
            }
            else if (ace && ace.enabled == false) ace.enabled = true;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rTex);
            if (DBDEGetter != null)
            {
                bool draw = true;
                List<DBDEDynamicBoneEdit> DBDES = new List<DBDEDynamicBoneEdit>();
                // try if DBDES can be obtained and the dynamic bones are still valid, else close UI
                // if we dont do this and the UI stays open in studio while the character its editing is deleted it goes nuts
                try { DBDES = DBDEGetter.Invoke(); DBDES.ForEach(d => d.GetButtonName()); } catch (Exception) { 
                    draw = false; 
                    _beenHiddenForFrames++; 
                    RefreshBoneList.Invoke();
                }
                if (draw && !DBDES.IsNullOrEmpty())
                {
                    if (DBDES.Count <= currentIndex) SetCurrentRightSide(DBDES.Count - 1);
                    _beenHiddenForFrames = 0;
                    windowRect = GUI.Window(5858350, windowRect, WindowFunction, $"DBDE v{DBDE.Version} - {TitleAppendix}", KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.window);
                }
            }
            if (currentEdit.HasValue) rCam.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>()?.OnGUI();

            if (_beenHiddenForFrames > 30)
            {
                _beenHiddenForFrames = 0;
                Close();
            }

        }

        private Rect lastUsedAceRect = new Rect(Screen.width / 2, Screen.height / 2, 500, 300);
        private AnimationCurveEditor.AnimationCurveEditor GetOrAddACE(AnimationCurve curve, DBDEDynamicBoneEdit Editing, int num)
        {
            AnimationCurveEditor.AnimationCurveEditor ace = rCam.GetOrAddComponent<AnimationCurveEditor.AnimationCurveEditor>();
            if (currentEdit.HasValue) lastUsedAceRect = ace.rect; // if ACE is already opened before this method was called.

            // normalise curve if needed
            if (curve.GetKeys()[0].time > 0)
            {
                Keyframe x = curve.GetKeys()[0];
#if KK
                curve.MoveKey(0, new Keyframe(x.time, x.value, 0, x.outTangent));
#elif KKS
                curve.MoveKey(0, new Keyframe(x.time, x.value, 0, x.outTangent, 0.1f, x.outWeight));
#endif
                float v = curve.Evaluate(0);
                curve.AddKey(new Keyframe(0, v));
            }
            if (curve.GetKeys()[curve.GetKeys().Count() - 1].time < 1)
            {
                Keyframe x = curve.GetKeys()[curve.GetKeys().Count() - 1];
#if KK
                curve.MoveKey(curve.GetKeys().Count() - 1, new Keyframe(x.time, x.value, x.inTangent, 0));
#elif KKS
                curve.MoveKey(curve.GetKeys().Count() - 1, new Keyframe(x.time, x.value, x.inTangent, 0, x.inWeight, 0.1f));
#endif
                float v = curve.Evaluate(1);
                curve.AddKey(new Keyframe(1, v));
            }

            ace.Init(curve, lastUsedAceRect, 2, 0, 0.5f);
            ace.enabled = true;
            ace.borderingKeyframesDeletable = false;
            ace.displayName = Editing.GetButtonName() + " - " + DistribKindNames[num];
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
            DynamicBone db = Editing.PrimaryDynamicBone;
            if (!db)
            {
                RefreshBoneList.Invoke();
                return;
            }
            currentIndex = boneIndex;
            BaseValueWrappers = new[]
            {
                new BaseValueEditWrapper(db.m_Damping, (v) => { Editing.baseValues[0].value = v; Editing.ApplyBaseValues(0); }),
                new BaseValueEditWrapper(db.m_Elasticity, (v) => { Editing.baseValues[1].value = v; Editing.ApplyBaseValues(1); }),
                new BaseValueEditWrapper(db.m_Inert, (v) => { Editing.baseValues[2].value = v; Editing.ApplyBaseValues(2); }),
                new BaseValueEditWrapper(db.m_Radius, (v) => { Editing.baseValues[3].value = v; Editing.ApplyBaseValues(3); }),
                new BaseValueEditWrapper(db.m_Stiffness, (v) => { Editing.baseValues[4].value = v; Editing.ApplyBaseValues(4); })
            };
            gravityWrapper = new Vector3EditWrapper(db.m_Gravity, (v) => { Editing.gravity.value = v; Editing.ApplyGravity(); });
            forceWrapper = new Vector3EditWrapper(db.m_Force, (v) => { Editing.force.value = v; Editing.ApplyForce(); });
            endOffsetWrapper = new Vector3EditWrapper(db.m_EndOffset, (v) => { Editing.endOffset.value = v; Editing.ApplyEndOffset(); });
            
            weightWrapper = new BaseValueEditWrapper(db.m_Weight, (v) => { Editing.weight.value = v; Editing.ApplyWeight();});
            
            DBDEGizmoController gizmo = Camera.main.GetOrAddComponent<DBDEGizmoController>();
            gizmo.Editing = Editing;
        }

        
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
            GUIStyle gizmoButtonStyle = new GUIStyle(GUI.skin.button);
            gizmoButtonStyle.fontSize = 10;
            Color guic = GUI.color;
            #endregion
            List<DBDEDynamicBoneEdit> DBDES = DBDEGetter.Invoke();
            if (DBDES.IsNullOrEmpty())
            {
                //RefreshBoneList.Invoke();
                //close();
                return;
            }

            if (GUI.Button(new Rect(windowRect.width - 18, 2, 15, 15), new GUIContent("X"), buttonStyle))
            {
                Close();
                return;
            }
            if (GUI.Button(new Rect(1,1, 90, 15), new GUIContent(DBDE.drawGizmos.Value ? "Gizmos ON" : "Gismos OFF", "Toggle Gizmos.\nBlue arrow: Gravity.\nRed arrow: Force.\nGreen arrow: Applied force")))
            {
                DBDE.drawGizmos.Value = !DBDE.drawGizmos.Value;
            }

            GUI.Box(new Rect(new Vector2(5, 20), new Vector2(windowRect.width / 6 * 2 + 5, windowRect.height - 25)), "");
            GUI.Box(new Rect(new Vector2(5 + (windowRect.width / 6 * 2 + 5), 20), new Vector2(windowRect.width - (windowRect.width / 6 * 2 + 10)-5, windowRect.height - 25)), "");

            GUILayout.BeginArea(new Rect(new Vector2(5, 20), windowRect.size - new Vector2(15, 25)));
            GUILayout.BeginHorizontal();

            #region Left Side
            GUILayout.BeginVertical(GUILayout.Width(windowRect.width / 6 * 2 + 5)); // width 2/6
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            filterText = GUILayout.TextField(filterText);
            if (GUILayout.Button("Clear", GUILayout.Width(55))) filterText = string.Empty;
            GUILayout.EndHorizontal();
            LeftSideScroll = GUILayout.BeginScrollView(LeftSideScroll); 
            for (int i = 0; i < DBDES.Count; i++)
            {
                DBDEDynamicBoneEdit DBEdit = DBDES[i];
                if (DBEdit == null) continue;
                if (!filterText.IsNullOrEmpty() && !DBEdit.GetButtonName().ToLower().Contains(filterText.ToLower())) continue;
                GUILayout.BeginHorizontal();
                GUI.color = currentIndex == i ? Color.cyan : DBEdit.IsEdited() ? Color.magenta : guic;
                if (GUILayout.Button(DBEdit.GetButtonName() + (DBEdit.IsEdited() ? "*" : "")))
                {
                    if (i != currentIndex) SetCurrentRightSide(i);
                }
                GUI.color = guic;
                if (DBEdit.IsEdited()) GUI.color = Color.magenta;
                else GUI.enabled = false;
                if (GUILayout.Button("R", GUILayout.Width(25)))
                {
                    DBEdit.ResetAll();
                }
                GUI.enabled = true; GUI.color = guic;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Refresh Bone List", GUILayout.Height(25)))
            {
                RefreshBoneList.Invoke();
            }
            GUILayout.Space(4);
            GUILayout.EndVertical();
            #endregion
            GUILayout.Space(3);

            #region Right Side

            DBDEDynamicBoneEdit Editing = DBDES[currentIndex];
            if (Editing != null && Editing.PrimaryDynamicBone != null)
            {
                // fix gizmo showing the wrong bone in some cases
                if (!Editing.Equals(Camera.main.GetComponent<DBDEGizmoController>()?.Editing)) SetCurrentRightSide(currentIndex);

                if (UpdateUIWhileOpen && (referencedChara == null || !referencedChara.IsLoading))
                {
                    Editing.ReferToDynamicBone();
                    gravityWrapper.SetForVector(Editing.gravity.value);
                    forceWrapper.SetForVector(Editing.force.value);
                }


                GUILayout.BeginVertical(); // bone settings
                #region Right Side - Header
                GUILayout.BeginHorizontal(); // header
                GUILayout.Label(new GUIContent(Editing.GetButtonName()), HeadStyle);
                if (MakerAPI.InsideAndLoaded && Editing.ReidentificationData is KeyValuePair<int, string> kvp)
                {
                    bool noShake = MakerAPI.GetCharacterControl().nowCoordinate.accessory.parts[kvp.Key].noShake;
                    if (noShake) GUI.enabled = false;
                }
                if (Editing.isActiveEdited()) GUI.color = Color.magenta;
                if (GUILayout.Button(new GUIContent(Editing.active ? "ON" : "OFF", "Toggle DynamicBone"), buttonStyle, GUILayout.Width(30), GUILayout.Height(25)))
                {
                    Editing.active = !Editing.active;
                }
                GUI.color = guic;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                #endregion

                RightSideScroll = GUILayout.BeginScrollView(RightSideScroll);
                #region Right Side - Freeze Axis
                GUILayout.BeginHorizontal();
                if (Editing.freezeAxis.IsEdited) GUI.color = Color.magenta;
                GUILayout.Label("Freeze Axis" + (Editing.freezeAxis.IsEdited ? "*" : ""), LabelStyle, GUILayout.Width(windowRect.width / 6));
                GUI.color = guic;
                int freeze = (int)Editing.freezeAxis.value;
                Editing.freezeAxis.value = (DynamicBone.FreezeAxis)GUILayout.Toolbar((int)Editing.freezeAxis.value, axisNames);
                if (freeze != (int)Editing.freezeAxis.value) Editing.ApplyFreezeAxis(); // Apply if freeze Axis was changed


                if (Clipboard != null)
                {
                    if (Clipboard.data is DynamicBone.FreezeAxis value) // draw paste button
                    {
                        if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {value}"), GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Editing.freezeAxis.value = value;
                            Editing.ApplyForce();
                        }
                    }
                    else DrawDisabledCopyButton();
                }
                else // draw copy button
                {
                    if (GUILayout.Button(new GUIContent("Copy", $"Copy Freeze Axis"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, -2, Editing.freezeAxis.value);
                    }
                }
                if (Editing.freezeAxis.IsEdited) GUI.color = Color.magenta;
                else GUI.enabled = false;
                if (GUILayout.Button(new GUIContent("R", $"Reset FreezeAxis"), GUILayout.Width(25)))
                {
                    Editing.freezeAxis.Reset();
                    Editing.ApplyFreezeAxis();
                }
                GUI.enabled = true; GUI.color = guic;

                GUILayout.EndHorizontal();
                GUILayout.Space(5);
                #endregion

                #region Right Side - Weight
                GUILayout.BeginHorizontal();
                GUI.color = Editing.isWeigthEdited() ? Color.magenta : guic;
                GUILayout.Label("Weight" + (Editing.isWeigthEdited() ? "*" : ""), LabelStyle, GUILayout.Width(windowRect.width / 6), GUILayout.Height(weightWrapper.Active ? 50 : 25)); // width 1/6
                GUI.color = guic;
                
                GUILayout.BeginVertical(); // two rows
                // top
                GUILayout.BeginHorizontal(GUILayout.Height(25));
                if (weightWrapper.Active) // draw input field
                {
                    weightWrapper.Text = GUILayout.TextField(weightWrapper.Text);
                }
                else // draw button
                {
                    if (GUILayout.Button(new GUIContent("Value: " + ((float)Editing.weight).ToString("0.000"), $"Weight. Click to edit!")))
                    {
                        weightWrapper.Activate(Editing.weight);
                    }
                }
                if (Clipboard != null)
                {
                    if (Clipboard.data is float value && Clipboard.distribIndex != -2) // draw paste button
                    {
                        string tooltipValue = value.ToString("0.000");
                        if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                        {
                            Editing.weight.value = value;
                            Editing.ApplyWeight();
                            if (weightWrapper.Active) weightWrapper.Value = value;

                        }
                    }
                    else DrawDisabledCopyButton();
                }
                else // draw enabled copy button
                {
                    if (GUILayout.Button(new GUIContent("Copy", $"Copy Weight"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        Clipboard = new ClipboardEntry(currentIndex, -3, Editing.weight.value);
                    }
                }
                if (Editing.weight.IsEdited) GUI.color = Color.magenta;
                else GUI.enabled = false;
                if (GUILayout.Button(new GUIContent("R", $"Reset Weight"), GUILayout.Width(25)))
                {
                    Editing.ResetWeight();
                    weightWrapper.Value = Editing.weight.value;
                }
                GUI.enabled = true;
                GUI.color = guic;
                
                GUILayout.EndHorizontal();
                // bottom
                GUILayout.BeginHorizontal(GUILayout.Height(25));
                if (weightWrapper.Active) // draw slider
                {
                    weightWrapper.Value = GUILayout.HorizontalSlider(weightWrapper.Value, 0f, 1f);
                    GUI.color = Color.green;
                    if (GUILayout.Button(new GUIContent("✓", "Accept Value"), GUILayout.Width(25)))
                    {
                        weightWrapper.Active = false;
                    }
                    GUI.color = guic;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                #endregion
                
                #region Right Side - BaseValue/Distribution
                for (int i = 0; i < 5; i++) // for "Damping", "Elasticity", "Inertia", "Radius", "Stiffness"
                {
                    int num = i;
                    GUILayout.BeginHorizontal();
                    GUI.color = currentEdit == num ? Color.cyan : Editing.IsEdited(num) ? Color.magenta : guic;
                    GUILayout.Label(DistribKindNames[i] + (Editing.IsEdited(num) ? "*" : ""), LabelStyle, GUILayout.Width(windowRect.width / 6), GUILayout.Height(50)); // width 1/6
                    GUI.color = guic;
                    #region BaseValue / Distribution Controls
                    GUILayout.BeginVertical(); // two rows
                                               // top
                    GUILayout.BeginHorizontal(GUILayout.Height(25));
                    if (BaseValueWrappers[num].Active) // draw input field
                    {
                        BaseValueWrappers[num].Text = GUILayout.TextField(BaseValueWrappers[num].Text);

                    }
                    else // draw button
                    {
                        if (GUILayout.Button(new GUIContent("Value: " + ((float)Editing.baseValues[num]).ToString("0.000"), $"{DistribKindNames[num]} Base Value. Click to edit!")))
                        {
                            BaseValueWrappers[num].Activate(Editing.baseValues[num]);
                        }
                    }
                    if (Clipboard != null)
                    {
                        if (Clipboard.data is float value && Clipboard.distribIndex != -2) // draw paste button
                        {
                            string tooltipValue = value.ToString("0.000");
                            if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                            {
                                Editing.baseValues[num].value = value;
                                Editing.ApplyBaseValues(num);
                                if (BaseValueWrappers[num].Active) BaseValueWrappers[num].Value = value;

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
                        if (GUILayout.Button(new GUIContent("✓", "Accept Value"), GUILayout.Width(25)))
                        {
                            BaseValueWrappers[num].Active = false;
                        }
                        GUI.color = guic;
                    }
                    else // draw distribution buttons
                    {
                        if (GUILayout.Button(new GUIContent("Edit Distribution", "Opens Curve Editor for this Distribution Curve")))
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

                                }
                            }
                            else DrawDisabledCopyButton();
                        }
                        else // draw enabled copy button
                        {
                            if (GUILayout.Button(new GUIContent("Copy", $"Copy {DistribKindNames[num]} Distribution Curve"), GUILayout.Width(COPYBUTTONWIDTH)))
                            {
                                Clipboard = new ClipboardEntry(currentIndex, num, Editing.GetAnimationCurve((byte)num));
                            }
                        }
                        if (Editing.distributions[i].IsEdited) GUI.color = Color.magenta;
                        else GUI.enabled = false;
                        if (GUILayout.Button(new GUIContent("R", $"Reset {DistribKindNames[num]} Distribution Curve"), GUILayout.Width(25)))
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

                DrawVectorControl("Gravity", (a) => Editing.IsGravityEdited(a), gravityWrapper, ref Editing.gravity, Editing.ApplyGravity, Editing.ResetGravity);
                DrawVectorControl("Force", (a) => Editing.IsForceEdited(a), forceWrapper, ref Editing.force, Editing.ApplyForce, Editing.ResetForce);
                DrawVectorControl("EndOffset", (a) => Editing.IsEndOffsetEdited(a), endOffsetWrapper, ref Editing.endOffset, Editing.ApplyEndOffset, Editing.ResetEndOffset);

                GUILayout.EndScrollView();

                #region Right Side - Footer
                GUILayout.BeginHorizontal(); // bone level buttons
                if (Clipboard != null && Clipboard.data is DBDEDynamicBoneEdit copied) // draw Paste button
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
                if (Editing.IsEdited()) GUI.color = Color.magenta;
                else GUI.enabled = false;
                if (GUILayout.Button("Reset All"))
                {
                    Editing.ResetAll();
                }
                GUI.enabled = true; GUI.color = guic;
                if (Clipboard != null) GUI.color = Color.red;
                else GUI.enabled = false;
                if (GUILayout.Button("Clear Clipboard"))
                {
                    Clipboard = null;
                }
                GUI.enabled = true; GUI.color = guic;
                GUILayout.EndHorizontal();
                #endregion
                GUILayout.EndVertical();
            }
            #endregion

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            KKAPI.Utilities.IMGUIUtils.DrawTooltip(windowRect, 150);
            windowRect = KKAPI.Utilities.IMGUIUtils.DragResizeEatWindow(WindowID, windowRect);
        }

        private void DrawVectorControl(
            string VectorName,
            Func<int?,bool> isEditedFunc,
            Vector3EditWrapper wrapper,
            ref EditableValue<Vector3> editableValue,
            Action applyFunc,
            Action resetFunc
        )
        {
            Color guic = GUI.color;
            GUIStyle LabelStyle = new GUIStyle(GUI.skin.box);
            LabelStyle.alignment = TextAnchor.MiddleCenter;


            GUILayout.BeginHorizontal();
            // == top row ==
            #region Top
            if (isEditedFunc.Invoke(null)) GUI.color = Color.magenta;
            GUILayout.Label(VectorName + (isEditedFunc.Invoke(null) ? "*" : ""), LabelStyle, GUILayout.Width(windowRect.width / 6));
            GUI.color = guic;
            if (wrapper.Active) // draw accept button
            {
                GUI.color = Color.green;
                if (GUILayout.Button(new GUIContent("✓", "Accept Values")))
                {
                    wrapper.Active = false;
                }
                GUI.color = guic;
            }
            else // draw value button
            {
                string tooltipText = editableValue.value.ToString("F3");
                if (GUILayout.Button(new GUIContent($"{tooltipText}", $"{VectorName} Vector. Click to edit!")))
                {
                    wrapper.Activte(editableValue.value);
                }
            }

            if (Clipboard != null)
            {
                if (Clipboard.data is Vector3 value) // draw paste button
                {
                    string tooltipValue = value.ToString("F3");
                    if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        editableValue.value = value;
                        applyFunc.Invoke();
                        if (wrapper.Active) wrapper.Value = value;
                    }
                }
                else DrawDisabledCopyButton();
            }
            else // draw enabled copy button
            {
                if (GUILayout.Button(new GUIContent("Copy", $"Copy {VectorName} Vector"), GUILayout.Width(COPYBUTTONWIDTH)))
                {
                    Clipboard = new ClipboardEntry(currentIndex, 0, editableValue.value);
                }
            }
            if (isEditedFunc.Invoke(null)) GUI.color = Color.magenta;
            else GUI.enabled = false;
            if (GUILayout.Button(new GUIContent("R", $"Reset {VectorName}"), GUILayout.Width(25)))
            {
                resetFunc.Invoke();
                wrapper.Value = editableValue.value;
            }
            GUI.enabled = true;
            GUI.color = guic;
            GUILayout.EndHorizontal();
            #endregion
            // == sliders ==
            if (wrapper.Active)
            {
                DrawAxisSliders(Vector3EditWrapper.Axis.X, wrapper, ref editableValue, editableValue.initialValue.x, isEditedFunc, applyFunc);
                DrawAxisSliders(Vector3EditWrapper.Axis.Y, wrapper, ref editableValue, editableValue.initialValue.y, isEditedFunc, applyFunc);
                DrawAxisSliders(Vector3EditWrapper.Axis.Z, wrapper, ref editableValue, editableValue.initialValue.z, isEditedFunc, applyFunc);
            }
            GUILayout.Space(5);
        }

        private void DrawAxisSliders(
            Vector3EditWrapper.Axis axis, 
            Vector3EditWrapper wrapper, 
            ref EditableValue<Vector3> editableValue,
            float resetTo,
            Func<int?, bool> isEditedFunc, 
            Action applyFunc
        )
        {
            Color guic = GUI.color;
            GUIStyle LabelStyle = new GUIStyle(GUI.skin.box);
            LabelStyle.alignment = TextAnchor.MiddleCenter;

            string AxisLetter = new string[] {"X", "Y", "Z" }[(int)axis];

            GUILayout.BeginHorizontal();
            if (isEditedFunc.Invoke((int)axis)) GUI.color = Color.magenta;
            GUILayout.Label($"{AxisLetter}:", LabelStyle, GUILayout.Width(25));
            GUI.color = guic;
            switch (axis)
            {
                case Vector3EditWrapper.Axis.X:
                    wrapper.TextX =  GUILayout.TextField(wrapper.TextX, GUILayout.Width(60));
                    wrapper.ValueX = GUILayout.HorizontalSlider(wrapper.ValueX,
                        wrapper.ValueX < VSLIDERMIN ? wrapper.ValueX : VSLIDERMIN,
                        wrapper.ValueX > VSLIDERMAX ? wrapper.ValueX : VSLIDERMAX);
                    break;
                case Vector3EditWrapper.Axis.Y:
                    wrapper.TextY = GUILayout.TextField(wrapper.TextY, GUILayout.Width(60));
                    wrapper.ValueY = GUILayout.HorizontalSlider(wrapper.ValueY,
                        wrapper.ValueY < VSLIDERMIN ? wrapper.ValueY : VSLIDERMIN,
                        wrapper.ValueY > VSLIDERMAX ? wrapper.ValueY : VSLIDERMAX);
                    break;
                case Vector3EditWrapper.Axis.Z:
                    wrapper.TextZ = GUILayout.TextField(wrapper.TextZ, GUILayout.Width(60));
                    wrapper.ValueZ = GUILayout.HorizontalSlider(wrapper.ValueZ,
                        wrapper.ValueZ < VSLIDERMIN ? wrapper.ValueZ : VSLIDERMIN,
                        wrapper.ValueZ > VSLIDERMAX ? wrapper.ValueZ : VSLIDERMAX);
                    break;
                default: break;
            }
            
            if (Clipboard != null)
            {
                if (Clipboard.data is float value && Clipboard.distribIndex == -2) // draw paste button
                {
                    string tooltipValue = value.ToString("0.00000");
                    if (GUILayout.Button(new GUIContent("Paste", $"Paste Value: {tooltipValue}"), GUILayout.Width(COPYBUTTONWIDTH)))
                    {
                        editableValue.value = GetNewVectorForAxis(value, axis, editableValue);
                        applyFunc.Invoke();
                        if (wrapper.Active)
                        {
                            switch(axis)
                            {
                                case Vector3EditWrapper.Axis.X:
                                    wrapper.ValueX = value;
                                    break;
                                case Vector3EditWrapper.Axis.Y:
                                    wrapper.ValueY = value;
                                    break;
                                case Vector3EditWrapper.Axis.Z:
                                    wrapper.ValueZ = value;
                                    break; 
                                default:break;
                            }
                        }
                    }
                }
                else DrawDisabledCopyButton();
            }
            else // draw copy button
            {
                if (GUILayout.Button(new GUIContent("Copy", $"Copy {AxisLetter} Value"), GUILayout.Width(COPYBUTTONWIDTH)))
                {
                    Clipboard = new ClipboardEntry(currentIndex, -2, axis == Vector3EditWrapper.Axis.X ? wrapper.ValueX : (axis == Vector3EditWrapper.Axis.Y ? wrapper.ValueY : wrapper.ValueZ));
                }
            }
            if (isEditedFunc.Invoke((int)axis)) GUI.color = Color.magenta;
            else GUI.enabled = false;
            if (GUILayout.Button(new GUIContent("R", $"Reset {AxisLetter}"), GUILayout.Width(25))) // draw reset button
            {
                switch (axis)
                {
                    case Vector3EditWrapper.Axis.X:
                        wrapper.ValueX = resetTo;
                        break;
                    case Vector3EditWrapper.Axis.Y:
                        wrapper.ValueY = resetTo;
                        break;
                    case Vector3EditWrapper.Axis.Z:
                        wrapper.ValueZ = resetTo;
                        break;
                    default: break;
                }
            }
            GUI.enabled = true; GUI.color = guic;
            GUILayout.EndHorizontal();
        }

        private static Vector3 GetNewVectorForAxis(float value, Vector3EditWrapper.Axis axis, EditableValue<Vector3> editableValue)
        {
            switch (axis)
            {
                case Vector3EditWrapper.Axis.X:
                    return new Vector3(value, editableValue.value.y, editableValue.value.z);
                case Vector3EditWrapper.Axis.Y:
                    return new Vector3(editableValue.value.x,value, editableValue.value.z);
                case Vector3EditWrapper.Axis.Z:
                    return new Vector3(editableValue.value.x, editableValue.value.y, value);
                default:
                    return editableValue.value;
            }
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

            public Vector3EditWrapper(Vector3 vector, Action<Vector3> onChange)
            {
                _textX = vector.x.ToString("0.00000");
                _textY = vector.y.ToString("0.00000");
                _textZ = vector.z.ToString("0.00000");
                _valueX = vector.x;
                _valueY = vector.y;
                _valueZ = vector.z;
                this._onChange = onChange;
            }

            internal void SetForVector(Vector3 vector)
            {
                TextX = vector.x.ToString("0.00000");
                TextY = vector.y.ToString("0.00000");
                TextZ = vector.z.ToString("0.00000");
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
                    if (float.TryParse(text, out float v) && v >= -1f && v <= 1f) ValueSetter.Invoke(v);
                }
                bTextField = text;
            }

            private void setValue(ref float bValueField, ref string bTextField, float value)
            {
                value = (float)System.Math.Round(value, 5);
                bool flag = false;
                if (bValueField != value)
                {
                    bTextField = value.ToString("0.00000");
                    flag = true;
                }
                bValueField = value;
                if (flag) _onChange.Invoke(new Vector3(_valueX, _valueY, _valueZ));
            }

            public enum Axis
            {
                X, Y, Z
            }
        }
    }
}

