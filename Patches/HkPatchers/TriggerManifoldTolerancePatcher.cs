using System.Xml.Linq;
using JetBrains.Annotations;

namespace DS3HavokConverter.Patches.HkPatchers;

[UsedImplicitly]
public class TriggerManifoldTolerancePatcher : IHkPatcher
{
    public string FieldName => "triggerManifoldTolerance";

    public bool Patch(XElement outputField, XElement inputObject)
    {
        outputField.RemoveNodes();
        XElement triggerManifoldTolerance = new("hkobject", new XAttribute("class", "hkUFloat8"),
            new XAttribute("name", "triggerManifoldTolerance"), new XAttribute("signature", "0x7c076f9a"),
            new XElement("hkparam", new XAttribute("name", "value"), "255"));
        outputField.Add(triggerManifoldTolerance);

        return true;
    }
}