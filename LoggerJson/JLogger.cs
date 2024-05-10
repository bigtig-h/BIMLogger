#region Namespaces
using Autodesk.Internal.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Windows;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

#endregion


namespace LoggerJson
{

    [Transaction(TransactionMode.Manual)]


    public class JLogger : IExternalApplication
    {
        public int checkRunNum;
        public static JLogger thisApp;
        public string folderPath;
        public string JsonFile;
        public string currentProjectName;
        public string userID;
        public string projectId;
        // JSON FIELD
        public string beginLog = @"{ 'bimlog' : { } }";
        // jobject 이놈이 계속해서 쓰이는 JObject
        public JObject jobject = new JObject();
        // Log를 담을 jarray : 계속해서 로그 누적시킴.
        public JArray jarray = new JArray();

        #region Functions for log
        //Log file path designation
        private void SetLogPath( )
        {
            try
            {
                FileInfo fi = new FileInfo("C:\\ProgramData\\Autodesk\\Revit\\BIG_Log Directory.txt");
                if (fi.Exists) //if Folder Path 가 존재한다면
                {
                    string LogFilePath = "C:\\ProgramData\\Autodesk\\Revit";
                    string pathFile = Path.Combine(LogFilePath, "BIG_Log Directory.txt");
                    using (StreamReader readtext = new StreamReader(pathFile, true))
                    {
                        string readText = readtext.ReadLine();
                        Console.WriteLine(readText);
                        folderPath = readText;
                    }
                }
                else
                {
                    System.Windows.Forms.FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog();
                    folderBrowser.Description = "Select a folder to save Revit Modeling Log file.";
                    folderBrowser.ShowNewFolderButton = true;
                    // folderBrowser.SelectedPath = Folder_Path;
                    //folderBrowser.RootFolder = Environment.SpecialFolder.Personal;
                    //folderBrowser.SelectedPath = project_name.Properties.Settings.Default.Folder_Path;

                    if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        folderPath = folderBrowser.SelectedPath;
                        string LogFilePath = "C:\\ProgramData\\Autodesk\\Revit";
                        string pathFile = Path.Combine(LogFilePath, "BIG_Log Directory.txt");

                        using (StreamWriter writetext = new StreamWriter(pathFile, true))
                        {

                            writetext.WriteLine(folderPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("ADD-IN FAILED", ex.Message);
            }
            JsonFile = Path.Combine(folderPath, DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss") + "_ver2.json");
            Debug.WriteLine(JsonFile);
        }


        public Result OnStartup( UIControlledApplication application )
        {
            thisApp = this;
            try
            {
                if (string.IsNullOrEmpty(folderPath))
                {
                    SetLogPath();
                }

                ComponentManager.ItemExecuted += new EventHandler<RibbonItemExecutedEventArgs>(CommandExecuted);
                application.ControlledApplication.DocumentChanged += new EventHandler<DocumentChangedEventArgs>(DocumentChangeTracker);
                application.ControlledApplication.FailuresProcessing += new EventHandler<FailuresProcessingEventArgs>(FailureTracker);
                // 석호 작성
                application.ControlledApplication.DocumentOpened += new EventHandler<DocumentOpenedEventArgs>(DocumentOpenedTracker);
                var startLogging = new StartLog() { startTime = DateTime.Now.ToString() };
                // 석호 작성
                // JSON 
                using (var streamWriter = new StreamWriter(JsonFile, true))
                {
                    using (var writer = new JsonTextWriter(streamWriter))
                    {
                        JObject JStart = JObject.Parse(beginLog);
                        JObject startLog = (JObject)JStart["bimlog"];
                        startLog.Add("startTime", startLogging.startTime);
                        jobject = JStart;
                        var serializer = new JsonSerializer();
                        serializer.Formatting = Formatting.Indented;
                        serializer.Serialize(writer, jobject);
                    }
                }


            }

            catch (Exception)
            {
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        public Result OnShutdown( UIControlledApplication application )
        {
            try
            {
                ComponentManager.ItemExecuted -= CommandExecuted;
                application.ControlledApplication.DocumentChanged -= DocumentChangeTracker;
                application.ControlledApplication.FailuresProcessing -= FailureTracker;
                application.ControlledApplication.DocumentOpened -= DocumentOpenedTracker;

                var finishLogging = new FinishLog() { EndTime = DateTime.Now.ToString() };
                // 석호 작성
                // JSON
                // 24.3.18. 수정
                File.WriteAllText(JsonFile, String.Empty);
                using (var streamWriter = new StreamWriter(JsonFile, true))
                {
                    using (var writer = new JsonTextWriter(streamWriter))
                    {
                        JObject newObject = JObject.FromObject(finishLogging);
                        JObject bimLog = (JObject)jobject["bimlog"];
                        bimLog.Property("startTime").AddAfterSelf(new JProperty("endTime", finishLogging.EndTime));
                        var serializer = new JsonSerializer();
                        serializer.Formatting = Formatting.Indented;
                        serializer.Serialize(writer, jobject);
                        Debug.WriteLine("finish");
                    }
                }

            }
            catch (Exception)
            {
                return Result.Failed;
            }
            return Result.Succeeded;
        }
        // 석호 추가 부분
        // Doc 열렸을 때 동작하는 Tracker
        public void DocumentOpenedTracker( object sender, DocumentOpenedEventArgs e )
        {
            Document doc = e.Document;
            userID = doc.Application.Username;
            string filename = doc.PathName;

            // 프로젝트 GUID
            BasicFileInfo info = BasicFileInfo.Extract(filename);
            DocumentVersion v = info.GetDocumentVersion();
            projectId = v.VersionGUID.ToString();

            string filenameShort = Path.GetFileNameWithoutExtension(filename);
            bool newProjectName = false;

            if (filenameShort == null || filenameShort == string.Empty)
            {
                filenameShort = "New Project";
            };

            if (currentProjectName != filenameShort)
            {
                newProjectName = true;
                currentProjectName = filenameShort;
            }

            if (newProjectName == true)
            {
                // 24.3.18. 수정
                File.WriteAllText(JsonFile, String.Empty);
                using (var streamWriter = new StreamWriter(JsonFile, true))
                {
                    using (var writer = new JsonTextWriter(streamWriter))
                    {
                        var infoLogging = new InfoLog() { InfoString = "Project Information", ProjectName = filenameShort, UserName = userID };
                        JObject bimLog = (JObject)jobject["bimlog"];
                        bimLog.Property("startTime").AddAfterSelf(new JProperty("userName", infoLogging.UserName));
                        bimLog.Property("userName").AddAfterSelf(new JProperty("projectName", infoLogging.ProjectName));
                        bimLog.Property("projectName").AddAfterSelf(new JProperty("projectGUID", projectId));
                        bimLog.Property("projectGUID").AddAfterSelf(new JProperty("Log", new JArray()));
                        var serializer = new JsonSerializer();
                        serializer.Formatting = Formatting.Indented;
                        serializer.Serialize(writer, jobject);
                        Debug.WriteLine("opened");
                    }
                }
            }


        }

        public void DocumentChangeTracker( object sender, DocumentChangedEventArgs args )
        {
            var app = sender as Autodesk.Revit.ApplicationServices.Application;   //send를 받아서 app에 어플리케이션으로 저장
            UIApplication uiapp = new UIApplication(app); // app에 대해 uiapplication으로 생성
            UIDocument uidoc = uiapp.ActiveUIDocument; //uiapp의 활성화된 UI다큐먼트
            Document doc = uidoc.Document; // uidoc의 다큐먼트
            userID = doc.Application.Username;
            string filename = doc.PathName;
            string filenameShort = Path.GetFileNameWithoutExtension(filename);
            bool newProjectName = false;
            if (filenameShort == null || filenameShort == string.Empty)
            {
                filenameShort = "New Project";
            };

            if (currentProjectName != filenameShort)
            {
                newProjectName = true;
                currentProjectName = filenameShort;
            }

            Selection sel = uidoc.Selection;
            ICollection<ElementId> selectedIds = sel.GetElementIds();
            ICollection<ElementId> deletedElements = args.GetDeletedElementIds();
            ICollection<ElementId> modifiedElements = args.GetModifiedElementIds();
            ICollection<ElementId> addedElements = args.GetAddedElementIds();
            int counter = deletedElements.Count + modifiedElements.Count + addedElements.Count;


            // 석호 작성
            if (newProjectName == true)
            {
                var infoLogging = new InfoLog() { InfoString = "Project Information", ProjectName = filenameShort, UserName = userID };
                // 24.3.18. 질문: newProjectName이란게 왜 존재하는가
                // 24.3.22. 답변 -> 새로운 이벤트 핸들러 만들면 없애도 됨!!
                // doc open or doc save 등 여러 이벤트 분류해서 생성해야됨!!!
                // 중요함
            }

            if (modifiedElements.Count != 0)
            {
                string cmd = "MODIFIED";

                foreach (ElementId eid in modifiedElements)
                {
                    var elem = doc.GetElement(eid);
                    Debug.WriteLine(elem);
                    try
                    {
                        if (selectedIds.Contains(eid))
                        {
                            if (doc.GetElement(eid) == null || doc.GetElement(doc.GetElement(eid).GetTypeId()) == null)
                                continue;
                            dynamic logdata = Log(doc, cmd, eid);
                            //JSON
                            // 24.3.18. 수정
                            if (logdata != null)
                            {
                                File.WriteAllText(JsonFile, String.Empty);
                                using (var streamWriter = new StreamWriter(JsonFile, true))
                                {
                                    using (var writer = new JsonTextWriter(streamWriter))
                                    {
                                        JObject o = (JObject)JToken.FromObject(logdata);
                                        jarray.Add(o);
                                        JArray JLog = (JArray)jobject["bimlog"]["Log"];
                                        JLog.Merge(jarray, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union });
                                        var json = new JObject();
                                        var serializer = new JsonSerializer();
                                        serializer.Formatting = Formatting.Indented;
                                        serializer.Serialize(writer, jobject);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            if (addedElements.Count != 0)
            {
                string cmd = "ADDED";

                foreach (ElementId eid in addedElements)
                {
                    var elem = doc.GetElement(eid);
                    //Debug.Print(doc.GetElement(eid).Category.ToString());
                    Debug.WriteLine(elem);
                    try
                    {
                        if (doc.GetElement(eid) == null || doc.GetElement(doc.GetElement(eid).GetTypeId()) == null)
                            continue;
                        dynamic logdata = Log(doc, cmd, eid);
                        //JSON
                        // 24.3.18. 수정
                        if (logdata != null)
                        {
                            File.WriteAllText(JsonFile, String.Empty);
                            using (var streamWriter = new StreamWriter(JsonFile, true))
                            {
                                using (var writer = new JsonTextWriter(streamWriter))
                                {
                                    JObject o = (JObject)JToken.FromObject(logdata);
                                    jarray.Add(o);
                                    JArray JLog = (JArray)jobject["bimlog"]["Log"];
                                    JLog.Merge(jarray, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union });
                                    var json = new JObject();
                                    var serializer = new JsonSerializer();
                                    serializer.Formatting = Formatting.Indented;
                                    serializer.Serialize(writer, jobject);

                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            if (deletedElements.Count != 0)
            {

                string cmd = "DELETED";

                foreach (ElementId eid in deletedElements)
                {
                    // 24.3.31. 수정
                    // delete될 때 객체만 선택되도록!!

                    var delElement = doc.GetElement(eid);
                    var checkElem = selectedIds.Contains(eid);
                    Debug.WriteLine(doc.GetElement(eid));
                    try
                    {
                        dynamic logdata = Log(doc, cmd, eid);
                        //JSON
                        // 24.3.18. 수정
                        if (logdata != null)
                        {
                            File.WriteAllText(JsonFile, String.Empty);
                            using (var streamWriter = new StreamWriter(JsonFile, true))
                            {
                                using (var writer = new JsonTextWriter(streamWriter))
                                {
                                    JObject data = (JObject)JToken.FromObject(logdata);
                                    jarray.Add(data);
                                    JArray JLog = (JArray)jobject["bimlog"]["Log"];
                                    JLog.Merge(jarray, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union });
                                    var json = new JObject();
                                    var serializer = new JsonSerializer();
                                    serializer.Formatting = Formatting.Indented;
                                    serializer.Serialize(writer, jobject);
                                }
                            }
                        }

                    }
                    catch { }
                }
            }


        }

        private void CommandExecuted( object sender, RibbonItemExecutedEventArgs args )
        {
            try
            {
                Autodesk.Windows.RibbonItem it = args.Item;
                if (args != null)
                {
                    // 수정
                    CommandLog commandLog = new CommandLog() { Timestamp = DateTime.Now.ToString(), ElementId = userID, CommandType = "COMMAND", ElementCategory = it.ToString(), CommandId = it.Id, CommandCookie = it.Cookie };
                    //**JSON 작성 필요
                    //
                    //
                    //
                }
            }

            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("ADD-IN FAILED", ex.Message);
            }
        }
        private void FailureTracker( object sender, FailuresProcessingEventArgs e )
        {
            var app = sender as Autodesk.Revit.ApplicationServices.Application;
            UIApplication uiapp = new UIApplication(app);

            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc != null)
            {
                Document doc = uidoc.Document;


                string user = doc.Application.Username;
                string filename = doc.PathName;
                string filenameShort = Path.GetFileNameWithoutExtension(filename);

                FailuresAccessor failuresAccessor = e.GetFailuresAccessor();
                IList<FailureMessageAccessor> fmas = failuresAccessor.GetFailureMessages();


                if (fmas.Count != 0)
                {
                    foreach (FailureMessageAccessor fma in fmas)
                    {
                        // 수정
                        FailureLog failureLog = new FailureLog() { Timestamp = DateTime.Now.ToString(), ElementId = user, CommandType = "FAILURE", ElementCategory = failuresAccessor.GetTransactionName(), FailureMessage = failuresAccessor.GetFailureMessages().ToString() };
                        //**JSON 작성 필요
                        //
                        //
                        //
                    }
                }

            }

        }
        #endregion
        #region Function
        public void getParameter( Parameter p, JObject addJson )
        {
            Parameter param = p;
            JObject builtin = (JObject)addJson["Parameter"]["Built-In"];
            JObject custom = (JObject)addJson["Parameter"]["Custom"];
            var pName = param.Definition.Name;
            var pDef = param.Definition as InternalDefinition;
            var pDefName = pDef.BuiltInParameter;
            bool checkNull = p.HasValue;
            var pAsValueString = p.AsValueString();
            var storageType = param.StorageType;
            string pStorageType = "";
            if (checkNull == true)
            {
                if (storageType == StorageType.String)
                {
                    pStorageType = "String";
                    var pValue = param.AsString();
                    Debug.WriteLine($"{pName} as definition: {pDefName} : {pAsValueString}, storageType: {pStorageType}, Value: {pValue}");
                    if (pDefName != BuiltInParameter.INVALID)
                    {
                        // Built-in Parameter
                        JObject parameters = new JObject(
                            new JProperty("StorageType", pStorageType),
                            new JProperty("Value", pValue),
                            new JProperty("ValueString", pAsValueString)
                        );
                        builtin.Add($"{pDefName}", parameters);
                    }
                    else
                    {
                        // Custom parameter
                        JObject parameters = new JObject(
                            new JProperty("StorageType", pStorageType),
                            new JProperty("Value", pValue),
                            new JProperty("ValueString", pAsValueString)
                        );
                        custom.Add($"{pName}", parameters);
                    }
                }
                else if (storageType == StorageType.Double)
                {
                    pStorageType = "Double";
                    var pValue = param.AsDouble();
                    Debug.WriteLine($"{pName} as definition: {pDefName} : {pAsValueString}, storageType: {pStorageType}, Value: {pValue}");
                    if (pDefName != BuiltInParameter.INVALID)
                    {
                        // Built-in Parameter
                        JObject parameters = new JObject(
                            new JProperty("StorageType", pStorageType),
                            new JProperty("Value", pValue),
                            new JProperty("ValueString", pAsValueString)
                        );
                        builtin.Add($"{pDefName}", parameters);
                    }
                    else
                    {
                        // Custom parameter
                        JObject parameters = new JObject(
                            new JProperty("StorageType", pStorageType),
                            new JProperty("Value", pValue),
                            new JProperty("ValueString", pAsValueString)
                        );
                        custom.Add($"{pName}", parameters);
                    }
                }
                else if (storageType == StorageType.Integer)
                {
                    pStorageType = "Integer";
                    var pValue = param.AsInteger();
                    Debug.WriteLine($"{pName} as definition: {pDefName} : {pAsValueString}, storageType: {pStorageType}, Value: {pValue}");
                    if (pDefName != BuiltInParameter.INVALID)
                    {
                        // Built-in Parameter
                        JObject parameters = new JObject(
                            new JProperty("StorageType", pStorageType),
                            new JProperty("Value", pValue),
                            new JProperty("ValueString", pAsValueString)
                        );
                        builtin.Add($"{pDefName}", parameters);
                    }
                    else
                    {
                        // Custom parameter
                        JObject parameters = new JObject(
                            new JProperty("StorageType", pStorageType),
                            new JProperty("Value", pValue),
                            new JProperty("ValueString", pAsValueString)
                        );
                        custom.Add($"{pName}", parameters);
                    }
                }
                else if (storageType == StorageType.ElementId)
                {
                    pStorageType = "ElementId";
                    var pValue = param.AsElementId().Value;
                    Debug.WriteLine($"{pName} as definition: {pDefName} : {pAsValueString}, storageType: {pStorageType}, Value: {pValue}");
                    if (pDefName != BuiltInParameter.INVALID)
                    {
                        // Built-in Parameter
                        JObject parameters = new JObject(
                            new JProperty("StorageType", pStorageType),
                            new JProperty("Value", pValue),
                            new JProperty("ValueString", pAsValueString)
                        );
                        builtin.Add($"{pDefName}", parameters);
                    }
                    else
                    {
                        // Custom parameter
                        JObject parameters = new JObject(
                            new JProperty("StorageType", pStorageType),
                            new JProperty("Value", pValue),
                            new JProperty("ValueString", pAsValueString)
                        );
                        custom.Add($"{pName}", parameters);
                    }
                }
                else if (storageType == StorageType.None)
                {
                    pStorageType = "None";
                    var pValue = param.AsString();
                    Debug.WriteLine($"{pName} as definition: {pDefName} : {pAsValueString}, storageType: {pStorageType}, Value: {pValue}");
                    if (pDefName != BuiltInParameter.INVALID)
                    {
                        // Built-in Parameter
                        JObject parameters = new JObject(
                            new JProperty("StorageType", pStorageType),
                            new JProperty("Value", pValue),
                            new JProperty("ValueString", pAsValueString)
                        );
                        builtin.Add($"{pDefName}", parameters);
                    }
                    else
                    {
                        // Custom parameter
                        JObject parameters = new JObject(
                            new JProperty("StorageType", pStorageType),
                            new JProperty("Value", pValue),
                            new JProperty("ValueString", pAsValueString)
                        );
                        custom.Add($"{pName}", parameters);
                    }
                }

            }


        }
        #endregion
        #region Mapping Class
        //==================================================Dynamic Logger
        public dynamic Log( Document doc, String cmd, ElementId eid )
        {
            // 수정사항
            // 1. Class -> JObject로 수정 및 JObject 구조`화
            // 2. Modify에서 read해서 ElementId 겹치는 로그 찾아서 수정된 부분만 기록되도록!!
            // 3. doc에서 elementId로 list 받아와서 반복문 돌면서 구조화하기..
            // 24.3.18. 수정

            JObject addJarray = new JObject(
                new JProperty("Common", new JObject()),
                new JProperty("Geometry", new JObject()),
                new JProperty("Parameter", new JObject(
                    new JProperty("Built-In", new JObject()),
                    new JProperty("Custom", new JObject()))),
                new JProperty("Property", new JObject())
                );
            var timestamp = DateTime.Now.ToString();
            var id = eid.ToString();
            var elem = doc.GetElement(eid);

            if (cmd == "DELETED")
            {
                //var log = new GeneralLog();
                //log.Timestamp = timestamp;
                //log.ElementId = id;
                //log.CommandType = cmd;
                //Common
                JObject deleteCommon = (JObject)addJarray["Common"];
                deleteCommon.Add("CommandType", cmd);
                deleteCommon.Add("Timestamp", timestamp);
                deleteCommon.Add("ElementId", id);
                return addJarray;

            }
            else
            {
                if (elem.Category != null)
                {
                    var cat = elem.get_Parameter(BuiltInParameter.ELEM_CATEGORY_PARAM).AsValueString();
                    var fam = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    var typ = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();
                    //var roomIds = GetRooms(elem);
                    var worksetId = elem.WorksetId.ToString();

                    switch (cat)
                    {
                        case "Walls":

                            var wall = elem as Wall;
                            var norm = wall.Orientation.ToString();
                            var isStr = wall.get_Parameter(BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT).AsInteger();
                            bool wallIsProfileWall = true;
                            var wallCurve = new JObject();
                            List<Autodesk.Revit.DB.Curve> curves = new List<Autodesk.Revit.DB.Curve>();

                            // 24.3.22. 수정
                            // common
                            JObject wallCommon = (JObject)addJarray["Common"];
                            wallCommon.Add("Timestamp", timestamp);
                            wallCommon.Add("ElementId", id);
                            wallCommon.Add("CommandType", cmd);
                            wallCommon.Add("ElementCategory", cat);
                            wallCommon.Add("ElementFamily", fam);
                            wallCommon.Add("ElementType", typ);

                            //24.5.1. 
                            // Parameter 부분
                            var wallParameters = wall.Parameters;
                            foreach (Parameter p in wallParameters)
                            {
                                getParameter(p, addJarray);
                            }

                            // geometry
                            if (doc.GetElement(wall.SketchId) == null)
                            {
                                wallIsProfileWall = false;
                            }

                            if (wallIsProfileWall == true)
                            {
                                Sketch wallSketch = doc.GetElement(wall.SketchId) as Sketch;
                                //wallCurve = GetProfileDescription(wallSketch);
                            }
                            else
                            {
                                Curve wallLocCrv = (wall.Location as LocationCurve).Curve;
                                wallCurve = GetCurveDescription(wallLocCrv);
                            }

                            var wallFlipped = wall.Flipped;
                            //var wallWidth = wall.Width;
                            // 24.3.24. 수정 wallWidth -> asvaluestring 으로
                            var wallWidth = doc.GetElement(wall.GetTypeId()).GetParameters("Width").First().AsValueString();
                            //24.3.19. 수정
                            // geometry
                            JObject wallGeometry = (JObject)addJarray["Geometry"];
                            wallGeometry.Add("IsProfileWall", wallIsProfileWall);
                            wallGeometry.Add("Curve", wallCurve);
                            // property
                            JObject wallProperty = (JObject)addJarray["Property"];
                            wallProperty.Add("Flipped", wallFlipped);
                            wallProperty.Add("Width", wallWidth);

                            return addJarray;

                        case "Floors":
                            // 24.3.29. 수정
                            var floor = elem as Floor;
                            var floorSketch = (doc.GetElement(floor.SketchId) as Sketch);
                            var profileEmpty = floorSketch.Profile.IsEmpty;
                            var floorEIds = floorSketch.GetAllElements();
                            var floorParameter = floorSketch.Parameters;
                            string floorSlopeArrow = null;
                            double floorSlope = 0;
                            string floorSpanDirection = null;

                            foreach (ElementId floorEId in floorEIds)
                            // 여기 안들어옴.
                            {
                                if (doc.GetElement(floorEId).Name == "Slope Arrow")
                                {
                                    var Floorcrv = (doc.GetElement(floorEId) as CurveElement).GeometryCurve;
                                    floorSlopeArrow = GetCurveDescription(Floorcrv).ToString();

                                    List<Parameter> floorParams = (doc.GetElement(floorEId) as ModelLine).GetOrderedParameters().ToList();

                                    foreach (var param in floorParams)
                                    {
                                        if (param.Definition.Name == "Slope")
                                        {
                                            floorSlope = param.AsDouble();
                                        }
                                    }
                                }
                                if (doc.GetElement(floorEId).Name == "Span Direction Edges")
                                {
                                    var Floorcrv = (doc.GetElement(floorEId) as CurveElement).GeometryCurve;
                                    floorSpanDirection = GetCurveDescription(Floorcrv).ToString();
                                }
                            }

                            if (floorSlopeArrow == null)
                            {
                                floorSlopeArrow = "None";
                            }

                            //여기 안됨
                            var floorProfile = GetProfileDescription(floorSketch);


                            var floorLevel = floor.get_Parameter(BuiltInParameter.LEVEL_PARAM).AsValueString();
                            var floorHeightOffsetFromLevel = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).AsDouble();
                            var floorRoomBounding = floor.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).AsInteger();
                            var floorRelatedtoMass = floor.get_Parameter(BuiltInParameter.RELATED_TO_MASS).AsInteger();
                            var floorStructural = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL).AsInteger();
                            var floorEnableAnalyticalModel = floor.get_Parameter(BuiltInParameter.STRUCTURAL_ANALYTICAL_MODEL).AsInteger();

                            string floorRebarCover_TopFace;
                            if (floor.get_Parameter(BuiltInParameter.CLEAR_COVER_TOP) != null)
                            {
                                floorRebarCover_TopFace = floor.get_Parameter(BuiltInParameter.CLEAR_COVER_TOP).AsValueString();
                            }
                            else
                            {
                                floorRebarCover_TopFace = null;
                            }

                            string floorRebarCover_BottomFace;
                            if (floor.get_Parameter(BuiltInParameter.CLEAR_COVER_BOTTOM) != null)
                            {
                                floorRebarCover_BottomFace = floor.get_Parameter(BuiltInParameter.CLEAR_COVER_BOTTOM).AsValueString();
                            }
                            else
                            {
                                floorRebarCover_BottomFace = null;
                            }

                            string floorRebarCover_OtherFace;
                            if (floor.get_Parameter(BuiltInParameter.CLEAR_COVER_OTHER) != null)
                            {
                                floorRebarCover_OtherFace = floor.get_Parameter(BuiltInParameter.CLEAR_COVER_OTHER).AsValueString();
                            }
                            else
                            {
                                floorRebarCover_OtherFace = null;
                            }

                            var floorPerimeter = floor.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED).AsDouble();
                            var floorArea = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
                            var floorVolume = floor.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                            var floorElevationatTop = floor.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP).AsDouble();
                            var floorElevationatBottom = floor.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM).AsDouble();
                            var floorThickness = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble();
                            var floorImage = floor.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                            var floorComments = floor.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                            var floorMark = floor.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                            var floorPhaseCreated = floor.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                            var floorPhaseDemolished = floor.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();

                            var floorLog = new FloorLog()
                            {
                                Profile = floorProfile,
                                SlopeArrow = floorSlopeArrow,
                                SpanDirection = floorSpanDirection,
                                Level = floorLevel,
                                HeightOffsetFromLevel = floorHeightOffsetFromLevel,
                                RoomBounding = floorRoomBounding,
                                RelatedtoMass = floorRelatedtoMass,
                                Structural = floorStructural,
                                RebarCover_TopFace = floorRebarCover_TopFace,
                                RebarCover_BottomFace = floorRebarCover_BottomFace,
                                RebarCover_OtherFace = floorRebarCover_OtherFace,
                                Slope = floorSlope,
                                Perimeter = floorPerimeter,
                                Area = floorArea,
                                Volume = floorVolume,
                                ElevationatTop = floorElevationatTop,
                                ElevationatBottom = floorElevationatBottom,
                                Thickness = floorThickness,
                                Image = floorImage,
                                Comments = floorComments,
                                Mark = floorMark,
                                PhaseCreated = floorPhaseCreated,
                                PhaseDemolished = floorPhaseDemolished,
                                Timestamp = timestamp,
                                ElementId = id,
                                CommandType = cmd,
                                ElementCategory = cat,
                                ElementFamily = fam,
                                ElementType = typ,
                                WorksetId = worksetId
                            };


                            return floorLog;

                        case "Roofs":

                            if (elem.GetType().Name == "FootPrintRoof")
                            {
                                cat = "Roofs: FootPrintRoof";
                                var footprintroof = elem as FootPrintRoof;

                                ElementClassFilter roofSktFilter = new ElementClassFilter(typeof(Sketch));
                                Sketch footPrintRoofSketch = doc.GetElement(footprintroof.GetDependentElements(roofSktFilter).ToList()[0]) as Sketch;
                                var footprintroofFootPrint = GetProfileDescription(footPrintRoofSketch);

                                var footprintroofBaseLevel = footprintroof.get_Parameter(BuiltInParameter.ROOF_BASE_LEVEL_PARAM).AsValueString();
                                var footprintroofRoomBounding = footprintroof.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).AsInteger();
                                var footprintroofRelatedtoMass = footprintroof.get_Parameter(BuiltInParameter.RELATED_TO_MASS).AsInteger();
                                var footprintroofBaseOffsetFromLevel = footprintroof.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM).AsDouble();
                                var footprintroofCutoffLevel = footprintroof.get_Parameter(BuiltInParameter.ROOF_UPTO_LEVEL_PARAM).AsValueString();
                                var footprintroofCutoffOffset = footprintroof.get_Parameter(BuiltInParameter.ROOF_UPTO_LEVEL_OFFSET_PARAM).AsDouble();
                                var footprintroofRafterCut = footprintroof.get_Parameter(BuiltInParameter.ROOF_EAVE_CUT_PARAM).AsInteger();
                                var footprintroofFasciaDepth = footprintroof.get_Parameter(BuiltInParameter.FASCIA_DEPTH_PARAM).AsDouble();
                                var footprintroofMaximumRidgeHeght = footprintroof.get_Parameter(BuiltInParameter.ACTUAL_MAX_RIDGE_HEIGHT_PARAM).AsDouble();
                                var footprintroofSlope = footprintroof.get_Parameter(BuiltInParameter.ROOF_SLOPE).AsDouble();
                                var footprintroofThickness = footprintroof.get_Parameter(BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM).AsDouble();
                                var footprintroofVolume = footprintroof.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                                var footprintroofArea = footprintroof.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
                                var footprintroofImage = footprintroof.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                                var footprintroofComments = footprintroof.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                                var footprintroofMark = footprintroof.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                                var footprintroofPhaseCreated = footprintroof.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                                var footprintroofPhaseDemolished = footprintroof.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();

                                var footprintroofLog = new FootPrintRoofLog()
                                {
                                    FootPrint = footprintroofFootPrint,
                                    BaseLevel = footprintroofBaseLevel,
                                    RoomBounding = footprintroofRoomBounding,
                                    RelatedtoMass = footprintroofRelatedtoMass,
                                    BaseOffsetFromLevel = footprintroofBaseOffsetFromLevel,
                                    CutoffLevel = footprintroofCutoffLevel,
                                    CutoffOffset = footprintroofCutoffOffset,
                                    RafterCut = footprintroofRafterCut,
                                    FasciaDepth = footprintroofFasciaDepth,
                                    MaximumRidgeHeght = footprintroofMaximumRidgeHeght,
                                    Slope = footprintroofSlope,
                                    Thickness = footprintroofThickness,
                                    Volume = footprintroofVolume,
                                    Area = footprintroofArea,
                                    Image = footprintroofImage,
                                    Comments = footprintroofComments,
                                    Mark = footprintroofMark,
                                    PhaseCreated = footprintroofPhaseCreated,
                                    PhaseDemolished = footprintroofPhaseDemolished,
                                    Timestamp = timestamp,
                                    ElementId = id,
                                    CommandType = cmd,
                                    ElementCategory = cat,
                                    ElementFamily = fam,
                                    ElementType = typ,
                                    WorksetId = worksetId
                                };


                                return footprintroofLog;
                            }

                            else if (elem.GetType().Name == "ExtrusionRoof")
                            {
                                cat = "Roofs: ExtrusionRoof";
                                var extrusionroof = elem as ExtrusionRoof;

                                var extrusionroofCrvLoop = new CurveLoop();
                                var extrusionroofProfileCurves = extrusionroof.GetProfile();
                                foreach (ModelCurve curve in extrusionroofProfileCurves)
                                {
                                    extrusionroofCrvLoop.Append(curve.GeometryCurve);
                                }

                                var extrusionroofWorkPlane = GetPlaneDescription(extrusionroofCrvLoop.GetPlane());
                                var extrusionroofProfile = GetCurveLoopDescription(extrusionroofCrvLoop);
                                var extrusionroofRoomBounding = extrusionroof.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).AsInteger();
                                var extrusionroofRelatedtoMass = extrusionroof.get_Parameter(BuiltInParameter.RELATED_TO_MASS).AsInteger();
                                var extrusionroofExtrusionStart = extrusionroof.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).AsDouble();
                                var extrusionroofExtrusionEnd = extrusionroof.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM).AsDouble();
                                var extrusionroofReferenceLevel = extrusionroof.get_Parameter(BuiltInParameter.ROOF_CONSTRAINT_LEVEL_PARAM).AsValueString();
                                var extrusionroofLevelOffset = extrusionroof.get_Parameter(BuiltInParameter.ROOF_CONSTRAINT_OFFSET_PARAM).AsDouble();
                                var extrusionroofFasciaDepth = extrusionroof.get_Parameter(BuiltInParameter.FASCIA_DEPTH_PARAM).AsDouble();
                                var extrusionroofRafterCut = extrusionroof.get_Parameter(BuiltInParameter.ROOF_EAVE_CUT_PARAM).AsInteger();
                                var extrusionroofSlope = extrusionroof.get_Parameter(BuiltInParameter.ROOF_SLOPE).AsDouble();
                                var extrusionroofThickness = extrusionroof.get_Parameter(BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM).AsDouble();
                                var extrusionroofVolume = extrusionroof.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                                var extrusionroofArea = extrusionroof.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
                                var extrusionroofImage = extrusionroof.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                                var extrusionroofComments = extrusionroof.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                                var extrusionroofMark = extrusionroof.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                                var extrusionroofPhaseCreated = extrusionroof.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                                var extrusionroofPhaseDemolished = extrusionroof.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();

                                var extrusionroofLog = new ExtrusionRoofLog()
                                {
                                    Profile = extrusionroofProfile,
                                    WorkPlane = extrusionroofWorkPlane,
                                    RoomBounding = extrusionroofRoomBounding,
                                    RelatedtoMass = extrusionroofRelatedtoMass,
                                    ExtrusionStart = extrusionroofExtrusionStart,
                                    ExtrusionEnd = extrusionroofExtrusionEnd,
                                    ReferenceLevel = extrusionroofReferenceLevel,
                                    LevelOffset = extrusionroofLevelOffset,
                                    FasciaDepth = extrusionroofFasciaDepth,
                                    RafterCut = extrusionroofRafterCut,
                                    Slope = extrusionroofSlope,
                                    Thickness = extrusionroofThickness,
                                    Volume = extrusionroofVolume,
                                    Area = extrusionroofArea,
                                    Image = extrusionroofImage,
                                    Comments = extrusionroofComments,
                                    Mark = extrusionroofMark,
                                    PhaseCreated = extrusionroofPhaseCreated,
                                    PhaseDemolished = extrusionroofPhaseDemolished,
                                    Timestamp = timestamp,
                                    ElementId = id,
                                    CommandType = cmd,
                                    ElementCategory = cat,
                                    ElementFamily = fam,
                                    ElementType = typ,
                                    WorksetId = worksetId
                                };

                                return extrusionroofLog;
                            }
                            else
                            {
                                return null;
                            }

                        case "Ceilings":
                            // 24.4.2. 수정
                            var ceiling = elem as Ceiling;
                            // common
                            JObject ceilingCommon = (JObject)addJarray["Common"];
                            ceilingCommon.Add("Timestamp", timestamp);
                            ceilingCommon.Add("ElementId", id);
                            ceilingCommon.Add("CommandType", cmd);
                            ceilingCommon.Add("ElementCategory", cat);
                            ceilingCommon.Add("ElementFamily", fam);
                            ceilingCommon.Add("ElementType", typ);

                            var ceilingCurveLoops = GetProfileDescription((doc.GetElement(ceiling.SketchId) as Sketch));
                            // 24.4.2. 수정
                            // 24.4.3. 추가작업
                            //Sketch ceilingSketch = (doc.GetElement((ceiling.SketchId)) as Sketch);
                            //IList<ElementId> lineList = ceilingSketch.GetAllElements();
                            //foreach (ElementId lineId in lineList)
                            //{
                            //    ModelLine line = (doc.GetElement(lineId)) as ModelLine;
                            //    Debug.WriteLine(line);
                            //    IList<XYZ> XYZList = line.GeometryCurve.Tessellate();
                            //    foreach (XYZ point in XYZList)
                            //    {
                            //        Debug.WriteLine(point.ToString());
                            //    }
                            //}

                            //
                            var ceilingEIds = (doc.GetElement(ceiling.SketchId) as Sketch).GetAllElements();
                            string ceilingSlopeArrow = null;
                            double ceilingSlope = 0;

                            foreach (ElementId ceilingEId in ceilingEIds)
                            {
                                if (doc.GetElement(ceilingEId).Name == "Slope Arrow")
                                {
                                    var ceilingCrv = (doc.GetElement(ceilingEId) as CurveElement).GeometryCurve;
                                    List<Parameter> slopeParams = (doc.GetElement(ceilingEId) as ModelLine).GetOrderedParameters().ToList();

                                    foreach (var param in slopeParams)
                                    {
                                        if (param.Definition.Name == "Slope")
                                        {
                                            ceilingSlope = param.AsDouble();
                                        }
                                    }

                                    ceilingSlopeArrow = GetCurveDescription(ceilingCrv).ToString();
                                };
                            }


                            if (ceilingSlopeArrow == null)
                            {
                                ceilingSlopeArrow = "None";
                            }
                            // parameter
                            var ceilingParameters = ceiling.Parameters;
                            foreach (Parameter p in ceilingParameters)
                            {
                                getParameter(p, addJarray);
                            }
                            var ceilingThickness = doc.GetElement(ceiling.GetTypeId()).get_Parameter(BuiltInParameter.CEILING_THICKNESS).AsDouble();


                            // geometry
                            JObject ceilingGeometry = (JObject)addJarray["Geometry"];
                            ceilingGeometry.Add("CurveLoops", ceilingCurveLoops);
                            ceilingGeometry.Add("SlopeArrow", ceilingSlopeArrow);
                            // property
                            JObject ceilingProperty = (JObject)addJarray["Property"];
                            ceilingProperty.Add("Thickness", ceilingThickness);


                            return addJarray;


                        case "Levels":
                            // 24.3.29. 수정 , 문제
                            // common
                            JObject levelCommon = (JObject)addJarray["Common"];
                            levelCommon.Add("Timestamp", timestamp);
                            levelCommon.Add("ElementId", id);
                            levelCommon.Add("CommandType", cmd);
                            levelCommon.Add("ElementCategory", cat);
                            levelCommon.Add("ElementFamily", fam);
                            levelCommon.Add("ElementType", typ);
                            var level = elem as Level;


                            // property

                            // parameter
                            var levelParameters = level.Parameters;
                            foreach (Parameter p in levelParameters)
                            {
                                getParameter(p, addJarray);
                            }

                            var levelElevation = level.get_Parameter(BuiltInParameter.LEVEL_ELEV).AsDouble();
                            // geometry
                            JObject levelGeometry = (JObject)addJarray["Geometry"];
                            levelGeometry.Add("Elevation", levelElevation);

                            return addJarray;

                        case "Grids":

                            var grid = elem as Grid;
                            var gridCurve = GetCurveDescription(grid.Curve);
                            var gridScopeBox = grid.get_Parameter(BuiltInParameter.DATUM_VOLUME_OF_INTEREST).AsValueString();
                            var gridName = grid.get_Parameter(BuiltInParameter.DATUM_TEXT).AsString();

                            var gridLog = new GridLog()
                            {
                                Curve = gridCurve,
                                ScopeBox = gridScopeBox,
                                Name = gridName,
                                Timestamp = timestamp,
                                ElementId = id,
                                CommandType = cmd,
                                ElementCategory = cat,
                                ElementFamily = fam,
                                ElementType = typ,
                                WorksetId = worksetId
                            };


                            return gridLog;

                        case "Stairs":

                            var stair = elem as Stairs;
                            var stairBaseLevel = stair.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM).AsValueString();
                            var stairBaseOffset = stair.get_Parameter(BuiltInParameter.STAIRS_BASE_OFFSET).AsDouble();
                            var stairTopLevel = stair.get_Parameter(BuiltInParameter.STAIRS_TOP_LEVEL_PARAM).AsValueString();
                            var stairTopOffset = stair.get_Parameter(BuiltInParameter.STAIRS_TOP_OFFSET).AsDouble();
                            var stairDesiredStairHeight = stair.get_Parameter(BuiltInParameter.STAIRS_STAIRS_HEIGHT).AsDouble();
                            var stairDesiredNumberofRisers = stair.get_Parameter(BuiltInParameter.STAIRS_DESIRED_NUMBER_OF_RISERS).AsInteger();
                            var stairActualNumberofRisers = stair.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_NUM_RISERS).AsInteger();
                            var stairActualRiserHeight = stair.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_RISER_HEIGHT).AsDouble();
                            var stairActualTreadDepth = stair.get_Parameter(BuiltInParameter.STAIRS_ACTUAL_TREAD_DEPTH).AsDouble();
                            var stairTreadRiserStartNumber = stair.get_Parameter(BuiltInParameter.STAIRS_TRISER_NUMBER_BASE_INDEX).AsInteger();
                            var stairImage = stair.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                            var stairComments = stair.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                            var stairMark = stair.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                            var stairPhaseCreated = stair.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                            var stairPhaseDemolished = stair.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();


                            var stairLog = new StairLog()
                            {
                                BaseLevel = stairBaseLevel,
                                BaseOffset = stairBaseOffset,
                                TopLevel = stairTopLevel,
                                TopOffset = stairTopOffset,
                                DesiredStairHeight = stairDesiredStairHeight,
                                DesiredNumberofRisers = stairDesiredNumberofRisers,
                                ActualNumberofRisers = stairActualNumberofRisers,
                                ActualRiserHeight = stairActualRiserHeight,
                                ActualTreadDepth = stairActualTreadDepth,
                                TreadRiserStartNumber = stairTreadRiserStartNumber,
                                Image = stairImage,
                                Comments = stairComments,
                                Mark = stairMark,
                                PhaseCreated = stairPhaseCreated,
                                PhaseDemolished = stairPhaseDemolished,
                                Timestamp = timestamp,
                                ElementId = id,
                                CommandType = cmd,
                                ElementCategory = cat,
                                ElementFamily = fam,
                                ElementType = typ,
                                WorksetId = worksetId
                            };


                            return stairLog;

                        case "Stairs: Runs":

                            var stairsruns = elem as StairsRun;
                            var stairsrunsStairsId = stairsruns.GetStairs().Id.ToString();
                            var stairsrunsLocationPath = GetCurveDescription(stairsruns.GetStairsPath().First()).ToString();

                            var stairsrunsLocationline = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_LOCATIONPATH_JUSTFICATION).AsInteger();
                            var stairsrunsRelativeBaseHeight = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_BOTTOM_ELEVATION).AsDouble();
                            var stairsrunsRelativeTopHeight = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_TOP_ELEVATION).AsDouble();


                            var stairsrunsRunHeight = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_HEIGHT).AsDouble();
                            var stairsrunsExtendBelowRiserBase = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_EXTEND_BELOW_RISER_BASE).AsDouble();
                            var stairsrunsBeginwithRiser = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_BEGIN_WITH_RISER).AsInteger();
                            var stairsrunsEndwithRiser = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_END_WITH_RISER).AsInteger();


                            var stairsrunsActualRunWidth = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RUN_WIDTH).AsDouble();
                            var stairsrunsActualRiserHeight = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_RISER_HEIGHT).AsDouble();
                            var stairsrunsActualTreadDepth = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_TREAD_DEPTH).AsDouble();
                            var stairsrunsActualNumberofRisers = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_NUMBER_OF_RISERS).AsInteger();
                            var stairsrunsActualNumberofTreads = stairsruns.get_Parameter(BuiltInParameter.STAIRS_RUN_ACTUAL_NUMBER_OF_TREADS).AsInteger();

                            var stairsrunsImage = stairsruns.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                            var stairsrunsComments = stairsruns.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                            var stairsrunsMark = stairsruns.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                            var stairsrunsPhaseCreated = stairsruns.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                            var stairsrunsPhaseDemolished = stairsruns.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();


                            var stairsrunsLog = new StairsRunsLog()
                            {
                                StairsId = stairsrunsStairsId,
                                LocationPath = stairsrunsLocationPath,
                                Locationline = stairsrunsLocationline,
                                RelativeBaseHeight = stairsrunsRelativeBaseHeight,
                                RelativeTopHeight = stairsrunsRelativeTopHeight,
                                RunHeight = stairsrunsRunHeight,
                                ExtendBelowRiserBase = stairsrunsExtendBelowRiserBase,
                                BeginwithRiser = stairsrunsBeginwithRiser,
                                EndwithRiser = stairsrunsEndwithRiser,
                                ActualRunWidth = stairsrunsActualRunWidth,
                                ActualRiserHeight = stairsrunsActualRiserHeight,
                                ActualTreadDepth = stairsrunsActualTreadDepth,
                                ActualNumberofRisers = stairsrunsActualNumberofRisers,
                                ActualNumberofTreads = stairsrunsActualNumberofTreads,
                                Image = stairsrunsImage,
                                Comments = stairsrunsComments,
                                Mark = stairsrunsMark,
                                PhaseCreated = stairsrunsPhaseCreated,
                                PhaseDemolished = stairsrunsPhaseDemolished,
                                Timestamp = timestamp,
                                ElementId = id,
                                CommandType = cmd,
                                ElementCategory = cat,
                                ElementFamily = fam,
                                ElementType = typ,
                                WorksetId = worksetId
                            };

                            return stairsrunsLog;

                        case "Stairs: Landings":

                            var stairslandings = elem as StairsLanding;
                            var stairslandingsStairsId = stairslandings.GetStairs().Id.ToString();
                            var stairslandingsCurveLoop = GetCurveLoopDescription(stairslandings.GetFootprintBoundary());
                            var stairslandingsRelativeHeight = stairslandings.get_Parameter(BuiltInParameter.STAIRS_LANDING_BASE_ELEVATION).AsDouble();
                            var stairslandingsTotalThickness = stairslandings.get_Parameter(BuiltInParameter.STAIRS_LANDING_BASE_ELEVATION).AsDouble();
                            var stairslandingsImage = stairslandings.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                            var stairslandingsComments = stairslandings.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                            var stairslandingsMark = stairslandings.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                            var stairslandingsPhaseCreated = stairslandings.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                            var stairslandingsPhaseDemolished = stairslandings.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();
                            var stairslandingsCategory = stairslandings.get_Parameter(BuiltInParameter.ELEM_CATEGORY_PARAM).AsValueString();
                            var stairslandingsType = stairslandings.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();
                            var stairslandingsTypeId = stairslandings.get_Parameter(BuiltInParameter.SYMBOL_ID_PARAM).AsValueString();
                            var stairslandingsTypeName = stairslandings.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME).AsString();
                            var stairslandingsFamily = stairslandings.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                            var stairslandingsFamilyName = stairslandings.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME).AsString();
                            var stairslandingsFamilyAndType = stairslandings.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).AsValueString();
                            var stairslandingsDesignOption = stairslandings.get_Parameter(BuiltInParameter.DESIGN_OPTION_ID).AsValueString();

                            var stairslandingsLog = new StairsLandingsLog()
                            {
                                StairsId = stairslandingsStairsId,
                                CurveLoop = stairslandingsCurveLoop,
                                RelativeHeight = stairslandingsRelativeHeight,
                                TotalThickness = stairslandingsTotalThickness,
                                Image = stairslandingsImage,
                                Comments = stairslandingsComments,
                                Mark = stairslandingsMark,
                                PhaseCreated = stairslandingsPhaseCreated,
                                PhaseDemolished = stairslandingsPhaseDemolished,
                                Category = stairslandingsCategory,
                                Type = stairslandingsType,
                                TypeId = stairslandingsTypeId,
                                TypeName = stairslandingsTypeName,
                                Family = stairslandingsFamily,
                                FamilyName = stairslandingsFamilyName,
                                FamilyAndType = stairslandingsFamilyAndType,
                                DesignOption = stairslandingsDesignOption,
                                Timestamp = timestamp,
                                ElementId = id,
                                CommandType = cmd,
                                ElementCategory = cat,
                                ElementFamily = fam,
                                ElementType = typ,
                                WorksetId = worksetId
                            };


                            return stairslandingsLog;

                        case "Railings":

                            var railing = elem as Railing;
                            ElementClassFilter filter = new ElementClassFilter(typeof(Sketch));
                            IList<ElementId> dependentRailIds = railing.GetDependentElements(filter);
                            var railingHostId = railing.HostId.ToString();

                            var railingCrvLoop = new CurveLoop();
                            var railingList = railing.GetPath().ToList();
                            Debug.Write(railingList);
                            foreach (Curve railCrv in railingList)
                            {
                                railingCrvLoop.Append(railCrv);
                            }
                            var railingCurveLoop = GetCurveLoopDescription(railingCrvLoop);

                            var railingFlipped = railing.Flipped;
                            var railingBaseLevel = railing.get_Parameter(BuiltInParameter.STAIRS_RAILING_BASE_LEVEL_PARAM).AsValueString();
                            var railingBaseOffset = railing.get_Parameter(BuiltInParameter.STAIRS_RAILING_HEIGHT_OFFSET).AsDouble();
                            var railingOffsetfromPath = railing.get_Parameter(BuiltInParameter.STAIRS_RAILING_PLACEMENT_OFFSET).AsDouble();
                            var railingLength = railing.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble();
                            var railingImage = railing.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                            var railingComments = railing.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                            var railingMark = railing.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                            var railingPhaseCreated = railing.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                            var railingPhaseDemolished = railing.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();

                            var railingLog = new RailingLog()
                            {
                                HostId = railingHostId,
                                CurveLoop = railingCurveLoop,
                                Flipped = railingFlipped,
                                BaseLevel = railingBaseLevel,
                                BaseOffset = railingBaseOffset,
                                OffsetfromPath = railingOffsetfromPath,
                                Length = railingLength,
                                Image = railingImage,
                                Comments = railingComments,
                                Mark = railingMark,
                                PhaseCreated = railingPhaseCreated,
                                PhaseDemolished = railingPhaseDemolished,
                                Timestamp = timestamp,
                                ElementId = id,
                                CommandType = cmd,
                                ElementCategory = cat,
                                ElementFamily = fam,
                                ElementType = typ,
                            };

                            return railingLog;

                        case "Windows":
                            // 24.3.24. 수정
                            JObject windowCommon = (JObject)addJarray["Common"];
                            JObject windowGeometry = (JObject)addJarray["Geometry"];
                            JObject windowProperty = (JObject)addJarray["Property"];
                            var window = elem as FamilyInstance;
                            //common
                            windowCommon.Add("Timestamp", timestamp);
                            windowCommon.Add("ElementId", id);
                            windowCommon.Add("CommandType", cmd);
                            windowCommon.Add("ElementCategory", cat);
                            windowCommon.Add("ElementFamily", fam);
                            windowCommon.Add("ElementType", typ);
                            // geometry
                            var windowHostId = window.Host.Id.ToString();
                            var windowLocation = GetXYZDescription((window.Location as LocationPoint).Point);
                            windowGeometry.Add("HostId", windowHostId);
                            windowGeometry.Add("Location", windowLocation);
                            // property
                            var windowFlipFacing = window.FacingFlipped;
                            var windowFlipHand = window.HandFlipped;
                            var windowHeight = doc.GetElement(window.GetTypeId()).get_Parameter(BuiltInParameter.DOOR_HEIGHT).AsValueString();
                            var windowWidth = doc.GetElement(window.GetTypeId()).get_Parameter(BuiltInParameter.FURNITURE_WIDTH).AsValueString();
                            windowProperty.Add("FlipFacing", windowFlipFacing);
                            windowProperty.Add("FlipHand", windowFlipHand);
                            windowProperty.Add("Height", windowHeight);
                            windowProperty.Add("Width", windowWidth);
                            //built-in
                            var windowParameters = window.Parameters;
                            foreach (Parameter p in windowParameters)
                            {
                                getParameter(p, addJarray);

                            }

                            return addJarray;

                        case "Doors":
                            // 24.4.3. 수정
                            // common
                            JObject doorCommon = (JObject)addJarray["Common"];
                            doorCommon.Add("Timestamp", timestamp);
                            doorCommon.Add("ElementId", id);
                            doorCommon.Add("CommandType", cmd);
                            doorCommon.Add("ElementCategory", cat);
                            doorCommon.Add("ElementFamily", fam);
                            doorCommon.Add("ElementType", typ);
                            var door = elem as FamilyInstance;
                            // geometry
                            JObject doorGeometry = (JObject)addJarray["Geometry"];
                            var doorHostId = door.Host.Id.ToString();
                            var doorLocation = GetXYZDescription((door.Location as LocationPoint).Point);
                            doorGeometry.Add("HostId", doorHostId);
                            doorGeometry.Add("Location", doorLocation);
                            // property
                            JObject doorProperty = (JObject)addJarray["Property"];
                            var doorFlipFacing = door.FacingFlipped;
                            var doorFlipHand = door.HandFlipped;
                            var doorHeight = doc.GetElement(door.GetTypeId()).get_Parameter(BuiltInParameter.DOOR_HEIGHT).AsValueString();
                            var doorWidth = doc.GetElement(door.GetTypeId()).get_Parameter(BuiltInParameter.DOOR_WIDTH).AsValueString();
                            doorProperty.Add("FlipFacing", doorFlipFacing);
                            doorProperty.Add("FlipHand", doorFlipHand);
                            doorProperty.Add("Height", doorHeight);
                            doorProperty.Add("Width", doorWidth);
                            // parameters
                            var doorParameters = door.Parameters;
                            foreach (Parameter p in doorParameters)
                            {
                                getParameter(p, addJarray);

                            }

                            return addJarray;

                        case "Furniture":

                            var furniture = elem as FamilyInstance;

                            var furnitureLocation = GetXYZDescription((furniture.Location as LocationPoint).Point);
                            var furnitureLevel = furniture.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM).AsValueString();
                            var furnitureElevationfromLevel = furniture.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).AsDouble();
                            var furnitureMovesWithNearbyElements = furniture.get_Parameter(BuiltInParameter.INSTANCE_OFFSET_POS_PARAM).AsInteger();
                            var furnitureImage = furniture.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                            var furnitureComments = furniture.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                            var furnitureMark = furniture.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                            var furniturePhaseCreated = furniture.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                            var furniturePhaseDemolished = furniture.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();

                            var furnitureLog = new FurnitureLog()
                            {
                                Location = furnitureLocation,
                                Level = furnitureLevel,
                                ElevationfromLevel = furnitureElevationfromLevel,
                                MovesWithNearbyElements = furnitureMovesWithNearbyElements,
                                Image = furnitureImage,
                                Comments = furnitureComments,
                                Mark = furnitureMark,
                                PhaseCreated = furniturePhaseCreated,
                                PhaseDemolished = furniturePhaseDemolished,
                                Timestamp = timestamp,
                                ElementId = id,
                                CommandType = cmd,
                                ElementCategory = cat,
                                ElementFamily = fam,
                                ElementType = typ,
                                WorksetId = worksetId
                            };


                            return furnitureLog;

                        case "Columns":
                            // 24.3.24. 수정
                            // common
                            JObject columnCommon = (JObject)addJarray["Common"];
                            columnCommon.Add("Timestamp", timestamp);
                            columnCommon.Add("ElementId", id);
                            columnCommon.Add("CommandType", cmd);
                            columnCommon.Add("ElementCategory", cat);
                            columnCommon.Add("ElementFamily", fam);
                            columnCommon.Add("ElementType", typ);

                            var column = elem as FamilyInstance;
                            // geometry
                            var columnLocation = GetXYZDescription((column.Location as LocationPoint).Point);
                            JObject columnGeometry = (JObject)addJarray["Geometry"];
                            columnGeometry.Add("Location", columnLocation);
                            // property
                            // 24.3.24. 추가작업
                            // 폭, 깊이 한글로 되어있음;;
                            // AsDouble 말고 AsValueString으로 받아옴
                            var columnWidth = doc.GetElement(column.GetTypeId()).GetParameters("폭").First().AsValueString();
                            var columnDepth = doc.GetElement(column.GetTypeId()).GetParameters("깊이").First().AsValueString();
                            var columnProperty = (JObject)addJarray["Property"];
                            columnProperty.Add("width", columnWidth);
                            columnProperty.Add("Depth", columnDepth);
                            // parameter 
                            var columnParameters = column.Parameters;
                            foreach (Parameter p in columnParameters)
                            {
                                getParameter(p, addJarray);

                            }

                            return addJarray;

                        case "Structural Columns":
                            // 24.4.5. 수정
                            JObject structuralColumnCommon = (JObject)addJarray["Common"];
                            structuralColumnCommon.Add("Timestamp", timestamp);
                            structuralColumnCommon.Add("ElementId", id);
                            structuralColumnCommon.Add("CommandType", cmd);
                            structuralColumnCommon.Add("ElementCategory", cat);
                            structuralColumnCommon.Add("ElementFamily", fam);
                            structuralColumnCommon.Add("ElementType", typ);

                            var structuralcolumn = elem as FamilyInstance;
                            bool structuralcolumnIsCurve = false;
                            string structuralcolumnLocation;
                            if (elem.Location as LocationCurve != null)
                            {
                                structuralcolumnIsCurve = true;
                            }

                            if (structuralcolumnIsCurve == true)
                            {
                                structuralcolumnLocation = GetCurveDescription((structuralcolumn.Location as LocationCurve).Curve).ToString();
                            }
                            else
                            {
                                structuralcolumnLocation = (structuralcolumn.Location as LocationPoint).Point.ToString();
                            }
                            // Geometry
                            JObject structuralColumnGeometry = (JObject)addJarray["Geometry"];
                            structuralColumnGeometry.Add("Location", structuralcolumnLocation);

                            // 24.4.3. 추가작업 얘네 없음...ㅅㅂ
                            //var structuralcolumnHeight = doc.GetElement(structuralcolumn.GetTypeId()).GetParameters("Height").First().AsDouble();
                            //var structuralcolumnWidth = doc.GetElement(structuralcolumn.GetTypeId()).GetParameters("Width").First().AsDouble();

                            // property
                            JObject structuralColumnProperty = (JObject)addJarray["Property"];
                            //structuralColumnProperty.Add("Height", structuralcolumnHeight);
                            //structuralColumnProperty.Add("Width", structuralcolumnWidth);

                            //parameter
                            var structuralColumnParameters = structuralcolumn.Parameters;
                            foreach (Parameter p in structuralColumnParameters)
                            {
                                getParameter(p, addJarray);
                            }

                            return addJarray;

                        case "Curtain Wall Mullions":

                            var curtainmullion = elem as Mullion;
                            var curtainmullionHostId = curtainmullion.Host.Id.ToString();
                            var curtainmullionCurve = GetCurveDescription(curtainmullion.LocationCurve).ToString();
                            var curtainmullionLocation = GetXYZDescription((curtainmullion.Location as LocationPoint).Point);
                            var curtainmullionLength = curtainmullion.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble();
                            var curtainmullionImage = curtainmullion.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                            var curtainmullionComments = curtainmullion.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                            var curtainmullionMark = curtainmullion.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                            var curtainmullionPhaseCreated = curtainmullion.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                            var curtainmullionPhaseDemolished = curtainmullion.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();

                            var curtainmullionLog = new CurtainMullionLog()
                            {
                                HostId = curtainmullionHostId,
                                Curve = curtainmullionCurve,
                                Location = curtainmullionLocation,
                                Length = curtainmullionLength,
                                Image = curtainmullionImage,
                                Comments = curtainmullionComments,
                                Mark = curtainmullionMark,
                                PhaseCreated = curtainmullionPhaseCreated,
                                PhaseDemolished = curtainmullionPhaseDemolished,
                                Timestamp = timestamp,
                                ElementId = id,
                                CommandType = cmd,
                                ElementCategory = cat,
                                ElementFamily = fam,
                                ElementType = typ,
                                WorksetId = worksetId
                            };


                            return curtainmullionLog;

                        case "Structural Framing":
                            // 24.4.5. 수정
                            JObject beamCommon = (JObject)addJarray["Common"];
                            beamCommon.Add("Timestamp", timestamp);
                            beamCommon.Add("ElementId", id);
                            beamCommon.Add("CommandType", cmd);
                            beamCommon.Add("ElementCategory", cat);
                            beamCommon.Add("ElementFamily", fam);
                            beamCommon.Add("ElementType", typ);

                            var structuralframing = elem as FamilyInstance;
                            // geometry
                            var structuralframingLocationCurve = GetCurveDescription((structuralframing.Location as LocationCurve).Curve).ToString();
                            JObject beamGeometry = (JObject)addJarray["Geometry"];
                            beamGeometry.Add("LocationCurve", structuralframingLocationCurve);
                            // property
                            var structuralframingHeight = doc.GetElement(structuralframing.Symbol.Id).GetParameters("Height").First().AsDouble();
                            var structuralframingWidth = doc.GetElement(structuralframing.Symbol.Id).GetParameters("Width").First().AsDouble();
                            JObject beamProperty = (JObject)addJarray["Property"];
                            beamProperty.Add("Height", structuralframingHeight);
                            beamProperty.Add("Width", structuralframingWidth);
                            // parameter
                            var beamParameters = structuralframing.Parameters;
                            foreach (Parameter p in beamParameters)
                            {
                                getParameter(p, addJarray);
                            }

                            return addJarray;


                        case "Walls: Wall Sweeps":


                            var wallsweep = elem as WallSweep;

                            var wallsweepWallId = GetIdListDescription(wallsweep.GetHostIds().ToList());

                            var wallsweepCutsWall = wallsweep.GetWallSweepInfo().CutsWall;
                            var wallsweepDefaultSetback = wallsweep.GetWallSweepInfo().DefaultSetback;
                            var wallsweepDistance = wallsweep.GetWallSweepInfo().Distance;

                            int wallsweepDistanceMeasuredFrom;
                            if (wallsweep.GetWallSweepInfo().DistanceMeasuredFrom == DistanceMeasuredFrom.Base)
                            {
                                wallsweepDistanceMeasuredFrom = 0;
                            }
                            else
                            {
                                wallsweepDistanceMeasuredFrom = 1;

                            }

                            var wallsweepId = wallsweep.GetWallSweepInfo().Id;
                            var wallsweepIsCutByInserts = wallsweep.GetWallSweepInfo().IsCutByInserts;
                            var wallsweepIsProfileFlipped = wallsweep.GetWallSweepInfo().IsProfileFlipped;
                            var wallsweepIsVertical = wallsweep.GetWallSweepInfo().IsVertical;
                            var wallsweepMaterialId = wallsweep.GetWallSweepInfo().MaterialId.ToString();
                            var wallsweepProfileId = wallsweep.GetWallSweepInfo().ProfileId.ToString();
                            var wallsweepWallOffset = wallsweep.GetWallSweepInfo().WallOffset;

                            int wallsweepWallSide;
                            if (wallsweep.GetWallSweepInfo().WallSide == WallSide.Exterior)
                            {
                                wallsweepWallSide = 0;
                            }
                            else
                            {
                                wallsweepWallSide = 1;

                            }

                            int wallsweepWallSweepOrientation;

                            if (wallsweep.GetWallSweepInfo().WallSweepOrientation == WallSweepOrientation.Horizontal)
                            {
                                wallsweepWallSweepOrientation = 0;
                            }
                            else
                            {
                                wallsweepWallSweepOrientation = 1;

                            }

                            int wallsweepWallSweepType;
                            if (wallsweep.GetWallSweepInfo().WallSweepType == WallSweepType.Sweep)
                            {
                                wallsweepWallSweepType = 0;
                            }
                            else
                            {
                                wallsweepWallSweepType = 1;

                            }

                            var wallsweepOffsetFromWall = wallsweep.get_Parameter(BuiltInParameter.WALL_SWEEP_WALL_OFFSET_PARAM).AsDouble();
                            var wallsweepLevel = wallsweep.get_Parameter(BuiltInParameter.WALL_SWEEP_LEVEL_PARAM).AsValueString();
                            var wallsweepOffsetFromLevel = wallsweep.get_Parameter(BuiltInParameter.WALL_SWEEP_OFFSET_PARAM).AsDouble();
                            var wallsweepLength = wallsweep.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble();
                            var wallsweepImage = wallsweep.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                            var wallsweepComments = wallsweep.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                            var wallsweepMark = wallsweep.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                            var wallsweepPhaseCreated = wallsweep.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                            var wallsweepPhaseDemolished = wallsweep.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();

                            var wallsweepLog = new WallSweepLog()
                            {
                                WallId = wallsweepWallId,
                                CutsWall = wallsweepCutsWall,
                                DefaultSetback = wallsweepDefaultSetback,
                                Distance = wallsweepDistance,
                                DistanceMeasuredFrom = wallsweepDistanceMeasuredFrom,
                                Id = wallsweepId,
                                IsCutByInserts = wallsweepIsCutByInserts,
                                IsProfileFlipped = wallsweepIsProfileFlipped,
                                IsVertical = wallsweepIsVertical,
                                MaterialId = wallsweepMaterialId,
                                ProfileId = wallsweepProfileId,
                                WallOffset = wallsweepWallOffset,
                                WallSide = wallsweepWallSide,
                                WallSweepOrientation = wallsweepWallSweepOrientation,
                                WallSweepType = wallsweepWallSweepType,
                                OffsetFromWall = wallsweepOffsetFromWall,
                                Level = wallsweepLevel,
                                OffsetFromLevel = wallsweepOffsetFromLevel,
                                Length = wallsweepLength,
                                Image = wallsweepImage,
                                Comments = wallsweepComments,
                                Mark = wallsweepMark,
                                PhaseCreated = wallsweepPhaseCreated,
                                PhaseDemolished = wallsweepPhaseDemolished,
                                Timestamp = timestamp,
                                ElementId = id,
                                CommandType = cmd,
                                ElementCategory = cat,
                                ElementFamily = fam,
                                ElementType = typ,
                                WorksetId = worksetId
                            };

                            return wallsweepLog;

                        case "Walls: Reveals":

                            var reveals = elem as WallSweep;

                            var revealsWallId = GetIdListDescription(reveals.GetHostIds().ToList());
                            var revealsCutsWall = reveals.GetWallSweepInfo().CutsWall;
                            var revealsDefaultSetback = reveals.GetWallSweepInfo().DefaultSetback;
                            var revealsDistance = reveals.GetWallSweepInfo().Distance;

                            int revealsDistanceMeasureFrom;
                            if (reveals.GetWallSweepInfo().DistanceMeasuredFrom == DistanceMeasuredFrom.Base)
                            {
                                revealsDistanceMeasureFrom = 0;
                            }
                            else
                            {
                                revealsDistanceMeasureFrom = 1;

                            }


                            var revealsId = reveals.GetWallSweepInfo().Id;
                            var revealsIsCutByInserts = reveals.GetWallSweepInfo().IsCutByInserts;
                            var revealsIsProfileFlipped = reveals.GetWallSweepInfo().IsProfileFlipped;
                            var revealsIsVertical = reveals.GetWallSweepInfo().IsVertical;
                            var revealsMaterialId = reveals.GetWallSweepInfo().MaterialId.ToString();
                            var revealsProfileId = reveals.GetWallSweepInfo().ProfileId.ToString();
                            var revealsWallOffset = reveals.GetWallSweepInfo().WallOffset;

                            int revealsWallSide;
                            if (reveals.GetWallSweepInfo().WallSide == WallSide.Exterior)
                            {
                                revealsWallSide = 0;
                            }
                            else
                            {
                                revealsWallSide = 1;

                            }

                            int revealsWallSweepOrientation;

                            if (reveals.GetWallSweepInfo().WallSweepOrientation == WallSweepOrientation.Horizontal)
                            {
                                revealsWallSweepOrientation = 0;
                            }
                            else
                            {
                                revealsWallSweepOrientation = 1;

                            }

                            int revealsWallSweepType;
                            if (reveals.GetWallSweepInfo().WallSweepType == WallSweepType.Sweep)
                            {
                                revealsWallSweepType = 0;
                            }
                            else
                            {
                                revealsWallSweepType = 1;

                            }

                            var revealsOffsetFromWall = reveals.get_Parameter(BuiltInParameter.WALL_SWEEP_WALL_OFFSET_PARAM).AsDouble();
                            var revealsLevel = reveals.get_Parameter(BuiltInParameter.WALL_SWEEP_LEVEL_PARAM).AsValueString();
                            var revealsOffsetFromLevel = reveals.get_Parameter(BuiltInParameter.WALL_SWEEP_OFFSET_PARAM).AsDouble();
                            var revealsLength = reveals.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble();

                            var revealsLog = new RevealsLog()
                            {
                                WallId = revealsWallId,
                                CutsWall = revealsCutsWall,
                                DefaultSetback = revealsDefaultSetback,
                                Distance = revealsDistance,
                                DistanceMeasureFrom = revealsDistanceMeasureFrom,
                                Id = revealsId,
                                IsCutByInserts = revealsIsCutByInserts,
                                IsProfileFlipped = revealsIsProfileFlipped,
                                IsVertical = revealsIsVertical,
                                MaterialId = revealsMaterialId,
                                ProfileId = revealsProfileId,
                                WallOffset = revealsWallOffset,
                                WallSide = revealsWallSide,
                                WallSweepOrientation = revealsWallSweepOrientation,
                                WallSweepType = revealsWallSweepType,
                                OffsetFromWall = revealsOffsetFromWall,
                                Level = revealsLevel,
                                OffsetFromLevel = revealsOffsetFromLevel,
                                Length = revealsLength,
                                Timestamp = timestamp,
                                ElementId = id,
                                CommandType = cmd,
                                ElementCategory = cat,
                                ElementFamily = fam,
                                ElementType = typ,
                                WorksetId = worksetId
                            };


                            return revealsLog;


                        case "Structural Foundations":

                            if (elem.GetType().Name == "FamilyInstance")
                            {
                                cat = "Structural Foundations: Isolated";

                                var isolatedfoundation = elem as FamilyInstance;
                                var isolatedfoundationLocation = GetXYZDescription((isolatedfoundation.Location as LocationPoint).Point);
                                var isolatedfoundationLevel = isolatedfoundation.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM).AsValueString();
                                var isolatedfoundationHost = isolatedfoundation.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_PARAM).AsString();
                                var isolatedfoundationHeightOffsetFromLevel = isolatedfoundation.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).AsValueString();
                                var isolatedfoundationMovesWithGrids = isolatedfoundation.get_Parameter(BuiltInParameter.INSTANCE_MOVES_WITH_GRID_PARAM).AsInteger();
                                var isolatedfoundationStructuralMaterial = isolatedfoundation.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM).AsValueString();
                                var isolatedfoundationRebarCover_TopFace = isolatedfoundation.get_Parameter(BuiltInParameter.CLEAR_COVER_TOP).AsValueString();
                                var isolatedfoundationRebarCover_BottomFace = isolatedfoundation.get_Parameter(BuiltInParameter.CLEAR_COVER_BOTTOM).AsValueString();
                                var isolatedfoundationRebarCover_OtherFace = isolatedfoundation.get_Parameter(BuiltInParameter.CLEAR_COVER_OTHER).AsValueString();
                                var isolatedfoundationElevationatTop = isolatedfoundation.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP).AsDouble();
                                var isolatedfoundationElevationatBottom = isolatedfoundation.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM).AsDouble();
                                var isolatedfoundationImage = isolatedfoundation.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                                var isolatedfoundationComments = isolatedfoundation.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                                var isolatedfoundationMark = isolatedfoundation.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                                var isolatedfoundationPhaseCreated = isolatedfoundation.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                                var isolatedfoundationPhaseDemolished = isolatedfoundation.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();

                                var isolatedfoundationLog = new IsolatedFoundationLog()
                                {
                                    Location = isolatedfoundationLocation,
                                    Level = isolatedfoundationLevel,
                                    Host = isolatedfoundationHost,
                                    HeightOffsetFromLevel = isolatedfoundationHeightOffsetFromLevel,
                                    MovesWithGrids = isolatedfoundationMovesWithGrids,
                                    StructuralMaterial = isolatedfoundationStructuralMaterial,
                                    RebarCover_TopFace = isolatedfoundationRebarCover_TopFace,
                                    RebarCover_BottomFace = isolatedfoundationRebarCover_BottomFace,
                                    RebarCover_OtherFace = isolatedfoundationRebarCover_OtherFace,
                                    ElevationatTop = isolatedfoundationElevationatTop,
                                    ElevationatBottom = isolatedfoundationElevationatBottom,
                                    Image = isolatedfoundationImage,
                                    Comments = isolatedfoundationComments,
                                    Mark = isolatedfoundationMark,
                                    PhaseCreated = isolatedfoundationPhaseCreated,
                                    PhaseDemolished = isolatedfoundationPhaseDemolished,
                                    Timestamp = timestamp,
                                    ElementId = id,
                                    CommandType = cmd,
                                    ElementCategory = cat,
                                    ElementFamily = fam,
                                    ElementType = typ,
                                    WorksetId = worksetId
                                };

                                return isolatedfoundationLog;
                            }

                            else if (elem.GetType().Name == "WallFoundation")
                            {
                                cat = "Structural Foundations: Wall";

                                var wallfoundation = elem as WallFoundation;
                                var wallfoundationWallId = wallfoundation.WallId.ToString();
                                var wallfoundationEccentricity = wallfoundation.get_Parameter(BuiltInParameter.CONTINUOUS_FOOTING_ECCENTRICITY).AsDouble();
                                var wallfoundationRebarCover_TopFace = wallfoundation.get_Parameter(BuiltInParameter.CLEAR_COVER_TOP).AsValueString();
                                var wallfoundationRebarCover_BottomFace = wallfoundation.get_Parameter(BuiltInParameter.CLEAR_COVER_BOTTOM).AsValueString();
                                var wallfoundationRebarCover_OtherFace = wallfoundation.get_Parameter(BuiltInParameter.CLEAR_COVER_OTHER).AsValueString();
                                var wallfoundationLength = wallfoundation.get_Parameter(BuiltInParameter.CONTINUOUS_FOOTING_LENGTH).AsDouble();
                                var wallfoundationWidth = wallfoundation.get_Parameter(BuiltInParameter.CONTINUOUS_FOOTING_WIDTH).AsDouble();
                                var wallfoundationElevationatTop = wallfoundation.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP).AsDouble();
                                var wallfoundationElevationatBottom = wallfoundation.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM).AsDouble();
                                var wallfoundationVolume = wallfoundation.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                                var wallfoundationImage = wallfoundation.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                                var wallfoundationComments = wallfoundation.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                                var wallfoundationMark = wallfoundation.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                                var wallfoundationPhaseCreated = wallfoundation.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                                var wallfoundationPhaseDemolished = wallfoundation.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();

                                var wallfoundationLog = new WallFoundationLog()
                                {
                                    WallId = wallfoundationWallId,
                                    Eccentricity = wallfoundationEccentricity,
                                    RebarCover_TopFace = wallfoundationRebarCover_TopFace,
                                    RebarCover_BottomFace = wallfoundationRebarCover_BottomFace,
                                    RebarCover_OtherFace = wallfoundationRebarCover_OtherFace,
                                    Length = wallfoundationLength,
                                    Width = wallfoundationWidth,
                                    ElevationatTop = wallfoundationElevationatTop,
                                    ElevationatBottom = wallfoundationElevationatBottom,
                                    Volume = wallfoundationVolume,
                                    Image = wallfoundationImage,
                                    Comments = wallfoundationComments,
                                    Mark = wallfoundationMark,
                                    PhaseCreated = wallfoundationPhaseCreated,
                                    PhaseDemolished = wallfoundationPhaseDemolished,
                                    Timestamp = timestamp,
                                    ElementId = id,
                                    CommandType = cmd,
                                    ElementCategory = cat,
                                    ElementFamily = fam,
                                    ElementType = typ,
                                    WorksetId = worksetId
                                };

                                return wallfoundationLog;
                            }
                            else if (elem.GetType().Name == "Floor")
                            {
                                cat = "Structural Foundations: Slab";
                                var slabfoundation = elem as Floor;

                                var slabfoundationSketch = (doc.GetElement(slabfoundation.SketchId) as Sketch);
                                var slabfoundationEIds = slabfoundationSketch.GetAllElements();

                                string slabfoundationSlopeArrow = null;
                                double slabfoundationSlope = 0;

                                string slabfoundationSpanDirection = null;

                                foreach (ElementId slabfoundationEId in slabfoundationEIds)
                                {
                                    if (doc.GetElement(slabfoundationEId).Name == "Slope Arrow")
                                    {
                                        var slabfoundationcrv = (doc.GetElement(slabfoundationEId) as CurveElement).GeometryCurve;
                                        slabfoundationSlopeArrow = GetCurveDescription(slabfoundationcrv).ToString();

                                        List<Parameter> slabfoundationParams = (doc.GetElement(slabfoundationEId) as ModelLine).GetOrderedParameters().ToList();

                                        foreach (var param in slabfoundationParams)
                                        {
                                            if (param.Definition.Name == "Slope")
                                            {
                                                slabfoundationSlope = param.AsDouble();
                                            }
                                        }
                                    }
                                    if (doc.GetElement(slabfoundationEId).Name == "Span Direction Edges")
                                    {
                                        var slabfoundationcrv = (doc.GetElement(slabfoundationEId) as CurveElement).GeometryCurve;
                                        slabfoundationSpanDirection = GetCurveDescription(slabfoundationcrv).ToString();
                                    }
                                }

                                if (slabfoundationSlopeArrow == null)
                                {
                                    slabfoundationSlopeArrow = "None";
                                }


                                var slabfoundationProfile = GetProfileDescription(slabfoundationSketch);

                                var slabfoundationLevel = slabfoundation.get_Parameter(BuiltInParameter.LEVEL_PARAM).AsValueString();
                                var slabfoundationHeightOffsetFromLevel = slabfoundation.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).AsDouble();
                                var slabfoundationRelatedtoMass = slabfoundation.get_Parameter(BuiltInParameter.RELATED_TO_MASS).AsInteger();
                                var slabfoundationStructural = slabfoundation.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL).AsInteger();
                                var slabfoundationRebarCover_TopFace = slabfoundation.get_Parameter(BuiltInParameter.CLEAR_COVER_TOP).AsValueString();
                                var slabfoundationRebarCover_BottomFace = slabfoundation.get_Parameter(BuiltInParameter.CLEAR_COVER_BOTTOM).AsValueString();
                                var slabfoundationRebarCover_OtherFace = slabfoundation.get_Parameter(BuiltInParameter.CLEAR_COVER_OTHER).AsValueString();
                                var slabfoundationPerimeter = slabfoundation.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED).AsDouble();
                                var slabfoundationArea = slabfoundation.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
                                var slabfoundationVolume = slabfoundation.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                                var slabfoundationElevationatTop = slabfoundation.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP).AsDouble();
                                var slabfoundationElevationatBottom = slabfoundation.get_Parameter(BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM).AsDouble();
                                var slabfoundationWidth = slabfoundation.get_Parameter(BuiltInParameter.CONTINUOUS_FOOTING_WIDTH).AsDouble();
                                var slabfoundationLength = slabfoundation.get_Parameter(BuiltInParameter.CONTINUOUS_FOOTING_LENGTH).AsDouble();
                                var slabfoundationThickness = slabfoundation.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble();
                                var slabfoundationImage = slabfoundation.get_Parameter(BuiltInParameter.ALL_MODEL_IMAGE).AsValueString();
                                var slabfoundationComments = slabfoundation.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
                                var slabfoundationMark = slabfoundation.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                                var slabfoundationPhaseCreated = slabfoundation.get_Parameter(BuiltInParameter.PHASE_CREATED).AsValueString();
                                var slabfoundationPhaseDemolished = slabfoundation.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED).AsValueString();

                                var slabfoundationLog = new SlabFoundationLog()
                                {
                                    Profile = slabfoundationProfile,
                                    SlopeArrow = slabfoundationSlopeArrow,
                                    SpanDirection = slabfoundationSpanDirection,
                                    Level = slabfoundationLevel,
                                    HeightOffsetFromLevel = slabfoundationHeightOffsetFromLevel,
                                    RelatedtoMass = slabfoundationRelatedtoMass,
                                    Structural = slabfoundationStructural,
                                    RebarCover_TopFace = slabfoundationRebarCover_TopFace,
                                    RebarCover_BottomFace = slabfoundationRebarCover_BottomFace,
                                    RebarCover_OtherFace = slabfoundationRebarCover_OtherFace,
                                    Slope = slabfoundationSlope,
                                    Perimeter = slabfoundationPerimeter,
                                    Area = slabfoundationArea,
                                    Volume = slabfoundationVolume,
                                    ElevationatTop = slabfoundationElevationatTop,
                                    ElevationatBottom = slabfoundationElevationatBottom,
                                    Width = slabfoundationWidth,
                                    Length = slabfoundationLength,
                                    Thickness = slabfoundationThickness,
                                    Image = slabfoundationImage,
                                    Comments = slabfoundationComments,
                                    Mark = slabfoundationMark,
                                    PhaseCreated = slabfoundationPhaseCreated,
                                    PhaseDemolished = slabfoundationPhaseDemolished,
                                    Timestamp = timestamp,
                                    ElementId = id,
                                    CommandType = cmd,
                                    ElementCategory = cat,
                                    ElementFamily = fam,
                                    ElementType = typ,
                                    WorksetId = worksetId
                                };
                                return slabfoundationLog;
                            }

                            return null;
                    }
                }

                return null;
            }

        }
        #endregion

        #region Define Class
        //==================================================Logging classes definitions

        public class JStartLog : InfoLog
        {
            public JStartLog( ) { }

            [Index(0)]
            public string startTime { get; set; }
        }

        public class GeneralLog
        {
            public GeneralLog( )
            {

            }
            [Index(0)]
            public string Timestamp { get; set; }
            [Index(1)]
            public string ElementId { get; set; }

            [Index(2)]
            public string CommandType { get; set; }

            [Index(3)]
            public string ElementCategory { get; set; }
        }

        public class StartLog
        {
            public StartLog( )
            {

            }
            [Index(0)]
            public string startTime { get; set; }
            //[Index(1)]
            //public string CommandType { get; set; }
        }
        public class FinishLog
        {
            public FinishLog( )
            {

            }
            [Index(0)]
            public string EndTime { get; set; }
            //[Index(1)]
            //public string CommandType { get; set; }
        }
        public class InfoLog
        {
            public InfoLog( )
            {

            }
            [Index(0)]
            public string InfoString { get; set; }
            [Index(1)]
            public string ProjectName { get; set; }
            [Index(2)]
            public string UserName { get; set; }
        }
        public class CommandLog : GeneralLog
        {
            public CommandLog( ) { }
            [Index(4)]
            public string CommandId { get; set; }
            [Index(5)]
            public string CommandCookie { get; set; }
        }
        public class FailureLog : GeneralLog
        {
            public FailureLog( ) { }
            [Index(4)]
            public string FailureMessage { get; set; }
        }

        public class DeletingLog : GeneralLog
        {
            public DeletingLog( ) { }

            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory);
            }
        }
        public class UpdatingLog : GeneralLog
        {
            public UpdatingLog( ) { }

            [Index(4)]
            public string ElementFamily { get; set; }

            [Index(5)]
            public string ElementType { get; set; }
            [Index(6)]
            public string WorksetId { get; set; }


        }

        //GridLog

        //GridLog

        public class GridLog : UpdatingLog
        {
            public GridLog( ) { }
            [Index(7)]
            public string Curve { get; set; }
            [Index(8)]
            public string ScopeBox { get; set; }
            [Index(9)]
            public string Name { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.Curve, this.ScopeBox, this.Name);
            }
        }

        public sealed class GridLogMap : ClassMap<GridLog>
        {
            public GridLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.Curve).Index(7);
                Map(m => m.ScopeBox).Index(8);
                Map(m => m.Name).Index(9);
            }
        }

        //RoomLog

        public class RoomLog : UpdatingLog
        {
            public RoomLog( ) { }
            [Index(7)]
            public string UV { get; set; }
            [Index(8)]
            public string Level { get; set; }
            [Index(9)]
            public string UpperLimit { get; set; }
            [Index(10)]
            public double LimitOffset { get; set; }
            [Index(11)]
            public double BaseOffset { get; set; }
            [Index(12)]
            public int Number { get; set; }
            [Index(13)]
            public string Name { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.UV, this.Level, this.UpperLimit, this.LimitOffset, this.BaseOffset, this.Number, this.Name);
            }
        }

        public sealed class RoomLogMap : ClassMap<RoomLog>
        {
            public RoomLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.UV).Index(7);
                Map(m => m.Level).Index(8);
                Map(m => m.UpperLimit).Index(9);
                Map(m => m.LimitOffset).Index(10);
                Map(m => m.BaseOffset).Index(11);
                Map(m => m.Number).Index(12);
                Map(m => m.Name).Index(13);
            }
        }

        //FloorLog

        public class FloorLog : UpdatingLog
        {
            public FloorLog( ) { }
            [Index(7)]
            public string Profile { get; set; }
            [Index(8)]
            public string SlopeArrow { get; set; }
            [Index(9)]
            public string SpanDirection { get; set; }
            [Index(10)]
            public string Level { get; set; }
            [Index(11)]
            public double HeightOffsetFromLevel { get; set; }
            [Index(12)]
            public int RoomBounding { get; set; }
            [Index(13)]
            public int RelatedtoMass { get; set; }
            [Index(14)]
            public int Structural { get; set; }
            [Index(15)]
            public string RebarCover_TopFace { get; set; }
            [Index(16)]
            public string RebarCover_BottomFace { get; set; }
            [Index(17)]
            public string RebarCover_OtherFace { get; set; }
            [Index(18)]
            public double Slope { get; set; }
            [Index(19)]
            public double Perimeter { get; set; }
            [Index(20)]
            public double Area { get; set; }
            [Index(21)]
            public double Volume { get; set; }
            [Index(22)]
            public double ElevationatTop { get; set; }
            [Index(23)]
            public double ElevationatBottom { get; set; }
            [Index(24)]
            public double Thickness { get; set; }
            [Index(25)]
            public string Image { get; set; }
            [Index(26)]
            public string Comments { get; set; }
            [Index(27)]
            public string Mark { get; set; }
            [Index(28)]
            public string PhaseCreated { get; set; }
            [Index(29)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format($"{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.Profile, this.SlopeArrow, this.SpanDirection, this.Level, this.HeightOffsetFromLevel, this.RoomBounding, this.RelatedtoMass, this.Structural, this.RebarCover_TopFace, this.RebarCover_BottomFace, this.RebarCover_OtherFace, this.Slope, this.Perimeter, this.Area, this.Volume, this.ElevationatTop, this.ElevationatBottom, this.Thickness, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class FloorLogMap : ClassMap<FloorLog>
        {
            public FloorLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.Profile).Index(7);
                Map(m => m.SlopeArrow).Index(8);
                Map(m => m.SpanDirection).Index(9);
                Map(m => m.Level).Index(10);
                Map(m => m.HeightOffsetFromLevel).Index(11);
                Map(m => m.RoomBounding).Index(12);
                Map(m => m.RelatedtoMass).Index(13);
                Map(m => m.Structural).Index(14);
                Map(m => m.RebarCover_TopFace).Index(15);
                Map(m => m.RebarCover_BottomFace).Index(16);
                Map(m => m.RebarCover_OtherFace).Index(17);
                Map(m => m.Slope).Index(18);
                Map(m => m.Perimeter).Index(19);
                Map(m => m.Area).Index(20);
                Map(m => m.Volume).Index(21);
                Map(m => m.ElevationatTop).Index(22);
                Map(m => m.ElevationatBottom).Index(23);
                Map(m => m.Thickness).Index(24);
                Map(m => m.Image).Index(25);
                Map(m => m.Comments).Index(26);
                Map(m => m.Mark).Index(27);
                Map(m => m.PhaseCreated).Index(28);
                Map(m => m.PhaseDemolished).Index(29);
            }
        }

        //FootPrintRoofLog

        public class FootPrintRoofLog : UpdatingLog
        {
            public FootPrintRoofLog( ) { }
            [Index(7)]
            public string FootPrint { get; set; }
            [Index(8)]
            public string BaseLevel { get; set; }
            [Index(9)]
            public int RoomBounding { get; set; }
            [Index(10)]
            public int RelatedtoMass { get; set; }
            [Index(11)]
            public double BaseOffsetFromLevel { get; set; }
            [Index(12)]
            public string CutoffLevel { get; set; }
            [Index(13)]
            public double CutoffOffset { get; set; }
            [Index(14)]
            public int RafterCut { get; set; }
            [Index(15)]
            public double FasciaDepth { get; set; }
            [Index(16)]
            public double MaximumRidgeHeght { get; set; }
            [Index(17)]
            public double Slope { get; set; }
            [Index(18)]
            public double Thickness { get; set; }
            [Index(19)]
            public double Volume { get; set; }
            [Index(20)]
            public double Area { get; set; }
            [Index(21)]
            public string Image { get; set; }
            [Index(22)]
            public string Comments { get; set; }
            [Index(23)]
            public string Mark { get; set; }
            [Index(24)]
            public string PhaseCreated { get; set; }
            [Index(25)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.FootPrint, this.BaseLevel, this.RoomBounding, this.RelatedtoMass, this.BaseOffsetFromLevel, this.CutoffLevel, this.CutoffOffset, this.RafterCut, this.FasciaDepth, this.MaximumRidgeHeght, this.Slope, this.Thickness, this.Volume, this.Area, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class FootPrintRoofLogMap : ClassMap<FootPrintRoofLog>
        {
            public FootPrintRoofLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.FootPrint).Index(7);
                Map(m => m.BaseLevel).Index(8);
                Map(m => m.RoomBounding).Index(9);
                Map(m => m.RelatedtoMass).Index(10);
                Map(m => m.BaseOffsetFromLevel).Index(11);
                Map(m => m.CutoffLevel).Index(12);
                Map(m => m.CutoffOffset).Index(13);
                Map(m => m.RafterCut).Index(14);
                Map(m => m.FasciaDepth).Index(15);
                Map(m => m.MaximumRidgeHeght).Index(16);
                Map(m => m.Slope).Index(17);
                Map(m => m.Thickness).Index(18);
                Map(m => m.Volume).Index(19);
                Map(m => m.Area).Index(20);
                Map(m => m.Image).Index(21);
                Map(m => m.Comments).Index(22);
                Map(m => m.Mark).Index(23);
                Map(m => m.PhaseCreated).Index(24);
                Map(m => m.PhaseDemolished).Index(25);
            }
        }

        //ExtrusionRoofLog

        public class ExtrusionRoofLog : UpdatingLog
        {
            public ExtrusionRoofLog( ) { }
            [Index(7)]
            public string Profile { get; set; }
            [Index(8)]
            public string WorkPlane { get; set; }
            [Index(9)]
            public int RoomBounding { get; set; }
            [Index(10)]
            public int RelatedtoMass { get; set; }
            [Index(11)]
            public double ExtrusionStart { get; set; }
            [Index(12)]
            public double ExtrusionEnd { get; set; }
            [Index(13)]
            public string ReferenceLevel { get; set; }
            [Index(14)]
            public double LevelOffset { get; set; }
            [Index(15)]
            public double FasciaDepth { get; set; }
            [Index(16)]
            public int RafterCut { get; set; }
            [Index(17)]
            public double Slope { get; set; }
            [Index(18)]
            public double Thickness { get; set; }
            [Index(19)]
            public double Volume { get; set; }
            [Index(20)]
            public double Area { get; set; }
            [Index(21)]
            public string Image { get; set; }
            [Index(22)]
            public string Comments { get; set; }
            [Index(23)]
            public string Mark { get; set; }
            [Index(24)]
            public string PhaseCreated { get; set; }
            [Index(25)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.Profile, this.WorkPlane, this.RoomBounding, this.RelatedtoMass, this.ExtrusionStart, this.ExtrusionEnd, this.ReferenceLevel, this.LevelOffset, this.FasciaDepth, this.RafterCut, this.Slope, this.Thickness, this.Volume, this.Area, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class ExtrusionRoofLogMap : ClassMap<ExtrusionRoofLog>
        {
            public ExtrusionRoofLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.Profile).Index(7);
                Map(m => m.WorkPlane).Index(8);
                Map(m => m.RoomBounding).Index(9);
                Map(m => m.RelatedtoMass).Index(10);
                Map(m => m.ExtrusionStart).Index(11);
                Map(m => m.ExtrusionEnd).Index(12);
                Map(m => m.ReferenceLevel).Index(13);
                Map(m => m.LevelOffset).Index(14);
                Map(m => m.FasciaDepth).Index(15);
                Map(m => m.RafterCut).Index(16);
                Map(m => m.Slope).Index(17);
                Map(m => m.Thickness).Index(18);
                Map(m => m.Volume).Index(19);
                Map(m => m.Area).Index(20);
                Map(m => m.Image).Index(21);
                Map(m => m.Comments).Index(22);
                Map(m => m.Mark).Index(23);
                Map(m => m.PhaseCreated).Index(24);
                Map(m => m.PhaseDemolished).Index(25);
            }
        }



        //StairLog

        public class StairLog : UpdatingLog
        {
            public StairLog( ) { }
            [Index(7)]
            public string BaseLevel { get; set; }
            [Index(8)]
            public double BaseOffset { get; set; }
            [Index(9)]
            public string TopLevel { get; set; }
            [Index(10)]
            public double TopOffset { get; set; }
            [Index(11)]
            public double DesiredStairHeight { get; set; }
            [Index(12)]
            public int DesiredNumberofRisers { get; set; }
            [Index(13)]
            public int ActualNumberofRisers { get; set; }
            [Index(14)]
            public double ActualRiserHeight { get; set; }
            [Index(15)]
            public double ActualTreadDepth { get; set; }
            [Index(16)]
            public int TreadRiserStartNumber { get; set; }
            [Index(17)]
            public string Image { get; set; }
            [Index(18)]
            public string Comments { get; set; }
            [Index(19)]
            public string Mark { get; set; }
            [Index(20)]
            public string PhaseCreated { get; set; }
            [Index(21)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.BaseLevel, this.BaseOffset, this.TopLevel, this.TopOffset, this.DesiredStairHeight, this.DesiredNumberofRisers, this.ActualNumberofRisers, this.ActualRiserHeight, this.ActualTreadDepth, this.TreadRiserStartNumber, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class StairLogMap : ClassMap<StairLog>
        {
            public StairLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.BaseLevel).Index(7);
                Map(m => m.BaseOffset).Index(8);
                Map(m => m.TopLevel).Index(9);
                Map(m => m.TopOffset).Index(10);
                Map(m => m.DesiredStairHeight).Index(11);
                Map(m => m.DesiredNumberofRisers).Index(12);
                Map(m => m.ActualNumberofRisers).Index(13);
                Map(m => m.ActualRiserHeight).Index(14);
                Map(m => m.ActualTreadDepth).Index(15);
                Map(m => m.TreadRiserStartNumber).Index(16);
                Map(m => m.Image).Index(17);
                Map(m => m.Comments).Index(18);
                Map(m => m.Mark).Index(19);
                Map(m => m.PhaseCreated).Index(20);
                Map(m => m.PhaseDemolished).Index(21);
            }
        }

        //StairsRunsLog

        public class StairsRunsLog : UpdatingLog
        {
            public StairsRunsLog( ) { }
            [Index(7)]
            public string StairsId { get; set; }
            [Index(8)]
            public string LocationPath { get; set; }
            [Index(9)]
            public int Locationline { get; set; }
            [Index(10)]
            public double RelativeBaseHeight { get; set; }
            [Index(11)]
            public double RelativeTopHeight { get; set; }
            [Index(12)]
            public double RunHeight { get; set; }
            [Index(13)]
            public double ExtendBelowRiserBase { get; set; }
            [Index(14)]
            public int BeginwithRiser { get; set; }
            [Index(15)]
            public int EndwithRiser { get; set; }
            [Index(16)]
            public double ActualRunWidth { get; set; }
            [Index(17)]
            public double ActualRiserHeight { get; set; }
            [Index(18)]
            public double ActualTreadDepth { get; set; }
            [Index(19)]
            public int ActualNumberofRisers { get; set; }
            [Index(20)]
            public int ActualNumberofTreads { get; set; }
            [Index(21)]
            public string Image { get; set; }
            [Index(22)]
            public string Comments { get; set; }
            [Index(23)]
            public string Mark { get; set; }
            [Index(24)]
            public string PhaseCreated { get; set; }
            [Index(25)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.StairsId, this.LocationPath, this.Locationline, this.RelativeBaseHeight, this.RelativeTopHeight, this.RunHeight, this.ExtendBelowRiserBase, this.BeginwithRiser, this.EndwithRiser, this.ActualRunWidth, this.ActualRiserHeight, this.ActualTreadDepth, this.ActualNumberofRisers, this.ActualNumberofTreads, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class StairsRunsLogMap : ClassMap<StairsRunsLog>
        {
            public StairsRunsLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.StairsId).Index(7);
                Map(m => m.LocationPath).Index(8);
                Map(m => m.Locationline).Index(9);
                Map(m => m.RelativeBaseHeight).Index(10);
                Map(m => m.RelativeTopHeight).Index(11);
                Map(m => m.RunHeight).Index(12);
                Map(m => m.ExtendBelowRiserBase).Index(13);
                Map(m => m.BeginwithRiser).Index(14);
                Map(m => m.EndwithRiser).Index(15);
                Map(m => m.ActualRunWidth).Index(16);
                Map(m => m.ActualRiserHeight).Index(17);
                Map(m => m.ActualTreadDepth).Index(18);
                Map(m => m.ActualNumberofRisers).Index(19);
                Map(m => m.ActualNumberofTreads).Index(20);
                Map(m => m.Image).Index(21);
                Map(m => m.Comments).Index(22);
                Map(m => m.Mark).Index(23);
                Map(m => m.PhaseCreated).Index(24);
                Map(m => m.PhaseDemolished).Index(25);
            }
        }

        //StairsLandingsLog

        public class StairsLandingsLog : UpdatingLog
        {
            public StairsLandingsLog( ) { }
            [Index(7)]
            public string StairsId { get; set; }
            [Index(8)]
            public string CurveLoop { get; set; }
            [Index(9)]
            public double RelativeHeight { get; set; }
            [Index(10)]
            public double TotalThickness { get; set; }
            [Index(11)]
            public string Image { get; set; }
            [Index(12)]
            public string Comments { get; set; }
            [Index(13)]
            public string Mark { get; set; }
            [Index(14)]
            public string PhaseCreated { get; set; }
            [Index(15)]
            public string PhaseDemolished { get; set; }
            [Index(16)]
            public string Category { get; set; }
            [Index(17)]
            public string Type { get; set; }
            [Index(18)]
            public string TypeId { get; set; }
            [Index(19)]
            public string TypeName { get; set; }
            [Index(20)]
            public string Family { get; set; }
            [Index(21)]
            public string FamilyName { get; set; }
            [Index(22)]
            public string FamilyAndType { get; set; }
            [Index(23)]
            public string DesignOption { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.StairsId, this.CurveLoop, this.RelativeHeight, this.TotalThickness, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished, this.Category, this.Type, this.TypeId, this.TypeName, this.Family, this.FamilyName, this.FamilyAndType, this.DesignOption);
            }
        }

        public sealed class StairsLandingsLogMap : ClassMap<StairsLandingsLog>
        {
            public StairsLandingsLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.StairsId).Index(7);
                Map(m => m.CurveLoop).Index(8);
                Map(m => m.RelativeHeight).Index(9);
                Map(m => m.TotalThickness).Index(10);
                Map(m => m.Image).Index(11);
                Map(m => m.Comments).Index(12);
                Map(m => m.Mark).Index(13);
                Map(m => m.PhaseCreated).Index(14);
                Map(m => m.PhaseDemolished).Index(15);
                Map(m => m.Category).Index(16);
                Map(m => m.Type).Index(17);
                Map(m => m.TypeId).Index(18);
                Map(m => m.TypeName).Index(19);
                Map(m => m.Family).Index(20);
                Map(m => m.FamilyName).Index(21);
                Map(m => m.FamilyAndType).Index(22);
                Map(m => m.DesignOption).Index(23);
            }
        }

        //RailingLog

        public class RailingLog : UpdatingLog
        {
            public RailingLog( ) { }
            [Index(7)]
            public string HostId { get; set; }
            [Index(8)]
            public string CurveLoop { get; set; }
            [Index(9)]
            public bool Flipped { get; set; }
            [Index(10)]
            public string BaseLevel { get; set; }
            [Index(11)]
            public double BaseOffset { get; set; }
            [Index(12)]
            public double OffsetfromPath { get; set; }
            [Index(13)]
            public double Length { get; set; }
            [Index(14)]
            public string Image { get; set; }
            [Index(15)]
            public string Comments { get; set; }
            [Index(16)]
            public string Mark { get; set; }
            [Index(17)]
            public string PhaseCreated { get; set; }
            [Index(18)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.HostId, this.CurveLoop, this.Flipped, this.BaseLevel, this.BaseOffset, this.OffsetfromPath, this.Length, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class RailingLogMap : ClassMap<RailingLog>
        {
            public RailingLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.HostId).Index(7);
                Map(m => m.CurveLoop).Index(8);
                Map(m => m.Flipped).Index(9);
                Map(m => m.BaseLevel).Index(10);
                Map(m => m.BaseOffset).Index(11);
                Map(m => m.OffsetfromPath).Index(12);
                Map(m => m.Length).Index(13);
                Map(m => m.Image).Index(14);
                Map(m => m.Comments).Index(15);
                Map(m => m.Mark).Index(16);
                Map(m => m.PhaseCreated).Index(17);
                Map(m => m.PhaseDemolished).Index(18);
            }
        }



        //FurnitureLog

        public class FurnitureLog : UpdatingLog
        {
            public FurnitureLog( ) { }
            [Index(7)]
            public string Location { get; set; }
            [Index(8)]
            public string Level { get; set; }
            [Index(9)]
            public double ElevationfromLevel { get; set; }
            [Index(10)]
            public int MovesWithNearbyElements { get; set; }
            [Index(11)]
            public string Image { get; set; }
            [Index(12)]
            public string Comments { get; set; }
            [Index(13)]
            public string Mark { get; set; }
            [Index(14)]
            public string PhaseCreated { get; set; }
            [Index(15)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.Location, this.Level, this.ElevationfromLevel, this.MovesWithNearbyElements, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class FurnitureLogMap : ClassMap<FurnitureLog>
        {
            public FurnitureLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.Location).Index(7);
                Map(m => m.Level).Index(8);
                Map(m => m.ElevationfromLevel).Index(9);
                Map(m => m.MovesWithNearbyElements).Index(10);
                Map(m => m.Image).Index(11);
                Map(m => m.Comments).Index(12);
                Map(m => m.Mark).Index(13);
                Map(m => m.PhaseCreated).Index(14);
                Map(m => m.PhaseDemolished).Index(15);
            }
        }



        //CurtainMullionLog

        public class CurtainMullionLog : UpdatingLog
        {
            public CurtainMullionLog( ) { }
            [Index(7)]
            public string HostId { get; set; }
            [Index(8)]
            public string Curve { get; set; }
            [Index(9)]
            public string Location { get; set; }
            [Index(10)]
            public double Length { get; set; }
            [Index(11)]
            public string Image { get; set; }
            [Index(12)]
            public string Comments { get; set; }
            [Index(13)]
            public string Mark { get; set; }
            [Index(14)]
            public string PhaseCreated { get; set; }
            [Index(15)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.HostId, this.Curve, this.Location, this.Length, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class CurtainMullionLogMap : ClassMap<CurtainMullionLog>
        {
            public CurtainMullionLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.HostId).Index(7);
                Map(m => m.Curve).Index(8);
                Map(m => m.Location).Index(9);
                Map(m => m.Length).Index(10);
                Map(m => m.Image).Index(11);
                Map(m => m.Comments).Index(12);
                Map(m => m.Mark).Index(13);
                Map(m => m.PhaseCreated).Index(14);
                Map(m => m.PhaseDemolished).Index(15);
            }
        }



        //IsolatedFoundationLog

        public class IsolatedFoundationLog : UpdatingLog
        {
            public IsolatedFoundationLog( ) { }
            [Index(7)]
            public string Location { get; set; }
            [Index(8)]
            public string Level { get; set; }
            [Index(9)]
            public string Host { get; set; }
            [Index(10)]
            public string HeightOffsetFromLevel { get; set; }
            [Index(11)]
            public int MovesWithGrids { get; set; }
            [Index(12)]
            public string StructuralMaterial { get; set; }
            [Index(13)]
            public string RebarCover_TopFace { get; set; }
            [Index(14)]
            public string RebarCover_BottomFace { get; set; }
            [Index(15)]
            public string RebarCover_OtherFace { get; set; }
            [Index(16)]
            public double ElevationatTop { get; set; }
            [Index(17)]
            public double ElevationatBottom { get; set; }
            [Index(18)]
            public string Image { get; set; }
            [Index(19)]
            public string Comments { get; set; }
            [Index(20)]
            public string Mark { get; set; }
            [Index(21)]
            public string PhaseCreated { get; set; }
            [Index(22)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.Location, this.Level, this.Host, this.HeightOffsetFromLevel, this.MovesWithGrids, this.StructuralMaterial, this.RebarCover_TopFace, this.RebarCover_BottomFace, this.RebarCover_OtherFace, this.ElevationatTop, this.ElevationatBottom, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class IsolatedFoundationLogMap : ClassMap<IsolatedFoundationLog>
        {
            public IsolatedFoundationLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.Location).Index(7);
                Map(m => m.Level).Index(8);
                Map(m => m.Host).Index(9);
                Map(m => m.HeightOffsetFromLevel).Index(10);
                Map(m => m.MovesWithGrids).Index(11);
                Map(m => m.StructuralMaterial).Index(12);
                Map(m => m.RebarCover_TopFace).Index(13);
                Map(m => m.RebarCover_BottomFace).Index(14);
                Map(m => m.RebarCover_OtherFace).Index(15);
                Map(m => m.ElevationatTop).Index(16);
                Map(m => m.ElevationatBottom).Index(17);
                Map(m => m.Image).Index(18);
                Map(m => m.Comments).Index(19);
                Map(m => m.Mark).Index(20);
                Map(m => m.PhaseCreated).Index(21);
                Map(m => m.PhaseDemolished).Index(22);
            }
        }

        //WallFoundationLog

        public class WallFoundationLog : UpdatingLog
        {
            public WallFoundationLog( ) { }
            [Index(7)]
            public string WallId { get; set; }
            [Index(8)]
            public double Eccentricity { get; set; }
            [Index(9)]
            public string RebarCover_TopFace { get; set; }
            [Index(10)]
            public string RebarCover_BottomFace { get; set; }
            [Index(11)]
            public string RebarCover_OtherFace { get; set; }
            [Index(12)]
            public double Length { get; set; }
            [Index(13)]
            public double Width { get; set; }
            [Index(14)]
            public double ElevationatTop { get; set; }
            [Index(15)]
            public double ElevationatBottom { get; set; }
            [Index(16)]
            public double Volume { get; set; }
            [Index(17)]
            public string Image { get; set; }
            [Index(18)]
            public string Comments { get; set; }
            [Index(19)]
            public string Mark { get; set; }
            [Index(20)]
            public string PhaseCreated { get; set; }
            [Index(21)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.WallId, this.Eccentricity, this.RebarCover_TopFace, this.RebarCover_BottomFace, this.RebarCover_OtherFace, this.Length, this.Width, this.ElevationatTop, this.ElevationatBottom, this.Volume, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class WallFoundationLogMap : ClassMap<WallFoundationLog>
        {
            public WallFoundationLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.WallId).Index(7);
                Map(m => m.Eccentricity).Index(8);
                Map(m => m.RebarCover_TopFace).Index(9);
                Map(m => m.RebarCover_BottomFace).Index(10);
                Map(m => m.RebarCover_OtherFace).Index(11);
                Map(m => m.Length).Index(12);
                Map(m => m.Width).Index(13);
                Map(m => m.ElevationatTop).Index(14);
                Map(m => m.ElevationatBottom).Index(15);
                Map(m => m.Volume).Index(16);
                Map(m => m.Image).Index(17);
                Map(m => m.Comments).Index(18);
                Map(m => m.Mark).Index(19);
                Map(m => m.PhaseCreated).Index(20);
                Map(m => m.PhaseDemolished).Index(21);
            }
        }

        //SlabFoundationLog

        public class SlabFoundationLog : UpdatingLog
        {
            public SlabFoundationLog( ) { }
            [Index(7)]
            public string Profile { get; set; }
            [Index(8)]
            public string SlopeArrow { get; set; }
            [Index(9)]
            public string SpanDirection { get; set; }
            [Index(10)]
            public string Level { get; set; }
            [Index(11)]
            public double HeightOffsetFromLevel { get; set; }
            [Index(12)]
            public int RelatedtoMass { get; set; }
            [Index(13)]
            public int Structural { get; set; }
            [Index(14)]
            public string RebarCover_TopFace { get; set; }
            [Index(15)]
            public string RebarCover_BottomFace { get; set; }
            [Index(16)]
            public string RebarCover_OtherFace { get; set; }
            [Index(17)]
            public double Slope { get; set; }
            [Index(18)]
            public double Perimeter { get; set; }
            [Index(19)]
            public double Area { get; set; }
            [Index(20)]
            public double Volume { get; set; }
            [Index(21)]
            public double ElevationatTop { get; set; }
            [Index(22)]
            public double ElevationatBottom { get; set; }
            [Index(23)]
            public double Width { get; set; }
            [Index(24)]
            public double Length { get; set; }
            [Index(25)]
            public double Thickness { get; set; }
            [Index(26)]
            public string Image { get; set; }
            [Index(27)]
            public string Comments { get; set; }
            [Index(28)]
            public string Mark { get; set; }
            [Index(29)]
            public string PhaseCreated { get; set; }
            [Index(30)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.Profile, this.SlopeArrow, this.SpanDirection, this.Level, this.HeightOffsetFromLevel, this.RelatedtoMass, this.Structural, this.RebarCover_TopFace, this.RebarCover_BottomFace, this.RebarCover_OtherFace, this.Slope, this.Perimeter, this.Area, this.Volume, this.ElevationatTop, this.ElevationatBottom, this.Width, this.Length, this.Thickness, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class SlabFoundationLogMap : ClassMap<SlabFoundationLog>
        {
            public SlabFoundationLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.Profile).Index(7);
                Map(m => m.SlopeArrow).Index(8);
                Map(m => m.SpanDirection).Index(9);
                Map(m => m.Level).Index(10);
                Map(m => m.HeightOffsetFromLevel).Index(11);
                Map(m => m.RelatedtoMass).Index(12);
                Map(m => m.Structural).Index(13);
                Map(m => m.RebarCover_TopFace).Index(14);
                Map(m => m.RebarCover_BottomFace).Index(15);
                Map(m => m.RebarCover_OtherFace).Index(16);
                Map(m => m.Slope).Index(17);
                Map(m => m.Perimeter).Index(18);
                Map(m => m.Area).Index(19);
                Map(m => m.Volume).Index(20);
                Map(m => m.ElevationatTop).Index(21);
                Map(m => m.ElevationatBottom).Index(22);
                Map(m => m.Width).Index(23);
                Map(m => m.Length).Index(24);
                Map(m => m.Thickness).Index(25);
                Map(m => m.Image).Index(26);
                Map(m => m.Comments).Index(27);
                Map(m => m.Mark).Index(28);
                Map(m => m.PhaseCreated).Index(29);
                Map(m => m.PhaseDemolished).Index(30);
            }
        }

        //WallSweepLog

        public class WallSweepLog : UpdatingLog
        {
            public WallSweepLog( ) { }
            [Index(7)]
            public string WallId { get; set; }
            [Index(8)]
            public bool CutsWall { get; set; }
            [Index(9)]
            public double DefaultSetback { get; set; }
            [Index(10)]
            public double Distance { get; set; }
            [Index(11)]
            public int DistanceMeasuredFrom { get; set; }
            [Index(12)]
            public int Id { get; set; }
            [Index(13)]
            public bool IsCutByInserts { get; set; }
            [Index(14)]
            public bool IsProfileFlipped { get; set; }
            [Index(15)]
            public bool IsVertical { get; set; }
            [Index(16)]
            public string MaterialId { get; set; }
            [Index(17)]
            public string ProfileId { get; set; }
            [Index(18)]
            public double WallOffset { get; set; }
            [Index(19)]
            public int WallSide { get; set; }
            [Index(20)]
            public int WallSweepOrientation { get; set; }
            [Index(21)]
            public int WallSweepType { get; set; }
            [Index(22)]
            public double OffsetFromWall { get; set; }
            [Index(23)]
            public string Level { get; set; }
            [Index(24)]
            public double OffsetFromLevel { get; set; }
            [Index(25)]
            public double Length { get; set; }
            [Index(26)]
            public string Image { get; set; }
            [Index(27)]
            public string Comments { get; set; }
            [Index(28)]
            public string Mark { get; set; }
            [Index(29)]
            public string PhaseCreated { get; set; }
            [Index(30)]
            public string PhaseDemolished { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.WallId, this.CutsWall, this.DefaultSetback, this.Distance, this.DistanceMeasuredFrom, this.Id, this.IsCutByInserts, this.IsProfileFlipped, this.IsVertical, this.MaterialId, this.ProfileId, this.WallOffset, this.WallSide, this.WallSweepOrientation, this.WallSweepType, this.OffsetFromWall, this.Level, this.OffsetFromLevel, this.Length, this.Image, this.Comments, this.Mark, this.PhaseCreated, this.PhaseDemolished);
            }
        }

        public sealed class WallSweepLogMap : ClassMap<WallSweepLog>
        {
            public WallSweepLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.WallId).Index(7);
                Map(m => m.CutsWall).Index(8);
                Map(m => m.DefaultSetback).Index(9);
                Map(m => m.Distance).Index(10);
                Map(m => m.DistanceMeasuredFrom).Index(11);
                Map(m => m.Id).Index(12);
                Map(m => m.IsCutByInserts).Index(13);
                Map(m => m.IsProfileFlipped).Index(14);
                Map(m => m.IsVertical).Index(15);
                Map(m => m.MaterialId).Index(16);
                Map(m => m.ProfileId).Index(17);
                Map(m => m.WallOffset).Index(18);
                Map(m => m.WallSide).Index(19);
                Map(m => m.WallSweepOrientation).Index(20);
                Map(m => m.WallSweepType).Index(21);
                Map(m => m.OffsetFromWall).Index(22);
                Map(m => m.Level).Index(23);
                Map(m => m.OffsetFromLevel).Index(24);
                Map(m => m.Length).Index(25);
                Map(m => m.Image).Index(26);
                Map(m => m.Comments).Index(27);
                Map(m => m.Mark).Index(28);
                Map(m => m.PhaseCreated).Index(29);
                Map(m => m.PhaseDemolished).Index(30);
            }
        }

        //RevealsLog

        public class RevealsLog : UpdatingLog
        {
            public RevealsLog( ) { }
            [Index(7)]
            public string WallId { get; set; }
            [Index(8)]
            public bool CutsWall { get; set; }
            [Index(9)]
            public double DefaultSetback { get; set; }
            [Index(10)]
            public double Distance { get; set; }
            [Index(11)]
            public int DistanceMeasureFrom { get; set; }
            [Index(12)]
            public int Id { get; set; }
            [Index(13)]
            public bool IsCutByInserts { get; set; }
            [Index(14)]
            public bool IsProfileFlipped { get; set; }
            [Index(15)]
            public bool IsVertical { get; set; }
            [Index(16)]
            public string MaterialId { get; set; }
            [Index(17)]
            public string ProfileId { get; set; }
            [Index(18)]
            public double WallOffset { get; set; }
            [Index(19)]
            public int WallSide { get; set; }
            [Index(20)]
            public int WallSweepOrientation { get; set; }
            [Index(21)]
            public int WallSweepType { get; set; }
            [Index(22)]
            public double OffsetFromWall { get; set; }
            [Index(23)]
            public string Level { get; set; }
            [Index(24)]
            public double OffsetFromLevel { get; set; }
            [Index(25)]
            public double Length { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25}", this.Timestamp, this.ElementId, this.CommandType, this.ElementCategory, this.ElementFamily, this.ElementType, this.WorksetId, this.WallId, this.CutsWall, this.DefaultSetback, this.Distance, this.DistanceMeasureFrom, this.Id, this.IsCutByInserts, this.IsProfileFlipped, this.IsVertical, this.MaterialId, this.ProfileId, this.WallOffset, this.WallSide, this.WallSweepOrientation, this.WallSweepType, this.OffsetFromWall, this.Level, this.OffsetFromLevel, this.Length);
            }
        }

        public sealed class RevealsLogMap : ClassMap<RevealsLog>
        {
            public RevealsLogMap( )
            {
                Map(m => m.Timestamp).Index(0);
                Map(m => m.ElementId).Index(1);
                Map(m => m.CommandType).Index(2);
                Map(m => m.ElementCategory).Index(3);
                Map(m => m.ElementFamily).Index(4);
                Map(m => m.ElementType).Index(5);
                Map(m => m.WorksetId).Index(6);
                Map(m => m.WallId).Index(7);
                Map(m => m.CutsWall).Index(8);
                Map(m => m.DefaultSetback).Index(9);
                Map(m => m.Distance).Index(10);
                Map(m => m.DistanceMeasureFrom).Index(11);
                Map(m => m.Id).Index(12);
                Map(m => m.IsCutByInserts).Index(13);
                Map(m => m.IsProfileFlipped).Index(14);
                Map(m => m.IsVertical).Index(15);
                Map(m => m.MaterialId).Index(16);
                Map(m => m.ProfileId).Index(17);
                Map(m => m.WallOffset).Index(18);
                Map(m => m.WallSide).Index(19);
                Map(m => m.WallSweepOrientation).Index(20);
                Map(m => m.WallSweepType).Index(21);
                Map(m => m.OffsetFromWall).Index(22);
                Map(m => m.Level).Index(23);
                Map(m => m.OffsetFromLevel).Index(24);
                Map(m => m.Length).Index(25);
            }
        }
        #endregion

        #region Define Placeholder Class
        //CurveArrArray, CurveLoop, Curve Classes

        public string GetProfileDescription( Sketch sketch )
        {
            //여기서 에러남
            CurveArrArray crvArrArr = sketch.Profile;

            string crvArrArrString = "Profile";

            foreach (CurveArray CrvArr in crvArrArr)
            {
                crvArrArrString += ", {CurveLoop";
                foreach (Curve wallCrv in CrvArr)
                {
                    crvArrArrString += ", [";
                    string description = GetCurveDescription(wallCrv).ToString();
                    crvArrArrString += description + "]";
                }
                crvArrArrString += "}";
            }
            return crvArrArrString;
        }

        public string GetCurveLoopDescription( CurveLoop curveLoop )
        {

            List<Curve> crvList = curveLoop.ToList();

            string crvLoopString = "CurveLoop";

            foreach (Curve crv in crvList)
            {
                crvLoopString += ", [";
                string description = GetCurveDescription(crv).ToString();
                crvLoopString += description + "]";
            }
            return crvLoopString;
        }

        public string GetCurveListDescription( List<Curve> crvList )
        {
            string crvListString = "CurveList";

            foreach (Curve crv in crvList)
            {
                crvListString += ", [";
                string description = GetCurveDescription(crv).ToString();
                crvListString += description + "]";
            }
            return crvListString;

        }

        public dynamic GetCurveDescription( Curve crv )
        {
            // 24.4.19. 작업

            JObject JCurve = new JObject();
            dynamic description;
            string typ = crv.GetType().Name;

            switch (typ)
            {
                case "Line":
                    // = new LineDescription() { Type = typ, StartPoint = "\"" + crv.GetEndPoint(0).ToString().Replace(" ", String.Empty) + "\"", EndPoint = "\"" + crv.GetEndPoint(1).ToString().Replace(" ", String.Empty) + "\"" };
                    JCurve.Add("Type", typ);
                    JCurve.Add("endPoints", new JArray(
                        new JObject(
                            new JProperty("X", crv.GetEndPoint(0).X),
                            new JProperty("Y", crv.GetEndPoint(0).Y),
                            new JProperty("Z", crv.GetEndPoint(0).Z)
                            ),
                        new JObject(
                            new JProperty("X", crv.GetEndPoint(1).X),
                            new JProperty("Y", crv.GetEndPoint(1).Y),
                            new JProperty("Z", crv.GetEndPoint(1).Z)
                            )
                        ));
                    break;

                case "Arc":

                    var arc = crv as Arc;
                    var arcCen = arc.Center;
                    var arcNorm = arc.Normal;
                    var rad = arc.Radius;
                    var arcXAxis = arc.XDirection;
                    var arcYAxis = arc.YDirection;
                    var plane = Plane.CreateByNormalAndOrigin(arcNorm, arcCen);
                    var startDir = (arc.GetEndPoint(0) - arcCen).Normalize();
                    var endDir = (arc.GetEndPoint(1) - arcCen).Normalize();
                    var startAngle = arcXAxis.AngleOnPlaneTo(startDir, arcNorm);
                    var endAngle = arcXAxis.AngleOnPlaneTo(endDir, arcNorm);

                    JCurve.Add("Type", typ);
                    JCurve.Add("center", new JObject(
                        new JProperty("X", arcCen.X),
                        new JProperty("Y", arcCen.Y),
                        new JProperty("Z", arcCen.Z)
                        ));
                    JCurve.Add("radius", rad);
                    JCurve.Add("startAngle", startAngle);
                    JCurve.Add("endAngle", endAngle);
                    JCurve.Add("xAxis", new JObject(
                        new JProperty("X", arcXAxis.X),
                        new JProperty("Y", arcXAxis.Y),
                        new JProperty("Z", arcXAxis.Z)
                        ));
                    JCurve.Add("yAxis", new JObject(
                        new JProperty("X", arcYAxis.X),
                        new JProperty("Y", arcYAxis.Y),
                        new JProperty("Z", arcYAxis.Z)
                        ));

                    //description = new ArcDescription() { Type = typ, Center = "\"" + arcCen.ToString().Replace(" ", String.Empty) + "\"", Radius = rad, StartAngle = startAngle, EndAngle = endAngle, xAxis = "\"" + arcXAxis.ToString().Replace(" ", String.Empty) + "\"", yAxis = "\"" + arcYAxis.ToString().Replace(" ", String.Empty) + "\"" };

                    break;

                case "Ellipse":
                    // 24.4.19. 작업 중
                    var ellip = crv as Ellipse;
                    var cen = ellip.Center;
                    var xRad = ellip.RadiusX;
                    var yRad = ellip.RadiusY;
                    var xAxis = ellip.XDirection;
                    var yAxis = ellip.YDirection;
                    var startParam = ellip.GetEndParameter(0);
                    var endParam = ellip.GetEndParameter(1);
                    description = new EllipseDescription() { Type = typ, Center = "\"" + cen.ToString().Replace(" ", String.Empty) + "\"", xRadius = xRad, yRadius = yRad, xAxis = "\"" + xAxis.ToString().Replace(" ", String.Empty) + "\"", yAxis = "\"" + yAxis.ToString().Replace(" ", String.Empty) + "\"", StartParameter = startParam, EndParameter = endParam };
                    break;

                case "CylindricalHelix":

                    var cylinHelix = crv as CylindricalHelix;
                    var basePoint = cylinHelix.BasePoint;
                    var radius = cylinHelix.Radius;
                    var xVector = cylinHelix.XVector;
                    var zVector = cylinHelix.ZVector;
                    var pitch = cylinHelix.Pitch;

                    var cylPlane = Plane.CreateByNormalAndOrigin(zVector, basePoint);
                    var cylStartDir = (cylinHelix.GetEndPoint(0) - basePoint).Normalize();
                    var cylEndDir = (cylinHelix.GetEndPoint(1) - basePoint).Normalize();

                    var cylStartAngle = cylStartDir.AngleOnPlaneTo(xVector, zVector);
                    var cylEndAngle = cylEndDir.AngleOnPlaneTo(xVector, zVector);

                    description = new CylindricalHelixDescription() { Type = typ, BasePoint = "\"" + basePoint.ToString().Replace(" ", String.Empty) + "\"", Radius = radius, xVector = "\"" + xVector.ToString().Replace(" ", String.Empty) + "\"", zVector = "\"" + zVector.ToString().Replace(" ", String.Empty) + "\"", Pitch = pitch, StartAngle = cylStartAngle, EndAngle = cylEndAngle };

                    break;

                case "HermiteSpline":

                    var herSpl = crv as HermiteSpline;
                    var contPts = herSpl.ControlPoints;
                    string stringConPts = "\"";
                    for (int i = 0 ; i < contPts.Count ; i++)
                    {
                        XYZ pt = contPts[i];
                        if (i != 0)
                        {
                            stringConPts += ";";
                        }
                        stringConPts += pt.ToString().Replace(" ", String.Empty);
                    }
                    var periodic = herSpl.IsPeriodic;
                    Int32 tangentCount = (herSpl.Tangents.Count - 1);
                    var startTangents = herSpl.Tangents[0].Normalize();
                    var endTangents = herSpl.Tangents[tangentCount].Normalize();
                    Debug.WriteLine("kk");
                    HermiteSplineTangents tangents = new HermiteSplineTangents() { StartTangent = startTangents, EndTangent = endTangents };
                    stringConPts += "\"";

                    string hermSplT = "\"" + tangents.StartTangent.ToString().Replace(" ", String.Empty) + ";" + tangents.EndTangent.ToString().Replace(" ", String.Empty) + "\"";

                    description = new HermiteSplineDescription() { Type = typ, ControlPoints = stringConPts, Periodic = periodic, HermiteSplineTangents = hermSplT };

                    break;

                case "NurbSpline":

                    var nurbsSpl = crv as NurbSpline;
                    var degree = nurbsSpl.Degree;

                    string knots = "\"";
                    for (int i = 0 ; i < nurbsSpl.Knots.OfType<double>().ToList().Count ; i++)
                    {
                        double knot = nurbsSpl.Knots.OfType<double>().ToList()[i];
                        if (i != 0)
                        {
                            knots += ";";
                        }
                        knots += knot;
                    }
                    knots += "\"";

                    string nurbsCtrlPts = "\"";
                    for (int i = 0 ; i < nurbsSpl.CtrlPoints.Count ; i++)
                    {
                        XYZ pt = nurbsSpl.CtrlPoints[i];
                        if (i != 0)
                        {
                            nurbsCtrlPts += ";";
                        }
                        nurbsCtrlPts += pt.ToString().Replace(" ", String.Empty);
                    }
                    nurbsCtrlPts += "\"";

                    string weights = "\"";
                    for (int i = 0 ; i < nurbsSpl.Weights.OfType<double>().ToList().Count ; i++)
                    {
                        double weight = nurbsSpl.Weights.OfType<double>().ToList()[i];
                        if (i != 0)
                        {
                            weights += ";";
                        }
                        weights += weight;
                    }
                    weights += "\"";


                    description = new NurbSplineDescription() { Type = typ, Degree = degree, Knots = knots, ControlPoints = nurbsCtrlPts, Weights = weights };

                    break;

                default:
                    description = null;
                    break;

            }
            return JCurve;
            //return description;
        }


        public class CurveDescription
        {
            public CurveDescription( ) { }
            public string GetStringDescription( )
            {
                string describe = "";
                switch (this.Type)
                {
                    case "Line":
                        describe = (this as LineDescription).ToString();
                        break;
                    case "Curve":
                        describe = (this as CurveDescription).ToString();
                        break;
                    case "Arc":
                        describe = (this as ArcDescription).ToString();
                        break;
                    case "Ellipse":
                        describe = (this as EllipseDescription).ToString();
                        break;
                    case "CylindricalHelix":
                        describe = (this as CylindricalHelixDescription).ToString();
                        break;
                    case "HermiteSpline":
                        describe = (this as HermiteSplineDescription).ToString();
                        break;
                    case "NurbSpline":
                        describe = (this as NurbSplineDescription).ToString();
                        break;
                }
                return describe;
            }
            [Index(0)]
            public string Type { get; set; }
        }

        public class LineDescription : CurveDescription
        {
            public LineDescription( ) { }
            [Index(1)]
            public string StartPoint { get; set; }
            [Index(2)]
            public string EndPoint { get; set; }

            public override string ToString( )
            {
                return String.Format("{0},{1},{2}",
                    this.Type, this.StartPoint, this.EndPoint);
            }
        }
        public sealed class LineDescriptionMap : ClassMap<LineDescription>
        {
            public LineDescriptionMap( )
            {
                Map(m => m.Type).Index(0);
                Map(m => m.StartPoint).Index(1);
                Map(m => m.EndPoint).Index(2);
            }
        }

        public class ArcDescription : CurveDescription
        {
            public ArcDescription( ) { }
            [Index(1)]
            public string Center { get; set; }
            [Index(2)]
            public double Radius { get; set; }
            [Index(3)]
            public double StartAngle { get; set; }
            [Index(4)]
            public double EndAngle { get; set; }
            [Index(5)]
            public string xAxis { get; set; }
            [Index(6)]
            public string yAxis { get; set; }


            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6}]",
                    this.Type, this.Center, this.Radius, this.StartAngle, this.EndAngle, this.xAxis, this.yAxis);
            }
        }
        public sealed class ArcDescriptionMap : ClassMap<ArcDescription>
        {
            public ArcDescriptionMap( )
            {
                Map(m => m.Type).Index(0);
                Map(m => m.Center).Index(1);
                Map(m => m.Radius).Index(2);
                Map(m => m.StartAngle).Index(3);
                Map(m => m.EndAngle).Index(4);
                Map(m => m.xAxis).Index(5);
                Map(m => m.yAxis).Index(6);
            }
        }

        public class EllipseDescription : CurveDescription
        {
            public EllipseDescription( ) { }
            [Index(1)]
            public string Center { get; set; }
            [Index(2)]
            public double xRadius { get; set; }
            [Index(3)]
            public double yRadius { get; set; }
            [Index(4)]
            public string xAxis { get; set; }
            [Index(5)]
            public string yAxis { get; set; }
            [Index(6)]
            public double StartParameter { get; set; }
            [Index(7)]
            public double EndParameter { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                    this.Type, this.Center, this.xRadius, this.yRadius, this.xAxis, this.yAxis, this.StartParameter, this.EndParameter);
            }
            public sealed class EllipseDescriptionMap : ClassMap<EllipseDescription>
            {
                public EllipseDescriptionMap( )
                {
                    Map(m => m.Type).Index(0);
                    Map(m => m.Center).Index(1);
                    Map(m => m.xRadius).Index(2);
                    Map(m => m.yRadius).Index(3);
                    Map(m => m.xAxis).Index(4);
                    Map(m => m.yAxis).Index(5);
                    Map(m => m.StartParameter).Index(6);
                    Map(m => m.EndParameter).Index(7);
                }
            }
        }

        public class CylindricalHelixDescription : CurveDescription
        {
            public CylindricalHelixDescription( ) { }
            [Index(1)]
            public String BasePoint { get; set; }
            [Index(2)]
            public Double Radius { get; set; }
            [Index(3)]
            public string xVector { get; set; }
            [Index(4)]
            public string zVector { get; set; }
            [Index(5)]
            public double Pitch { get; set; }
            [Index(6)]
            public double StartAngle { get; set; }
            [Index(7)]
            public double EndAngle { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                    this.Type, this.BasePoint, this.Radius, this.xVector, this.zVector, this.Pitch, this.StartAngle, this.EndAngle);
            }
        }
        public sealed class CylindricalHelixDescriptionMap : ClassMap<CylindricalHelixDescription>
        {
            public CylindricalHelixDescriptionMap( )
            {
                Map(m => m.Type).Index(0);
                Map(m => m.BasePoint).Index(1);
                Map(m => m.Radius).Index(2);
                Map(m => m.xVector).Index(3);
                Map(m => m.zVector).Index(4);
                Map(m => m.Pitch).Index(5);
                Map(m => m.StartAngle).Index(6);
                Map(m => m.EndAngle).Index(7);
            }
        }

        public class HermiteSplineDescription : CurveDescription
        {
            public HermiteSplineDescription( ) { }
            [Index(1)]
            public string ControlPoints { get; set; }
            [Index(2)]
            public bool Periodic { get; set; }
            [Index(3)]
            public string HermiteSplineTangents { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3}",
                    this.Type, this.ControlPoints, this.Periodic, this.HermiteSplineTangents);
            }
        }
        public sealed class HermiteSplineDescriptionMap : ClassMap<HermiteSplineDescription>
        {
            public HermiteSplineDescriptionMap( )
            {
                Map(m => m.Type).Index(0);
                Map(m => m.ControlPoints).Index(1);
                Map(m => m.Periodic).Index(2);
                Map(m => m.HermiteSplineTangents).Index(3);
            }
        }

        public class NurbSplineDescription : CurveDescription
        {
            public NurbSplineDescription( ) { }
            [Index(1)]
            public int Degree { get; set; }
            [Index(2)]
            public string Knots { get; set; }
            [Index(3)]
            public string ControlPoints { get; set; }
            [Index(4)]
            public string Weights { get; set; }
            public override string ToString( )
            {
                return String.Format("{0},{1},{2},{3},{4}",
                    this.Type, this.Degree, this.Knots, this.ControlPoints, this.Weights);
            }
        }
        public sealed class NurbSlineDescriptionMap : ClassMap<NurbSplineDescription>
        {
            public NurbSlineDescriptionMap( )
            {
                Map(m => m.Type).Index(0);
                Map(m => m.Degree).Index(1);
                Map(m => m.Knots).Index(2);
                Map(m => m.ControlPoints).Index(3);
                Map(m => m.Weights).Index(4);
            }
        }

        //Plane Description
        public string GetPlaneDescription( Plane plane )
        {
            return plane.Origin.ToString().Replace(" ", string.Empty) + ";" + plane.XVec.ToString().Replace(" ", string.Empty) + ";" + plane.YVec.ToString().Replace(" ", string.Empty);
        }
        public string GetXYZDescription( XYZ xyz )
        {
            return xyz.ToString().Replace(" ", String.Empty);
        }

        public string GetIdListDescription( List<ElementId> elementIds )
        {
            string description = "[";
            for (int i = 0 ; i < elementIds.Count ; i++)
            {
                if (i == 0)
                {
                    description += elementIds[i].ToString();
                }
                else
                {
                    description += "," + elementIds[i].ToString();
                }
            }
            description += "]";

            return description;
        }

        //public string GetRooms( Element element )
        // {
        //    string roomString = "[";
        //    try
        //    {

        //    Document doc = element.Document;
        //    BoundingBoxXYZ element_bb = element.get_BoundingBox(null);
        //    Outline outline = new Outline(element_bb.Min, element_bb.Max);
        //    BoundingBoxIntersectsFilter bbfilter = new BoundingBoxIntersectsFilter(outline);
        //    List<Element> room_elems = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WherePasses(bbfilter).ToElements().ToList();

        //    for (int i = 0 ; i < room_elems.Count ; i++)
        //    {
        //        ElementId eid = room_elems[i].Id;
        //        if (i != 0)
        //        {
        //            roomString += ";";
        //        }
        //        roomString += eid.ToString();
        //    }
        //    roomString += "]";

        //    if (roomString == "[]")
        //    {
        //        roomString = null;
        //    }

        //    }
        //        catch(Exception ex)
        //    {
        //        Debug.WriteLine(ex);
        //    }
        //    return roomString;
        //}
        #endregion



    }
}