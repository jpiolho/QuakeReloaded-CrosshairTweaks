using CrosshairTweaks.Template;
using QuakeReloaded.Interfaces;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace CrosshairTweaks
{
    /// <summary>
    /// Your mod logic goes here.
    /// </summary>
    public class Mod : ModBase // <= Do not Remove.
    {
        /// <summary>
        /// Provides access to the mod loader API.
        /// </summary>
        private readonly IModLoader _modLoader;

        /// <summary>
        /// Provides access to the Reloaded.Hooks API.
        /// </summary>
        /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
        private readonly IReloadedHooks? _hooks;

        /// <summary>
        /// Provides access to the Reloaded logger.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Entry point into the mod, instance that created this class.
        /// </summary>
        private readonly IMod _owner;

        /// <summary>
        /// The configuration of the currently executing mod.
        /// </summary>
        private readonly IModConfig _modConfig;

        private IntPtr _cvarAlpha, _cvarSize;

        private unsafe float* _valueAlpha, _valueSize;

        public Mod(ModContext context)
        {
            _modLoader = context.ModLoader;
            _hooks = context.Hooks;
            _logger = context.Logger;
            _owner = context.Owner;
            _modConfig = context.ModConfig;



            var currentProcess = Process.GetCurrentProcess();
            var mainModule = currentProcess.MainModule!;
            var t = _modLoader.GetController<IStartupScanner>();
            if (!t.TryGetTarget(out var scanner))
                throw new Exception("Failed to get scanner");

            if (!_modLoader.GetController<IQuakeReloaded>().TryGetTarget(out var qreloaded))
                throw new Exception("Could not get QuakeReloaded API. Are you sure QuakeReloaded is loaded before this mod?");

            qreloaded.Events._EXPERIMENTAL_RegisterOnInitialized(() =>
            {
                _cvarAlpha = qreloaded.Cvars.Register("crosshair_alpha", "1.0f", "How transparent the crosshair is", CvarFlags.Float | CvarFlags.Saved, 0f, 1f);
                _cvarSize = qreloaded.Cvars.Register("crosshair_size", "0.0f", "How big the crosshair is", CvarFlags.Float | CvarFlags.Saved, 0.0f, 10f);
                
                qreloaded.Console.PrintLine("CrosshairTweaks initialized", 0, 255, 0);
            });

            qreloaded.Events._EXPERIMENTAL_RegisterOnRenderFrame(() =>
            {
                unsafe
                {
                    *_valueSize = qreloaded.Cvars.GetFloatValue(_cvarSize, 0.0f);
                    *_valueAlpha = qreloaded.Cvars.GetFloatValue(_cvarAlpha, 1.0f);
                }
            });


            unsafe
            {
                _valueAlpha = (float*)Marshal.AllocHGlobal(sizeof(float));
                *_valueAlpha = 1.0f;
                
                _valueSize= (float*)Marshal.AllocHGlobal(sizeof(float));
                *_valueSize = 0.0f;
            }

            scanner.AddMainModuleScan("F3 44 0F 11 44 24 ?? F3 0F 11 7C 24 ?? F3 0F 11 74 24 ?? F3 44 0F 11 4C 24 ??", (result) =>
            {
                var offset = mainModule.BaseAddress + result.Offset;

                unsafe
                {
                    _hooks!.CreateAsmHook(new[]
                    {
                        $"use64",
                        $"push rax",

                        $"movss xmm9, dword [qword 0x{new IntPtr(_valueAlpha):x}]",

                        //Push xmm1
                        $"sub esp, 32",
                        $"movdqu  dqword [esp], xmm1",
                        $"movdqu  dqword [esp], xmm2",
                        
                        $"movss xmm1, dword [qword 0x{new IntPtr(_valueSize):x}]",
                        
                        $"mov eax,0x3dcccccd", // (float)0.1
                        $"movd xmm2, eax",
                        $"comisd xmm1, xmm2", // Check if _valueSize below 0.1
                        $"jb .skip",
                        
                        $"movss xmm0, xmm1",

                        $".skip:",
                        // Pop xmm1
                        $"movdqu xmm2, dqword [esp]",
                        $"movdqu xmm1, dqword [esp]",
                        $"add esp, 32",

                        $"pop rax",
                    }, (long)offset, Reloaded.Hooks.Definitions.Enums.AsmHookBehaviour.ExecuteFirst).Activate();
                }
            });
        }


        #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Mod() { }
#pragma warning restore CS8618
        #endregion
    }
}