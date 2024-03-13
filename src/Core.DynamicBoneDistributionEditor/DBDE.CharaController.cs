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
using DBDE.KK_Plugins.DynamicBoneEditor;
using BepInEx.Bootstrap;
using KKAPI.Studio;
using UnityEngine.UI;
using ActionGame.Chara.Mover;

namespace DynamicBoneDistributionEditor
{
    public class DBDECharaController : CharaCustomFunctionController
    {
        internal Dictionary<int, List<DBDEDynamicBoneEdit>> DistributionEdits = new Dictionary<int, List<DBDEDynamicBoneEdit>>();

        // used to transfer plugin data from Coordinate Load Options temp character to the real characters.
        private static PluginData cloTransferPluginData;

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            DistributionEdits.Clear();
            PluginData data = GetExtendedData();
            PluginData DBE_data = Chainloader.PluginInfos.ContainsKey("com.deathweasel.bepinex.dynamicboneeditor") ? null : ExtendedSave.GetExtendedDataById(MakerAPI.LastLoadedChaFile ?? ChaFileControl, "com.deathweasel.bepinex.dynamicboneeditor");
            if (data == null && DBE_data == null) return;
            for (int cSet = 0; cSet < ChaControl.chaFile.coordinate.Length; cSet++)
            {
                List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = null;
                if (data != null && data.data.TryGetValue($"AccessoryEdits{cSet}", out var binaries) && binaries != null)
                {
                    accessoryEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<KeyValuePair<int, string>, byte[]>>>((byte[])binaries);
                }
                List<KeyValuePair<string, byte[]>> normalEdits = null;
                if (data != null && data.data.TryGetValue($"NormalEdits{cSet}", out var binaries2) && binaries2 != null)
                {
                    normalEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<string, byte[]>>>((byte[])binaries2);
                }
                List<DynamicBoneData> DBEEdits = new List<DynamicBoneData>();
                if (DBE_data != null && DBE_data.data.TryGetValue("AccessoryDynamicBoneData", out var binaries3) && binaries3 != null)
                {
                    DBDE.Logger.LogInfo($"Found DynamicBoneEditor data on outfit {cSet}. DBDE will try to load it...");
                    var x = MessagePackSerializer.Deserialize<List<DynamicBoneData>>((byte[])binaries3);
                    if (x != null) DBEEdits = x.Where(d => d.CoordinateIndex == cSet).ToList();
                }
                StartCoroutine(LoadData(cSet, accessoryEdits, normalEdits, DBEEdits));
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

        internal IEnumerator removeCloTransferPluginData()
        {
            yield return null;
            yield return null;
            cloTransferPluginData = null;
        }

        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate)
        {

            bool loadAccessories = true;
            bool cMode = false;
            List<int> cloImportAccessories = new List<int>(); // slots that are loaded new
            if (Chainloader.PluginInfos.ContainsKey("com.jim60105.kks.coordinateloadoption"))
            {
                DBDE.Logger.LogInfo("Coordinate Load Option dedected");
                if (GameObject.Find("CoordinateTooglePanel")?.activeInHierarchy == true)
                {
                    DBDE.Logger.LogInfo("Coordinate Load Option enabled");
                    bool? accEnabled = GameObject.Find("CoordinateTooglePanel/accessories")?.GetComponent<Toggle>()?.isOn;
                    if (accEnabled == true)
                    {
                        DBDE.Logger.LogInfo("Coordinate Load Option accessory load enabled, entering compatibility mode");
                        cMode = true;

                        if (GameObject.Find("CoordinateTooglePanel/AccessoriesTooglePanel/BtnChangeAccLoadMode")?.GetComponentInChildren<Text>()?.text != "Replace Mode")
                        {
                            DBDE.Logger.LogMessage("DBDE WARNING: Add Mode is not supported! DBDE Data will not be loaded");
                            loadAccessories = false;
                        }

                        GameObject list = GameObject.Find("CoordinateTooglePanel/AccessoriesTooglePanel/scroll/Viewport/Content");
                        for (int i = 0; i < list.transform.childCount; i++)
                        {
                            GameObject item = list.transform.GetChild(i).gameObject;
                            bool? isOn = item.GetComponent<Toggle>()?.isOn;
                            bool isEmpty = item.transform.Find("Label")?.gameObject.GetComponent<Text>()?.text == "Empty";

                            if (isOn == true && !isEmpty)
                            {
                                cloImportAccessories.Add(i);
                            }
                        }

                    }
                    else if (accEnabled == false)
                    {
                        DBDE.Logger.LogInfo("Coordinate Load Option accessory load disabled -> do not load new DBDE asset data.");
                        loadAccessories = false;
                    }
                }
            }

            // Maker partial coordinate load fix
            if (MakerAPI.InsideAndLoaded)
            {
                // dont load accessories if they are not loaded
                if (GameObject.Find("cosFileControl")?.GetComponentInChildren<ChaCustom.CustomFileWindow>()?.tglCoordeLoadAcs.isOn == false) loadAccessories = false;
            }

            PluginData data = null;
            if (cMode) // grab transfer plugindata if exists
            {
                if (cloTransferPluginData != null) data = cloTransferPluginData;
                else
                {
                    data = cloTransferPluginData = GetCoordinateExtendedData(coordinate);
                    if (data != null)
                    {
                        // remove transfer plugindata after load is finished; Coroutine cannot be started on *this* as it's being destroyed by clo too early
                        DBDE.Instance.StartCoroutine(removeCloTransferPluginData());
                    }
                }
            }
            else data = GetCoordinateExtendedData(coordinate);

            PluginData DBE_data = Chainloader.PluginInfos.ContainsKey("com.deathweasel.bepinex.dynamicboneeditor") ? null : ExtendedSave.GetExtendedDataById(coordinate, "com.deathweasel.bepinex.dynamicboneeditor"); // load data from DynamicBoneEditor
            if (data == null && DBE_data == null) return;
            List<KeyValuePair<string, byte[]>> normalEdits = null;
            if (data != null && data.data.TryGetValue("NormalEdits", out var binaries2) && binaries2 != null)
            {
                normalEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<string, byte[]>>>((byte[])binaries2);
            }
            List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = null;
            if (loadAccessories && data != null && data.data.TryGetValue("AccessoryEdits", out var binaries) && binaries != null)
            {
                accessoryEdits = MessagePackSerializer.Deserialize<List<KeyValuePair<KeyValuePair<int, string>, byte[]>>>((byte[])binaries);
                if (cMode && accessoryEdits.IsNullOrEmpty())
                {
                    accessoryEdits = accessoryEdits.Where(x => cloImportAccessories.Contains(x.Key.Key)).ToList(); // keep only edits of which the accessory slot is being loaded
                }
            }
            List<DynamicBoneData> DBEEdits = new List<DynamicBoneData>();
            if (loadAccessories && DBE_data != null && DBE_data.data.TryGetValue("AccessoryDynamicBoneData", out var binaries3) && binaries3 != null)
            {
                DBDE.Logger.LogInfo($"Found DynamicBoneEditor data on outfit {ChaControl.fileStatus.coordinateType}. DBDE will try to load it...");
                DBEEdits = MessagePackSerializer.Deserialize<List<DynamicBoneData>>((byte[])binaries3);
                if (cMode && DBEEdits.IsNullOrEmpty())
                {
                    DBEEdits = DBEEdits.Where(x => cloImportAccessories.Contains(x.Slot)).ToList(); // keep only edits of which the accessory slot is being loaded
                }
            }

            StartCoroutine(LoadData(ChaControl.fileStatus.coordinateType, accessoryEdits, normalEdits, DBEEdits, cMode));

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
            DBDE.UI.UpdateUIWhileOpen = false;
            StartCoroutine(ApplyCurrentDelayed());
        }

