using HarmonyLib;
using UnityEngine;
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
            Transform t = __instance.transform.Find("BodyTop/ct_clothesBot");
            if (!t) return;
            GameObject clothesGameObject = t.gameObject;
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged(clothesGameObject);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesBra))]
        private static void ChangeClothsBraPostfix(ChaControl __instance)
        {
            Transform t = __instance.transform.Find("BodyTop/ct_bra");
            if (!t) return;
            GameObject clothesGameObject = t.gameObject;
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged(clothesGameObject);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesGloves))]
        private static void ChangeClothsGlovesPostfix(ChaControl __instance)
        {
            Transform t = __instance.transform.Find("BodyTop/ct_gloves");
            if (!t) return;
            GameObject clothesGameObject = t.gameObject;
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged(clothesGameObject);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesPanst))]
        private static void ChangeClothsPanstPostfix(ChaControl __instance)
        {
            Transform t = __instance.transform.Find("BodyTop/ct_panst");
            if (!t) return;
            GameObject clothesGameObject = t.gameObject;
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged(clothesGameObject);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesShoes))]
        private static void ChangeClothsShoesPostfix(ChaControl __instance, int type)
        {
            string ty = type == 0 ? "ct_shoes_inner" : "ct_shoes_outer";
            Transform t = __instance.transform.Find($"BodyTop/{ty}");
            if (!t) return;
            GameObject clothesGameObject = t.gameObject;
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged(clothesGameObject);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesSocks))]
        private static void ChangeClothsSocksPostfix(ChaControl __instance)
        {
            Transform t = __instance.transform.Find("BodyTop/ct_socks");
            if (!t) return;
            GameObject clothesGameObject = t.gameObject;
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged(clothesGameObject);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesTop))]
        private static void ChangeClothsTopPostfix(ChaControl __instance)
        {
            Transform t = __instance.transform.Find("BodyTop/ct_top");
            if (!t) return;
            GameObject clothesGameObject = t.gameObject;
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged(clothesGameObject);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesShorts))]
        private static void ChangeClothsShortsPostfix(ChaControl __instance)
        {
            Transform t = __instance.transform.Find("BodyTop/ct_shorts");
            if (!t) return;
            GameObject clothesGameObject = t.gameObject;
            __instance.GetComponent<DBDECharaController>()?.ClothesChanged(clothesGameObject);
        }
    }
}
