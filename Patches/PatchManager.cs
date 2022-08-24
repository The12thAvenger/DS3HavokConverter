using System.Xml.Linq;

namespace DS3HavokConverter.Patches;

public static class PatchManager
{
    private static readonly Dictionary<string, IHkPatcher> PatchLibrary;

    static PatchManager()
    {
        PatchLibrary = new Dictionary<string, IHkPatcher>();

        IEnumerable<Type> patchTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(t => t.GetInterfaces().Contains(typeof(IHkPatcher)));
        foreach (Type patchType in patchTypes)
        {
            IHkPatcher patcher = (IHkPatcher)Activator.CreateInstance(patchType)!;
            PatchLibrary.Add(patcher.FieldName, patcher);
        }
    }

    public static bool Patch(XElement outputField, XElement inputObject)
    {
        string fieldName = outputField.Attribute("name")!.Value;

        return PatchLibrary.TryGetValue(fieldName, out IHkPatcher? patcher) && patcher.Patch(outputField, inputObject);
    }
}