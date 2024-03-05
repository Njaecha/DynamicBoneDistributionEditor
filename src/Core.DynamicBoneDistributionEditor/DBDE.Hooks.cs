using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicBoneDistributionEditor
{
    internal class DBDEHooks
    {
        [HarmonyPrefix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        private static void ChangeCoordinateTypePrefix(ChaControl __instance)
        {
            __instance.GetComponent<DBDECharaController>()?.CoordinateChangeEvent();
        }
    }
}
