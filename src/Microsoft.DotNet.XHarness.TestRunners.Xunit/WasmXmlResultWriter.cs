// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Text;
using System.Security.Cryptography;

#nullable enable

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal class WasmXmlResultWriter
{
#if !NET || DEBUG
    public static void WriteOnSingleLine(XElement assembliesElement)
    {
        using var ms = new MemoryStream();
        assembliesElement.Save(ms);
        ms.TryGetBuffer(out var bytes);
        var base64 = Convert.ToBase64String(bytes, Base64FormattingOptions.None);
        Console.WriteLine($"STARTRESULTXML {bytes.Count} {base64} ENDRESULTXML");
        Console.WriteLine($"Finished writing {bytes.Count} bytes of RESULTXML");
    }
#else
    private class ToBase64CharTransform : ICryptoTransform
    {
        private readonly ToBase64Transform _base64Transform = new ToBase64Transform();

        public int InputBlockSize => _base64Transform.InputBlockSize;
        public int OutputBlockSize => _base64Transform.OutputBlockSize * 2;
        public bool CanTransformMultipleBlocks => _base64Transform.CanTransformMultipleBlocks;
        public bool CanReuseTransform => _base64Transform.CanReuseTransform;

        public void Dispose()
        {
            _base64Transform.Dispose();
        }

        public int TransformBlock(
            byte[] inputBuffer, int inputOffset, int inputCount,
            byte[] outputBuffer, int outputOffset)
        {
            int totalBytesWritten = 0;
            int inputProcessed = 0;

            while (inputProcessed < inputCount)
            {
                int bytesToProcess = Math.Min(InputBlockSize, inputCount - inputProcessed);

                // Calculate positions in the output buffer
                int base64OutputStart = outputOffset + totalBytesWritten + OutputBlockSize / 2;
                int base64OutputLength = _base64Transform.OutputBlockSize;

                // Apply Base64 transformation directly to the second half of the output buffer
                int base64BytesWritten = _base64Transform.TransformBlock(
                    inputBuffer, inputOffset + inputProcessed, bytesToProcess,
                    outputBuffer, base64OutputStart);

                var outputSpan = MemoryMarshal.Cast<byte, char>(outputBuffer.AsSpan(outputOffset + totalBytesWritten, OutputBlockSize));
                for (int i = 0; i < base64BytesWritten; i++)
                {
                    char base64Char = (char)outputBuffer[base64OutputStart + i];
                    outputSpan[i] = base64Char;
                }

                inputProcessed += bytesToProcess;
                totalBytesWritten += base64BytesWritten * 2;
            }

            return totalBytesWritten;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            // Apply Base64 transformation to the final block
            byte[] base64Buffer = _base64Transform.TransformFinalBlock(inputBuffer, inputOffset, inputCount);

            // Expand each Base64 byte to two bytes in the output buffer
            byte[] outputBuffer = new byte[base64Buffer.Length * 2];
            Span<char> outputSpan = MemoryMarshal.Cast<byte, char>(outputBuffer.AsSpan());
            for (int i = 0; i < base64Buffer.Length; i++)
            {
                // Convert each byte to a char
                char base64Char = (char)base64Buffer[i];
                outputSpan[i] = base64Char;
            }

            return outputBuffer;
        }
    }

    public static void WriteOnSingleLine(XElement assembliesElement)
    {
        using var ms = new MemoryStream();
        using var transform = new ToBase64CharTransform();
        using var cryptoStream = new CryptoStream(ms, transform, CryptoStreamMode.Write);

        // Create a StreamWriter to write the XML content to the CryptoStream
        using var xmlWriter = new StreamWriter(cryptoStream, Encoding.UTF8);

        assembliesElement.Save(xmlWriter);

        // Ensure all data is flushed through the CryptoStream
        xmlWriter.Flush();
        cryptoStream.FlushFinalBlock();

        ms.TryGetBuffer(out var bytes);
        var charData = MemoryMarshal.Cast<byte,char>(bytes);

        // Output the result
        Console.WriteLine($"STARTRESULTXML {charData.Length} {charData} ENDRESULTXML");
        Console.WriteLine($"Finished writing {charData.Length} bytes of RESULTXML");
    }
#endif
}
