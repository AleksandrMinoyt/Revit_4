using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.ApplicationServices;
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
            // размер дома можно задать произвольный
            walls = CreateWalls(doc, 10000, 10000, Level1, Level2);

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


            FootPrintRoof roof = AddFootRoof(doc, Level2, walls);
            // ExtrusionRoof roof = AddExtrusionRoof(doc, Level2, walls);

            if (roof == null)
            {
                message = "Ошибка создания крыши";
                return Result.Failed;
            }


            //Найдём 3д виды

            var view3d = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .Where(x => x.IsTemplate == false)
                    .FirstOrDefault();

            // И если они есть, то переключимся в 3д

            if (view3d != null)
            {
                try
                {
                    commandData.Application.ActiveUIDocument.ActiveView = view3d;
                    Transaction trans = new Transaction(doc, "Переключаем в 3D вид");
                    trans.Start();
                    view3d.DetailLevel = ViewDetailLevel.Fine;  // детагизацию побольше
                    view3d.DisplayStyle = DisplayStyle.Realistic;  // реализма тоже
                    trans.Commit();
                }
                catch
                {
                    //ну не вышло, так не вышло...
                }

            }

            return Result.Succeeded;
        }


        private ExtrusionRoof AddExtrusionRoof(Document doc, Level level, List<Wall> walls)
        {
            try
            {
                Transaction trans = new Transaction(doc, "Создаём крышу");
                trans.Start();

                var roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();
                Application application = doc.Application;

                LocationCurve curve1 = walls[0].Location as LocationCurve;
                XYZ p0 = curve1.Curve.GetEndPoint(0);
                XYZ p01 = curve1.Curve.GetEndPoint(1);
                LocationCurve curve3 = walls[3].Location as LocationCurve;
                XYZ p3 = curve3.Curve.GetEndPoint(0);

                double lenWall = (p01.X - p0.X) / 2;

                CurveArray curveArray = new CurveArray();
                // высоту стен для профиля задаём жестко 4000 + поднимаем на 400(толщина крыши), высота конька 6000, остальное считаем по стенам
                curveArray.Append(Line.CreateBound(new XYZ(0, p0.Y - walls[0].Width, UnitUtils.ConvertToInternalUnits(4400, UnitTypeId.Millimeters)), new XYZ(0, 0, UnitUtils.ConvertToInternalUnits(6000, UnitTypeId.Millimeters))));
                curveArray.Append(Line.CreateBound(new XYZ(0, 0, UnitUtils.ConvertToInternalUnits(6000, UnitTypeId.Millimeters)), new XYZ(0, p3.Y + walls[0].Width, UnitUtils.ConvertToInternalUnits(4400, UnitTypeId.Millimeters))));

                ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 1), new XYZ(0, 1, 0), doc.ActiveView);
                // крышу делаем с свесами по 300мм с каждого края
                ExtrusionRoof extrusionRoof = doc.Create.NewExtrusionRoof(curveArray, plane, level, roofType, -lenWall - UnitUtils.ConvertToInternalUnits(300, UnitTypeId.Millimeters), lenWall + UnitUtils.ConvertToInternalUnits(300, UnitTypeId.Millimeters));

                trans.Commit();
                return extrusionRoof;

            }

            catch (Exception ex)
            {
                return null;
            }

        }

        private FootPrintRoof AddFootRoof(Document doc, Level level, List<Wall> walls)
        {
            try
            {
                Transaction trans = new Transaction(doc, "Создаём крышу");
                trans.Start();

                var roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();
                Application application = doc.Application;
                CurveArray footPrint = application.Create.NewCurveArray();

                double wallDt = walls[0].Width / 2;
                List<XYZ> points = new List<XYZ>();
                points.Add(new XYZ(-wallDt, -wallDt, 0));
                points.Add(new XYZ(wallDt, -wallDt, 0));
                points.Add(new XYZ(wallDt, wallDt, 0));
                points.Add(new XYZ(-wallDt, wallDt, 0));
                points.Add(new XYZ(-wallDt, -wallDt, 0));

                for (int i = 0; i < walls.Count; i++)
                {
                    LocationCurve curve = walls[i].Location as LocationCurve;
                    XYZ p1 = curve.Curve.GetEndPoint(0);
                    XYZ p2 = curve.Curve.GetEndPoint(1);
                    Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
                    footPrint.Append(line);
                }

                ModelCurveArray footPrintModelCurveMapping = new ModelCurveArray();
                FootPrintRoof footPrintRoof = doc.Create.NewFootPrintRoof(footPrint, level, roofType, out footPrintModelCurveMapping);

                foreach (ModelCurve item in footPrintModelCurveMapping)
                {
                    footPrintRoof.set_DefinesSlope(item, true);
                    footPrintRoof.set_SlopeAngle(item, 0.6);
                }

                trans.Commit();

                return footPrintRoof;

            }

            catch (Exception ex)
            {
                return null;
            }

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
