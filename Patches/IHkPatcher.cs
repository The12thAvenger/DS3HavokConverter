using System.Xml.Linq;

namespace DS3HavokConverter.Patches;

public interface IHkPatcher
{
    public string FieldName { get; }

    public bool Patch(XElement outputField, XElement inputObject);
}