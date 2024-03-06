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

namespace DynamicBoneDistributionEditor
{
    public class DBDESceneController : SceneCustomFunctionController
    {
        Dictionary<int, List<DBDEDynamicBoneEdit>> DistributionEdits = new Dictionary<int, List<DBDEDynamicBoneEdit>>();

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            if (operation == SceneOperationKind.Load || operation == SceneOperationKind.Clear) DistributionEdits.Clear();

            PluginData data = GetExtendedData();
            if (data.data.TryGetValue("Edits", out var bytes) && bytes != null)
            {
                Dictionary<int, List<byte[]>> intermediate = MessagePackSerializer.Deserialize<Dictionary<int, List<byte[]>>>((byte[])bytes);

                foreach(int oldKey in intermediate.Keys)
                {
                    if (loadedItems[oldKey] is OCIItem oci)
                    {
                        if (!DistributionEdits.ContainsKey(oci.itemInfo.dicKey)) DistributionEdits.Add(oci.itemInfo.dicKey, new List<DBDEDynamicBoneEdit>());
                        for (int i = 0; i < oci.dynamicBones.Length; i++)
                        {
                            DistributionEdits[oci.itemInfo.dicKey].Add(new DBDEDynamicBoneEdit(() => ((OCIItem)Studio.Studio.Instance.dicObjectCtrl[oci.itemInfo.dicKey]).dynamicBones[i], intermediate[oldKey][i]) { RedindificiationData = oci.itemInfo.dicKey });
                        }
                    }
                }
            }
            // Apply Edits on all DBs 
            DistributionEdits.Values.ToList().ForEach(x => x.ForEach(y => { y.ApplyDistribution(); y.ApplyBaseValues(); }));
        }

        protected override void OnSceneSave()
        {
            PluginData data = new PluginData();

            data.data.Add("Edits", MessagePackSerializer.Serialize(DistributionEdits.Select(e => new KeyValuePair<int, List<byte[]>>(e.Key, e.Value.Select(de => de.Sersialise()).ToList())).ToDictionary(x => x.Key, x => x.Value)));
            SetExtendedData(data);
        }

        protected override void OnObjectDeleted(ObjectCtrlInfo objectCtrlInfo)
        {
            DistributionEdits.Remove(objectCtrlInfo.objectInfo.dicKey);

            base.OnObjectDeleted(objectCtrlInfo);
        }

        protected override void OnObjectsCopied(ReadOnlyDictionary<int, ObjectCtrlInfo> copiedItems)
        {
            foreach(int id in copiedItems.Keys)
            {
                if (!(copiedItems[id] is OCIItem newItem)) return;
                OCIItem olditem = Studio.Studio.GetCtrlInfo(id) as OCIItem;
                if (DistributionEdits.ContainsKey(olditem.itemInfo.dicKey))
                {
                    DistributionEdits.Add(newItem.itemInfo.dicKey, new List<DBDEDynamicBoneEdit>());
                    for (int i = 0; i < newItem.dynamicBones.Length; i++)
                    {
                        DistributionEdits[newItem.itemInfo.dicKey].Add(new DBDEDynamicBoneEdit(() => ((OCIItem)Studio.Studio.Instance.dicObjectCtrl[newItem.itemInfo.dicKey]).dynamicBones[i], DistributionEdits[olditem.itemInfo.dicKey][i]) { RedindificiationData = newItem.itemInfo.dicKey});
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
                    DynamicBone db = oci.dynamicBones[i];
                    DBDE.Logger.LogInfo($"Adding DynamicBone: Key={oci.itemInfo.dicKey} | DB={db.name} | {oci.dynamicBones.Length}");
                    DistributionEdits[oci.itemInfo.dicKey].Add(new DBDEDynamicBoneEdit(() => ((OCIItem)Studio.Studio.Instance.dicObjectCtrl[oci.itemInfo.dicKey]).dynamicBones[i]) { RedindificiationData = oci.itemInfo.dicKey});
                }
            }
            DBDE.UI.Open(() => DistributionEdits.ContainsKey(oci.itemInfo.dicKey) ? DistributionEdits[oci.itemInfo.dicKey] : null);
        }
    }
}

