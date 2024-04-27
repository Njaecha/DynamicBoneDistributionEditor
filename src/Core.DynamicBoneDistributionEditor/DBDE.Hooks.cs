using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using Studio;
using System.Linq;

namespace DynamicBoneDistributionEditor
{
    internal class DBDEHooks
    {
        [HarmonyPrefix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        private static void ChangeCoordinateTypePrefix(ChaControl __instance)
        {
            DBDECharaController c = __instance.GetComponent<DBDECharaController>();
            if (c != null)
            {
                DBDE.Logger.LogDebug("CoordinateChanged");
                c.IsLoading = true;
                c.CoordinateChangeEvent();
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesBot))]
        private static void ChangeClothsBotPostfix(ChaControl __instance)
        {
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged();
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesBra))]
        private static void ChangeClothsBraPostfix(ChaControl __instance)
        {
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged();
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesGloves))]
        private static void ChangeClothsGlovesPostfix(ChaControl __instance)
        {
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged();
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesPanst))]
        private static void ChangeClothsPanstPostfix(ChaControl __instance)
        {
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged();
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesShoes))]
        private static void ChangeClothsShoesPostfix(ChaControl __instance)
        {
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged();
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesTop))]
        private static void ChangeClothsSocksPostfix(ChaControl __instance)
        {
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged();
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesShoes))]
        private static void ChangeClothsTopPostfix(ChaControl __instance)
        {
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged();
        }
    }
}
