import sys
import os
from lxml import etree

def main():
    parser = etree.XMLParser(remove_blank_text=True)
    file = etree.parse(sys.argv[1], parser=parser)
    data = file.getroot().find("hksection[@name='__data__']")
    classDict = etree.Element("Classes")
    if os.path.isfile("Res/Ds3ClothClasses.xml"):
        classDict = etree.parse("Res/Ds3ClothClasses.xml", parser=parser).getroot()
    dump_unique_objects(data, classDict)
    etree.ElementTree(classDict).write("Res/Ds3ClothClasses.xml", encoding="ASCII", xml_declaration=True, method="xml", standalone=False, pretty_print=True)
    
   
def dump_unique_objects(data, classDict):
    for object in data.findall("hkobject"):
        if classDict.find(f'hkobject[@class="{object.get("class")}"]') == None:
            classDict.append(object)

if __name__ == "__main__":
    main()