using BUTR.CrashReport.Server.Controllers;
using BUTR.CrashReport.Server.Models;
using BUTR.CrashReport.Server.Models.API;

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BUTR.CrashReport.Server;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(FileMetadata))]
[JsonSerializable(typeof(FileMetadata[]))]
[JsonSerializable(typeof(IEnumerable<FileMetadata>))]
[JsonSerializable(typeof(TLSError))]
[JsonSerializable(typeof(ReportManagementController.GetNewCrashReportsBody))]
[JsonSerializable(typeof(ICollection<string>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(string))]
public partial class AppJsonSerializerContext : JsonSerializerContext;