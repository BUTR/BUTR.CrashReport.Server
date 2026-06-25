using BUTR.CrashReport.Server.Controllers;
using BUTR.CrashReport.Server.Models;
using BUTR.CrashReport.Server.Models.API;

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BUTR.CrashReport.Server;

// Source-generated metadata for the API DTOs (de)serialized through the MVC pipeline.
// This is prepended to the resolver chain; the reflection-based default resolver remains as a
// fallback for everything else, notably the external BUTR.CrashReport.Models payloads handled
// by the v13/v14 upload handlers (which rely on custom converters/polymorphism and are not
// suitable for source generation here).
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(FileMetadata))]
[JsonSerializable(typeof(FileMetadata[]))]
[JsonSerializable(typeof(IEnumerable<FileMetadata>))]
[JsonSerializable(typeof(TLSError))]
[JsonSerializable(typeof(ReportController.GetNewCrashReportsBody))]
[JsonSerializable(typeof(ICollection<string>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(string))]
public partial class AppJsonSerializerContext : JsonSerializerContext;
