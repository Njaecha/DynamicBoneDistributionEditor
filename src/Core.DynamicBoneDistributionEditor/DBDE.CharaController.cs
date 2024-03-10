using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using AnimationCurveEditor;
using ExtensibleSaveFormat;
using MessagePack;
using ADV.Commands.Chara;
using ActionGame;
using System.Collections;
using static Illusion.Game.Utils;

namespace DynamicBoneDistributionEditor
{
    public class DBDECharaController : CharaCustomFunctionController
    {
        Dictionary<int, List<DBDEDynamicBoneEdit>> DistributionEdits = new Dictionary<int, List<DBDEDynamicBoneEdit>>();

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            DistributionEdits.Clear();
            PluginData data = GetExtendedData();
            if (data == null) return;
            for (int cSet = 0; cSet < ChaControl.chaFile.coordinate.Length; cSet++)
            {
                List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = null;
                if (data.data.TryGetValue($"AccessoryEdits{cSet}", out var binaries) && binaries != null)
                {
                    accessoryEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<KeyValuePair<int, string>, byte[]>>>((byte[])binaries);
                }
                List<KeyValuePair<string, byte[]>> normalEdits = null;
                if (data.data.TryGetValue($"NormalEdits{cSet}", out var binaries2) && binaries2 != null)
                {
                    normalEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<string, byte[]>>>((byte[])binaries2);
                }
                StartCoroutine(LoadData(cSet, accessoryEdits, normalEdits));
            }
            StartCoroutine(RefreshBoneListDelayed());
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
                    if (!edit.IsEdited()) continue;
                    if (edit.ReidentificationData is KeyValuePair<int, string> rData)
                    {
                        accessoryEdits.Add(new KeyValuePair<KeyValuePair<int, string>, byte[]>(rData, edit.Sersialise()));
                    }
                    if (edit.ReidentificationData is string name)
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
            if (data == null) return;
            List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = null;
            if (data.data.TryGetValue("AccessoryEdits", out var binaries) && binaries != null)
            {
                accessoryEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<KeyValuePair<int, string>, byte[]>>>((byte[])binaries);
            }
            List<KeyValuePair<string, byte[]>> normalEdits = null;
            if (data.data.TryGetValue("NormalEdits", out var binaries2) && binaries2 != null)
            {
                normalEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<string, byte[]>>>((byte[])binaries2);
            }
            StartCoroutine(LoadData(ChaControl.fileStatus.coordinateType, accessoryEdits, normalEdits));

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
                if (!edit.IsEdited()) continue;
                if (edit.ReidentificationData is KeyValuePair<int, string> rData)
                {
                    accessoryEdits.Add(new KeyValuePair<KeyValuePair<int, string>, byte[]>(rData, edit.Sersialise()));
                }
                if (edit.ReidentificationData is string name)
                {
                    normalEdits.Add(new KeyValuePair<string, byte[]>(name, edit.Sersialise()));
                }
            }
            data.data.Add("AccessoryEdits", MessagePackSerializer.Serialize(accessoryEdits));
            data.data.Add("NormalEdits", MessagePackSerializer.Serialize(normalEdits));

            SetCoordinateExtendedData(coordinate, data);

