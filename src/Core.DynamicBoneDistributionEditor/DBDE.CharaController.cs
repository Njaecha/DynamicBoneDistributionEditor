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
using System.Collections;
using static Illusion.Game.Utils;
using DBDE.KK_Plugins.DynamicBoneEditor;
using BepInEx.Bootstrap;
using KKAPI.Studio;
using UnityEngine.UI;
using Illusion.Extensions;
using ADV.Commands.Chara;
using System.Runtime.CompilerServices;

namespace DynamicBoneDistributionEditor
{
    public class DBDECharaController : CharaCustomFunctionController
    {
        internal Dictionary<int, List<DBDEDynamicBoneEdit>> DistributionEdits = new Dictionary<int, List<DBDEDynamicBoneEdit>>();

        // <outfit, [accessory edits, normal edits, DBE-Data]>
        internal Dictionary<int, List<object>> DistributionEditsNotLoaded = new Dictionary<int, List<object>>();

        // used to transfer plugin data from Coordinate Load Options temp character to the real characters.
        private static PluginData cloTransferPluginData; // for DBDE
        private static PluginData cloTransferPluginDataDBE; // for DBE

        private PluginData DBEData = null;

        private bool _isLoading = false;
        public bool IsLoading { get => _isLoading; internal set => setIsLoading(value); }

        private void setIsLoading(bool value)
        {
            if (value && !_isLoading) StartCoroutine(endLoading(LoadIgnoreFrameCount));
            _isLoading = value;
        }

        private IEnumerator endLoading(int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
            {
                DBDE.Logger.LogDebug($"DBDE Waiting for load to finish -> Frame {i + 1}/{frameCount}");
                yield return null;
            }
            IsLoading = false;
        }

        protected override void Awake()
        {
            LoadIgnoreFrameCount = MakerAPI.InsideMaker ? 5 : 2;
            IsLoading = true;
            LoadIgnoreFrameCount = 3;
            base.Awake();
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            IsLoading = true;

            DistributionEdits.Clear();
            DistributionEditsNotLoaded.Clear();

            PluginData data = GetExtendedData();
            PluginData DBE_data = DBEData = Chainloader.PluginInfos.ContainsKey("com.deathweasel.bepinex.dynamicboneeditor") ? null : ExtendedSave.GetExtendedDataById(MakerAPI.LastLoadedChaFile ?? ChaFileControl, "com.deathweasel.bepinex.dynamicboneeditor");
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
                DistributionEditsNotLoaded.Add(cSet, new List<object> { accessoryEdits, normalEdits, DBEEdits });
            }
            base.OnReload(currentGameMode, maintainState);

        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            PluginData data = new PluginData();
            RefreshBoneList("Card Being Saved");

