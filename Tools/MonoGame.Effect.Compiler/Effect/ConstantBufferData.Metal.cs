// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

namespace MonoGame.Effect
{
    internal partial class ConstantBufferData
    {
        /// <summary>
        /// Parses SPIR-V reflection JSON data to extract constant buffer parameters.
        /// This uses spirv-cross --reflect to get uniform buffer information.
        /// </summary>
        /// <param name="reflectionJson">JSON reflection data from spirv-cross</param>
        public static List<ConstantBufferData> ParseSpirvReflection(string reflectionJson)
        {
            using var doc = JsonDocument.Parse(reflectionJson);
            var root = doc.RootElement;
            
            var cbuffers = new List<ConstantBufferData>();
            
            if (root.TryGetProperty("ubos", out var ubos))
            {
                foreach (var ubo in ubos.EnumerateArray())
                {
                    if (ubo.TryGetProperty("name", out var nameElement) &&
                        ubo.TryGetProperty("type", out var typeElement))
                    {
                        var cbuffer = new ConstantBufferData(nameElement.GetString() ?? "UnknownBuffer");
                        
                        // Get the type definition for this UBO
                        var typeName = typeElement.GetString();
                        if (!string.IsNullOrEmpty(typeName) && root.TryGetProperty("types", out var types))
                        {
                            foreach (var type in types.EnumerateObject())
                            {
                                if (type.Name == typeName && type.Value.TryGetProperty("members", out var members))
                                {
                                    foreach (var member in members.EnumerateArray())
                                    {
                                        var param = ParseMember(member, types);
                                        if (param != null)
                                        {
                                            cbuffer.Parameters.Add(param);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        
                        cbuffers.Add(cbuffer);
                        
                        // Finalize the constant buffer (similar to Vulkan processing)
                        FinalizeConstantBuffer(cbuffer);
                    }
                }
            }
            
            return cbuffers;
        }
        
        private static void FinalizeConstantBuffer(ConstantBufferData cbuffer)
        {
            // Sort parameters by offset for consistent results
            cbuffer.Parameters = cbuffer.Parameters.OrderBy(e => e.bufferOffset).ToList();

            // Recreate the parameter offsets and calculate the size
            cbuffer.ParameterOffset.Clear();
            
            for (int i = 0; i < cbuffer.Parameters.Count; i++)
            {
                var p = cbuffer.Parameters[i];
                cbuffer.ParameterOffset.Add(p.bufferOffset);

                var esize = p.rows * p.columns * 4;
                if (p.element_count > 0)
                    esize = (esize + (16 - (esize % 16))) * p.element_count;

                var newSize = p.bufferOffset + (int)esize;
                if (newSize > cbuffer.Size)
                    cbuffer.Size = newSize;
            }
            
            // Note: ParameterIndex is populated later in EffectObject.cs when integrating
            // constant buffers into the global effect parameters list
        }

        private void ParseUniformBuffer(JsonElement ubo)
        {
            if (!ubo.TryGetProperty("members", out JsonElement members))
                return;

            foreach (JsonElement member in members.EnumerateArray())
            {
                if (member.TryGetProperty("name", out JsonElement nameElement) &&
                    member.TryGetProperty("type", out JsonElement typeElement) &&
                    member.TryGetProperty("offset", out JsonElement offsetElement))
                {
                    string name = nameElement.GetString();
                    string type = typeElement.GetString();
                    int offset = offsetElement.GetInt32();

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(type))
                    {
                        AddParameterFromSpirvType(name, type, offset);
                    }
                }
            }
        }

        private void AddParameterFromSpirvType(string name, string spirvType, int byteOffset)
        {
            // Has this parameter already been added?
            var found = Parameters.FirstOrDefault(p => p.name == name);
            if (found != null)
                return;

            // Create the new parameter.
            var param = new EffectObject.d3dx_parameter();
            param.name = name;
            param.semantic = string.Empty;
            param.bufferOffset = byteOffset;

            // Map SPIR-V types to MonoGame parameter types
            MapSpirvTypeToParameter(spirvType, param);

            var byteSize = param.rows * param.columns * 4;
            param.data = new byte[byteSize];

            // Add the new parameter and resort by offset
            Parameters.Add(param);
            Parameters = Parameters.OrderBy(e => e.bufferOffset).ToList();

            // Recalculate size and parameter offsets
            UpdateSizeAndOffsets();
        }

        private static EffectObject.d3dx_parameter ParseMember(JsonElement member, JsonElement types)
        {
            var param = new EffectObject.d3dx_parameter();
            
            // Initialize required fields
            param.semantic = string.Empty;
            
            // Initialize data field for serialization
            var defaultSize = 16; // Default to 16 bytes (4 floats)
            param.data = new byte[defaultSize];
            
            if (member.TryGetProperty("name", out var nameElement))
            {
                param.name = nameElement.GetString() ?? "unknown";
            }
            
            if (member.TryGetProperty("offset", out var offsetElement))
            {
                param.bufferOffset = offsetElement.GetInt32();
            }
            
            if (member.TryGetProperty("type", out var typeElement))
            {
                var typeName = typeElement.GetString();
                if (!string.IsNullOrEmpty(typeName))
                {
                    MapSpirvTypeToParameter(typeName, param);
                    
                    // Update data array size based on parameter dimensions
                    var dataSize = param.rows * param.columns * 4; // 4 bytes per component
                    param.data = new byte[dataSize];
                }
            }
            
            return param;
        }
        
        private static void MapSpirvTypeToParameter(string spirvType, EffectObject.d3dx_parameter param)
        {
            // Set default values first
            param.rows = 1;
            param.columns = 1;
            param.type = EffectObject.D3DXPARAMETER_TYPE.FLOAT;
            param.class_ = EffectObject.D3DXPARAMETER_CLASS.SCALAR;
            param.element_count = 0;
            
            // Handle basic scalar types
            switch (spirvType.ToLowerInvariant())
            {
                case "float":
                    param.rows = 1;
                    param.columns = 1;
                    param.type = EffectObject.D3DXPARAMETER_TYPE.FLOAT;
                    param.class_ = EffectObject.D3DXPARAMETER_CLASS.SCALAR;
                    break;

                case "int":
                case "uint":
                    param.rows = 1;
                    param.columns = 1;
                    param.type = EffectObject.D3DXPARAMETER_TYPE.INT;
                    param.class_ = EffectObject.D3DXPARAMETER_CLASS.SCALAR;
                    break;

                case "bool":
                    param.rows = 1;
                    param.columns = 1;
                    param.type = EffectObject.D3DXPARAMETER_TYPE.BOOL;
                    param.class_ = EffectObject.D3DXPARAMETER_CLASS.SCALAR;
                    break;

                // Handle vector types
                case "vec2":
                case "float2":
                    param.rows = 1;
                    param.columns = 2;
                    param.type = EffectObject.D3DXPARAMETER_TYPE.FLOAT;
                    param.class_ = EffectObject.D3DXPARAMETER_CLASS.VECTOR;
                    break;

                case "vec3":
                case "float3":
                    param.rows = 1;
                    param.columns = 3;
                    param.type = EffectObject.D3DXPARAMETER_TYPE.FLOAT;
                    param.class_ = EffectObject.D3DXPARAMETER_CLASS.VECTOR;
                    break;

                case "vec4":
                case "float4":
                    param.rows = 1;
                    param.columns = 4;
                    param.type = EffectObject.D3DXPARAMETER_TYPE.FLOAT;
                    param.class_ = EffectObject.D3DXPARAMETER_CLASS.VECTOR;
                    break;

                // Handle matrix types
                case "mat4":
                case "float4x4":
                    param.rows = 4;
                    param.columns = 4;
                    param.type = EffectObject.D3DXPARAMETER_TYPE.FLOAT;
                    param.class_ = EffectObject.D3DXPARAMETER_CLASS.MATRIX_COLUMNS;
                    break;

                case "mat3":
                case "float3x3":
                    param.rows = 3;
                    param.columns = 3;
                    param.type = EffectObject.D3DXPARAMETER_TYPE.FLOAT;
                    param.class_ = EffectObject.D3DXPARAMETER_CLASS.MATRIX_COLUMNS;
                    break;

                case "mat2":
                case "float2x2":
                    param.rows = 2;
                    param.columns = 2;
                    param.type = EffectObject.D3DXPARAMETER_TYPE.FLOAT;
                    param.class_ = EffectObject.D3DXPARAMETER_CLASS.MATRIX_COLUMNS;
                    break;

                default:
                    // Default to float scalar for unknown types
                    param.rows = 1;
                    param.columns = 1;
                    param.type = EffectObject.D3DXPARAMETER_TYPE.FLOAT;
                    param.class_ = EffectObject.D3DXPARAMETER_CLASS.SCALAR;
                    break;
            }
        }

        private void UpdateSizeAndOffsets()
        {
            Size = 0;
            ParameterOffset.Clear();
            
            foreach (var p in Parameters)
            {
                ParameterOffset.Add(p.bufferOffset);
                var esize = p.rows * p.columns * 4;
                
                if (p.element_count > 0)
                    esize = (esize + (16 - (esize % 16))) * p.element_count;

                Size = Math.Max(Size, p.bufferOffset + (int)esize);
            }
        }
    }
}
