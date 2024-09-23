using System;
using System.Xml.Serialization;

namespace BUTR.CrashReport.Server.Models.Sitemaps;

[XmlRoot(ElementName = "url", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
public class Url
{
    [XmlElement(ElementName = "loc", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
    public string Location { get; set; }

    [XmlIgnore]
    public DateTime TimeStamp { get; set; }

    [XmlElement(ElementName = "lastmod", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
    public string LastMod
    {
        get => TimeStamp.ToString("yyyy-MM-ddTHH:mm:sszzz");
        set => TimeStamp = DateTime.Parse(value);
    }

    [XmlElement(ElementName = "changefreq", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
    public ChangeFrequency ChangeFrequency { get; set; }

    [XmlElement(ElementName = "priority", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
    public double Priority { get; set; }
}