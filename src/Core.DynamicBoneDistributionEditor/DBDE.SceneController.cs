using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using KKAPI;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using Studio;
using UnityEngine;
using ExtensibleSaveFormat;
using MessagePack;
using AnimationCurveEditor;
using static AnimationCurveEditor.AnimationCurveEditor;
using System.Collections;
using KKAPI.Studio;

namespace DynamicBoneDistributionEditor
{
    public class DBDESceneController : SceneCustomFunctionController
    {
        internal static DBDESceneController instance;

        internal Dictionary<int, List<DBDEDynamicBoneEdit>> DistributionEdits = new Dictionary<int, List<DBDEDynamicBoneEdit>>();

        void Start()
        {
            instance = this;
        }

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            if (operation == SceneOperationKind.Load || operation == SceneOperationKind.Clear) DistributionEdits.Clear();

            StartCoroutine(SceneLoadDelayed(loadedItems));
        }

        internal DynamicBone GetDynamicBone(int key, int index)
        {
            if (Studio.Studio.Instance.dicObjectCtrl.TryGetValue(key, out var oci) && oci is OCIItem item && item.dynamicBones.Count() > index) return item.dynamicBones[index];
            return null;
        }

        private IEnumerator SceneLoadDelayed(ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            yield return null;
            yield return null; // wait two frames so that all other mods have applies their stuff

            PluginData data = GetExtendedData();
            if (data.data.TryGetValue("Edits", out var bytes) && bytes != null)
            {
                Dictionary<int, List<byte[]>> intermediate = MessagePackSerializer.Deserialize<Dictionary<int, List<byte[]>>>((byte[])bytes);

                foreach (int oldKey in intermediate.Keys)
                {
                    if (loadedItems[oldKey] is OCIItem oci)
                    {
                        int newKey = oci.GetSceneId();
                        if (!DistributionEdits.ContainsKey(newKey)) DistributionEdits.Add(newKey, new List<DBDEDynamicBoneEdit>());
                        for (int i = 0; i < oci.dynamicBones.Length; i++)
                        {
                            int num = i;
                            DistributionEdits[newKey].Add(new DBDEDynamicBoneEdit(() => GetDynamicBone(newKey, num), intermediate[oldKey][i]) { ReidentificationData = newKey });
                        }
                    }
                }
            }
            // Apply Edits on all DBs 
            DistributionEdits.Values.ToList().ForEach(x => x.ForEach(y => y.ApplyAll()));
        }

        protected override void OnSceneSave()
        {
            PluginData data = new PluginData();

            data.data.Add("Edits", MessagePackSerializer.Serialize(DistributionEdits.Select(e => new KeyValuePair<int, List<byte[]>>(e.Key, e.Value.Select(de =>
            {
                de.ReferToDynamicBone();
                return de.Sersialise();
            }).ToList())).ToDictionary(x => x.Key, x => x.Value)));
            SetExtendedData(data);
        }

        protected override void OnObjectDeleted(ObjectCtrlInfo objectCtrlInfo)
        {
            DistributionEdits.Remove(objectCtrlInfo.objectInfo.dicKey);

            base.OnObjectDeleted(objectCtrlInfo);
        }

        protected override void OnObjectsCopied(ReadOnlyDictionary<int, ObjectCtrlInfo> copiedItems)
        {
            StartCoroutine(ObjectsCopiesDelayed(copiedItems));  
        }

        private IEnumerator ObjectsCopiesDelayed(ReadOnlyDictionary<int, ObjectCtrlInfo> copiedItems)
        {
            yield return null;
            yield return null;
            foreach (int id in copiedItems.Keys)
            {
                if (!(copiedItems[id] is OCIItem newItem)) continue;
                OCIItem olditem = Studio.Studio.GetCtrlInfo(id) as OCIItem;
                if (DistributionEdits.ContainsKey(olditem.itemInfo.dicKey))
                {
                    DistributionEdits.Add(newItem.itemInfo.dicKey, new List<DBDEDynamicBoneEdit>());
                    for (int i = 0; i < newItem.dynamicBones.Length; i++)
                    {
                        int num = i;
                        DistributionEdits[newItem.itemInfo.dicKey].Add(new DBDEDynamicBoneEdit(() => GetDynamicBone(newItem.GetSceneId(), num), DistributionEdits[olditem.itemInfo.dicKey][num]) { ReidentificationData = newItem.GetSceneId() });
                    }
                }
            }
        }

        internal void OpenDBDE(int dictKey)
        {
            ObjectCtrlInfo objectCtrlInfo = Studio.Studio.GetCtrlInfo(dictKey);
            if (objectCtrlInfo is OCIItem oci)
            {
                OpenDBDE(oci);
            }
        }

        public void OpenDBDE(OCIItem oci)
        {
            if (!DistributionEdits.ContainsKey(oci.itemInfo.dicKey))
            {
                DistributionEdits.Add(oci.itemInfo.dicKey, new List<DBDEDynamicBoneEdit>());
                for (int i = 0; i < oci.dynamicBones.Length; i++)
                {
                    int num = i;
                    int dicKey = oci.itemInfo.dicKey;
                    DynamicBone db = oci.dynamicBones[i];
                    //DBDE.Logger.LogInfo($"Adding DynamicBone: Key={oci.itemInfo.dicKey} | DB={db.name} | {oci.dynamicBones.Length}");
                    DistributionEdits[oci.itemInfo.dicKey].Add(new DBDEDynamicBoneEdit(() => GetDynamicBone(dicKey, num)) { ReidentificationData = oci.itemInfo.dicKey});
                }
            }
            DBDE.UI.Open(() => DistributionEdits.ContainsKey(oci.itemInfo.dicKey) ? DistributionEdits[oci.itemInfo.dicKey] : null, () => { });
            DBDE.UI.TitleAppendix = oci.treeNodeObject.textName;
        }
    }
}

