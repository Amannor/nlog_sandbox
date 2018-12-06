using NLog;
using NLog.Config;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using NLog.Targets;
using NLog.Layouts;
using System.Threading;

namespace NLog_logstashProj
{
    class Program
    {
        public enum Severity { Debug = 1, Verbose = 2, Info = 3, Warn = 4, Error = 5, Critical = 6, Warning = Warn, Fatal = Critical }; //From https://coralogix.com/integrations/coralogix-rest-api/

        /*TODO:
         * 1) In the fileName of Nlog, write s.t. the filename will be the Epoch time. Note: There's a chance this entails writing a custom NLog.LayoutRenderer
         * 2) archiveEvery custom value (e.g. every 12 hours)
         * 3) Because of the log fileName formatting (currently ${date:format=yyyy-MM-dd}) need to check what happens when day changes (at midnight) - are files 
         * (or log entries) that are written just before midnight getting archived
         */
        /*
         * Excpetion writing config:
           <target name="errors" xsi:type="File" layout="
           ${message}
           ${onexception:EXCEPTION OCCURRED\:${exception:format=type,message,method:maxInnerExceptionLevel=5:innerFormat=shortType,message,method}}"
           fileName="\Logs\errors-${shortdate}.log"
           concurrentWrites="true"
           />
 </targets>
         */
        public static string sampleNLogXMLConfig = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<nlog xmlns = ""http://www.nlog-project.org/schemas/NLog.xsd""
      xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">

    <targets>

    <target name=""jsonFile"" 
            type=""File""
            fileName=""logs/loggingPoc___AppDomainId___.log""
            
            archiveAboveSize=""4096000000""
            createDirs=""true""
            maxArchiveFiles=""1""
            concurrentWrites=""false""
            keepFileOpen =""true""
            openFileCacheTimeout =""30""
            cleanupFileName =""false""
            autoFlush=""false""
            openFileFlushTimeout =""1""

            enableArchiveFileCompression =""true""
    >
          <layout xsi:type=""JsonLayout"">
            <attribute name='msg' encode='false'>
                <layout type='JsonLayout'>
                  
                  <attribute name = ""Logger"" layout=""${logger}"" />
                  <attribute name = ""entryBody"" layout=""${message}"" />

                  <attribute name = ""GlobalContextItem"" layout=""${gdc:item=globalItemVal}""/>

                  <attribute name = ""mdcItem"" layout=""${mdc:item=mdcItemVal}""/>

                  <attribute name = ""mdlcItem"" layout=""${mdlc:item=mdlcItemVal}""/>

                   <attribute name = ""myTimestamp"" layout=""${event-properties:item=epochInMs}"" />
                  <attribute name = ""Severity"" layout=""${event-properties:item=coralogixSeverityMapping}""/>
                </layout>
            </attribute>
          </layout>
</target>
    </targets>

    <rules>
        <logger name = ""*"" minlevel=""Debug"" writeTo=""jsonFile"" />
    </rules>
</nlog>";
        /*  throwConfigExceptions="true"
         *  archiveOldFileOnStartup=""true""        
         *  archiveFileName=""archive/{#}.log""
            archiveNumbering=""DateAndSequence""
            archiveEvery=""Day""
            <attribute name = ""Timestamp"" layout=""${longdate}"" />
         *         public const string sampleNLogXMLConfig = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<nlog xmlns = ""http://www.nlog-project.org/schemas/NLog.xsd""
      xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
     
    <targets>
        <target name = ""logfile"" xsi:type=""File"" fileName=""${ticks}.log"" layout=""${longdate}|${level:uppercase=true}|${logger}|${message}""/>
    </targets>
      <target name="f" type="File" layout="${message}${onexception:EXCEPTION OCCURRED\:${exception:format=tostring}}" />

    <rules>
        <logger name = ""*"" minlevel=""Info"" writeTo=""logconsole"" />
        <logger name = ""*"" minlevel=""Debug"" writeTo=""logfile"" />
    </rules>
</nlog>";
         */
        //public static readonly IList<string> DEBUG_LEVELS = new ReadOnlyCollection<string>
        //    (new List<String> { "Debug", "Verbose", "Info", "Warn", "Error", "Critical" });
        public class sampleLogEntry
        {
            [JsonProperty]
            public double timestamp { get; set; }
            [JsonProperty]
            public int threadId { get; set; }
            [JsonProperty]
            public string entryBody { get; set; }
            [JsonProperty]
            public string severity { get; set; }
            private Dictionary<string, object> generalProps;
            public sampleLogEntry()
            {
                timestamp = getCurrentTimeInSeconds();
                generalProps = new Dictionary<string, object>();
            }

