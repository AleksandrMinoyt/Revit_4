using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
namespace Revit_4
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand

    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            //var listWall = new FilteredElementCollector(doc)
            //     .OfClass(typeof(Wall))
            //     .OfType<Wall>()
            //     .ToList();

            //var listWallType = new FilteredElementCollector(doc)
            //     .OfClass(typeof(WallType))
            //     .OfType<WallType>()
            //     .ToList();

            //var listExternalFamily = new FilteredElementCollector(doc)
            //     .OfClass(typeof(FamilyInstance))
            //     .OfType<FamilyInstance>()
            //     .ToList();

            //var listDoor = new FilteredElementCollector(doc)
            //     .OfClass(typeof(FamilyInstance))
            //     .OfCategory(BuiltInCategory.OST_Doors)
            //     .OfType<FamilyInstance>()
            //     .ToList();

            //var listDoorName = new FilteredElementCollector(doc)
            //    .OfClass(typeof(FamilyInstance))
            //    .OfCategory(BuiltInCategory.OST_Doors)
            //    .OfType<FamilyInstance>()
            //    .Where(x=>x.Name.Equals("0915 x 2134 мм"))
            //    .ToList();

            var ListLevel = new FilteredElementCollector(doc)
               .OfClass(typeof(Level))
               .OfType<Level>()
               .ToList();

            var Level1 = ListLevel
                 .Where(x => x.Name.Equals("Уровень 1"))
                 .FirstOrDefault();

            var Level2 = ListLevel
                .Where(x => x.Name.Equals("Уровень 2"))
                .FirstOrDefault();

            List<Wall> walls = new List<Wall>();
            walls = CreateWalls(doc, 10000, 5000, Level1, Level2);

            if (walls==null)
            {
                message = "Ошибка создания стен";
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        private List<Wall> CreateWalls(Document doc, double width, double depth, Level levelDown, Level lewelUp)
        {
            try
            {
                double dx = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters) / 2;
                double dy = UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters) / 2;

                List<XYZ> points = new List<XYZ>();
                points.Add(new XYZ(-dx, -dy, 0));
                points.Add(new XYZ(dx, -dy, 0));
                points.Add(new XYZ(dx, dy, 0));
                points.Add(new XYZ(-dx, dy, 0));
                points.Add(new XYZ(-dx, -dy, 0));

                List<Wall> walls = new List<Wall>();

                Transaction trans = new Transaction(doc, "Создаём стены");
                trans.Start();

                for (int i = 0; i < 4; i++)
                {
                    Wall wall = CreateWallUsingCurve(doc, levelDown, points[i], points[i + 1]);
                    wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(lewelUp.Id);
                    walls.Add(wall);
                }

                trans.Commit();
                return walls;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Wall CreateWallUsingCurve(Document document, Level level, XYZ start, XYZ end)
        {
            Line geomLine = Line.CreateBound(start, end);
            return Wall.Create(document, geomLine, level.Id, true);
        }
    }
}
