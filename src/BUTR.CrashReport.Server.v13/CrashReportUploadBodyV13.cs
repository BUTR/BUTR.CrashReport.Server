using BUTR.CrashReport.Models;

using System.Collections.Generic;

namespace BUTR.CrashReport.Server.v13;

public sealed record CrashReportUploadBodyV13(CrashReportModel CrashReport, ICollection<LogSource> LogSources);