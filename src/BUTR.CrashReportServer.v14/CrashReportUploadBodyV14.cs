using BUTR.CrashReport.Models;

using System.Collections.Generic;

namespace BUTR.CrashReportServer.v14;

public sealed record CrashReportUploadBodyV14(CrashReportModel CrashReport, ICollection<LogSource> LogSources);