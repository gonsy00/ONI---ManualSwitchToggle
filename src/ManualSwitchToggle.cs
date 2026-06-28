using HarmonyLib;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;

namespace ManualSwitchToggle {

    public class ManualSwitchToggleMod : KMod.UserMod2 {
        public override void OnLoad(Harmony harmony) {
            base.OnLoad(harmony);
            PatchAllToggleTypes(harmony);
            Debug.Log("[ManualSwitchToggle] Cargado");
        }

        private static void PatchAllToggleTypes(Harmony harmony) {
            var toggleInterface = typeof(IPlayerControlledToggle);

            var ifaceToggledByPlayer = toggleInterface.GetMethod("ToggledByPlayer");
            if (ifaceToggledByPlayer == null) {
                Debug.LogWarning("[ManualSwitchToggle] IPlayerControlledToggle.ToggledByPlayer no encontrado.");
                return;
            }

            var ifaceToggleRequestedSetter = toggleInterface.GetProperty("ToggleRequested")?.GetSetMethod();
            if (ifaceToggleRequestedSetter == null) {
                Debug.LogWarning("[ManualSwitchToggle] IPlayerControlledToggle.set_ToggleRequested no encontrado.");
                return;
            }

            var toggledByPlayerPrefix = typeof(Patches.AnySwitch_ToggledByPlayer_Patch)
                .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
            var toggleRequestedPrefix = typeof(Patches.AnySwitch_ToggleRequested_Patch)
                .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);

            var implementors = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .Where(t => t.IsClass && !t.IsAbstract && toggleInterface.IsAssignableFrom(t))
                .ToList();

            if (implementors.Count == 0) {
                Debug.LogWarning("[ManualSwitchToggle] Ningun tipo implementa IPlayerControlledToggle.");
                return;
            }

            var patchedSetters = new System.Collections.Generic.HashSet<System.Reflection.MethodBase>();

            foreach (var type in implementors) {
                try {
                    var map = type.GetInterfaceMap(toggleInterface);

                    // Camino sin pausa: ToggledByPlayer
                    int tbpIdx = Array.IndexOf(map.InterfaceMethods, ifaceToggledByPlayer);
                    if (tbpIdx >= 0) {
                        var targetMethod = map.TargetMethods[tbpIdx];
                        harmony.Patch(targetMethod, prefix: new HarmonyMethod(toggledByPlayerPrefix));
                        Debug.Log($"[ManualSwitchToggle] Patcheado ToggledByPlayer en {type.Name} -> {targetMethod.Name}");
                    }

                    // Camino con pausa: set_ToggleRequested
                    // Usamos HashSet para no parchear el mismo metodo dos veces si hay clases derivadas
                    int trIdx = Array.IndexOf(map.InterfaceMethods, ifaceToggleRequestedSetter);
                    if (trIdx >= 0) {
                        var targetSetter = map.TargetMethods[trIdx];
                        if (patchedSetters.Add(targetSetter)) {
                            harmony.Patch(targetSetter, prefix: new HarmonyMethod(toggleRequestedPrefix));
                            Debug.Log($"[ManualSwitchToggle] Patcheado set_ToggleRequested en {type.Name} -> {targetSetter.Name}");
                        }
                    }
                } catch (Exception e) {
                    Debug.LogWarning($"[ManualSwitchToggle] Error patcheando {type.Name}: {e.Message}");
                }
            }
        }
    }

    // Registra el circuit-toggle del switch como handler en el Toggleable existente.
    // Toggleable ya tiene offset table, only_when_operational=false, Prioritizable y status items.
    public class SwitchToggleLink : KMonoBehaviour {
        private int toggleableTargetIdx = -1;
        public bool IsCompletingWork { get; private set; } = false;

        protected override void OnSpawn() {
            base.OnSpawn();
            var toggleable = GetComponent<Toggleable>();
            var toggle = GetComponent<IPlayerControlledToggle>();
            if (toggleable != null && toggle != null) {
                toggleableTargetIdx = toggleable.SetTarget(new SwitchCircuitToggleHandler(this, toggle));
                Debug.Log($"[ManualSwitchToggle] Registrado en '{gameObject.name}' con idx={toggleableTargetIdx}");
            } else {
                Debug.LogWarning($"[ManualSwitchToggle] Toggleable o IPlayerControlledToggle no encontrado en '{gameObject.name}'");
            }
        }

        public void RequestToggle() {
            if (toggleableTargetIdx < 0) return;
            GetComponent<Toggleable>()?.Toggle(toggleableTargetIdx);
        }

        private class SwitchCircuitToggleHandler : IToggleHandler {
            private readonly SwitchToggleLink link;
            private readonly IPlayerControlledToggle toggle;

            public SwitchCircuitToggleHandler(SwitchToggleLink link, IPlayerControlledToggle toggle) {
                this.link = link;
                this.toggle = toggle;
            }

            public void HandleToggle() {
                link.IsCompletingWork = true;
                try { toggle.ToggledByPlayer(); }
                finally { link.IsCompletingWork = false; }
            }

            public bool IsHandlerOn() {
                return toggle.ToggledOn();
            }
        }

        protected override void OnCleanUp() {
            base.OnCleanUp();
        }
    }

    public static class Patches {
        [HarmonyPatch(typeof(LogicSwitchConfig), nameof(LogicSwitchConfig.DoPostConfigureComplete))]
        public static class LogicSwitchConfig_Patch {
            public static void Postfix(GameObject go) {
                go.AddOrGet<SwitchToggleLink>();
                Prioritizable.AddRef(go);
            }
        }

        [HarmonyPatch(typeof(SwitchConfig), nameof(SwitchConfig.DoPostConfigureComplete))]
        public static class SwitchConfig_Patch {
            public static void Postfix(GameObject go) {
                go.AddOrGet<SwitchToggleLink>();
                Prioritizable.AddRef(go);
            }
        }

        // Intercepta el toggle instantaneo (camino sin pausa).
        // Igual que la puerta: en InstantBuildMode deja pasar para cambio inmediato sin chore.
        public static class AnySwitch_ToggledByPlayer_Patch {
            public static bool Prefix(object __instance) {
                if (DebugHandler.InstantBuildMode) return true;

                var mono = __instance as MonoBehaviour;
                if (mono == null) return true;

                var link = mono.GetComponent<SwitchToggleLink>();
                if (link == null) return true;

                if (link.IsCompletingWork) return true;

                link.RequestToggle();
                return false;
            }
        }

        // Intercepta el shortcut de pausa: PlayerControlledToggleSideScreen.RequestToggle()
        // pone ToggleRequested=true cuando el juego esta en pausa, y Sim33ms lo ejecuta
        // directamente saltandose el chore. Bloqueamos y redirigimos al chore normal.
        // Igual que la puerta: en InstantBuildMode dejamos pasar para cambio inmediato.
        public static class AnySwitch_ToggleRequested_Patch {
            public static bool Prefix(object __instance, bool value) {
                if (!value) return true; // reset a false: limpieza normal, siempre dejar pasar

                if (DebugHandler.InstantBuildMode) return true;

                var mono = __instance as MonoBehaviour;
                if (mono == null) return true;

                var link = mono.GetComponent<SwitchToggleLink>();
                if (link == null) return true;

                if (link.IsCompletingWork) return true;

                link.RequestToggle();
                return false;
            }
        }
    }
}
