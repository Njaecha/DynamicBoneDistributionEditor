using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KKAPI;
using KKAPI.Chara;
using AnimationCurveEditor;
using ExtensibleSaveFormat;
using MessagePack;
using ADV.Commands.Chara;

namespace DynamicBoneDistributionEditor
{
    public class DBDECharaController : CharaCustomFunctionController
    {
        Dictionary<int, List<DBDEDynamicBoneEdit>> DistributionEdits = new Dictionary<int, List<DBDEDynamicBoneEdit>>();

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            PluginData data = GetExtendedData();
            for (int cSet = 0; cSet < ChaControl.chaFile.coordinate.Length; cSet++)
            {
                List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = null;
                if (data.data.TryGetValue($"AccessoryeEdits{cSet}", out var binaries) && binaries != null)
                {
                    accessoryEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<KeyValuePair<int, string>, byte[]>>>((byte[])binaries);
                }
                List<KeyValuePair<string, byte[]>> normalEdits = null;
                if (data.data.TryGetValue($"NormalEdits{cSet}", out var binaries2) && binaries2 != null)
                {
                    normalEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<string, byte[]>>>((byte[])binaries2);
                }
                LoadData(cSet, accessoryEdits, normalEdits);
            }
            RefreshBoneList();
            base.OnReload(currentGameMode, maintainState);
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            PluginData data = new PluginData();
            RefreshBoneList();
            // serialise edits list for current coordinate
            foreach (int key in DistributionEdits.Keys)
            {
                List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = new List<KeyValuePair<KeyValuePair<int, string>, byte[]>>();
                List<KeyValuePair<string, byte[]>> normalEdits = new List<KeyValuePair<string, byte[]>>();
                foreach (DBDEDynamicBoneEdit edit in DistributionEdits[key])
                {
                    if (edit.RedindificiationData is KeyValuePair<int, string> rData)
                    {
                        accessoryEdits.Add(new KeyValuePair<KeyValuePair<int, string>, byte[]>(rData, edit.Sersialise()));
                    }
                    if (edit.RedindificiationData is string name)
                    {
                        normalEdits.Add(new KeyValuePair<string, byte[]>(name, edit.Sersialise()));
                    }
                }
                data.data.Add($"AccessoryEdits{key}", MessagePackSerializer.Serialize(accessoryEdits));
                data.data.Add($"NormalEdits{key}", MessagePackSerializer.Serialize(normalEdits));
            }
            SetExtendedData(data);
        }

