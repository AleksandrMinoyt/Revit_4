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

            if (walls == null)
            {
                message = "Ошибка создания стен";
                return Result.Failed;
            }


            FamilyInstance door = AddDoor(doc, Level1, walls[0]);


            if (door == null)
            {
                message = "Ошибка создания двери";
                return Result.Failed;
            }


            List<FamilyInstance> windows = new List<FamilyInstance>();

            for (int i = 1; i < 4; i++)
            {
                FamilyInstance window = AddWindow(doc, Level1, walls[i]);
                if (window == null)
                {
                    message = "Ошибка создания окна";
                    return Result.Failed;
                }
                windows.Add(window);
            }    

            return Result.Succeeded;
        }

        private FamilyInstance AddWindow(Document doc, Level level, Wall wall)
        {
            try
            {
                Transaction trans = new Transaction(doc, "Создаём окно");

                trans.Start();
                var windowType = new FilteredElementCollector(doc)
                              .OfClass(typeof(FamilySymbol))
                              .OfCategory(BuiltInCategory.OST_Windows)
                              .OfType<FamilySymbol>()
                              .Where(x => x.Name.Equals("0915 x 1830 мм"))
                              .Where(x => x.FamilyName.Equals("Фиксированные"))
                              .FirstOrDefault();
                var lc = wall.Location as LocationCurve;

                XYZ point1 = lc.Curve.GetEndPoint(0);
                XYZ point2 = lc.Curve.GetEndPoint(1);

                XYZ point = (point1 + point2) / 2;

                point = point.Add(new XYZ(0, 0, UnitUtils.ConvertToInternalUnits(600, UnitTypeId.Millimeters)));

                if (!windowType.IsActive)
                    windowType.Activate();

                FamilyInstance window = doc.Create.NewFamilyInstance(point, windowType, wall, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                trans.Commit();

                return window;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private FamilyInstance AddDoor(Document doc, Level level, Wall wall)
        {
            try
            {
                Transaction trans = new Transaction(doc, "Создаём дверь");

                trans.Start();
                var doorType = new FilteredElementCollector(doc)
                              .OfClass(typeof(FamilySymbol))
                              .OfCategory(BuiltInCategory.OST_Doors)
                              .OfType<FamilySymbol>()
                              .Where(x => x.Name.Equals("0915 x 2134 мм"))
                              .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                              .FirstOrDefault();
                var lc = wall.Location as LocationCurve;

                XYZ point1 = lc.Curve.GetEndPoint(0);
                XYZ point2 = lc.Curve.GetEndPoint(1);

                XYZ point = (point1 + point2) / 2;

                if (!doorType.IsActive)
                    doorType.Activate();

                FamilyInstance door = doc.Create.NewFamilyInstance(point, doorType, wall, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                trans.Commit();

                return door;
            }
            catch (Exception)
            {
                return null;
            }
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
