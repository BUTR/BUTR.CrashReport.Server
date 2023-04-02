using System;

namespace BUTR.CrashReportServer.Models.Database
{
    public sealed record FileEntity : IEntity
    {
        public required string Name { get; init; }
        public required DateTimeOffset Created { get; init; }
        public required DateTimeOffset Modified { get; init; }
        public required long SizeOriginal { get; init; }
        public required byte[] DataCompressed { get; init; }
    }
}