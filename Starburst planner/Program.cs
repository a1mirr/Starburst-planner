using Newtonsoft.Json;
using System.Collections.Concurrent;

public class Portal
{
    public long Timestamp { get; set; }
    public string Guid { get; set; }
    public string Team { get; set; }
    public string Title { get; set; }
    public string Image { get; set; }
    public int LatE6 { get; set; }
    public int LngE6 { get; set; }
    public List<string> Ornaments { get; set; }
    public bool Mission { get; set; }
    public bool Mission50plus { get; set; }
    public int Level { get; set; }
    public int ResCount { get; set; }
    public int Health { get; set; }
    public HashSet<Link> BlockedBy { get; set; } = new HashSet<Link>();
    public HashSet<Portal> Blocks { get; set; } = new HashSet<Portal>();
}

public class Link
{
    public long Timestamp { get; set; }
    public string Guid { get; set; }
    public string Team { get; set; }
    public Portal Orig { get; set; }
    public Portal Dest { get; set; }
}

public class MapData
{
    public long Timestamp { get; set; }
    public int ZoomLevel { get; set; }
    public List<Portal> Portals { get; set; }
    public List<Link> Links { get; set; }
}

public class Program
{
    public static void Main(string[] args)
    {
        // Default values
        const int defaultTargetLinks = 1400;
        const double defaultMaxDistanceKm = 6.0;
        string defaultTargetPortalGuid = "888af823724633ef9c6f7d3564976640.16"; // Lany
        string defaultInputFilePath = "C:\\Users\\zemch\\Downloads\\20240715133524_Neu_Enl_Res_AllLinks.json";
        string defaultOutputFilePath = "D:\\result.json";

        // Parse command-line arguments
        var parsedArgs = ParseArguments(args, defaultTargetPortalGuid, defaultTargetLinks, defaultMaxDistanceKm, defaultInputFilePath, defaultOutputFilePath);

        // Process map data
        MapData mapData = ParseMapFile(parsedArgs.InputFilePath);
        FilterPortalsAndLinks(mapData, parsedArgs.TargetPortalGuid, parsedArgs.MaxDistanceKm);
        List<Portal> selectedPortals = PlanStarburst(mapData, parsedArgs.TargetPortalGuid, parsedArgs.TargetLinks);

        // Output results
        string outputJson = FormatOutput(selectedPortals, mapData.Portals.First(p => p.Guid == parsedArgs.TargetPortalGuid));
        File.WriteAllText(parsedArgs.OutputFilePath, outputJson);

        Console.WriteLine("Output written to " + parsedArgs.OutputFilePath);
    }

    private static (string InputFilePath, string OutputFilePath, string TargetPortalGuid, int TargetLinks, double MaxDistanceKm) ParseArguments(
        string[] args,
        string defaultTargetPortalGuid,
        int defaultTargetLinks,
        double defaultMaxDistanceKm,
        string defaultInputFilePath,
        string defaultOutputFilePath)
    {
        // Initialize variables with default values
        string targetPortalGuid = defaultTargetPortalGuid;
        int targetLinks = defaultTargetLinks;
        double maxDistanceKm = defaultMaxDistanceKm;
        string inputFilePath = defaultInputFilePath;
        string outputFilePath = defaultOutputFilePath;

        // Parse command-line arguments
        foreach (string arg in args)
        {
            if (arg.StartsWith("/inputFilePath="))
            {
                inputFilePath = arg.Substring("/inputFilePath=".Length);
            }
            else if (arg.StartsWith("/outputFilePath="))
            {
                outputFilePath = arg.Substring("/outputFilePath=".Length);
            }
            else if (arg.StartsWith("/targetPortalGuid="))
            {
                targetPortalGuid = arg.Substring("/targetPortalGuid=".Length);
            }
            else if (arg.StartsWith("/targetLinksCount="))
            {
                targetLinks = int.Parse(arg.Substring("/targetLinksCount=".Length));
            }
            else if (arg.StartsWith("/maxDistanceKm="))
            {
                maxDistanceKm = double.Parse(arg.Substring("/maxDistanceKm=".Length));
            }
        }

        return (inputFilePath, outputFilePath, targetPortalGuid, targetLinks, maxDistanceKm);
    }

