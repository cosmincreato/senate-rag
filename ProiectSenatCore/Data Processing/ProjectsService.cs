namespace ProiectSenatCore;

using System.Xml;

public static class ProjectsService
{
    private static readonly HttpClient SharedClient = new()
    {
        BaseAddress = new Uri("https://www.senat.ro/"),
    };

    public static async Task<List<Dictionary<string, string>>> GetProjectsAsync(string an = "", string nrSenat = "",
        string nrDeputati = "")
    {
        try
        {
            string endpoint = $"exportdata.asmx/ListaProiectelor?An={an}&NrSenat={nrSenat}&NrDeputati={nrDeputati}";
            HttpResponseMessage response = await SharedClient.GetAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"GET response for An={an}, NrSenat={nrSenat}, NrDeputati={nrDeputati}");
                return ParseProjects(content);
            }
            else
            {
                Console.WriteLine($"Request failed with status: {response.StatusCode}");
                return new List<Dictionary<string, string>>();
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"HTTP request error: {e.Message}");
            return new List<Dictionary<string, string>>();
        }
        catch (Exception e)
        {
            Console.WriteLine($"General error: {e.Message}");
            return new List<Dictionary<string, string>>();
        }
    }

    public static async Task<List<string>> GetAllPdfUrlsAsync(string nrSe = "", string an = "")
    {
        try
        {
            string endpoint = $"/exportdata.asmx/proiect_xml?NR_SE={nrSe}&AN_SE={an}";
            HttpResponseMessage response = await SharedClient.GetAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"GET response for NR_SE={nrSe}, An={an}");
                return ParseAllPdfUrls(content);
            }
            else
            {
                Console.WriteLine($"Request failed with status: {response.StatusCode}");
                return new List<string>();
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"HTTP request error: {e.Message}");
            return new List<string>();
        }
        catch (Exception e)
        {
            Console.WriteLine($"General error: {e.Message}");
            return new List<string>();
        }
    }

    private static List<Dictionary<string, string>> ParseProjects(string xmlContent)
    {
        try
        {
            List<Dictionary<string, string>> projects = new List<Dictionary<string, string>>();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlContent);

            XmlNodeList projectNodes = doc.GetElementsByTagName("Proiect");

            Console.WriteLine($"Found {projectNodes.Count} projects.");

            foreach (XmlNode project in projectNodes)
                projects.Add(ParseProject(project));

            return projects;
        }
        catch (XmlException e)
        {
            Console.WriteLine($"XML parsing error: {e.Message}");
            return new List<Dictionary<string, string>>();
        }
        catch (Exception e)
        {
            Console.WriteLine($"General parsing error: {e.Message}");
            return new List<Dictionary<string, string>>();
        }
    }

    private static List<string> ParseAllPdfUrls(string xmlContent)
    {
        try
        {
            List<string> allPdfUrls = new List<string>();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlContent);

            // Get all <Fisier> elements directly
            XmlNodeList fisierNodes = doc.GetElementsByTagName("Fisier");
            Console.WriteLine($"Found {fisierNodes.Count} PDF files.");

            foreach (XmlNode fisier in fisierNodes)
            {
                string pdfUrl = fisier.InnerText.Trim();
                if (!string.IsNullOrEmpty(pdfUrl))
                {
                    allPdfUrls.Add(pdfUrl);
                }
            }

            return allPdfUrls;
        }
        catch (XmlException e)
        {
            Console.WriteLine($"XML parsing error: {e.Message}");
            return new List<string>();
        }
        catch (Exception e)
        {
            Console.WriteLine($"General parsing error: {e.Message}");
            return new List<string>();
        }
    }

    private static Dictionary<string, string> ParseProject(XmlNode projectNode)
    {
        Dictionary<string, string> projectData = new Dictionary<string, string>();
        if (projectNode.Attributes != null)
        {
            foreach (XmlAttribute attr in projectNode.Attributes)
            {
                projectData[attr.Name] = attr.Value;
            }
        }

        return projectData;
    }
}