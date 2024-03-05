using System;
using System.Collections.Generic;
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

        internal static DBDEUI UI;

        void Awake()
        {
            Logger = base.Logger;

            StudioSaveLoadApi.RegisterExtraBehaviour<DBDESceneController>(GUID);
            CharacterApi.RegisterExtraBehaviour<DBDECharaController>(GUID);

            UI = this.GetOrAddComponent<DBDEUI>();

            KKAPI.Maker.AccessoriesApi.AccessoryKindChanged += AccessoryKindChanged;
            KKAPI.Maker.AccessoriesApi.AccessoriesCopied += AccessoryCopied;
            KKAPI.Maker.AccessoriesApi.AccessoryTransferred += AccessoryTransferred;

            Instance = this;
        }

        private void AccessoryTransferred(object sender, KKAPI.Maker.AccessoryTransferEventArgs e)
        {
            int dSlot = e.DestinationSlotIndex;
            int sSlot = e.SourceSlotIndex;
            KKAPI.Maker.MakerAPI.GetCharacterControl().gameObject.GetComponent<DBDECharaController>().AccessoryTransferedEvent(sSlot, dSlot);
        }

        private void AccessoryCopied(object sender, KKAPI.Maker.AccessoryCopyEventArgs e)
        {
            ChaFileDefine.CoordinateType dType = e.CopyDestination;
            ChaFileDefine.CoordinateType sType = e.CopySource;
            IEnumerable<int> slots = e.CopiedSlotIndexes;
            KKAPI.Maker.MakerAPI.GetCharacterControl().gameObject.GetComponent<DBDECharaController>().AccessoryCopiedEvent((int)sType, (int)dType, slots);
        }

        private void AccessoryKindChanged(object sender, KKAPI.Maker.AccessorySlotEventArgs e)
        {
            int slot = e.SlotIndex;
            KKAPI.Maker.MakerAPI.GetCharacterControl().gameObject.GetComponent<DBDECharaController>().AccessoryChangedEvent(slot);
        }
    }
}