    public static MapData ParseMapFile(string filePath)
    {
        string jsonData = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<MapData>(jsonData);
    }

    public static void FilterPortalsAndLinks(MapData mapData, string targetPortalGuid, double maxDistanceKm)
    {
        var targetPortal = mapData.Portals.First(p => p.Guid == targetPortalGuid);
        double targetLat = targetPortal.LatE6 / 1e6;
        double targetLng = targetPortal.LngE6 / 1e6;

        mapData.Portals = mapData.Portals
            .Where(p => Distance(targetLat, targetLng, p.LatE6 / 1e6, p.LngE6 / 1e6) <= maxDistanceKm)
            .ToList();

        mapData.Links = mapData.Links
            .Where(link =>
                Distance(targetLat, targetLng, link.Orig.LatE6 / 1e6, link.Orig.LngE6 / 1e6) <= maxDistanceKm ||
                Distance(targetLat, targetLng, link.Dest.LatE6 / 1e6, link.Dest.LngE6 / 1e6) <= maxDistanceKm)
            .ToList();
    }

    public static double Distance(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371; // Radius of the Earth in km
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLng = (lng2 - lng1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                   Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    public static void CalculateBlockingInfo(MapData mapData, string targetPortalGuid)
    {
        var targetPortal = mapData.Portals.First(p => p.Guid == targetPortalGuid);
        int totalPortals = mapData.Portals.Count;

        // Use thread-safe collections
        var portalBlocksDict = new ConcurrentDictionary<string, ConcurrentBag<Portal>>();
        var portalBlockedByDict = new ConcurrentDictionary<string, ConcurrentBag<Link>>();

        // Initialize the progress variables
        int processedPortals = 0;
        int nextProgressUpdate = 5;
        object progressLock = new object();

        Parallel.ForEach(mapData.Portals, portal =>
        {
            foreach (var link in mapData.Links.Where(l => l.Dest.Guid != targetPortalGuid))
            {
                if (link.Team == "E" && LinkIntersects(portal, targetPortal, link))
                {
                    portalBlockedByDict.GetOrAdd(portal.Guid, new ConcurrentBag<Link>()).Add(link);

                    var origPortal = mapData.Portals.FirstOrDefault(p => p.Guid == link.Orig.Guid);
                    var destPortal = mapData.Portals.FirstOrDefault(p => p.Guid == link.Dest.Guid);

                    if (origPortal != null)
                    {
                        portalBlocksDict.GetOrAdd(origPortal.Guid, new ConcurrentBag<Portal>()).Add(portal);
                    }
                    if (destPortal != null)
                    {
                        portalBlocksDict.GetOrAdd(destPortal.Guid, new ConcurrentBag<Portal>()).Add(portal);
                    }
                }
            }

            // Update progress
            lock (progressLock)
            {
                processedPortals++;
                int progress = (processedPortals * 100) / totalPortals;
                if (progress >= nextProgressUpdate)
                {
                    Console.WriteLine($"Progress: {progress}%");
                    nextProgressUpdate += 5;
                }
            }
        });

        // Update the original portal objects with the collected data
        foreach (var portal in mapData.Portals)
        {
            if (portalBlockedByDict.TryGetValue(portal.Guid, out var blockedByLinks))
            {
                portal.BlockedBy = new HashSet<Link>(blockedByLinks);
            }
            if (portalBlocksDict.TryGetValue(portal.Guid, out var blocksPortals))
            {
                portal.Blocks = new HashSet<Portal>(blocksPortals);
            }
        }
    }

    public static bool LinkIntersects(Portal portal, Portal targetPortal, Link link)
    {
        double portalLat = portal.LatE6 / 1e6;
        double portalLng = portal.LngE6 / 1e6;
        double targetLat = targetPortal.LatE6 / 1e6;
        double targetLng = targetPortal.LngE6 / 1e6;

        double linkOrigLat = link.Orig.LatE6 / 1e6;
        double linkOrigLng = link.Orig.LngE6 / 1e6;
        double linkDestLat = link.Dest.LatE6 / 1e6;
        double linkDestLng = link.Dest.LngE6 / 1e6;

        // Check if the link starts from the same point as the portal
        if ((portalLat == linkOrigLat && portalLng == linkOrigLng) ||
            (portalLat == linkDestLat && portalLng == linkDestLng))
        {
            return false; // The link starts from the same point as the portal, not an intersection
        }

        return LinesIntersect(portalLat, portalLng, targetLat, targetLng, linkOrigLat, linkOrigLng, linkDestLat, linkDestLng);
    }

    public static bool LinesIntersect(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
    {
        double denominator = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
        if (denominator == 0) return false; // Lines are parallel

        double ua = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / denominator;
        double ub = ((x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3)) / denominator;

        return (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1);
    }

    public static List<Portal> InitializeLinkablePortals(List<Portal> portals)
    {
        return portals.Where(p => !p.BlockedBy.Any()).ToList();
    }
    public static List<Portal> PlanStarburst(MapData mapData, string targetPortalGuid, int targetLinks)
    {
        var portals = mapData.Portals;
        CalculateBlockingInfo(mapData, targetPortalGuid);
        var linkablePortals = InitializeLinkablePortals(portals);
        int iteration = 0;
        Console.WriteLine($"Iteration #{iteration}, linkable portals: {linkablePortals.Count}");

        while (linkablePortals.Count < targetLinks)
        {
            iteration++;
            var portalToNeutralize = portals
                .Where(p => p.Blocks.Any())
                .OrderByDescending(p => portals.Count(portal => portal.BlockedBy.All(link => link.Orig.Guid == p.Guid || link.Dest.Guid == p.Guid)))
                .FirstOrDefault();

            if (portalToNeutralize == null)
            {
                break; // No more portals to neutralize
            }

            UpdateBlockingInfo(portals, portalToNeutralize);
            linkablePortals = InitializeLinkablePortals(portals);

            Console.WriteLine($"Iteration #{iteration}, removed portal {portalToNeutralize.Title}, linkable portals: {linkablePortals.Count}");
        }

        return linkablePortals;
    }

    public static void UpdateBlockingInfo(List<Portal> portals, Portal neutralizedPortal)
    {
        foreach (var portal in portals)
        {
            portal.BlockedBy.RemoveWhere(link => link.Orig.Guid == neutralizedPortal.Guid || link.Dest.Guid == neutralizedPortal.Guid);
        }
        neutralizedPortal.Blocks.Clear();
    }

    public static string FormatOutput(List<Portal> selectedPortals, Portal targetPortal)
    {
        var output = new List<object>();

        foreach (var portal in selectedPortals)
        {
            // Add polyline
            output.Add(new
            {
                type = "polyline",
                latLngs = new[]
                {
                new { lat = portal.LatE6 / 1e6, lng = portal.LngE6 / 1e6 },
                new { lat = targetPortal.LatE6 / 1e6, lng = targetPortal.LngE6 / 1e6 }
            },
                color = "#a24ac3"
            });

            //// Add marker for the selected portal
            //output.Add(new
            //{
            //    type = "marker",
            //    latLng = new
            //    {
            //        lat = portal.LatE6 / 1e6,
            //        lng = portal.LngE6 / 1e6
            //    },
            //    color = "#a24ac3"
            //});
        }

        return JsonConvert.SerializeObject(output);
    }
}
