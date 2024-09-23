using System.Collections.Generic;
using System.Xml.Serialization;

namespace BUTR.CrashReport.Server.Models.Sitemaps;

[XmlRoot(ElementName = "sitemapindex", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
public class SitemapIndex
{

    [XmlElement(ElementName = "sitemap", Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9")]
    public List<Sitemap> Sitemap { get; set; }
}