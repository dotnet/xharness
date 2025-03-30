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
#if DEBUG
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
        private byte[] _intermediate = new byte[2];

        public int InputBlockSize => _base64Transform.InputBlockSize; // 3 bytes of input
        public int OutputBlockSize => _base64Transform.OutputBlockSize * 2; // 4 bytes of base64 output * 2 for UTF-16 encoding

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
            int inputBlocks = Math.DivRem(inputCount, InputBlockSize, out int inputRemainder);

            if (inputRemainder != 0)
            {
                throw new ArgumentException($"Input count must be a multiple of {InputBlockSize}.", nameof(inputCount));
            }

            if (inputCount == 0)
            {
                throw new ArgumentException("Input count must be greater than 0.", nameof(inputCount));
            }

            /*
            Input Buffer ("hi mom"):
            +-----+-----+-----+-----+-----+-----+
            | 'h' | 'i' | ' ' | 'm' | 'o' | 'm' |
            +-----+-----+-----+-----+-----+-----+
            |104  |105  | 32  |109  |111  |109  |
            +-----+-----+-----+-----+-----+-----+

            Base64 Encoding Process:
            - 'hi ' -> 'aGkg'
            - 'mom' -> 'bW9t'

            Base64 Encoded Output:
            |                   |base64Written      |                   | base64Written     |
            +----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+
            | \0 | \0 | \0 | \0 |'a' |'G' |'k' |'g' | \0 | \0 | \0 | \0 |'b' |'W' |'9' |'t' |
            +----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+
            |  0 |  0 |  0 |  0 | 97 | 71 |107 |103 |  0 |  0 |  0 |  0 | 98 | 87 | 57 |116 |
            +----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+

            Expanded Output Buffer (UTF-16 Encoding):
            | outputChars                           | outputChars                           |
            +----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+
            | \0 |'a' | \0 |'G' | \0 |'k' | \0 |'g' | \0 |'b' | \0 |'W' | \0 |'9' | \0 |'t' |
            +----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+
            | 0  | 97 | 0  | 71 | 0  |107 | 0  |103 | 0  | 98 | 0  | 87 | 0  | 57 | 0  |116 |
            +----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+----+

            */

            // Calculate positions in the output buffer
            int base64OutputStart = outputOffset + OutputBlockSize / 2;

            // write Base64 transformation directly to the second half of the output buffer
            int base64BytesWritten = _base64Transform.TransformBlock(
                inputBuffer, inputOffset, inputCount,
                outputBuffer, base64OutputStart);

            var base64Written = outputBuffer.AsSpan(base64OutputStart, base64BytesWritten);
            var outputChars = outputBuffer.AsSpan(outputOffset, OutputBlockSize);

            for (int i = 0; i < base64BytesWritten; i++)
            {
                // Expand each ascii byte to a char write it in the same logical position
                // as a char in outputChars eventually filling the output buffer
                if (!BitConverter.TryWriteBytes(outputChars.Slice(i * 2), (char)base64Written[i]))
                {
                    BitConverter.TryWriteBytes(_intermediate, (char)base64Written[i]);
                    _intermediate.CopyTo(outputChars.Slice(i * 2, 2));
                }
            }

            return base64BytesWritten * 2;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            // Apply Base64 transformation to the final block
            byte[] base64Buffer = _base64Transform.TransformFinalBlock(inputBuffer, inputOffset, inputCount);

            // Expand each Base64 byte to two bytes in the output buffer
            byte[] outputBuffer = new byte[base64Buffer.Length * 2];
            for (int i = 0; i < base64Buffer.Length; i++)
            {
                // Convert each ascii byte to a char
                BitConverter.TryWriteBytes(outputBuffer.AsSpan(i * 2), (char)base64Buffer[i]);
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

        // guaranteed to succeed with the MemoryStream() constructor
        ms.TryGetBuffer(out var bytes);
        // we went to a lot of trouble to put characters in the final buffer
        // so that we can avoid a copy here and pass the span directly to the
        // string interpolation logic.
        Span<char> charData = MemoryMarshal.Cast<byte, char>(bytes.AsSpan());

        // Output the result and the the ascii length of the data
        Console.Write($"STARTRESULTXML {charData.Length} ");
        Console.Write(charData);
        Console.WriteLine(" ENDRESULTXML");
        Console.WriteLine($"Finished writing {charData.Length} bytes of RESULTXML");
    }
#endif
}