            base.OnCoordinateBeingSaved(coordinate);
        }

        internal void CoordinateChangeEvent()
        {
            if (!DistributionEdits.ContainsKey(ChaControl.fileStatus.coordinateType)) return;

            StartCoroutine(ApplyCurrentDelayed());
        }

        private IEnumerator ApplyCurrentDelayed()
        {
            yield return null;
            yield return null;
            DistributionEdits[ChaControl.fileStatus.coordinateType].ForEach(d => d.ApplyAll());
        }

        private IEnumerator LoadData(int outfit, List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits, List<KeyValuePair<string, byte[]>> normalEdits)
        {
            yield return null;
            yield return null;

            if (KKAPI.Studio.StudioAPI.InsideStudio)
            {
                yield return null;
                yield return null;
            }

            while (ChaControl == null || ChaControl.objHead == null)
                yield return null;

            DistributionEdits.Remove(outfit);
            DistributionEdits.Add(outfit, new List<DBDEDynamicBoneEdit>());
            if (accessoryEdits != null)
            {
                foreach(var x in accessoryEdits)
                {
                    DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => getDynamicBone(x.Key.Value, x.Key.Key), x.Value) { ReidentificationData = x.Key });
                }
            }
            if (normalEdits != null)
            {
                foreach(var x in normalEdits)
                {
                    DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => getDynamicBone(x.Key), x.Value) { ReidentificationData = x.Key });
                }
            }

            DistributionEdits[ChaControl.fileStatus.coordinateType].ForEach(d => d.ApplyAll());
        }

        internal void AccessoryChangedEvent(int slot)
        {
            RefreshBoneList();
        }

        internal void AccessoryTransferedEvent(int source, int destination)
        {
            StartCoroutine(AccessoryTransferedDelayed(source, destination));
        }

        private IEnumerator AccessoryTransferedDelayed(int source, int destination)
        {
            yield return null;
            yield return null;

            DynamicBone[] sourcDBs = ChaControl.GetAccessoryComponent(source).GetComponentsInChildren<DynamicBone>();
            DynamicBone[] destDBs = ChaControl.GetAccessoryComponent(destination).GetComponentsInChildren<DynamicBone>();
            for (int i = 0; i < destDBs.Length; i++)
            {
                // data used for DBAccessor
                sourcDBs[i].TryGetAccessoryQualifiedName(out string name);
                int newSlot = destination;
                // find according DBDE Data on the source accessory
                DBDEDynamicBoneEdit sourceEdit = DistributionEdits[ChaControl.fileStatus.coordinateType].Find(dbde => dbde.ReidentificationData is KeyValuePair<int, string> kvp && kvp.Key == source && kvp.Value == name);
                // add new DBDE Data for DynamicBone on the destitination accessory, whilees; copying the DBDE data. 
                DistributionEdits[ChaControl.fileStatus.coordinateType].Add(new DBDEDynamicBoneEdit(() => getDynamicBone(name, newSlot), sourceEdit) { ReidentificationData = new KeyValuePair<int, string>(newSlot, name) });
                sourceEdit.ApplyAll();
            }
            StartCoroutine(RefreshBoneListDelayed());
        }

        internal void AccessoryCopiedEvent(int sourceOutfit, int destinationOutfit, IEnumerable<int> slots)
        {
            StartCoroutine(AccessoryCopiedDelayed(sourceOutfit, destinationOutfit,slots));
        }

        private IEnumerator AccessoryCopiedDelayed(int sourceOutfit, int destinationOutfit, IEnumerable<int> slots)
        {
            yield return null;
            yield return null;

            if (!DistributionEdits.ContainsKey(sourceOutfit)) yield break;
            if (!DistributionEdits.ContainsKey(destinationOutfit))
            {
                DistributionEdits.Add(destinationOutfit, new List<DBDEDynamicBoneEdit>());
            }
            DistributionEdits[sourceOutfit]
                .Where(edit => edit.ReidentificationData is KeyValuePair<int, string> kv && slots.Contains(kv.Key)) // only Edits that match the copied slots
                .ToList()
                // add a new DBDEDynamicBoneEdit with the same accessor (slot stays the same), same identifier (slot) and the same values
                .ForEach(edit => DistributionEdits[destinationOutfit].Add(new DBDEDynamicBoneEdit(edit.AccessorFunciton, edit) { ReidentificationData = edit.ReidentificationData }));
            
            StartCoroutine(RefreshBoneListDelayed());
        }

        public void OpenDBDE()
        {
            OpenDBDE(ChaControl.fileStatus.coordinateType);
        }

        private void OpenDBDE(int outfit)
        {
            RefreshBoneList(outfit);
            DBDE.UI.Open(() => DistributionEdits[outfit], RefreshBoneList);
            DBDE.UI.TitleAppendix = ChaControl.chaFile.GetFancyCharacterName();
        }

        public IEnumerator RefreshBoneListDelayed()
        {
            yield return null;
            RefreshBoneList();
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
            List<KeyValuePair<string, DynamicBone>> nonAccDBs = ChaControl.GetComponentsInChildren<DynamicBone>(true)
                .Where(db => db.GetComponentsInParent<ChaAccessoryComponent>().IsNullOrEmpty() && db.TryGetChaControlQualifiedName(out _) && db.m_Root != null )
                .Select(db => new KeyValuePair<string, DynamicBone>(db.GetChaControlQualifiedName(), db))
                .GroupBy(pair => pair.Key)
                .Select(group => group.First())
                .ToList();

            nonAccDBs.Where(pair => !DistributionEdits[outfit].Any(a => a.ReidentificationData is string q && q==pair.Key))
                .ToList().ForEach(pair => {
                    DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => getDynamicBone(pair.Key)) { ReidentificationData = pair.Key }); 
                });

            // for accessory dynamic bones
            for(int i = 0; i < ChaControl.infoAccessory.Length; i++)
            {
                int slot = i;
                ChaAccessoryComponent accs = ChaControl.GetAccessoryComponent(slot);
                if (accs != null)
                {
                    List<KeyValuePair<string, DynamicBone>> accDbs = accs.GetComponentsInChildren<DynamicBone>(true)
                        .Where(db => db.TryGetAccessoryQualifiedName(out _) && db.m_Root != null)
                        .Select(db => new KeyValuePair<string, DynamicBone>(db.GetAccessoryQualifiedName(), db))
                        .GroupBy(pair => pair.Key)
                        .Select(group => group.First())
                        .ToList();

                    if (accDbs.IsNullOrEmpty()) continue;
                    accDbs.Where(pair => !DistributionEdits[outfit].Any(edit => 
                        (edit.ReidentificationData is KeyValuePair<int, string> kv) && kv.Key==slot && kv.Value==pair.Key))
                        .ToList().ForEach(pair => {
                            DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => getDynamicBone(pair.Key, slot)) { ReidentificationData = new KeyValuePair<int, string>(slot, pair.Key) });
                        });
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

        private DynamicBone getDynamicBone(string name, int? slot = null)
        {
            List<DynamicBone> searchList = new List<DynamicBone>();
            if (slot.HasValue) searchList = ChaControl.GetAccessoryComponent(slot.Value)?.GetComponentsInChildren<DynamicBone>(true)?
                    .Where(db => db.TryGetAccessoryQualifiedName(out string n) && n == name && db.m_Root != null)
                    .Select(db => new KeyValuePair<string, DynamicBone>(db.GetAccessoryQualifiedName(), db))
                    .GroupBy(pair => pair.Key)
                    .Select(group => group.First().Value)
                    .ToList();

            else searchList = ChaControl.GetComponentsInChildren<DynamicBone>(true)?
                    .Where(db => db.GetComponentsInParent<ChaAccessoryComponent>(true).IsNullOrEmpty() && db.TryGetChaControlQualifiedName(out string n) && n == name && db.m_Root != null)
                    .Select(db => new KeyValuePair<string, DynamicBone>(db.GetChaControlQualifiedName(), db))
                    .GroupBy(pair => pair.Key)
                    .Select(group => group.First().Value)
                    .ToList();

            if (searchList.IsNullOrEmpty()) return null;
            if (searchList.Count > 1) DBDE.Logger.LogWarning($"WARNING: Ambiguous result for dynamic bone with qualified name {name} and slot {slot}. Using first value in list, this might cause issues!!");
            return searchList[0];
        }
    }
}

