using System.Xml.Serialization;

namespace BUTR.CrashReportServer.Models.Sitemaps;

[XmlRoot(ElementName="sitemap", Namespace="http://www.sitemaps.org/schemas/sitemap/0.9")]
public class Sitemap
{ 
    [XmlElement(ElementName="loc", Namespace="http://www.sitemaps.org/schemas/sitemap/0.9")] 
    public string Location { get; set; }
}