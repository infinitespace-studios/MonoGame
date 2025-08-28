using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using MonoGame.Effect.TPGParser;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGame.Effect
{
	    internal partial class ShaderData
    {
        private const int _SUCCESS_RETURN_CODE = 0;

        // For debugging purposes.
        internal byte[] MetalShaderBytes { get; set; }

        /// <summary>
        /// Converts SPIR-V to Metal first to then be consumed by other code.
        /// TODO: Cleanup unnecessary params.
        /// </summary>
        public static ShaderData CreateMetalFromSpirV(ShaderData shaderData, bool isVertexShader,
            List<ConstantBufferData> cbuffers, int sharedIndex, Dictionary<string, SamplerStateInfo> samplerStates,
            bool debug, string shaderFunctionName, ShaderInfo shaderInfo, Dictionary<string, string> macros,
            ShaderResult shaderResult)
        {
            Console.WriteLine($"Converting SPIRV->MSL for {shaderFunctionName} in {shaderResult.FilePath}");
            string spirVCrossTool = "spirv-cross";

            {
                // Convert from HLSL to SpirV - output is in a temporary directory.
                var spirvFile = shaderData.SpirVOutputFile;
                if (string.IsNullOrEmpty(spirvFile) || !File.Exists(spirvFile))
                {
                    throw new Exception($"SpirV output is missing {spirvFile}");
                }

                shaderData.MetalOutputFile = Path.ChangeExtension(shaderData.SpirVOutputFile, ".msl");
                var reflectionFile = Path.ChangeExtension(shaderData.SpirVOutputFile, ".json");
                
                var additionalOptions =
                    $"--rename-entry-point {shaderFunctionName} main {(isVertexShader ? "vert" : "frag")} " +
                    $"";
                    
                // First, generate reflection data to extract constant buffer information
                if (ExternalTool.Run(spirVCrossTool,
                        $"--reflect --output {reflectionFile} {spirvFile}",
                        out var reflectStdout, out var reflectStderr) == _SUCCESS_RETURN_CODE &&
                    File.Exists(reflectionFile))
                {
                    // Parse the reflection data to extract constant buffer and sampler info
                    var reflectionJson = File.ReadAllText(reflectionFile);
                    
                    // Parse constant buffers from SPIR-V reflection
                    var parsedBuffers = ConstantBufferData.ParseSpirvReflection(reflectionJson);
                    cbuffers.AddRange(parsedBuffers);
                    
                    // Parse samplers from SPIR-V reflection 
                    var parsedSamplers = ParseSamplersFromReflection(reflectionJson, samplerStates);
                    shaderData._samplers = parsedSamplers.ToArray();
                    
                    // Parse vertex attributes from SPIR-V reflection (only for vertex shaders)
                    var parsedAttributes = ParseAttributesFromReflection(reflectionJson, isVertexShader);
                    shaderData._attributes = parsedAttributes.ToArray();
                    
                    // Clean up reflection file if not debugging
                    if (!debug)
                        File.Delete(reflectionFile);
                }
                else
                {
                    Console.WriteLine($"Failed to generate reflection data: stdout={reflectStdout}, stderr={reflectStderr}");
                }
                
                // MSL Version: MMmmpp (1.2.0) - this allows us to target the lowest Metal-supported devices such
                // as iPad Mini 2, iPhone 5s. See https://developer.apple.com/support/required-device-capabilities/#iphone-devices
                // Note that this generates code that is supported by the lowest version: we still need to nudge
                // the compiler (at runtime for instance) to explicitly provide the compiler version via MTLCompileOptions MTLLanguageVersion.
                if (ExternalTool.Run(spirVCrossTool,
                        $"--msl --msl-ios  --msl-version 10200 {additionalOptions} --output {shaderData.MetalOutputFile} {shaderData.SpirVOutputFile}",
                        out var stdout, out var stderr) != _SUCCESS_RETURN_CODE ||
                    !File.Exists(shaderData.MetalOutputFile))
                {
                    throw new Exception($"Unable to convert spirv to metal:\n{stdout}\n{stderr}");
                }

                Console.WriteLine($" -- MetalSL written to {shaderData.MetalOutputFile}");
                var metalBytes = File.ReadAllBytes(shaderData.MetalOutputFile);
                shaderData.MetalShaderBytes = metalBytes;
                shaderData.ShaderCode = metalBytes;
            }

            return shaderData;
        }

        private static List<Sampler> ParseSamplersFromReflection(string reflectionJson, Dictionary<string, SamplerStateInfo> samplerStates)
        {
            var samplers = new List<Sampler>();

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(reflectionJson))
                {
                    var root = doc.RootElement;

                    // Get separate images (textures)
                    var separateImages = new Dictionary<int, (string name, string type)>();
                    if (root.TryGetProperty("separate_images", out var imagesElement))
                    {
                        foreach (var image in imagesElement.EnumerateArray())
                        {
                            var name = image.GetProperty("name").GetString();
                            var type = image.GetProperty("type").GetString();
                            var binding = image.GetProperty("binding").GetInt32();
                            separateImages[binding] = (name, type);
                        }
                    }

                    // Get separate samplers
                    if (root.TryGetProperty("separate_samplers", out var samplersElement))
                    {
                        foreach (var samplerInfo in samplersElement.EnumerateArray())
                        {
                            var samplerName = samplerInfo.GetProperty("name").GetString();
                            var samplerType = samplerInfo.GetProperty("type").GetString();
                            var samplerBinding = samplerInfo.GetProperty("binding").GetInt32();

                            // Map sampler type to MonoGame sampler type
                            var mojoSamplerType = MojoShader.MOJOSHADER_samplerType.MOJOSHADER_SAMPLER_2D;
                            if (separateImages.TryGetValue(samplerBinding, out var textureInfo))
                            {
                                switch (textureInfo.type)
                                {
                                    case "texture1D":
                                        mojoSamplerType = MojoShader.MOJOSHADER_samplerType.MOJOSHADER_SAMPLER_1D;
                                        break;
                                    case "texture2D":
                                        mojoSamplerType = MojoShader.MOJOSHADER_samplerType.MOJOSHADER_SAMPLER_2D;
                                        break;
                                    case "texture3D":
                                        mojoSamplerType = MojoShader.MOJOSHADER_samplerType.MOJOSHADER_SAMPLER_VOLUME;
                                        break;
                                    case "textureCube":
                                        mojoSamplerType = MojoShader.MOJOSHADER_samplerType.MOJOSHADER_SAMPLER_CUBE;
                                        break;
                                    default:
                                        mojoSamplerType = MojoShader.MOJOSHADER_samplerType.MOJOSHADER_SAMPLER_2D;
                                        break;
                                }
                            }

                            var sampler = new Sampler
                            {
                                type = mojoSamplerType,
                                textureSlot = samplerBinding,  // In HLSL/SPIR-V separate samplers, texture and sampler use same binding
                                samplerSlot = samplerBinding,
                                samplerName = samplerName,
                                parameterName = samplerName,
                                parameter = -1,  // Will be set later during effect processing
                                state = null
                            };

                            // Try to find matching sampler state from the shader info
                            if (samplerStates.TryGetValue(samplerName, out var stateInfo))
                            {
                                sampler.state = stateInfo.State;
                                if (!string.IsNullOrEmpty(stateInfo.TextureName))
                                    sampler.parameterName = stateInfo.TextureName;
                            }

                            samplers.Add(sampler);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing sampler reflection data: {ex.Message}");
            }

            return samplers;
        }

        private static List<Attribute> ParseAttributesFromReflection(string reflectionJson, bool isVertexShader)
        {
            var attributes = new List<Attribute>();

            // Only vertex shaders have input attributes
            if (!isVertexShader)
                return attributes;

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(reflectionJson))
                {
                    var root = doc.RootElement;

                    // Get vertex stage inputs
                    if (root.TryGetProperty("inputs", out var inputsElement))
                    {
                        var sortedInputs = new List<(JsonElement input, int location)>();
                        
                        foreach (var input in inputsElement.EnumerateArray())
                        {
                            if (input.TryGetProperty("location", out var locationElement))
                            {
                                var location = locationElement.GetInt32();
                                sortedInputs.Add((input, location));
                            }
                        }

                        // Sort by location for consistent ordering
                        sortedInputs.Sort((a, b) => a.location.CompareTo(b.location));

                        foreach (var (input, location) in sortedInputs)
                        {
                            var attribute = new Attribute();

                            if (input.TryGetProperty("name", out var nameElement))
                            {
                                var name = nameElement.GetString() ?? "unknown";
                                
                                // Extract semantic and index from variable name
                                // HLSL inputs like "in_var_POSITION0" become "POSITION" with index 0
                                var match = Regex.Match(name, @"in_var_(\w+)(\d*)$");
                                if (match.Success)
                                {
                                    var semanticName = match.Groups[1].Value;
                                    var indexStr = match.Groups[2].Value;
                                    
                                    attribute.index = string.IsNullOrEmpty(indexStr) ? 0 : int.Parse(indexStr);
                                    
                                    // Map semantic name to VertexElementUsage
                                    switch (semanticName.ToUpper())
                                    {
                                        case "POSITION":
                                            attribute.usage = VertexElementUsage.Position;
                                            break;
                                        case "NORMAL":
                                            attribute.usage = VertexElementUsage.Normal;
                                            break;
                                        case "TANGENT":
                                            attribute.usage = VertexElementUsage.Tangent;
                                            break;
                                        case "BINORMAL":
                                            attribute.usage = VertexElementUsage.Binormal;
                                            break;
                                        case "COLOR":
                                            attribute.usage = VertexElementUsage.Color;
                                            break;
                                        case "TEXCOORD":
                                            attribute.usage = VertexElementUsage.TextureCoordinate;
                                            break;
                                        case "BLENDINDICES":
                                            attribute.usage = VertexElementUsage.BlendIndices;
                                            break;
                                        case "BLENDWEIGHT":
                                            attribute.usage = VertexElementUsage.BlendWeight;
                                            break;
                                        case "DEPTH":
                                            attribute.usage = VertexElementUsage.Depth;
                                            break;
                                        case "FOG":
                                            attribute.usage = VertexElementUsage.Fog;
                                            break;
                                        case "POINTSIZE":
                                            attribute.usage = VertexElementUsage.PointSize;
                                            break;
                                        case "TESSELLATEFACTOR":
                                            attribute.usage = VertexElementUsage.TessellateFactor;
                                            break;
                                        default:
                                            attribute.usage = VertexElementUsage.TextureCoordinate;
                                            break;
                                    }
                                }
                                else
                                {
                                    // Fallback: use the raw name and default usage
                                    attribute.index = 0;
                                    attribute.usage = VertexElementUsage.TextureCoordinate;
                                }
                            }

                            // These fields are unused in new native backends but required for serialization
                            attribute.location = 0;
                            attribute.name = string.Empty;

                            attributes.Add(attribute);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing attribute reflection data: {ex.Message}");
            }

            return attributes;
        }

        public string MetalOutputFile { get; set; }
    }
}