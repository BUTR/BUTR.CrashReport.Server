using BUTR.CrashReport.Models;

using System.Collections.Generic;

namespace BUTR.CrashReportServer.v13;

public sealed record CrashReportUploadBodyV13(CrashReportModel CrashReport, ICollection<LogSource> LogSources);