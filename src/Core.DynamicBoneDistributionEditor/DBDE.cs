using System;
using BepInEx;
using BepInEx.Logging;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Studio.SaveLoad;

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

        void Awake()
        {
            Logger = base.Logger;

            StudioSaveLoadApi.RegisterExtraBehaviour<DBDESceneController>(GUID);
            CharacterApi.RegisterExtraBehaviour<DBDECharaController>(GUID);

            Instance = this;
        }
    }
}

