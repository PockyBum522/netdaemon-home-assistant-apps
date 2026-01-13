namespace AllenStreetNetDaemonApps.Utilities;

public static class NameFetcher
{
    public static string GetTrimmedName(object classToWork)
    {
        var namespaceBuiltString = classToWork.GetType().Namespace ?? "ErrorGettingNamespace";
        
        namespaceBuiltString += "." + classToWork.GetType().Name;

        namespaceBuiltString = namespaceBuiltString.Replace("AllenStreetNetDaemonApps.", "");
            
        return namespaceBuiltString;
    }
}