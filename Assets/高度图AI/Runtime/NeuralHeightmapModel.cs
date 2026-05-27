using System;
using UnityEngine;

namespace HeightmapAI
{
#pragma warning disable 0649
    [Serializable]
    internal sealed class NeuralHeightmapModelData
    {
        public int version;
        public int tileWidth;
        public int tileHeight;
        public int frequencyCount;
        public int hiddenWidth;
        public int hiddenLayers;
        public string activation;
        public float heightMin;
        public float heightMax;
        public NeuralHeightmapLayerData[] layers;
        public NeuralHeightmapMetricsData metrics;
    }

    [Serializable]
    internal sealed class NeuralHeightmapLayerData
    {
        public int inputSize;
        public int outputSize;
        public float[] weights;
        public float[] bias;
    }

    [Serializable]
    internal sealed class NeuralHeightmapMetricsData
    {
        public float mse;
        public float mae;
        public float maxError;
        public int sourceBytes;
        public int modelBytes;
        public float compressionRatio;
    }
#pragma warning restore 0649

    public sealed class NeuralHeightmapModel
    {
        private readonly NeuralHeightmapModelData data;
        private readonly float[] encodingBuffer;
        private readonly float[][] layerBuffers;

        private NeuralHeightmapModel(NeuralHeightmapModelData data)
        {
            Validate(data);

            this.data = data;
            encodingBuffer = new float[2 + 4 * data.frequencyCount];
            layerBuffers = new float[data.layers.Length][];
            for (int i = 0; i < data.layers.Length; i++)
            {
                layerBuffers[i] = new float[data.layers[i].outputSize];
            }
        }

        public int TileWidth => data.tileWidth;
        public int TileHeight => data.tileHeight;
        public float Mse => data.metrics != null ? data.metrics.mse : 0f;
        public float Mae => data.metrics != null ? data.metrics.mae : 0f;
        public float MaxError => data.metrics != null ? data.metrics.maxError : 0f;
        public float CompressionRatio => data.metrics != null ? data.metrics.compressionRatio : 0f;

        public static NeuralHeightmapModel FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Model JSON is empty.", nameof(json));
            }

            NeuralHeightmapModelData parsed = JsonUtility.FromJson<NeuralHeightmapModelData>(json);
            return new NeuralHeightmapModel(parsed);
        }

        public float EvaluateHeight(Vector2 uv)
        {
            Encode(new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y)), encodingBuffer, data.frequencyCount);

            float[] current = encodingBuffer;
            for (int layerIndex = 0; layerIndex < data.layers.Length; layerIndex++)
            {
                NeuralHeightmapLayerData layer = data.layers[layerIndex];
                float[] output = layerBuffers[layerIndex];
                for (int outputIndex = 0; outputIndex < layer.outputSize; outputIndex++)
                {
                    float value = layer.bias[outputIndex];
                    int weightOffset = outputIndex * layer.inputSize;
                    for (int inputIndex = 0; inputIndex < layer.inputSize; inputIndex++)
                    {
                        value += layer.weights[weightOffset + inputIndex] * current[inputIndex];
                    }

                    bool isHiddenLayer = layerIndex < data.layers.Length - 1;
                    output[outputIndex] = isHiddenLayer ? Mathf.Max(0f, value) : value;
                }

                current = output;
            }

            return Mathf.Clamp01(current[0]);
        }

        public Texture2D ReconstructTexture()
        {
            Texture2D texture = new Texture2D(data.tileWidth, data.tileHeight, TextureFormat.RGBA32, false, true);
            for (int y = 0; y < data.tileHeight; y++)
            {
                float v = data.tileHeight <= 1 ? 0f : (float)y / (data.tileHeight - 1);
                for (int x = 0; x < data.tileWidth; x++)
                {
                    float u = data.tileWidth <= 1 ? 0f : (float)x / (data.tileWidth - 1);
                    float height = EvaluateHeight(new Vector2(u, v));
                    texture.SetPixel(x, y, new Color(height, height, height, 1f));
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static void Encode(Vector2 uv, float[] output, int frequencyCount)
        {
            output[0] = uv.x;
            output[1] = uv.y;
            int index = 2;
            for (int frequency = 1; frequency <= frequencyCount; frequency++)
            {
                float angleU = 2f * Mathf.PI * frequency * uv.x;
                float angleV = 2f * Mathf.PI * frequency * uv.y;
                output[index++] = Mathf.Sin(angleU);
                output[index++] = Mathf.Cos(angleU);
                output[index++] = Mathf.Sin(angleV);
                output[index++] = Mathf.Cos(angleV);
            }
        }

        private static void Validate(NeuralHeightmapModelData data)
        {
            if (data == null)
            {
                throw new ArgumentException("Model JSON could not be parsed.");
            }

            if (data.version != 1)
            {
                throw new ArgumentException($"Unsupported model version {data.version}.");
            }

            if (!string.Equals(data.activation, "relu", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Unsupported activation '{data.activation}'.");
            }

            if (data.tileWidth <= 0 || data.tileHeight <= 0)
            {
                throw new ArgumentException("Model tile size must be greater than zero.");
            }

            if (data.frequencyCount <= 0)
            {
                throw new ArgumentException("Model frequency count must be greater than zero.");
            }

            if (data.layers == null || data.layers.Length == 0)
            {
                throw new ArgumentException("Model must contain at least one layer.");
            }

            if (data.hiddenLayers < 0)
            {
                throw new ArgumentException("Model hidden layer count cannot be negative.");
            }

            if (data.layers.Length != data.hiddenLayers + 1)
            {
                throw new ArgumentException($"Model has {data.layers.Length} layers, expected {data.hiddenLayers + 1}.");
            }

            if (data.hiddenLayers > 0 && data.hiddenWidth <= 0)
            {
                throw new ArgumentException("Model hidden width must be greater than zero.");
            }

            int expectedInputSize = 2 + 4 * data.frequencyCount;
            for (int i = 0; i < data.layers.Length; i++)
            {
                NeuralHeightmapLayerData layer = data.layers[i];
                if (layer == null)
                {
                    throw new ArgumentException($"Layer {i} is missing.");
                }

                if (layer.inputSize != expectedInputSize)
                {
                    throw new ArgumentException($"Layer {i} input size is {layer.inputSize}, expected {expectedInputSize}.");
                }

                if (layer.outputSize <= 0)
                {
                    throw new ArgumentException($"Layer {i} output size must be greater than zero.");
                }

                bool isHiddenLayer = i < data.layers.Length - 1;
                if (isHiddenLayer && layer.outputSize != data.hiddenWidth)
                {
                    throw new ArgumentException($"Layer {i} output size is {layer.outputSize}, expected hidden width {data.hiddenWidth}.");
                }

                if (layer.weights == null || layer.weights.Length != layer.inputSize * layer.outputSize)
                {
                    throw new ArgumentException($"Layer {i} has an invalid weight array length.");
                }

                if (layer.bias == null || layer.bias.Length != layer.outputSize)
                {
                    throw new ArgumentException($"Layer {i} has an invalid bias array length.");
                }

                expectedInputSize = layer.outputSize;
            }

            if (data.layers[data.layers.Length - 1].outputSize != 1)
            {
                throw new ArgumentException("Final layer output size must be 1.");
            }
        }
    }
}
