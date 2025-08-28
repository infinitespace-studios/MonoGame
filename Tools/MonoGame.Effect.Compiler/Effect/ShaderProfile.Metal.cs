// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MonoGame.Effect.TPGParser;

namespace MonoGame.Effect
{
    class MetalShaderProfile : ShaderProfile
    {
        public MetalShaderProfile()
            : base("Metal", 80)
        {
        }

        internal override void AddMacros(Dictionary<string, string> macros)
        {
            macros.Add("SM6", "1");
            macros.Add("METAL", "1");
        }

        internal override void ValidateShaderModels(PassInfo pass)
        {
            if (!string.IsNullOrEmpty(pass.vsFunction))
            {
                if (pass.vsModel != "vs_6_0")
                    throw new Exception(String.Format("Invalid Metal vertex profile '{0}'! Requires vs_6_0.", pass.vsModel));
            }

            if (!string.IsNullOrEmpty(pass.psFunction))
            {
                if (pass.psModel != "ps_6_0")
                    throw new Exception(String.Format("Invalid Metal pixel profile '{0}'! Requires ps_6_0.", pass.psModel));
            }
        }

        internal override ShaderData CreateShader(ShaderResult shaderResult, string shaderFunction,
            string shaderProfile, bool isVertexShader, EffectObject effect, ref string errorsAndWarnings)
        {
            // First look to see if we already created this same shader.
            foreach (var shader in effect.Shaders)
            {
                if (shader.SourceFile == shaderResult.FilePath && shader.Entrypoint == shaderFunction)
                    return shader;
            }

            try
            {
                // Create macros dictionary for compilation
                var macros = new Dictionary<string, string>();
                AddMacros(macros);
                
                // Step 1: Create SPIR-V from HLSL using DXC
                var spirvShaderData = new ShaderData(isVertexShader, effect.Shaders.Count, new byte[0]);
                spirvShaderData = ShaderData.CreateSpirVFromFX(spirvShaderData, isVertexShader, shaderResult.Debug, 
                    shaderFunction, shaderResult, macros);

                // Step 2: Convert SPIR-V to Metal using SPIRV-Cross
                var shaderInfo = shaderResult.ShaderInfo;
                var constantBuffers = effect.ConstantBuffers ?? new List<ConstantBufferData>();
                var samplerStates = shaderInfo?.SamplerStates ?? new Dictionary<string, SamplerStateInfo>();
                
                var metalShaderData = ShaderData.CreateMetalFromSpirV(spirvShaderData, isVertexShader, 
                    constantBuffers, effect.Shaders.Count, samplerStates, shaderResult.Debug, 
                    shaderFunction, shaderInfo, new Dictionary<string, string>(), shaderResult);

                // Validate the result
                if (metalShaderData == null)
                {
                    errorsAndWarnings += "Failed to create Metal shader data from SPIR-V";
                    throw new ShaderCompilerException();
                }

                // Store metadata for the shader
                metalShaderData.SourceFile = shaderResult.FilePath;
                metalShaderData.Entrypoint = shaderFunction;
                metalShaderData.ShaderProfile = "Metal";

                // Process constant buffers extracted during Metal compilation
                var cbufferIndex = new List<int>();
                foreach (var cbuffer in constantBuffers)
                {
                    // Look for existing matching constant buffers
                    var match = effect.ConstantBuffers.FindIndex(e => e.SameAs(cbuffer));
                    if (match == -1)
                    {
                        cbufferIndex.Add(effect.ConstantBuffers.Count);
                        effect.ConstantBuffers.Add(cbuffer);
                    }
                    else
                        cbufferIndex.Add(match);
                }

                // Initialize arrays for serialization with constant buffer and sampler information
                metalShaderData._cbuffers = cbufferIndex.ToArray();
                // _samplers is populated during Metal compilation via reflection data
                metalShaderData._attributes = new ShaderData.Attribute[0]; // TODO: Extract from SPIR-V reflection

                effect.Shaders.Add(metalShaderData);
                return metalShaderData;
            }
            catch (Exception ex)
            {
                errorsAndWarnings += ex.Message;
                throw new ShaderCompilerException();
            }
        }
    }
}