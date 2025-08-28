// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Content.Pipeline;
using MonoGame.Effect.TPGParser;
using MonoGame.Tool;

namespace MonoGame.Effect
{
    internal partial class ShaderData
    {
        /// <summary>
        /// Converts HLSL to SPIR-V first to then be consumed by other code.
        /// </summary>
        public static ShaderData CreateSpirVFromFX(ShaderData shaderData, bool isVertexShader,
            bool debug, string shaderFunctionName,
            ShaderResult shaderResult, Dictionary<string, string> macros = null)
        {
            Console.WriteLine(
                $"Converting HLSL->SPIRV for {shaderFunctionName} in {shaderResult.FilePath}");

            {
                // Convert from HLSL to SpirV - output is in a temporary directory.
                var tempFile = Path.GetTempFileName();
                string reflectionData = string.Empty, stderr = string.Empty, stdout = string.Empty, errorsAndWarnings = string.Empty;
                var spirvFile = tempFile + ".spv";
                var targetProfile =
                    isVertexShader ? "vs_6_0" : "ps_6_0"; // MGFX SM4 is too old! But latest versions work well.
                // See this doc for FXC->DXC porting guide: https://github.com/microsoft/DirectXShaderCompiler/wiki/Porting-shaders-from-FXC-to-DXC
                var additionalOptions = " -fvk-use-gl-layout " + // The Vulkan flags are probably unnecessary.
                                        " -fvk-auto-shift-bindings " +
                                        " -flegacy-macro-expansion " + // Maintain legacy behavior (DX9-ish)
                                        " -flegacy-resource-reservation " + // Maintain legacy behavior (DX9-ish)
                                        " -no-warnings " + // Warnings spook the MGCB pipeline and disrupts the flow.
                                        "  ";

                // Add macros to the compilation
                if (macros != null)
                {
                    foreach (var kv in macros) 
                    { 
                        additionalOptions += $" -D {kv.Key}={kv.Value} "; 
                    }
                }

                var result = Dxc.Run(
                    $" -spirv -T {targetProfile} -E {shaderFunctionName} {additionalOptions}  -Fo {spirvFile} {shaderResult.FilePath}", // Make sure the shader file is at the end.
                    out reflectionData, out stderr);
                errorsAndWarnings += stderr;
                if (result > 0)
                    throw new ShaderCompilerException();

                // TODO Also set:
                /*
                  -Fo <file>              Output object file
                  -Fre <file>             Output reflection to the given file
                  -Frs <file>             Output root signature to the given file
                  -Fsh <file>             Output shader hash to the given file
                                 */
                if (!string.IsNullOrEmpty(stderr))
                {
                    Console.WriteLine($"{stdout}\n{stderr}");
                }

                Console.WriteLine($" ---- SpirV written to {spirvFile}");
                shaderData.SpirVOutputFile = spirvFile;
            }

            return shaderData;
        }

        public string SpirVOutputFile { get; set; }
    }
}