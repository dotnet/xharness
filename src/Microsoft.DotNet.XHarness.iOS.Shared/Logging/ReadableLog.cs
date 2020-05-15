using System.IO;
using Microsoft.DotNet.XHarness.Common.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging
{
    public abstract class ReadableLog : Log, IReadableLog
    {

        protected ReadableLog(string? description = null) : base(description) { }

        public abstract StreamReader GetReader();
    }

    public abstract class FileBackedLog : ReadableLog, IFileBackedLog
    {
        protected FileBackedLog(string? description = null) : base(description) { }

        public abstract string FullPath { get; }
    }
}
