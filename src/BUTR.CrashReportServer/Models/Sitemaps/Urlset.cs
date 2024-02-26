using System.Collections.Generic;
using System.Xml.Serialization;

namespace BUTR.CrashReportServer.Models.Sitemaps;

[XmlRoot(ElementName = "urlset", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
public class Urlset
{

    [XmlElement(ElementName = "url", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
    public List<Url> Url { get; set; }
}