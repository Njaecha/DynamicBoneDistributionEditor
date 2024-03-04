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
                            DistributionEdits[oci.itemInfo.dicKey].Add(new DBDEDynamicBoneEdit(oci.dynamicBones[i], intermediate[oldKey][i]));
                        }
                    }
                }
            }
            // Apply Edits on all DBs 
            DistributionEdits.Values.ToList().ForEach(x => x.ForEach(y => y.Apply()));
        }

        protected override void OnSceneSave()
        {
            PluginData data = new PluginData();

            data.data.Add("Edits", MessagePackSerializer.Serialize(DistributionEdits.Select(e => new KeyValuePair<int, List<byte[]>>(e.Key, e.Value.Select(de => de.Sersialise()).ToList())).ToDictionary(x => x.Key, x => x.Value)));
            SetExtendedData(data);
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
                foreach ( DynamicBone db in oci.dynamicBones)
                {
                    DistributionEdits[oci.itemInfo.dicKey].Add(new DBDEDynamicBoneEdit(db));
                }
            }
            currentlyDisplayedObject = oci.itemInfo.dicKey;
        }

        private int? currentlyDisplayedObject = null;

        private void OnGUI()
        {
            if (currentlyDisplayedObject.HasValue && DistributionEdits.ContainsKey(currentlyDisplayedObject.Value))
            {
                windowRect = GUI.Window(5858350, windowRect, WindowFunction, "DynamicBoneDistributionEditor", KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.window);
            }
        }

        private Rect windowRect = new Rect(100, 100, 600, 400);

        private void WindowFunction(int WindowID)
        {



            windowRect = KKAPI.Utilities.IMGUIUtils.DragResizeEatWindow(WindowID, windowRect);
        }
    }
}

