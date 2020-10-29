using LiveSplit.ComponentUtil;
using LiveSplit.Model;
using LiveSplit.VoxSplitter;
using System;
using System.Collections.Generic;

namespace LiveSplit.Carto {
    public class CartoMemory : Memory {

        private Pointer<IntPtr> globalVar;
        private Pointer<int> status;

        private Pointer<IntPtr> playMgr;
        private Pointer<int> uiLock;
        private StringPointer playerTile;

        private int flagsVer = 0;
        private int countsVer = 0;
        private readonly Dictionary<string, bool> flagsDict = new Dictionary<string, bool>();
        private readonly Dictionary<string, int> countsDict = new Dictionary<string, int>();

        private readonly RemainingDictionary remainingSplits;

        private readonly MonoHelper mono;
        
        public CartoMemory(Logger logger) : base(logger) {
            processName = "Carto";
            remainingSplits = new RemainingDictionary(logger);
            mono = new MonoHelper(this);
        }

        public override bool IsReady() => base.IsReady() && mono.IsCompleted;

        protected override void OnGameHook() {
            mono.Run(() => {
                MonoNestedPointerFactory ptrFactory = new MonoNestedPointerFactory(this, mono);

                var game = ptrFactory.Make<IntPtr>("Game", "ins_", out IntPtr gameClass);
                globalVar = ptrFactory.Make<IntPtr>(game, mono.GetFieldOffset(gameClass, "<GlobalVar>k__BackingField"));
                status = ptrFactory.Make<int>(game, mono.GetFieldOffset(gameClass, "<status>k__BackingField"));

                playMgr = ptrFactory.Make<IntPtr>("PlayMgr", "ins", out IntPtr playMgrClass);
                uiLock = ptrFactory.Make<int>(playMgr, mono.GetFieldOffset(playMgrClass, "<ui>k__BackingField"), 0x174);
                playerTile = ptrFactory.MakeString(playMgr, mono.GetFieldOffset(playMgrClass, "<player>k__BackingField"), 0x38, 0x20, 0x10, 0x100, 0x14);
                playerTile.StringType = EStringType.UTF16Sized;

                Logger.Log(ptrFactory.ToString());
            });
        }

        public override bool Start(int start) {
            return uiLock.Old != 0 && uiLock.New == 0 && playerTile.New.Equals("zone.01.island.v3+v3.101#");
        }

        public override void OnStart(TimerModel timer, HashSet<string> splits) {
            remainingSplits.Setup(splits);

            flagsDict.Clear();
            countsDict.Clear();
        }

        public override bool Split() {
            return remainingSplits.Count != 0 && (SplitGlobalVars());

            bool SplitGlobalVars() {
                return remainingSplits.ContainsKey("Var")
                    && (ReadGlobalVar(0x18, ref flagsVer, flagsDict) || ReadGlobalVar(0x20, ref countsVer, countsDict));

                bool ReadGlobalVar<T>(int offset, ref int lastVer, Dictionary<string, T> dict) where T : unmanaged {
                    IntPtr vars = Game.Read<IntPtr>(globalVar.New + offset);
                    int newVer = Game.Read<int>(vars + 0x44);
                    if(lastVer != newVer) {
                        lastVer = newVer;
                        int varCount = Game.Read<int>(vars + 0x40);
                        IntPtr varArr = Game.Read<IntPtr>(vars + 0x18);
                        for(int varId = 0; varId < varCount; varId++) {
                            IntPtr strPtr = Game.Read<IntPtr>(varArr + 0x28 + 0x18 * varId);
                            string key = Game.ReadString(strPtr + 0x14, EStringType.UTF16Sized);
                            T value = Game.Read<T>(varArr + 0x30 + 0x18 * varId);
                            if(dict.ContainsKey(key)) {
                                if(!dict[key].Equals(value)) {
                                    dict[key] = value;
                                    if(remainingSplits.Split("Var", key + "_" + value)) {
                                        return true;
                                    }
                                }
                            } else {
                                dict.Add(key, value);
                                if(remainingSplits.Split("Var", key + "_" + value)) {
                                    return true;
                                }
                            }
                        }
                    }
                    return false;
                }
            }
        }

        public override bool Reset(int reset) => playMgr.Old != default && playMgr.New == default;

        public override bool Loading() => status.New == 1;

        public override void Dispose() => mono.Dispose();
    }
}