using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI.Sidebar;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;
using IllusionFixes;

namespace DynamicBoneDistributionEditor
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public class DBDE : BaseUnityPlugin
    {
        public const string PluginName = "DynamicBoneDistributionEditor";
        public const string GUID = "org.njaecha.plugins.dbde";
        public const string Version = "0.0.1";

        internal new static ManualLogSource Logger;
        internal static DBDE Instance;

        internal static DBDEUI UI;

        internal static SidebarToggle toggle;

        void Awake()
        {
            Logger = base.Logger;

            StudioSaveLoadApi.RegisterExtraBehaviour<DBDESceneController>(GUID);
            CharacterApi.RegisterExtraBehaviour<DBDECharaController>(GUID);

            UI = this.GetOrAddComponent<DBDEUI>();

            AccessoriesApi.AccessoryKindChanged += AccessoryKindChanged;
            AccessoriesApi.AccessoriesCopied += AccessoryCopied;
            AccessoriesApi.AccessoryTransferred += AccessoryTransferred;

            Harmony.CreateAndPatchAll(typeof(DBDEHooks));

            MakerAPI.MakerBaseLoaded += createSideBarToggle;

            Instance = this;
        }

        private void toggleEvent(bool toggle)
        {
            if (toggle) MakerAPI.GetCharacterControl()?.GetComponent<DBDECharaController>()?.OpenDBDE();
        }

        private void createSideBarToggle(object sender, RegisterCustomControlsEvent e)
        {
            toggle = e.AddSidebarControl(new SidebarToggle("DynamicBone Distribution Edit", false, this));
            toggle.ValueChanged.Subscribe(toggleEvent);
        }

        private void AccessoryTransferred(object sender, AccessoryTransferEventArgs e)
        {
            int dSlot = e.DestinationSlotIndex;
            int sSlot = e.SourceSlotIndex;
            MakerAPI.GetCharacterControl().gameObject.GetComponent<DBDECharaController>().AccessoryTransferedEvent(sSlot, dSlot);
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

        internal CursorManager getMakerCursorMangaer()
        {
            return base.gameObject.GetComponent<CursorManager>();
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
            if (dynamicBone.m_Root == null)
            {
                value = null; return false;
            }
            if (cacheA.TryGetValue(dynamicBone, out value) && value.EndsWith(dynamicBone.m_Root.name)) return true;
            ChaAccessoryComponent component = dynamicBone.m_Root?.transform.GetComponentInParent<ChaAccessoryComponent>();
            if (component == null) return false;
            string rootBonePath = dynamicBone.m_Root.transform.GetFullPath().Trim().Replace(" [Transform]", "");
            string componentPath = component.transform.GetFullPath().Trim().Replace(" [Transform]", "");
            if (!rootBonePath.StartsWith(componentPath)) return false;
            value = rootBonePath.Replace(componentPath, string.Empty);
            cacheA[dynamicBone] = value;
            return true;
        }

        public static string GetAccessoryQualifiedName(this DynamicBone dynamicBone)
        {
            if (dynamicBone.m_Root == null) return null;
            if (cacheA.TryGetValue(dynamicBone, out string value) && value.EndsWith(dynamicBone.m_Root.name)) return value;
            ChaAccessoryComponent component = dynamicBone.m_Root?.transform.GetComponentInParent<ChaAccessoryComponent>();
            if (component == null) return null;
            string rootBonePath = dynamicBone.m_Root.transform.GetFullPath().Trim().Replace(" [Transform]", "");
            string componentPath = component.transform.GetFullPath().Trim().Replace(" [Transform]", "");
            if (!rootBonePath.StartsWith(componentPath)) return null;
            value = rootBonePath.Replace(componentPath, string.Empty);
            cacheA[dynamicBone] = value;
            return value;
        }

        /// <summary>
        /// Try to get a name by finding the path from a parent ChaControl (if there is any) to the root bone of the Dynamic Bone.
        /// </summary>
        /// <returns>If name could be constructed</returns>
        public static bool TryGetChaControlQualifiedName(this DynamicBone dynamicBone, out string value)
        {
            if (dynamicBone.m_Root == null)
            {
                value = null; return false;
            }
            if (cacheC.TryGetValue(dynamicBone, out value) && value.EndsWith(dynamicBone.m_Root.name)) return true;
            ChaControl component = dynamicBone.m_Root?.transform.GetComponentInParent<ChaControl>();
            if (component == null) return false;
            string rootBonePath = dynamicBone.m_Root?.transform.GetFullPath().Trim().Replace(" [Transform]", "");
            string componentPath = component.transform.GetFullPath().Trim().Replace(" [Transform]", "");
            if (!rootBonePath.StartsWith(componentPath)) return false;
            value = rootBonePath.Replace(componentPath, string.Empty);
            cacheC[dynamicBone] = value;
            return true;
        }

        public static string GetChaControlQualifiedName(this DynamicBone dynamicBone)
        {
            if (dynamicBone.m_Root == null) return null;
            if (cacheA.TryGetValue(dynamicBone, out string value) && value.EndsWith(dynamicBone.m_Root.name)) return value;
            ChaControl component = dynamicBone.m_Root?.transform.GetComponentInParent<ChaControl>();
            if (component == null) return null;
            string rootBonePath = dynamicBone.m_Root.transform.GetFullPath().Trim().Replace(" [Transform]", "");
            string componentPath = component.transform.GetFullPath().Trim().Replace(" [Transform]", "");
            if (!rootBonePath.StartsWith(componentPath)) return null;
            value = rootBonePath.Replace(componentPath, string.Empty);
            cacheC[dynamicBone] = value;
            return value;
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
            }
        }
    }
}

