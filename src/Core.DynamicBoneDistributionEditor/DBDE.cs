using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI.Sidebar;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using IllusionFixes;
using Config;
using System.Linq;
using ADV.Commands.Effect;
using KKAPI.Studio;
using Screencap;
using BepInEx.Bootstrap;
using ExtensibleSaveFormat;

namespace DynamicBoneDistributionEditor
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInDependency(ScreenshotManager.GUID)]
    public class DBDE : BaseUnityPlugin
    {
        public const string PluginName = "DynamicBoneDistributionEditor";
        public const string GUID = "org.njaecha.plugins.dbde";
        public const string Version = "1.5.1";

        internal new static ManualLogSource Logger;
        internal static DBDE Instance;

        internal static DBDEUI UI;

        internal static SidebarToggle toggle;

        public static ConfigEntry<bool> loadSettingsAsDefault;
        public static ConfigEntry<bool> drawGizmos;

        private static Button DBDEStudioButton;
        
        Harmony harmony;

        void Awake()
        {
            Logger = base.Logger;

            StudioSaveLoadApi.RegisterExtraBehaviour<DBDESceneController>(GUID);
            CharacterApi.RegisterExtraBehaviour<DBDECharaController>(GUID);

            UI = this.GetOrAddComponent<DBDEUI>();

            AccessoriesApi.AccessoryKindChanged += AccessoryKindChanged;
            AccessoriesApi.AccessoriesCopied += AccessoryCopied;
            AccessoriesApi.AccessoryTransferred += AccessoryTransferred;

            harmony = Harmony.CreateAndPatchAll(typeof(DBDEHooks));

            MakerAPI.MakerBaseLoaded += createSideBarToggle;

            MakerAPI.MakerExiting += LeavingMaker;

            loadSettingsAsDefault = Config.Bind("", "Load Settings as default", false, "Enable this to load the settings saved by DBDE as defaults (for the revert buttons) instead of the dynamic bone's own defaults (set by game/zipmod).");
            drawGizmos = Config.Bind("", "Draw Gizmos", true, "Toggle gizmos. Can also be toggled in the UI");

            Instance = this;

            if (KKAPI.Studio.StudioAPI.StudioLoaded)
            {
                createStudioButton("Studio");
            }
        }

        private void OnDestroy()
        {
            Destroy(DBDESceneController.Instance);
            Destroy(DBDEGizmoController.Instance);
            harmony?.UnpatchSelf();
        }

        private static void LeavingMaker(object sender, EventArgs e)
        {
            UI.Close();
        }

        private void ScreenshotManager_OnPostCapture()
        {
            DBDEGizmoController gizmo = Camera.main.GetComponent<DBDEGizmoController>();
            if (gizmo != null) gizmo.enabled = true;
        }

        private void ScreenshotManager_OnPreCapture()
        {
            DBDEGizmoController gizmo = Camera.main.GetComponent<DBDEGizmoController>();
            if (gizmo != null) gizmo.enabled = false;
        }

        void Start()
        {
            SceneManager.sceneLoaded += (s, lsm) => createStudioButton(s.name);
            ScreenshotManager.OnPreCapture += ScreenshotManager_OnPreCapture;
            ScreenshotManager.OnPostCapture += ScreenshotManager_OnPostCapture;
        }

        // borrowed from Material Editor
        private void createStudioButton(string sceneName)
        {
            if (sceneName != "Studio") return;
            SceneManager.sceneLoaded -= (s, lsm) => createStudioButton(s.name);

            RectTransform original = GameObject.Find("StudioScene").transform.Find("Canvas Object List/Image Bar/Button Route").GetComponent<RectTransform>();

            DBDEStudioButton = Instantiate(original.gameObject).GetComponent<Button>();
            DBDEStudioButton.name = "Button DBDE";

            RectTransform DBDEStudioButtonTransfrom = DBDEStudioButton.transform as RectTransform;
            DBDEStudioButton.transform.SetParent(original.parent, true);
            DBDEStudioButton.transform.localScale = original.localScale;
            DBDEStudioButtonTransfrom.SetRect(original.anchorMin, original.anchorMax, original.offsetMin, original.offsetMax);

            DBDEStudioButtonTransfrom.anchoredPosition = original.anchoredPosition + new Vector2(-48f*3+4, 0f);


            Texture2D texture2D = new Texture2D(80, 80);
            texture2D.LoadImage(Convert.FromBase64String("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"));
            Image DBDEIcon = DBDEStudioButton.targetGraphic as Image;
            DBDEIcon.sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 80, 80), new Vector2(40, 40));
            DBDEIcon.color = Color.white;

            DBDEStudioButton.onClick = new Button.ButtonClickedEvent();
            DBDEStudioButton.onClick.AddListener(() => {
                List<Studio.ObjectCtrlInfo> ocis = StudioAPI.GetSelectedObjects().ToList();
                if (!ocis.IsNullOrEmpty())
                {
                    if (ocis[0] is Studio.OCIItem item)
                    {
                        DBDESceneController.Instance?.OpenDBDE(item);
                    }

                    if (ocis[0] is Studio.OCIChar cha)
                    {
                        cha.GetChaControl()?.GetComponent<DBDECharaController>()?.OpenDBDE();
                    }
                }
            });
        }

        private void toggleEvent(bool toggle)
        {
            if (toggle)
            {
                MakerAPI.GetCharacterControl()?.GetComponent<DBDECharaController>()?.OpenDBDE();
            }
            else UI.Close();
        }

        private void createSideBarToggle(object sender, RegisterCustomControlsEvent e)
        {
            toggle = e.AddSidebarControl(new SidebarToggle("DBDE", false, this));
            toggle.ValueChanged.Subscribe(toggleEvent);
        }

        private void AccessoryTransferred(object sender, AccessoryTransferEventArgs e)
        {
            int dSlot = e.DestinationSlotIndex;
            int sSlot = e.SourceSlotIndex;
            MakerAPI.GetCharacterControl().gameObject.GetComponent<DBDECharaController>().AccessoryTransferredEvent(sSlot, dSlot);
        }

        private void AccessoryCopied(object sender, AccessoryCopyEventArgs e)
        {
            ChaFileDefine.CoordinateType dType = e.CopyDestination;
            ChaFileDefine.CoordinateType sType = e.CopySource;
            IEnumerable<int> slots = e.CopiedSlotIndexes;
            MakerAPI.GetCharacterControl().gameObject.GetComponent<DBDECharaController>().AccessoryCopiedEvent((int)sType, (int)dType, slots);
        }

        private void AccessoryKindChanged(object sender, AccessorySlotEventArgs e)
        {
            int slot = e.SlotIndex;
            MakerAPI.GetCharacterControl().gameObject.GetComponent<DBDECharaController>().AccessoryChangedEvent(slot);
        }

        internal CursorManager getMakerCursorManager()
        {
            CursorManager cursorManager = base.gameObject.GetComponent<CursorManager>();
            if (!cursorManager) cursorManager = GameObject.Find("BepInEx_Manager").GetComponent<CursorManager>();
            return cursorManager;
        }
    }

    public static class Extension
    {
        private static readonly Dictionary<DynamicBone, string> cacheA = new Dictionary<DynamicBone, string>();
        private static readonly Dictionary<DynamicBone, string> cacheC = new Dictionary<DynamicBone, string>();
        private static readonly Dictionary<DynamicBone, string> cacheI = new Dictionary<DynamicBone, string>();

        /// <summary>
        /// Try to get a name by finding the path from a parent ChaAccessoryComponent (if there is any) to the root bone of the Dynamic Bone.
        /// </summary>
        /// <returns>If name could be constructed</returns>
        public static bool TryGetAccessoryQualifiedName(this DynamicBone dynamicBone, out string value)
        {
            if (!dynamicBone.m_Root)
            {
                value = null; return false;
            }
            if (cacheA.TryGetValue(dynamicBone, out value) && value.EndsWith(dynamicBone.m_Root.name)) return true;
            ChaAccessoryComponent component = dynamicBone.m_Root?.transform.GetComponentsInParent<ChaAccessoryComponent>(true)?.FirstOrDefault();
            if (!component) return false;
            value = component.transform.GetPathToChild(dynamicBone.m_Root);
            if (value == null) return false;
            cacheA[dynamicBone] = value;
            return true;
        }

        public static string GetAccessoryQualifiedName(this DynamicBone dynamicBone)
        {
            if (!dynamicBone.m_Root) return null;
            if (cacheA.TryGetValue(dynamicBone, out string value) && value.EndsWith(dynamicBone.m_Root.name)) return value;
            ChaAccessoryComponent component = dynamicBone.m_Root?.transform.GetComponentsInParent<ChaAccessoryComponent>(true)?[0];
            if (!component) return null;
            value = component.transform.GetPathToChild(dynamicBone.m_Root);
            if (value == null) return null;
            cacheA[dynamicBone] = value;
            return value;
        }

        /// <summary>
        /// Try to get a name by finding the path from a parent ChaControl (if there is any) to the root bone of the Dynamic Bone.
        /// </summary>
        /// <returns>If name could be constructed</returns>
        public static bool TryGetChaControlQualifiedName(this DynamicBone dynamicBone, out string value)
        {
            if (!dynamicBone.m_Root)
            {
                value = null; return false;
            }
            if (cacheC.TryGetValue(dynamicBone, out value) && value.EndsWith(dynamicBone.m_Root.name)) return true;
            ChaControl component = dynamicBone.m_Root?.transform.GetComponentsInParent<ChaControl>(true)?.FirstOrDefault();
            if (!component) return false;
            value = component.transform.GetPathToChild(dynamicBone.m_Root);
            if (value == null) return false;
            cacheC[dynamicBone] = value;
            return true;
        }

        public static string GetChaControlQualifiedName(this DynamicBone dynamicBone)
        {
            if (!dynamicBone.m_Root) return null;
            if (cacheA.TryGetValue(dynamicBone, out string value) && value.EndsWith(dynamicBone.m_Root.name)) return value;
            ChaControl component = dynamicBone.m_Root?.transform.GetComponentsInParent<ChaControl>(true)?[0];
            if (!component) return null;
            value = component.transform.GetPathToChild(dynamicBone.m_Root);
            if (value == null) return null;
            cacheC[dynamicBone] = value;
            return value;
        }

        /// <summary>
        /// Gets Path from this transform to one of its children.
        /// The string returned by this extension can be used with .Find() on the parent to find the child.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="child"></param>
        /// <returns></returns>
        public static string GetPathToChild(this Transform transform, Transform child)
        {
            string pPath = transform.GetFullPath().Trim().Replace(" [Transform]", "");
            string cPath = child.GetFullPath().Trim().Replace(" [Transform]", "");
            if (pPath == cPath || pPath.IsNullOrEmpty() || cPath.IsNullOrEmpty() ) return null;
            return !cPath.StartsWith(pPath) ? null : cPath.Replace(pPath, string.Empty).Remove(0,1);
        }

        /// <summary>
        /// Applies changes made to the values on the dynamic bone by updating all particles. 
        /// </summary>
        /// <param name="dynamicBone"></param>
        public static void UpdateParticles(this DynamicBone dynamicBone)
        {
            for (int i = 0; i < dynamicBone.m_Particles.Count; i++)
            {
                DynamicBone.Particle particle = dynamicBone.m_Particles[i];
                if (dynamicBone.m_BoneTotalLength > 0f)
                {
                    float time = particle.m_BoneLength / dynamicBone.m_BoneTotalLength;
                    if (dynamicBone.m_DampingDistrib != null && dynamicBone.m_DampingDistrib.keys.Length != 0)
                    {
                        particle.m_Damping = dynamicBone.m_Damping * dynamicBone.m_DampingDistrib.Evaluate(time);
                    }
                    else particle.m_Damping = dynamicBone.m_Damping;
                    if (dynamicBone.m_ElasticityDistrib != null && dynamicBone.m_ElasticityDistrib.keys.Length != 0)
                    {
                        particle.m_Elasticity = dynamicBone.m_Elasticity * dynamicBone.m_ElasticityDistrib.Evaluate(time);
                    }
                    else particle.m_Elasticity = dynamicBone.m_Elasticity;
                    if (dynamicBone.m_StiffnessDistrib != null && dynamicBone.m_StiffnessDistrib.keys.Length != 0)
                    {
                        particle.m_Stiffness = dynamicBone.m_Stiffness * dynamicBone.m_StiffnessDistrib.Evaluate(time);
                    }
                    else particle.m_Stiffness = dynamicBone.m_Stiffness;
                    if (dynamicBone.m_InertDistrib != null && dynamicBone.m_InertDistrib.keys.Length != 0)
                    {
                        particle.m_Inert = dynamicBone.m_Inert * dynamicBone.m_InertDistrib.Evaluate(time);
                    }
                    else particle.m_Inert = dynamicBone.m_Inert;
                    if (dynamicBone.m_RadiusDistrib != null && dynamicBone.m_RadiusDistrib.keys.Length != 0)
                    {
                        particle.m_Radius = dynamicBone.m_Radius * dynamicBone.m_RadiusDistrib.Evaluate(time);
                    }
                    else particle.m_Radius = dynamicBone.m_Radius;
                }
                particle.m_Damping = Mathf.Clamp01(particle.m_Damping);
                particle.m_Elasticity = Mathf.Clamp01(particle.m_Elasticity);
                particle.m_Stiffness = Mathf.Clamp01(particle.m_Stiffness);
                particle.m_Inert = Mathf.Clamp01(particle.m_Inert);
                particle.m_Radius = Mathf.Max(particle.m_Radius, 0f);

                if (particle.m_Transform != null)
                {
                    particle.m_Transform.localPosition = particle.m_InitLocalPosition;
                    particle.m_Transform.localRotation = particle.m_InitLocalRotation;
                }
            }
        }

        public static void SetRect(this RectTransform self, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            self.anchorMin = anchorMin;
            self.anchorMax = anchorMax;
            self.offsetMin = offsetMin;
            self.offsetMax = offsetMax;
        }

        public static bool AxisEdited(this EditableValue<Vector3> self, int? axis = null)
        {
            if (!axis.HasValue) return self.IsEdited;
            switch (axis.Value)
            {
                case 0:
                    return !Mathf.Approximately(self.value.x, self.initialValue.x);
                case 1:
                    return !Mathf.Approximately(self.value.y, self.initialValue.y);
                case 2:
                    return !Mathf.Approximately(self.value.z, self.initialValue.z);
                default:
                    return false;
            }
        }
    }
}

