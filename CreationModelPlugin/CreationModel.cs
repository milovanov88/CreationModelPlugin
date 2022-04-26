using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            List<Level> lelelsList = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            Level level1 = lelelsList
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();

            Level level2 = lelelsList
               .Where(x => x.Name.Equals("Уровень 2"))
               .FirstOrDefault();

            List<Wall> wallsList = new List<Wall>();

            CreateWalls(doc, level1, level2, wallsList, 10000, 5000);
            AddDoor(doc, level1, wallsList[0]);
            for (int i = 0; i < wallsList.Count-1; i++)
            {
                AddWindow(doc, level1, wallsList[i + 1], 1000);
            }
            AddRoof(doc, level2, wallsList, 10000, 5000, 3);

            return Result.Succeeded;
        }
        public void CreateWalls(Document doc, Level levelBase, Level levelUp, List<Wall> wallsList, double Width, double Depth)
        {
            double width = UnitUtils.ConvertToInternalUnits(Width, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(Depth, UnitTypeId.Millimeters);
            double dx = width / 2;
            double dy = depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            Transaction tr = new Transaction(doc, "Построение стен");
            tr.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, levelBase.Id, false);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(levelUp.Id);
                wallsList.Add(wall);
            }
            tr.Commit();
        }
        public void AddDoor(Document doc, Level level, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
               .OfClass(typeof(FamilySymbol))
               .OfCategory(BuiltInCategory.OST_Doors)
               .OfType<FamilySymbol>()
               .Where(x => x.Name.Equals("0915 x 2134 мм"))
               .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
               .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            Transaction tr = new Transaction(doc, "Построение двери");
            tr.Start();
            if (!doorType.IsActive) doorType.Activate();
            doc.Create.NewFamilyInstance(point, doorType, wall, level, StructuralType.NonStructural);
            tr.Commit();
        }
        public void AddWindow(Document doc, Level level, Wall wall, double Height)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
               .OfClass(typeof(FamilySymbol))
               .OfCategory(BuiltInCategory.OST_Windows)
               .OfType<FamilySymbol>()
               .Where(x => x.Name.Equals("0915 x 1830 мм"))
               .Where(x => x.FamilyName.Equals("Фиксированные"))
               .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            Transaction tr = new Transaction(doc, "Построение окна");
            tr.Start();
            if (!windowType.IsActive) windowType.Activate();
            FamilyInstance window = doc.Create.NewFamilyInstance(point, windowType, wall, level, StructuralType.NonStructural);
            window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(UnitUtils.ConvertToInternalUnits(Height, UnitTypeId.Millimeters));
            tr.Commit();
        }
        private void AddRoof(Document doc, Level level, List<Wall> wallsList, double Width, double Depth, double height)
        {
            RoofType roofType = new FilteredElementCollector(doc)
              .OfClass(typeof(RoofType))
              .OfType<RoofType>()
              .Where(x => x.Name.Equals("Типовой - 400мм"))
              .Where(x => x.FamilyName.Equals("Базовая крыша"))
              .FirstOrDefault();

            {
                /*double wallWidth = wallsList[0].Width;
                double dt = wallWidth / 2;
                List<XYZ> points = new List<XYZ>();
                points.Add(new XYZ(-dt, -dt, 0));
                points.Add(new XYZ(dt, -dt, 0));
                points.Add(new XYZ(dt, dt, 0));
                points.Add(new XYZ(-dt, dt, 0));
                points.Add(new XYZ(-dt, -dt, 0));

                Application application = doc.Application;
                CurveArray footprint = application.Create.NewCurveArray();
                for (int i = 0; i < 4; i++)
                {
                    LocationCurve curve = wallsList[i].Location as LocationCurve;
                    XYZ point1 = curve.Curve.GetEndPoint(0);
                    XYZ point2 = curve.Curve.GetEndPoint(1);
                    Line line = Line.CreateBound(point1 + points[i], point2 + points[i + 1]);
                    footprint.Append(line);
                }
                Transaction tr = new Transaction(doc, "Построение крыши");
                tr.Start();
                ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
                FootPrintRoof footPrintRoof = doc.Create.NewFootPrintRoof(footprint, level, roofType, out footPrintToModelCurveMapping);
                foreach (ModelCurve m in footPrintToModelCurveMapping)
                {
                    footPrintRoof.set_DefinesSlope(m, true);
                    footPrintRoof.set_SlopeAngle(m, 0.5);
                }
                tr.Commit();
                */
            }

            double width = UnitUtils.ConvertToInternalUnits(Width, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(Depth, UnitTypeId.Millimeters);

            double wallWidth = wallsList[0].Width;
            double dt = wallWidth / 2;

            double extrusionStart = -width / 2 - dt;
            double extrusionEnd = width / 2 + dt;

            double curveStart = -depth / 2 - dt;
            double curveEnd = depth / 2 + dt;

            Application application = doc.Application;
            CurveArray curveArray = application.Create.NewCurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(0, curveStart, level.Elevation), new XYZ(0, 0, level.Elevation + height)));
            curveArray.Append(Line.CreateBound(new XYZ(0, 0, level.Elevation + height), new XYZ(0, curveEnd, level.Elevation)));

            Transaction tr = new Transaction(doc, "Построение крыши");
            tr.Start();
            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            ExtrusionRoof extrusionRoof = doc.Create.NewExtrusionRoof(curveArray, plane, level, roofType, extrusionStart, extrusionEnd);
            extrusionRoof.EaveCuts = EaveCutterType.TwoCutSquare;
            tr.Commit();
        }
    }
}