        private IEnumerator ApplyCurrentDelayed()
        {
            yield return null;
            yield return null;
            yield return null; // for good measure
            if (KKAPI.Studio.StudioAPI.InsideStudio)
            {
                yield return null;
                yield return null;
            }
            RefreshBoneList();
            DistributionEdits[ChaControl.fileStatus.coordinateType].ForEach(d => d.ApplyAll());
            DBDE.UI.UpdateUIWhileOpen = true;
        }

        private IEnumerator LoadData(int outfit, List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits, List<KeyValuePair<string, byte[]>> normalEdits, List<DynamicBoneData> DBE_Data, bool cMode = false)
        {
            // wait until the user switches to this coordinate
            // else I cant create DBDEDynamicBoneEdit because I need to have the dynamic bones to do so, but they only exist after oufit is active
            while (ChaControl.fileStatus.coordinateType != outfit)
                yield return null; 
            
            yield return null;
            yield return null;

            if (KKAPI.Studio.StudioAPI.InsideStudio)
            {
                yield return null;
                yield return null;
            }

            while (ChaControl == null || ChaControl.objHead == null)
                yield return null;
            

            //if (!cMode) DistributionEdits.Remove(outfit); // retain data if coordinate load option is used
            if (!DistributionEdits.ContainsKey(outfit)) DistributionEdits.Add(outfit, new List<DBDEDynamicBoneEdit>());
            DBDE.Logger.LogInfo(DistributionEdits[outfit]);
            if ((accessoryEdits != null || DBE_Data != null))
            {
                if (accessoryEdits != null)
                {
                    foreach(var x in accessoryEdits)
                    {
                        if (DBE_Data != null)
                        {
                            List<DynamicBoneData> searchResults = DBE_Data.FindAll(data => outfit == data.CoordinateIndex && x.Key.Key == data.Slot && x.Key.Value.EndsWith(data.BoneName));
                            // warn if multiple thigns found
                            if (!searchResults.IsNullOrEmpty() && searchResults.Count > 1) DBDE.Logger.LogWarning($"Found multiple DynamicBoneData saved on the card that match Outfit={outfit} Slot={x.Key.Key} BoneName={x.Key.Value.Split('/').Last()}. Using first entry.");
                            
                            if (!searchResults.IsNullOrEmpty()) // if something was found
                            {
                                DBE_Data.RemoveAll(data => searchResults.Contains(data)); // remove because its already taken into account
                                DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(x.Key.Value, x.Key.Key), x.Value, searchResults[0]) { ReidentificationData = x.Key }); // new Edit with DBE data
                            }
                            else DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(x.Key.Value, x.Key.Key), x.Value) { ReidentificationData = x.Key }); // new Edit without DBE data
                        }
                        else DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(x.Key.Value, x.Key.Key), x.Value) { ReidentificationData = x.Key }); // new Edit without DBE data
                    }
                }
                if (!DBE_Data.IsNullOrEmpty()) // if DBE data still contains entries that havent been loaded together with DBDE data, create Edits for them
                {
                    foreach (var data in DBE_Data.Where(data => data.CoordinateIndex == outfit))
                    {
                        // try to find the bone that DynamicBoneEditor meant
                        List<DynamicBone> foundDBs = ChaControl.GetAccessoryComponent(data.Slot)?.GetComponentsInChildren<DynamicBone>(true)?.ToList().FindAll(db => db.m_Root != null && db.m_Root.name == data.BoneName);
                        if (foundDBs.IsNullOrEmpty())
                        {
                            DBDE.Logger.LogWarning($"Found zero DynamicBones matching Name={data.BoneName} on Slot {data.Slot}  (Outfit {outfit}). Skipping enrty.");
                            continue;
                        }
                        if (foundDBs.Count > 1) DBDE.Logger.LogWarning($"Found multiple DynamicBones matching Name={data.BoneName} on Slot {data.Slot}  (Outfit {outfit}). Using first entry.");

                        int slot = data.Slot;
                        string qName = foundDBs[0].GetAccessoryQualifiedName();
                        if (qName.IsNullOrEmpty()) DBDE.Logger.LogError($"Couldn't retrive Accessory Qualified Name for DynamicBone with BasicName={data.BoneName} on Slot {data.Slot}  (Outfit {outfit}). Skipping Entry.");
                        else DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(qName, slot), null, data) { ReidentificationData = new KeyValuePair<int, string>(slot, qName) }); // new Edit with only DBE data
                    }
                }
            }
            if (normalEdits != null)
            {
                foreach(var x in normalEdits)
                {
                    DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(x.Key), x.Value) { ReidentificationData = x.Key });
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
            DBDE.UI.UpdateUIWhileOpen = false;
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
                DistributionEdits[ChaControl.fileStatus.coordinateType].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(name, newSlot), sourceEdit) { ReidentificationData = new KeyValuePair<int, string>(newSlot, name) });
                sourceEdit.ApplyAll();
            }
            StartCoroutine(RefreshBoneListDelayed());
            DBDE.UI.UpdateUIWhileOpen = true;
        }

        internal void AccessoryCopiedEvent(int sourceOutfit, int destinationOutfit, IEnumerable<int> slots)
        {
            StartCoroutine(AccessoryCopiedDelayed(sourceOutfit, destinationOutfit,slots));
        }

        private IEnumerator AccessoryCopiedDelayed(int sourceOutfit, int destinationOutfit, IEnumerable<int> slots)
        {
            DBDE.UI.UpdateUIWhileOpen = false;
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
            DBDE.UI.UpdateUIWhileOpen = true;
        }

        public void OpenDBDE()
        {
            RefreshBoneList();
            DBDE.UI.Open(() => DistributionEdits[ChaControl.fileStatus.coordinateType], RefreshBoneList);
            DBDE.UI.TitleAppendix = ChaControl.chaFile.GetFancyCharacterName();
        }

        public IEnumerator RefreshBoneListDelayed()
        {
            yield return null;
            RefreshBoneList();
        }

        private void RefreshBoneList()
        {
            int outfit = ChaControl.fileStatus.coordinateType;
            if (!DistributionEdits.ContainsKey(outfit))
            {
                DistributionEdits.Add(outfit, new List<DBDEDynamicBoneEdit>());
            }
            if (outfit != ChaControl.fileStatus.coordinateType) return;

            // == add new DBDEDynamicBoneEdits for found DBs that dont have a according DBDEDynamicBoneEdit.
            // == splitup between accessories and body/cloth dynamic bones to reduce possibility for ambiguous results in the getDynamicBone method.

            // for non accessory dynamic bones
            List<KeyValuePair<string, DynamicBone>> nonAccDBs = ChaControl.GetComponentsInChildren<DynamicBone>(true)
                .Where(db => db.GetComponentsInParent<ChaAccessoryComponent>(true).IsNullOrEmpty() && db.TryGetChaControlQualifiedName(out _) && db.m_Root != null )
                .Select(db => new KeyValuePair<string, DynamicBone>(db.GetChaControlQualifiedName(), db))
                .GroupBy(pair => pair.Key)
                .Select(group => group.First())
                .ToList();

            nonAccDBs.Where(pair => !DistributionEdits[outfit].Any(a => a.ReidentificationData is string q && q==pair.Key))
                .ToList().ForEach(pair => {
                    DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(pair.Key)) { ReidentificationData = pair.Key }); 
                });

            // for accessory dynamic bones
            for(int i = 0; i < ChaControl.infoAccessory.Length; i++)
            {
                int slot = i;
                ChaAccessoryComponent accs = ChaControl.GetAccessoryComponent(slot);
                if (accs != null)
                {
                    List<KeyValuePair<string, DynamicBone>> accDbs = accs.GetComponentsInChildren<DynamicBone>(true)
                        .Where(db => db.TryGetAccessoryQualifiedName(out string n) && !n.IsNullOrEmpty() && db.m_Root != null)
                        .Select(db => new KeyValuePair<string, DynamicBone>(db.GetAccessoryQualifiedName(), db))
                        .GroupBy(pair => pair.Key)
                        .Select(group => group.First())
                        .ToList();

                    if (accDbs.IsNullOrEmpty()) continue;
                    accDbs.Where(pair => !DistributionEdits[outfit].Any(edit => 
                        (edit.ReidentificationData is KeyValuePair<int, string> kv) && kv.Key==slot && kv.Value==pair.Key))
                        .ToList().ForEach(pair => {
                            DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(pair.Key, slot)) { ReidentificationData = new KeyValuePair<int, string>(slot, pair.Key) });
                        });
                }
            }


            // == remove DBDEDynamicBoneEdits whose dynamic bones could not be found anymore.
            DistributionEdits[outfit].RemoveAll(a => a.DynamicBone == null);

            // == order list so that it matches the output of GetComponentsInChildren()
            DynamicBone[] foundDBs = ChaControl.GetComponentsInChildren<DynamicBone>(true);
            // AI generated this, idk if this works...
            DistributionEdits[outfit] = DistributionEdits[outfit]
                .Join(foundDBs.Select(
                    (b, i) => new { B = b, Index = i }),
                    a => a.DynamicBone,
                    tmp => tmp.B,
                    (a, tmp) => new { A = a, tmp.Index })
                .OrderBy(x => x.Index)
                .Select(x => x.A)
                .ToList();

            // DistributionEdits[outfit].ForEach(d => d.ReferToDynamicBone()); // sync all with their dynamic bones

        }

        private Dictionary<int, Dictionary<string, DynamicBone>> DynamicBoneCache = new Dictionary<int, Dictionary<string, DynamicBone>>();

        private DynamicBone WouldYouBeSoKindTohandMeTheDynamicBonePlease(string name, int? slot = null)
        {
            if (DynamicBoneCache.TryGetValue(slot ?? -1, out var a) && a.TryGetValue(name, out var dBone) && dBone != null) return dBone;

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
            
            if (!DynamicBoneCache.ContainsKey(slot ?? -1)) DynamicBoneCache[slot ?? -1] = new Dictionary<string, DynamicBone>();
            DynamicBoneCache[slot ?? -1][name] = searchList[0];

            return searchList[0];
        }
    }
}