            Dictionary<int, List<KeyValuePair<KeyValuePair<int, string>, byte[]>>> ACC = new Dictionary<int, List<KeyValuePair<KeyValuePair<int, string>, byte[]>>>();
            Dictionary<int, List<KeyValuePair<string, byte[]>>> NORM = new Dictionary<int, List<KeyValuePair<string, byte[]>>>();

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
                        accessoryEdits.Add(new KeyValuePair<KeyValuePair<int, string>, byte[]>(rData, edit.Serialise()));
                    }
                    if (edit.ReidentificationData is string name)
                    {
                        normalEdits.Add(new KeyValuePair<string, byte[]>(name, edit.Serialise()));
                    }
                }
                if (!accessoryEdits.IsNullOrEmpty()) ACC[key] = accessoryEdits;
                if (!normalEdits.IsNullOrEmpty()) NORM[key] = normalEdits;
                
            }
            // also take in account data in DistributionEditsNotLoaded
            bool resaveDBEData = false;
            foreach (int key in DistributionEditsNotLoaded.Keys)
            {
                List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = DistributionEditsNotLoaded[key][0] as List<KeyValuePair<KeyValuePair<int, string>, byte[]>>;
                List<KeyValuePair<string, byte[]>> normalEdits = DistributionEditsNotLoaded[key][1] as List<KeyValuePair<string, byte[]>>;
                if (DistributionEditsNotLoaded[key][2] is List<DynamicBoneData>) // have to resave DBE Data so that it doesnt get lost
                {
                    resaveDBEData = true;
                }

                if (!accessoryEdits.IsNullOrEmpty())
                {
                    if (ACC.ContainsKey(key)) ACC[key].AddRange(accessoryEdits);
                    else ACC[key] = accessoryEdits;
                }
                if (!normalEdits.IsNullOrEmpty())
                {
                    if(NORM.ContainsKey(key)) NORM[key].AddRange(normalEdits);
                    else NORM[key] = normalEdits;
                }
            }
            
            foreach (int key in ACC.Keys) data.data.Add($"AccessoryEdits{key}", MessagePackSerializer.Serialize(ACC[key]));
            foreach (int key in NORM.Keys) data.data.Add($"NormalEdits{key}", MessagePackSerializer.Serialize(NORM[key]));

            if (resaveDBEData && DBEData != null)
            {
                ExtendedSave.SetExtendedDataById(ChaFileControl, "com.deathweasel.bepinex.dynamicboneeditor", DBEData);
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
            cloTransferPluginDataDBE = null;
        }

        /// <summary>
        /// Checks if coordinateLoadOption is being used and if yes if accessories should be loaded or not
        /// </summary>
        /// <param name="loadAccessories">Should AccessoryData be loaded? Default: True</param>
        /// <param name="cMode">Is CoordinateLoadOption being used? Default: False</param>
        /// <param name="cloImportAccessories">The accessoriesSlots that should be loaded. Default: EmptyList</param>
        private void checkCoordinateLoadOption(out bool loadAccessories, out bool cMode, out List<int> cloImportAccessories)
        {
            loadAccessories = true;
            cMode = false;
            cloImportAccessories = new List<int>(); // slots that are loaded new
            if (Chainloader.PluginInfos.ContainsKey("com.jim60105.kks.coordinateloadoption") || Chainloader.PluginInfos.ContainsKey("com.jim60105.kk.coordinateloadoption") )
            {
                DBDE.Logger.LogDebug("Coordinate Load Option detected");
                if (GameObject.Find("CoordinateTooglePanel")?.activeInHierarchy == true)
                {
                    DBDE.Logger.LogDebug("Coordinate Load Option enabled");
                    bool? accEnabled = GameObject.Find("CoordinateTooglePanel/accessories")?.GetComponent<Toggle>()?.isOn;
                    if (accEnabled == true)
                    {
                        DBDE.Logger.LogDebug("Coordinate Load Option accessory load enabled, entering compatibility mode");
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
                        DBDE.Logger.LogDebug("Coordinate Load Option accessory load disabled -> do not load new DBDE accessory data.");
                        loadAccessories = false;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the DBE-Plugin data if DynamicBoneEditor is NOT installed, else returns null.
        /// </summary>
        /// <param name="coordinate"></param>
        /// <returns></returns>
        private PluginData GrabDBEPluginData(ChaFileCoordinate coordinate)
        {
            return Chainloader.PluginInfos.ContainsKey("com.deathweasel.bepinex.dynamicboneeditor") ? null : ExtendedSave.GetExtendedDataById(coordinate, "com.deathweasel.bepinex.dynamicboneeditor"); // load data from DynamicBoneEditor
        }

        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate)
        {
            IsLoading = true;
            checkCoordinateLoadOption(out bool loadAccessories, out bool cMode, out List<int> cloImportAccessories);

            // Maker partial coordinate load fix
            if (MakerAPI.InsideAndLoaded)
            {
                // dont load accessories if the checkbox is not set
                DBDE.Logger.LogDebug("Coordinate Load Option accessory load disabled -> do not load new DBDE accessory data.");
                if (GameObject.Find("cosFileControl")?.GetComponentInChildren<ChaCustom.CustomFileWindow>()?.tglCoordeLoadAcs.isOn == false) loadAccessories = false;
            }

            #region TranserferData Logic
            PluginData data = null;
            PluginData DBE_data = null;
            if (cMode)
            {
                // if CoordinateLoadOption is being used and no TransferData exsits -> must be loading the MainChara right now
                if (cloTransferPluginData != null)
                {
                    DBDE.Logger.LogDebug("Using DBDE Data from Transfer");
                    data = cloTransferPluginData;
                }
                else // if no TransferData exists -> must be loading the DummyChara right now
                {
                    data = cloTransferPluginData = GetCoordinateExtendedData(coordinate); // write DBDE Data from DummyChara into transfer
                    DBDE.Logger.LogDebug("Setting DBDE TransferData");
                    if (cloTransferPluginData != null)
                    {
                        // remove transfer plugindata after load is finished; Coroutine cannot be started on *this* as it's being destroyed by clo too early
                        DBDE.Instance.StartCoroutine(removeCloTransferPluginData());
                    }
                }

                if (cloTransferPluginDataDBE != null)
                {
                    DBDE.Logger.LogDebug("Using DBE Data from Transfer");
                    DBE_data = cloTransferPluginDataDBE;
                }
                else 
                {
                    DBE_data = cloTransferPluginDataDBE = GrabDBEPluginData(coordinate); // write DBE Data from DummyChara into transfer
                    DBDE.Logger.LogDebug("Setting DBE TransferData");
                    if (cloTransferPluginDataDBE != null)
                    {
                        DBDE.Instance.StartCoroutine(removeCloTransferPluginData());
                    }
                }
            }
            else
            {
                data = GetCoordinateExtendedData(coordinate);
                DBE_data = GrabDBEPluginData(coordinate);
            }
            #endregion

            // at this point we should have DBDE Data and/or DBE Data from either the Outfit being loaded or transfered from the DummyCharacter

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
                if (cMode && !accessoryEdits.IsNullOrEmpty())
                {
                    accessoryEdits = accessoryEdits.Where(x => cloImportAccessories.Contains(x.Key.Key)).ToList(); // keep only edits of which the accessory slot is being loaded
                }
            }
            List<DynamicBoneData> DBEEdits = new List<DynamicBoneData>();
            if (loadAccessories && DBE_data != null && DBE_data.data.TryGetValue("AccessoryDynamicBoneData", out var binaries3) && binaries3 != null)
            {
                DBDE.Logger.LogInfo($"Found DynamicBoneEditor data on outfit {ChaControl.fileStatus.coordinateType}. DBDE will try to load it...");
                DBEEdits = MessagePackSerializer.Deserialize<List<DynamicBoneData>>((byte[])binaries3);
                if (cMode && !DBEEdits.IsNullOrEmpty())
                {
                    DBEEdits = DBEEdits.Where(x => cloImportAccessories.Contains(x.Slot)).ToList(); // keep only edits of which the accessory slot is being loaded
                }
            }
            DBDE.UI.Close();
            StartCoroutine(LoadData(ChaControl.fileStatus.coordinateType, accessoryEdits, normalEdits, DBEEdits, true));
            base.OnCoordinateBeingLoaded(coordinate);
        }

        protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
        {
            PluginData data = new PluginData();
            RefreshBoneList("Coordinate Being Saved");
            // serialise edits list for current coordinate
            List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = new List<KeyValuePair<KeyValuePair<int, string>, byte[]>>();
            List<KeyValuePair<string, byte[]>> normalEdits = new List<KeyValuePair<string, byte[]>>();
            foreach(DBDEDynamicBoneEdit edit in DistributionEdits[ChaControl.fileStatus.coordinateType])
            {
                if (!edit.IsEdited()) continue;
                if (edit.ReidentificationData is KeyValuePair<int, string> rData)
                {
                    accessoryEdits.Add(new KeyValuePair<KeyValuePair<int, string>, byte[]>(rData, edit.Serialise()));
                }
                if (edit.ReidentificationData is string name)
                {
                    normalEdits.Add(new KeyValuePair<string, byte[]>(name, edit.Serialise()));
                }
            }
            data.data.Add("AccessoryEdits", MessagePackSerializer.Serialize(accessoryEdits));
            data.data.Add("NormalEdits", MessagePackSerializer.Serialize(normalEdits));

            SetCoordinateExtendedData(coordinate, data);

            base.OnCoordinateBeingSaved(coordinate);
        }

        protected override void Update()
        {
            int cSet = ChaControl.fileStatus.coordinateType;

            // start load coroutine if current outfit has data in the collection of not loaded data.
            if (DistributionEditsNotLoaded.Count > 0 && DistributionEditsNotLoaded.Keys.Contains(cSet))
            {
                List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits = null;
                List<KeyValuePair<string, byte[]>> normalEdits = null;
                List<DynamicBoneData> DBEEdits = null;
                if (DistributionEditsNotLoaded[cSet][0] is List<KeyValuePair<KeyValuePair<int, string>, byte[]>> a) accessoryEdits = a;
                if (DistributionEditsNotLoaded[cSet][1] is List<KeyValuePair<string, byte[]>> b) normalEdits = b;
                if (DistributionEditsNotLoaded[cSet][2] is List<DynamicBoneData> c) DBEEdits = c;
                StartCoroutine(LoadData(cSet, accessoryEdits, normalEdits, DBEEdits));
                DistributionEditsNotLoaded.Remove(cSet);
            }


            if (DistributionEdits.ContainsKey(ChaControl.fileStatus.coordinateType))
            {
                foreach(var edit in DistributionEdits[ChaControl.fileStatus.coordinateType])
                {
                    edit.UpdateActiveStack();
                }
            }
            base.Update();
        }

        internal void ClothesChanged(GameObject clothGO)
        {
            if (IsLoading) return;
            DBDE.UI.UpdateUIWhileOpen = false;
            
            _dynamicBoneCache.Clear();
            RefreshBoneList($"Clothes Changed (clothing part: {clothGO.name})");

            List<DynamicBone> clothDBs = clothGO.GetComponentsInChildren<DynamicBone>(true).ToList();

            if (DistributionEdits.ContainsKey(ChaControl.fileStatus.coordinateType))
            {
                foreach (var edit in DistributionEdits[ChaControl.fileStatus.coordinateType])
                {
                    DynamicBone db = clothDBs.Find(b => b.m_Root.name == edit.PrimaryDynamicBone.m_Root.name);
                    if (db != null)
                    {
                        edit.ReferInitialsToDynamicBone(db);
                    }
                    edit.UpdateActiveStack();
                }
            }
            DBDE.UI.UpdateUIWhileOpen = true;
        }

        internal void CoordinateChangeEvent()
        {
            if (!DistributionEdits.ContainsKey(ChaControl.fileStatus.coordinateType)) return;
            DBDE.UI.UpdateUIWhileOpen = false;
            StartCoroutine(ApplyCurrentDelayed());
        }

        private IEnumerator ApplyCurrentDelayed()
        {
            while (IsLoading) yield return null;

            if (KKAPI.Studio.StudioAPI.InsideStudio)
            {
                yield return null;
                yield return null;
            }
            DBDE.Logger.LogDebug($"Applying Data for outfit {ChaControl.fileStatus.coordinateType} on character {ChaControl.chaFile.GetFancyCharacterName()} (from Delayed)");
            RefreshBoneList("From ApplyCurrent Delayed (see above)");
            for (int i = 0; i < DistributionEdits[ChaControl.fileStatus.coordinateType].Count; i++)
            {
                DBDEDynamicBoneEdit edit = DistributionEdits[ChaControl.fileStatus.coordinateType][i];
                edit.MultiBoneFix();
                edit.ApplyAll();
            }
            DBDE.UI.UpdateUIWhileOpen = true;
        }

        

        public int LoadIgnoreFrameCount = 3;

        private IEnumerator LoadData(int outfit, List<KeyValuePair<KeyValuePair<int, string>, byte[]>> accessoryEdits, List<KeyValuePair<string, byte[]>> normalEdits, List<DynamicBoneData> DBE_Data, bool outfitLoad = false)
        {
            while (IsLoading) yield return null;

            if (KKAPI.Studio.StudioAPI.InsideStudio)
            {
                yield return null;
                yield return null;
            }

            while (ChaControl == null || ChaControl.objHead == null)
                yield return null;

            if (!DistributionEdits.ContainsKey(outfit)) DistributionEdits.Add(outfit, new List<DBDEDynamicBoneEdit>());

            if (!accessoryEdits.IsNullOrEmpty() || !DBE_Data.IsNullOrEmpty())
            {
                if (!accessoryEdits.IsNullOrEmpty())
                {
                    foreach(var x in accessoryEdits)
                    {
                        // remove old Edit so that new one can take its place but keep remaining edits
                        if (!DistributionEdits[outfit].IsNullOrEmpty()) DistributionEdits[outfit].RemoveAll(d => d.ReidentificationData.Equals(x.Key)); 
                        if (DBE_Data != null)
                        {
                            // find DynamicBoneData for this accessory
                            List<DynamicBoneData> searchResults = DBE_Data.FindAll(data => outfit == data.CoordinateIndex && x.Key.Key == data.Slot && x.Key.Value.EndsWith(data.BoneName));
                            // warn if multiple things found
                            if (!searchResults.IsNullOrEmpty() && searchResults.Count > 1) DBDE.Logger.LogWarning($"Found multiple DynamicBoneData saved on the card that match Outfit={outfit} Slot={x.Key.Key} BoneName={x.Key.Value.Split('/').Last()}. Using first entry.");
                            
                            if (!searchResults.IsNullOrEmpty()) // if something was found
                            {
                                DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(x.Key.Value, x.Key.Key), x.Value, searchResults[0]) { ReidentificationData = x.Key }); // new Edit with DBE data
                                DBE_Data.RemoveAll(searchResults.Contains); // remove from list because we already loaded the edit here
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
                        // remove old Edit so that new one can take its place but keep remaining edits
                        if (!DistributionEdits[outfit].IsNullOrEmpty()) DistributionEdits[outfit].RemoveAll(d => d.ReidentificationData is KeyValuePair<int, string> kvp && kvp.Key == slot && kvp.Value == qName);
                        if (qName.IsNullOrEmpty()) DBDE.Logger.LogError($"Couldn't retrive Accessory Qualified Name for DynamicBone with BasicName={data.BoneName} on Slot {data.Slot}  (Outfit {outfit}). Skipping Entry.");
                        else DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(qName, slot), null, data) { ReidentificationData = new KeyValuePair<int, string>(slot, qName) }); // new Edit with only DBE data
                    }
                }
            }
            if (normalEdits != null)
            {
                foreach(var x in normalEdits)
                {
                    // remove old Edit so that new one can take its place but keep remaining edits
                    if (!DistributionEdits[outfit].IsNullOrEmpty()) DistributionEdits[outfit].RemoveAll(d => d.ReidentificationData.Equals(x.Key));
                    DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(x.Key), x.Value) { ReidentificationData = x.Key });
                }
            }

            DBDE.Logger.LogDebug($"Applying Data for outfit {outfit} on character {ChaControl.chaFile.GetFancyCharacterName()}");
            for (int i = 0; i < DistributionEdits[outfit].Count; i++)
            {
                DistributionEdits[outfit][i].ApplyAll();
            }
        }

        internal void AccessoryChangedEvent(int slot)
        {
            _dynamicBoneCache.Clear();
            RefreshBoneList("Accessory Changed");
            if (DistributionEdits.ContainsKey(ChaControl.fileStatus.coordinateType))
            {
                foreach (var edit in DistributionEdits[ChaControl.fileStatus.coordinateType])
                {
                    edit.UpdateActiveStack(true);
                    //edit.MultiBoneFix();
                    edit.ApplyAll();
                }
            }
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
                sourceEdit.MultiBoneFix();
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
                .ForEach(edit => DistributionEdits[destinationOutfit].Add(new DBDEDynamicBoneEdit(edit.AccessorFunction, edit) { ReidentificationData = edit.ReidentificationData }));
            
            StartCoroutine(RefreshBoneListDelayed());
            DBDE.UI.UpdateUIWhileOpen = true;
        }

        public void OpenDBDE()
        {
            RefreshBoneList("UI Open");
            DBDE.UI.Open(() => DistributionEdits[ChaControl.fileStatus.coordinateType], () => RefreshBoneList("UI Button"));
            DBDE.UI.TitleAppendix = ChaControl.chaFile.GetFancyCharacterName();
            DBDE.UI.referencedChara = this;
        }

        public IEnumerator RefreshBoneListDelayed()
        {
            yield return null;
            RefreshBoneList("From Delayed");
        }

        private void RefreshBoneList(string callSource, bool removeDeadOnes = true)
        {
            if (IsLoading) return;

            int outfit = ChaControl.fileStatus.coordinateType;

            DBDE.Logger.LogDebug($"Refreshing Bone List on {ChaControl.chaFile.GetFancyCharacterName()} - Outfit {outfit} | removeDead {removeDeadOnes} | callSource {callSource}");

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
                    // get all dynamic bones under that accessory
                    List<DynamicBone> dynamicBones = accs.GetComponentsInChildren<DynamicBone>(true).ToList();

                    // get all dynamic bones that have their m_Root located under that accessory (normal)
                    List<DynamicBone> aDynamicBones = dynamicBones
                        .Where(db =>
                            db.m_Root != null
                            && db.TryGetAccessoryQualifiedName(out string name)
                            && !name.IsNullOrEmpty()
                        ).ToList();
                    dynamicBones.RemoveAll(db => aDynamicBones.Contains(db));
                    // grab AccessoryQualifiedName and remove duplicates
                    List<KeyValuePair<string, DynamicBone>> aDynamicBonesFiltered = aDynamicBones.Select(db => new KeyValuePair<string, DynamicBone>(db.GetAccessoryQualifiedName(), db)).GroupBy(kvp => kvp.Key).Select(group => group.First()).ToList();
                    // remove those that already have a DBDEDynamicBoneEdit
                    aDynamicBonesFiltered.RemoveAll(namdAndDB => DistributionEdits[outfit].Any(edit =>
                            (edit.ReidentificationData is KeyValuePair<int, string> slotAndName) && slotAndName.Key == slot && slotAndName.Value == namdAndDB.Key
                        ));
                    if (!aDynamicBonesFiltered.IsNullOrEmpty())
                    {
                        // add DBDEs for those remaining
                        aDynamicBonesFiltered.ForEach(nameAndDB =>
                        {
                            DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(nameAndDB.Key, slot)) { ReidentificationData = new KeyValuePair<int, string>(slot, nameAndDB.Key) });
                        });
                    }

                    // get all dynamic bones that have their m_Root located somewhere else (c2a accessories)
                    List<DynamicBone> nDynamicBones = dynamicBones
                        .Where( db =>
                            db.m_Root != null
                            && db.TryGetChaControlQualifiedName(out string name)
                            && !name.IsNullOrEmpty()
                        ).ToList();
                    // grab ChaControlQualifiedName and remove duplicates
                    List<KeyValuePair<string, DynamicBone>> nDynamicBonesFiltered = nDynamicBones.Select(db => new KeyValuePair<string, DynamicBone>(db.GetChaControlQualifiedName(), db)).GroupBy(kvp => kvp.Key).Select(group => group.First()).ToList();
                    // remove those that already have a DBDEDynamicBoneEdit
                    nDynamicBonesFiltered.RemoveAll(namdAndDB => DistributionEdits[outfit].Any(edit =>
                            (edit.ReidentificationData is string name) && name == namdAndDB.Key
                        ));
                    if (!nDynamicBonesFiltered.IsNullOrEmpty())
                    {
                        // add DBDEs for those remaining
                        nDynamicBonesFiltered.ForEach(nameAndDB =>
                        {
                            DistributionEdits[outfit].Add(new DBDEDynamicBoneEdit(() => WouldYouBeSoKindTohandMeTheDynamicBonePlease(nameAndDB.Key)) { ReidentificationData = nameAndDB.Key });
                        });
                    }
                }
            }


            // == remove DBDEDynamicBoneEdits whose dynamic bones could not be found anymore.
            if (removeDeadOnes) DistributionEdits[outfit].RemoveAll(a => {
                if (a.DynamicBones.IsNullOrEmpty() || a.DynamicBones.All(d => d == null))
                {
                    string print = "";
                    if (a.ReidentificationData is KeyValuePair<int, string> acc) print = $"{acc.Key} -> {acc.Value}";
                    else if (a.ReidentificationData is string nor) print = nor;
                    DBDE.Logger.LogDebug($"Outfit{outfit} - Removed: {print}");
                    return true;
                }
                else
                {
                    if (a.DynamicBones.Any(d => d == null)) _dynamicBoneCache.Clear(); // dead bone stuck in cache
                }
                return false;
            });

            /*
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
            */
            // DistributionEdits[outfit].ForEach(d => d.ReferToDynamicBone()); // sync all with their dynamic bones

        }

        private readonly Dictionary<int, Dictionary<string, List<DynamicBone>>> _dynamicBoneCache = new Dictionary<int, Dictionary<string, List<DynamicBone>>>();

        private List<DynamicBone> WouldYouBeSoKindTohandMeTheDynamicBonePlease(string identificationName, int? slot = null)
        {
            if (identificationName.StartsWith("/")) identificationName = identificationName.Remove(0, 1);
            
            if (_dynamicBoneCache.TryGetValue(slot ?? -1, out Dictionary<string, List<DynamicBone>> a) && a.TryGetValue(identificationName, out List<DynamicBone> dBones) && !dBones.IsNullOrEmpty() && !dBones.Any(d =>  !d || !d.m_Root)) return dBones;

            List<DynamicBone> searchList;
            if (slot.HasValue) searchList = ChaControl.GetAccessoryComponent(slot.Value)?.GetComponentsInChildren<DynamicBone>(true)?
                    .Where(db => db.m_Root && db.TryGetAccessoryQualifiedName(out string n) && n == identificationName)
                    .Select(db => new KeyValuePair<string, DynamicBone>(db.gameObject.name, db))
                    .GroupBy(pair => pair.Key)
                    .Select(group => group.First().Value)
                    .ToList();

            else searchList = ChaControl.GetComponentsInChildren<DynamicBone>(true)?
                    .Where(db => db.m_Root && !db.TryGetAccessoryQualifiedName(out _) && db.TryGetChaControlQualifiedName(out string n) && n == identificationName)
                    .Select(db => new KeyValuePair<string, DynamicBone>(db.gameObject.name, db))
                    .GroupBy(pair => pair.Key)
                    .Select(group => group.First().Value)
                    .ToList();

            if (searchList.IsNullOrEmpty()) return null;
            
            if (!_dynamicBoneCache.ContainsKey(slot ?? -1)) _dynamicBoneCache[slot ?? -1] = new Dictionary<string, List<DynamicBone>>();
            _dynamicBoneCache[slot ?? -1][identificationName] = searchList;

            return searchList;
        }
    }
}

