﻿using Harmony;
using ProjectRimFactory.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Verse;

namespace ProjectRimFactory.Common.HarmonyPatches
{
    /// <summary>
    /// infrastructure to suppress item splurge during load. Thanks to DoctorVanGogh
    /// </summary>
    [HarmonyPatch(typeof(GenSpawn), "Spawn", new Type[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool) })]
    public static class GenSpawnPatch
    {
        public static FieldInfo LoadedFullThingsField = typeof(Map).GetField("loadedFullThings", BindingFlags.NonPublic | BindingFlags.Static);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            bool ldargsSeen = false;
            Label l = il.DefineLabel();
            List<CodeInstruction> instrList = instructions.ToList();
            for (int i = 0; i < instrList.Count; i++)
            {
                if (!ldargsSeen && instrList[i].opcode == OpCodes.Ldarg_S && instrList[i].operand.Equals((byte)4))
                {
                    Label jmpLabel = instrList[i].labels[0];
                    instrList[i].labels.Clear();
                    CodeInstruction ins = new CodeInstruction(OpCodes.Ldarg_1)
                    {
                        labels = new List<Label>() { jmpLabel }
                    };
                    yield return ins;
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)5);
                    yield return new CodeInstruction(OpCodes.Call, typeof(GenSpawnPatch).GetMethod("ShouldDisplaceOtherItems"));
                    yield return new CodeInstruction(OpCodes.Brfalse, l);
                    ldargsSeen = true;
                }

                if (i + 2 < instrList.Count && instrList[i + 2].opcode == OpCodes.Callvirt && instrList[i + 2].operand == typeof(Thing).GetProperty("Rotation").GetSetMethod())
                {
                    instrList[i].labels.Add(l);
                }

                yield return instrList[i];
            }
        }

        public static bool ShouldDisplaceOtherItems(IntVec3 cell, Map map, bool respawningAfterLoad)
        {
            return !respawningAfterLoad || map?.thingGrid.ThingsListAtFast(cell).OfType<Building_MassStorageUnit>().Any() != true;
        }
    }
}