            public void AddProps(Dictionary<string, object> propsToAdd)
            {
                foreach (KeyValuePair<string, object> prop in propsToAdd)
                {
                    generalProps[prop.Key] = prop.Value;
                }
            }

            public void GetReadyToBeSent()
            {
                if (entryBody == null)
                {
                    entryBody = "";
                }
                if (generalProps.Count >= 1)
                {
                    entryBody += JsonConvert.SerializeObject(generalProps);
                }
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            }


            
        }


        static Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static void Main(string[] args)
        {


            //Configuring NLogfrom code:
            //var config = new NLog.Config.LoggingConfiguration();
            //var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "file.txt" };
            //var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            //config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            //config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
            //NLog.LogManager.Configuration = config;

            Random rnd = new Random();

            string curRunId = $"ThreadId {Thread.CurrentThread.ManagedThreadId} AppDomainId {AppDomain.CurrentDomain.Id}";
            string curTime = DateTime.Now.ToString("h:mm:ss tt");
            Console.WriteLine($"Run {curRunId} - Start. {curTime}");

            //int sleepTime = rnd.Next(1000, 2000);
            //Console.WriteLine($"Sleeping for {sleepTime} ms");
            //System.Threading.Thread.Sleep(sleepTime);
            
            //Configuring NLog from xml string:
            sampleNLogXMLConfig = sampleNLogXMLConfig.Replace("___AppDomainId___", Convert.ToString(AppDomain.CurrentDomain.Id));
            StringReader sr = new StringReader(sampleNLogXMLConfig);
            XmlReader xr = XmlReader.Create(sr);
            XmlLoggingConfiguration config = new XmlLoggingConfiguration(xr, null, false);
            LogManager.Configuration = config;
            //NLog is now configured just as if the XML above had been in NLog.config or app.config

            uint numOfLogWrites = 5;
            List<Task> tasks = new List<Task>();

            GlobalDiagnosticsContext.Set("globalItemVal", "This is a global param val");

            for (int i = 0; i < numOfLogWrites; i++)
            {
                Task curTask =
                new Task((object state) =>
                {
                    int index = (int)state;
                    //int msgSizeInBytes = rnd.Next(1, 60);
                    int alphabetRange = (int)'z' - (int)'a';
                    char c = 'a';
                    c += (char)(index % alphabetRange);
                    //string content = new string(c, 15 * 1024);
                    string content ="aaa";
                    double currentTimeInSeconds = getCurrentTimeInSeconds();
                    string entryBody = $"logMsg#{index} time:{currentTimeInSeconds}{Environment.NewLine}{content}";

                    //logEntry.AddProps(new Dictionary<string, object> { { $"prop{index}", $"val{index}" } });
                    
                    if (index % 2 == 0)
                    {
                        if(index == 2)
                        {
                            MappedDiagnosticsContext.Set("mdcItemVal", null);
                        }
                        else
                        {
                            MappedDiagnosticsContext.Set("mdcItemVal", $"This is a MappedDiagnosticsContext param val, index {index}");
                        }
                    }
                    else if (index % 3 == 0)
                    {
                        MappedDiagnosticsLogicalContext.Set("mdlcItemVal", $"This is a MappedDiagnosticsLogicalContext param val, index {index}");
                    }
                    writeToLog(entryBody);

                    //exception write tst:
                    //else
                    //{
                    //    Exception dummyEx = new Exception($"dummyEx#{index}");
                    //    writeToLog(dummyEx, entryBody);

                    //}
                }, (object)i);
                curTask.Start(TaskScheduler.Default);
                tasks.Add(curTask);
                //if (i % 2 == 0)
                //{
                //    int sleepTime = rnd.Next(0, 1500);
                //    Console.WriteLine($"Sleeping for {sleepTime} ms");
                //    System.Threading.Thread.Sleep(sleepTime);
                //}
            }
            Task.WaitAll(tasks.ToArray());
            curTime = DateTime.Now.ToString("h:mm:ss tt");
            Console.WriteLine($"Run ${curRunId} - End. {curTime}");

        }