        public void ReloadCoordianteData()
        {
            OnCoordinateBeingLoaded(ChaControl.nowCoordinate);
        }

        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate)
        {
            PluginData data = GetCoordinateExtendedData(coordinate);
            List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = null;
            if (data.data.TryGetValue("AccessoryeEdits", out var binaries) && binaries != null)
            {
                accessoryEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<KeyValuePair<int, string>, byte[]>>>((byte[])binaries);
            }
            List<KeyValuePair<string, byte[]>> normalEdits = null;
            if (data.data.TryGetValue("NormalEdits", out var binaries2) && binaries2 != null)
            {
                normalEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<string, byte[]>>>((byte[])binaries2);
            }
            LoadData(ChaControl.fileStatus.coordinateType, accessoryEdits, normalEdits);

            RefreshBoneList();
            base.OnCoordinateBeingLoaded(coordinate);
        }

        protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
        {
            PluginData data = new PluginData();
            RefreshBoneList();
            // serialise edits list for current coordinate
            List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = new List<KeyValuePair<KeyValuePair<int, string>, byte[]>>();
            List<KeyValuePair<string, byte[]>> normalEdits = new List<KeyValuePair<string, byte[]>>();
            foreach(DBDEDynamicBoneEdit edit in DistributionEdits[ChaControl.fileStatus.coordinateType])
            {
                if (edit.RedindificiationData is KeyValuePair<int, string> rData)
                {
                    accessoryEdits.Add(new KeyValuePair<KeyValuePair<int, string>, byte[]>(rData, edit.Sersialise()));
                }
                if (edit.RedindificiationData is string name)
                {
                    normalEdits.Add(new KeyValuePair<string, byte[]>(name, edit.Sersialise()));
                }
            }
            data.data.Add("AccessoryEdits", MessagePackSerializer.Serialize(accessoryEdits));
            data.data.Add("NormalEdits", MessagePackSerializer.Serialize(normalEdits));

            SetCoordinateExtendedData(coordinate, data);

            base.OnCoordinateBeingSaved(coordinate);
        }

        protected override void OnReload(GameMode currentGameMode)
        {
            base.OnReload(currentGameMode);
        }

        private void LoadData(int outfit, List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits, List<KeyValuePair<string, byte[]>> normalEdits)
        {
            DistributionEdits.Remove(outfit);
            DistributionEdits.Add(outfit, new List<DBDEDynamicBoneEdit>());
            if (accessoryEdits != null)
            {
                foreach(var x in accessoryEdits)
                {
                    DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => getDynamicBone(x.Key.Value, x.Key.Key), x.Value) { RedindificiationData = x.Key });
                }
            }
            if (normalEdits != null)
            {
                foreach(var x in normalEdits)
                {
                    DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => getDynamicBone(x.Key), x.Value) { RedindificiationData = x.Key });
                }
            }
        }

        internal void AccessoryChangedEvent(int slot)
        {
            RefreshBoneList();
        }

        internal void AccessoryTransferedEvent(int source, int destination)
        {
            DynamicBone[] sourcDBs = ChaControl.GetAccessoryComponent(source).GetComponentsInChildren<DynamicBone>();
            DynamicBone[] destDBs = ChaControl.GetAccessoryComponent(destination).GetComponentsInChildren<DynamicBone>();
            for (int i = 0; i < destDBs.Length; i++)
            {
                // data used for DBAccessor
                string name = destDBs[i].m_Root.name;
                int slot = destination;
                // find according DBDE Data on the source accessory
                DBDEDynamicBoneEdit sourceEdit = DistributionEdits[ChaControl.fileStatus.coordinateType].Find(dbde => dbde.dynamicBone.Equals(sourcDBs[i]));
                // add new DBDE Data for DynamicBone on the destitination accessory, whilees; copying the DBDE data. 
                DistributionEdits[ChaControl.fileStatus.coordinateType].Add(new DBDEDynamicBoneEdit(() => getDynamicBone(name, slot), sourceEdit) { RedindificiationData = new KeyValuePair<int, string> (slot, name) });
            }
            RefreshBoneList();
        }

        internal void AccessoryCopiedEvent(int sourceOutfit, int destinationOutfit, IEnumerable<int> slots)
        {
            if (!DistributionEdits.ContainsKey(sourceOutfit)) return;
            if (!DistributionEdits.ContainsKey(destinationOutfit))
            {
                DistributionEdits.Add(destinationOutfit, new List<DBDEDynamicBoneEdit>());
            }
            DistributionEdits[sourceOutfit]
                .Where(edit => edit.RedindificiationData is KeyValuePair<int, string> kv && slots.Contains(kv.Key)) // only Edits that match the copied slots
                .ToList()
                // add a new DBDEDynamicBoneEdit with the same accessor (slot stays the same), same identifier (slot) and the same values
                .ForEach(edit => DistributionEdits[destinationOutfit].Add(new DBDEDynamicBoneEdit(edit.AccessorFunciton, edit) { RedindificiationData = edit.RedindificiationData }));

        }

        public void OpenDBDE(int outfit)
        {
            RefreshBoneList(outfit);
            DBDE.UI.Open(() => DistributionEdits[outfit]);
        }

        public void RefreshBoneList()
        {
            RefreshBoneList(ChaControl.fileStatus.coordinateType);
        }

        public void RefreshBoneList(int outfit)
        {
            if (!DistributionEdits.ContainsKey(outfit))
            {
                DistributionEdits.Add(outfit, new List<DBDEDynamicBoneEdit>());
            }
            if (outfit != ChaControl.fileStatus.coordinateType) return;

            // == add new DBDEDynamicBoneEdits for found DBs that dont have a accorind DBDEDynamicBoneEdit.
            // == splitup between accessories and body/cloth dynamic bones to reduce possibility for ambiguous results in the getDynamicBone method.

            // for non accessory dynamic bones
            List<DynamicBone> nonAccDBs = ChaControl.GetComponentsInChildren<DynamicBone>(true).Where(db => db.GetComponentsInParent<ChaAccessoryComponent>().IsNullOrEmpty()).ToList();
            nonAccDBs.Where(b => !DistributionEdits[outfit].Any(a => a.dynamicBone.Equals(b)))
                .ToList().ForEach(b => DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => getDynamicBone(b.m_Root.name)) { RedindificiationData = b.m_Root.name}));

            // for accessory dynamic bones
            for(int i = 0; i < ChaControl.infoAccessory.Length; i++)
            {
                int slot = i;
                ChaAccessoryComponent accs = ChaControl.GetAccessoryComponent(slot);
                if (accs != null)
                {
                    List<DynamicBone> accDbs = accs.GetComponentsInChildren<DynamicBone>(true).ToList();
                    accDbs.Where(b => !DistributionEdits[outfit].Any(a => a.dynamicBone.Equals(b)))
                        .ToList().ForEach(b => DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => getDynamicBone(b.m_Root.name, slot)) { RedindificiationData = new KeyValuePair<int,string>(slot, b.m_Root.name)}));
                }
            }


            // == remove DBDEDynamicBoneEdits whose dynamic bones could not be found anymore.
            DistributionEdits[outfit].RemoveAll(a => a.dynamicBone == null);

            // == order list so that it matches the output of GetComponentsInChildren()
            DynamicBone[] foundDBs = ChaControl.GetComponentsInChildren<DynamicBone>();
            // AI generated this, idk if this works...
            DistributionEdits[outfit] = DistributionEdits[outfit]
                .Join(foundDBs.Select(
                    (b, i) => new { B = b, Index = i }),
                    a => a.dynamicBone,
                    tmp => tmp.B,
                    (a, tmp) => new { A = a, tmp.Index })
                .OrderBy(x => x.Index)
                .Select(x => x.A)
                .ToList();
            
        }

        private DynamicBone getDynamicBone(string name, int? slot = null, bool useRootName = true)
        {
            List<DynamicBone> searchList = new List<DynamicBone>();
            if (slot.HasValue) searchList = ChaControl.GetAccessoryComponent(slot.Value)?.GetComponentsInChildren<DynamicBone>()?.ToList().FindAll(db => (useRootName ? db.m_Root.name.Equals(name) : db.name.Equals(name)));
            else searchList = ChaControl.GetComponentsInChildren<DynamicBone>()?.ToList().FindAll(db => (useRootName ? db.m_Root.name.Equals(name) : db.name.Equals(name)) && db.GetComponentsInParent<ChaAccessoryComponent>().IsNullOrEmpty());

            if (searchList.Count > 1) DBDE.Logger.LogWarning($"WARNING: Ambiguous result for dynamic bone with name {name} (usingRootName={useRootName}) and slot {slot}. Using first value in list, this might cause issues!!");
            if (searchList.IsNullOrEmpty()) return null;
            return searchList[0];
        }
    }
}

