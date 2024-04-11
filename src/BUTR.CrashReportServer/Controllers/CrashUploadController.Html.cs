extern alias v13;
using v13::BUTR.CrashReport.Bannerlord.Parser;
using v13::BUTR.CrashReport.Models;

namespace BUTR.CrashReportServer.Controllers;

partial class CrashUploadController
{
    private static (bool isValid, byte version, CrashReportModel? crashReportModel) ParseHtmlV13(string html)
    {
        var valid = CrashReportParser.TryParse(html, out var version, out var crashReportModel, out _);
        return (valid, version, crashReportModel);
    }
}