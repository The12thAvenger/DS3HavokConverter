using System.Xml.Linq;
using JetBrains.Annotations;

namespace DS3HavokConverter.Patches.HkPatchers;

[UsedImplicitly]
public class MotionIdPatcher : IHkPatcher
{
    public string FieldName => "motionId";

    public bool Patch(XElement outputField, XElement inputObject)
    {
        outputField.Value = inputObject.Parent!.Elements().ToList().IndexOf(inputObject).ToString();
        return true;
    }
}