        private static void addOrUpdateAttributesToJsonFile(Dictionary<string, string> additionalParams)
        {
            if (additionalParams == null || additionalParams.Count() == 0)
            {
                return;
            }
            FileTarget target = (FileTarget)LogManager.Configuration.FindTargetByName("jsonFile");
            JsonLayout curlayout = (JsonLayout)target.Layout;
            //HashSet<JsonAttribute> curAttributes = new HashSet<JsonAttribute> ( curlayout.Attributes ); //HashSet due to time efficiency considerations

            foreach (KeyValuePair<string, string> param in additionalParams)
            {
                JsonAttribute attr = curlayout.Attributes.FirstOrDefault(a => String.Equals(a.Name, param.Key, StringComparison.OrdinalIgnoreCase));
                if (attr != null)
                {
                    curlayout.Attributes.Remove(attr);
                }
                curlayout.Attributes.Add(new JsonAttribute(param.Key, param.Value));
            }
            LogManager.ReconfigExistingLoggers();
            //TODO: foreach config param check if value changed and don't call LogManager.ReconfigExistingLoggers() if no value changed


        }

        /// <summary>
        /// Note: current implementation is only for .Net Framework >=4.5 (see: https://stackoverflow.com/questions/9293227/how-to-check-if-ioexception-is-not-enough-disk-space-exception-type)
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private static bool IsDiskFull(Exception ex)
        {

            const int HR_ERROR_HANDLE_DISK_FULL = unchecked((int)0x80070027);
            const int HR_ERROR_DISK_FULL = unchecked((int)0x80070070);

            return ex.HResult == HR_ERROR_HANDLE_DISK_FULL
                || ex.HResult == HR_ERROR_DISK_FULL;
        }

        public static void writeToLog(string msg, string logLevel = "warn", Dictionary<string, string> additionalParams = null, Exception logEx = null)
        {
            try
            {
                addOrUpdateAttributesToJsonFile(additionalParams);
                LogEventInfo theEvent = new LogEventInfo(LogLevel.FromString(logLevel), logger.Name, msg);
                theEvent.Properties["epochInMs"] = getCurrentTimeInSeconds();// coralogixSeverityMapping

                int coralogixSeverityMapping = (int)Enum.Parse(typeof(Severity), logLevel, true);
                theEvent.Properties["coralogixSeverityMapping"] = coralogixSeverityMapping;

                logger.Log(theEvent);
            }
            catch (IOException ex)
            {
                if (IsDiskFull(ex))
                {
                    /*TODO:
                     * 1) Check if we have log files in this folder:
                     * 1.1) If so: Delete file\s according to LRU & then retry log it 
                     * 1.2) Else: Log it to eventviewer and throw Exception as is (by "throw;")
                     */
                }
            }
            catch (Exception ex)
            {
                //Idk what do we wanna do here? I think throw as is
            }

        }

        public static void writeToLog(sampleLogEntry logEntry, string logLevel = "info")
        {
            logEntry.GetReadyToBeSent();
            writeToLog(JsonConvert.SerializeObject(logEntry), logLevel);
        }
        public static double getCurrentTimeInSeconds()
        {
            DateTime dt1970 = new DateTime(1970, 1, 1);
            DateTime current = DateTime.Now;//DateTime.UtcNow for unix timestamp
            TimeSpan span = current - dt1970;
            return span.TotalSeconds;
        }
        public static double getCurrentTimeInMS()
        {
            DateTime dt1970 = new DateTime(1970, 1, 1);
            DateTime current = DateTime.Now;//DateTime.UtcNow for unix timestamp
            TimeSpan span = current - dt1970;
            return span.TotalMilliseconds;
        }
    }
}
