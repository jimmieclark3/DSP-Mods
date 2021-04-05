using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DistributeSpaceWarper
{
    static class PatchStationComponent
    {
        [HarmonyPatch(typeof(PlanetTransport), "GameTick", typeof(long), typeof(bool))]
        [HarmonyPostfix]
        public static void GameTick_Postfix(PlanetTransport __instance)
        {
            ELogisticStorage defaultLocalMode = Config.General.WarperLocalMode.Value;
            ELogisticStorage defaultRemoteMode = Config.General.WarperRemoteMode.Value;
            bool warpersRequiredToggleAutomation = Config.General.WarpersRequiredToggleAutomation.Value;
            bool showWarperSlot =  Config.General.ShowWarperSlot.Value;
            int defaultMaxValue = Config.General.WarperMaxValue.Value;
            int warperId = ItemProto.kWarperId;
            int IlsId = 2104;
            bool needRefreshTraffic = false;
            PrefabDesc prefabDesc = LDB.items.Select(IlsId).prefabDesc;
            int warperSlotIndex = prefabDesc.stationMaxItemKinds;
            for (int j = 1; j < __instance.stationCursor; j++)
            {
                if (__instance.stationPool[j] != null && __instance.stationPool[j].id == j)
                {
                    StationComponent stationComponent = __instance.stationPool[j];
                    if (stationComponent.isCollector == true || stationComponent.isStellar == false)
                    {
                        continue;
                    }
                    if (stationComponent.storage.Length < prefabDesc.stationMaxItemKinds + 1)
                    {
                        List<StationStore> storeCopy = new List<StationStore>(stationComponent.storage);
                        storeCopy.Add(new StationStore(warperId, 0,0,0,defaultMaxValue, defaultLocalMode, defaultRemoteMode));
                        stationComponent.storage = storeCopy.ToArray();
                        needRefreshTraffic = true;
                    }
                    if (showWarperSlot == true && stationComponent.storage[warperSlotIndex].itemId != warperId)
                    {
                        stationComponent.storage[warperSlotIndex] = new StationStore(warperId, 0, 0, 0, defaultMaxValue, defaultLocalMode, defaultRemoteMode);
                        needRefreshTraffic = true;
                    }
                    if (warpersRequiredToggleAutomation == true)
                    {
                        if (stationComponent.warperNecessary == true &&
                            stationComponent.storage[warperSlotIndex].localLogic != defaultLocalMode)
                        {
                            stationComponent.storage[warperSlotIndex].localLogic = defaultLocalMode;
                            needRefreshTraffic = true;
                        }
                        else if (stationComponent.warperNecessary == false &&
                                 stationComponent.storage[warperSlotIndex].localLogic != ELogisticStorage.Supply)
                        {
                            stationComponent.storage[warperSlotIndex].localLogic = ELogisticStorage.Supply;
                            needRefreshTraffic = true;
                        }
                    }
                    int manualWarperStoreIndex = Array.FindIndex(stationComponent.storage, store => store.itemId == warperId);
                    if (manualWarperStoreIndex != -1 
                        && manualWarperStoreIndex != warperSlotIndex
                        && stationComponent.storage[warperSlotIndex].count < stationComponent.storage[warperSlotIndex].max 
                        && stationComponent.storage[manualWarperStoreIndex].count > stationComponent.storage[manualWarperStoreIndex].max)
                    {
                        int warperOverflowInManualSlot = stationComponent.storage[manualWarperStoreIndex].count - stationComponent.storage[manualWarperStoreIndex].max;
                        int warperNeedInAutomaticSlot = stationComponent.storage[warperSlotIndex].max - stationComponent.storage[warperSlotIndex].count;
                        int transferAmount = Mathf.Min(warperNeedInAutomaticSlot, warperOverflowInManualSlot);
                        stationComponent.storage[manualWarperStoreIndex].count -= transferAmount;
                        stationComponent.storage[warperSlotIndex].count += transferAmount;
                        stationComponent.storage[warperSlotIndex].localOrder = 0;
                        stationComponent.storage[warperSlotIndex].remoteOrder = 0;
                        needRefreshTraffic = true;
                    }
                }
            }
            if (needRefreshTraffic)
            {
                __instance.RefreshTraffic();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIStationStorage), "OnItemPickerReturn", typeof(ItemProto))]
        public static bool OnItemPickerReturn_Prefix(UIStationStorage __instance, ItemProto itemProto)
        {
            int warperId = ItemProto.kWarperId;
            int IlsId = 2104;
            PrefabDesc prefabDesc = LDB.items.Select(IlsId).prefabDesc;
            int warperSlotIndex = prefabDesc.stationMaxItemKinds;
            
            if (itemProto == null)
            {
                return false;
            }
            if (__instance.station == null || __instance.index >= __instance.station.storage.Length)
            {
                return false;
            }
            ItemProto itemProto2 = LDB.items.Select((int)__instance.stationWindow.factory.entityPool[__instance.station.entityId].protoId);
            if (itemProto2 == null)
            {
                return false;
            }
            for (int i = 0; i < __instance.station.storage.Length; i++)
            {
                if (__instance.station.storage[i].itemId == itemProto.ID)
                {
                    if (__instance.station.isStellar == true && __instance.station.isCollector == false && i == warperSlotIndex && itemProto.ID == warperId)
                    {
                        continue;
                    }
                    UIRealtimeTip.Popup("不选择重复物品".Translate(), true, 0);
                    return false;
                }
            }
            __instance.stationWindow.transport.SetStationStorage(__instance.station.id, __instance.index, 
                itemProto.ID, itemProto2.prefabDesc.stationMaxItemCount, ELogisticStorage.Supply,
                (!__instance.station.isStellar) ? ELogisticStorage.None : ELogisticStorage.Supply, GameMain.mainPlayer);
            return false;
        }
        
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIStationWindow), "_OnCreate")]
        public static bool _OnCreatePrefix(UIStationWindow __instance, ref UIStationStorage[] ___storageUIs, UIStationStorage ___storageUIPrefab)
        {
            bool showWarperSlot =  Config.General.ShowWarperSlot.Value;
            if (showWarperSlot == true)
            {
                return true;
            }
            ___storageUIs = new UIStationStorage[5];
            for (int i = 0; i < ___storageUIs.Length; i++)
            {
                ___storageUIs[i] = Object.Instantiate(___storageUIPrefab, ___storageUIPrefab.transform.parent);
                ((RectTransform) ___storageUIs[i].transform).anchoredPosition = new Vector2(40f, -90 - 76 * i);
                ___storageUIs[i].stationWindow = __instance;
                ___storageUIs[i]._Create();
            }
            return false;
        }
    }
}

