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

namespace NLog_logstashProj
{
    class Program
    {
        const double NUM_OF_BYTES_IN_MB = 1024 * 1024;
        /*TODO:
         * 1) In the fileName of Nlog, write s.t. the filename will be the Epoch time. Note: There's a chance this entails writing a custom NLog.LayoutRenderer
         * 2) archiveEvery custom value (e.g. every 12 hours)
         * 3) Because of the log fileName formatting (currently ${date:format=yyyy-MM-dd}) need to check what happens when day changes (at midnight) - are files 
         * (or log entries) that are written just before midnight getting archived
         */
        public const string sampleNLogXMLConfig = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<nlog xmlns = ""http://www.nlog-project.org/schemas/NLog.xsd""
      xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">

    <targets>

    <target name=""jsonFile"" 
            type=""File""
            fileName=""logs/loggingPoc2.json""
            archiveOldFileOnStartup=""true""        
            archiveAboveSize=""10240""
            createDirs=""true""
            archiveFileName=""readyToBeSent/archived_{#}.log""
            archiveNumbering=""DateAndSequence""
            archiveEvery=""Minute""
            concurrentWrites=""false""
    >
          <layout xsi:type=""JsonLayout"">
              <attribute name = ""time"" layout=""${longdate}"" />
              <attribute name = ""level"" layout=""${level:upperCase=true}""/>
              <attribute name = ""message"" layout=""${message}"" />
       </layout>
</target>
    </targets>

    <rules>
        <logger name = ""*"" minlevel=""Debug"" writeTo=""jsonFile"" />
    </rules>
</nlog>";
        /*
         *         public const string sampleNLogXMLConfig = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<nlog xmlns = ""http://www.nlog-project.org/schemas/NLog.xsd""
      xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
     
    <targets>
        <target name = ""logfile"" xsi:type=""File"" fileName=""${ticks}.log"" layout=""${longdate}|${level:uppercase=true}|${logger}|${message}""/>
    </targets>

    <rules>
        <logger name = ""*"" minlevel=""Info"" writeTo=""logconsole"" />
        <logger name = ""*"" minlevel=""Debug"" writeTo=""logfile"" />
    </rules>
</nlog>";
         */
        public static readonly IList<string> DEBUG_LEVELS = new ReadOnlyCollection<string>
            (new List<String> { "Debug", "Verbose", "Info", "Warn", "Error", "Critical" });
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
                timestamp = getCurrentTime();
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


            public static double getCurrentTime()
            {
                DateTime dt1970 = new DateTime(1970, 1, 1);
                DateTime current = DateTime.Now;//DateTime.UtcNow for unix timestamp
                TimeSpan span = current - dt1970;
                return span.TotalMilliseconds;
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


            //Configuring NLog from xml string:
            StringReader sr = new StringReader(sampleNLogXMLConfig);
            XmlReader xr = XmlReader.Create(sr);
            XmlLoggingConfiguration config = new XmlLoggingConfiguration(xr, null, false);
            LogManager.Configuration = config;
            //NLog is now configured just as if the XML above had been in NLog.config or app.config

            uint numOfLogWrites = 2;
            List<Task> tasks = new List<Task>();
            Random rnd = new Random();
            for (int i = 0; i < numOfLogWrites; i++)
            {
                Task curTask =
                new Task((object state) =>
                {
                    int index = (int)state;
                    int msgSizeInBytes = rnd.Next(1, 60);
                    int alphabetRange = (int)'z' - (int)'a';
                    char c = 'a';
                    c += (char)(index % alphabetRange);
                    string content = new string(c, msgSizeInBytes * 1024);
                    string entryBody = $"logMsg#{index}{Environment.NewLine}{content}";

                    //logEntry.AddProps(new Dictionary<string, object> { { $"prop{index}", $"val{index}" } });
                    if (index % 2 == 0)
                    {
                        writeToLog(entryBody);
                    }
                    else
                    {
                        Exception dummyEx = new Exception($"dummyEx#{index}");
                        writeToLog(dummyEx, entryBody);

                    }
                }, (object)i);
                curTask.Start(TaskScheduler.Default);
                tasks.Add(curTask);
                if (i % 2 == 0)
                {
                    int sleepTime = rnd.Next(0, 1500);
                    Console.WriteLine($"Sleeping for {sleepTime} ms");
                    System.Threading.Thread.Sleep(sleepTime);
                }
            }
            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("FIN");
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

        public static void writeToLog(Exception logEx, string msg, string logLevel = "info", Dictionary<string, string> additionalParams = null)
        {
            try
            {
                addOrUpdateAttributesToJsonFile(additionalParams);
                if (String.Equals(logLevel, "info"))
                {
                    logger.Info(logEx, msg);
                    //var s = Newtonsoft.Json.JsonConvert.SerializeObject(logger);

                }
                else
                {
                    logger.Warn(logEx, msg);
                }
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
        public static void writeToLog(string msg, string logLevel = "info", Dictionary<string, string> additionalParams = null)
        {
            try
            {
                addOrUpdateAttributesToJsonFile(additionalParams);
                if (String.Equals(logLevel, "info"))
                {
                    logger.Info(msg);
                    //var s = Newtonsoft.Json.JsonConvert.SerializeObject(logger);

                }
                else
                {
                    logger.Warn(msg);
                }
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

    }
}
