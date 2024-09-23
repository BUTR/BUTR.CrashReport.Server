using System;
using System.Xml.Serialization;

namespace BUTR.CrashReport.Server.Models.Sitemaps;

[Serializable]
public enum ChangeFrequency
{
    [XmlEnum(Name = "always")]
    Always,

    [XmlEnum(Name = "hourly")]
    Hourly,

    [XmlEnum(Name = "daily")]
    Daily,

    [XmlEnum(Name = "weekly")]
    Weekly,

    [XmlEnum(Name = "monthly")]
    Monthly,

    [XmlEnum(Name = "yearly")]
    Yearly,

    [XmlEnum(Name = "never")]
    Never
}