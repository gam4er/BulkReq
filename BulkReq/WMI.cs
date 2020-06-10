using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Generic;
using Microsoft.Management.Infrastructure.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BulkReq
{
    class WMI
    {
        public WMI(WMIOptions opts)
        {
            RunWMI(opts);
        }

        public static async Task OneTaskQueryProcessAsync(CimSession session)
        {
            var instanceObject = new CimInstanceWatcher();
            var AR = AsyncListRunningProcesses(session).Subscribe(instanceObject);
            //instanceObject.OnCompleted();
            instanceObject.WaitForCompletion();
            instanceObject.Dispose();
            AR.Dispose();
            
            //instanceObject = null;
        }

        public static List<Task> RemoveFinishedTaskAndAddNew(CimSession session, List<Task> WMITasks)
        {
            WMITasks.Any(n => {
                if (n.IsCanceled || n.IsCompleted || n.IsFaulted)
                {
                    n.Dispose();
                    WMITasks[WMITasks.IndexOf(n)] = OneTaskQueryProcessAsync(session);
                    return true;
                }
                else
                    return false;
            }
            );
            return WMITasks;
        }

        public static void GetRunningProcessesAsync(CimSession session, int Threads)
        {
            // ***Create a query that, when executed, returns a collection of tasks.
            //IEnumerable<Task> TasksQuery = from i in Enumerable.Range(0, Threads) select OneTaskQueryProcessAsync(session);
            
            // ***Use ToList to execute the query and start the tasks.
            List<Task> WMITasks = (from i in Enumerable.Range(0, Threads) select OneTaskQueryProcessAsync(session)).ToList();

            // ***Add a loop to process the tasks one at a time until none remain.
            while (WMITasks.Count > 0)
            {
                // Identify the first task that completes.
                /*Task firstFinishedTask =*/
                Task.WhenAny(WMITasks).Wait();
                WMITasks = RemoveFinishedTaskAndAddNew(session, WMITasks);
                // ***Remove the selected task from the list so that you don't
                // process it more than once.
                //WMITasks.Remove(firstFinishedTask);

                // and add another one
                //firstFinishedTask = OneTaskQueryProcessAsync(session);
                //WMITasks.Add(firstFinishedTask);
            }
        }


        public static Task OneTaskQueryProcessSync(CimSession session)
        {
            //ListRunningProcesses(session);
            Task t = new Task (() => { ListRunningProcesses(session); });
            t.Start();
            return t;
        }

        public static void GetRunningProcessesSync(CimSession session, int Threads)
        {
            //DelegateListRunningProcesses @delegate = ListRunningProcesses;

            // ***Create a query that, when executed, returns a collection of tasks.
            IEnumerable<Task> TasksQuery = from i in Enumerable.Range(0, Threads) select OneTaskQueryProcessSync(session);                                           

            // ***Use ToList to execute the query and start the tasks.
            List<Task> WMITasks = TasksQuery.ToList();
            //WMITasks.ForEach(t => t.Start());


            // ***Add a loop to process the tasks one at a time until none remain.

            for (; ; )
            {
                for (int i = 0; i < WMITasks.Count; i++)
                {
                    if (WMITasks[i].IsCanceled || WMITasks[i].IsCompleted || WMITasks[i].IsFaulted)
                    {
                        WMITasks[i].Dispose();
                        WMITasks.RemoveAt(i);
                        WMITasks.Add( OneTaskQueryProcessSync(session));
                        //WMITasks[i].Start();
                    }
                }
                Thread.Sleep(200);
            }
        }

        public static void PrintCimException(CimException exception)
        {
            Console.WriteLine("Error Code = " + exception.NativeErrorCode);
            Console.WriteLine("MessageId = " + exception.MessageId);
            Console.WriteLine("ErrorSource = " + exception.ErrorSource);
            Console.WriteLine("ErrorType = " + exception.ErrorType);
            Console.WriteLine("Status Code = " + exception.StatusCode);
        }

        public static int RunWMI(WMIOptions opts) 
        {
            CimSession session = CreateSession(opts.Host, opts.DCOM);
            ListBasicInfo(session);

            if (opts.AsyncOnly)
            {
                Task t = Task.Run(() => GetRunningProcessesAsync(session, opts.Threads));
                if (opts.Minutes > 0)
                {
                    TimeSpan ts = TimeSpan.FromMilliseconds(opts.Minutes * 1000 * 60);
                    if (!t.Wait(ts))
                        Console.WriteLine("The timeout interval elapsed.");
                }
                else
                {
                    t.Wait();
                }
            }
            else
            {
                Task t = Task.Run(() => GetRunningProcessesSync(session, opts.Threads));
                if (opts.Minutes > 0)
                {
                    TimeSpan ts = TimeSpan.FromMilliseconds(opts.Minutes * 1000 * 60);
                    if (!t.Wait(ts))
                        Console.WriteLine("The timeout interval elapsed.");
                }
                else
                {
                    t.Wait();
                }
            }
            return 0;
        }

        static CimSession CreateSession(string host, bool DCOM)
        {
            Console.WriteLine("Querying localhost");
            //
            if (DCOM)
            {
                var so = new DComSessionOptions
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                return CimSession.Create(host, so);
            }
            else
            {
                CimSessionOptions so = new CimSessionOptions();
                return CimSession.Create(host, so);
            }
        }

        class CimInstanceWatcher : IObserver<CimInstance>
        {
            private readonly ManualResetEventSlim doneEvent = new ManualResetEventSlim(false);

            public void OnCompleted()
            {
                //throw new NotImplementedException();
                //var t = this.GetType();
                this.doneEvent.Set();

            }

            public void WaitForCompletion()
            {
                this.doneEvent.Wait();
            }

            public void OnError(Exception error)
            {
                CimException cimException = error as CimException;
                if (cimException != null)
                {
                    PrintCimException(cimException);
                }
                else
                {
                    throw error;
                }

                this.doneEvent.Set();
            }

            public void OnNext(CimInstance value)
            {
                //Console.WriteLine("Value: " + value);
                try
                {
                    var s1 = value.CimInstanceProperties["ProcessID"].Value;
                    var s2 = value.CimInstanceProperties["ParentProcessID"].Value;
                    var s3 = value.CimInstanceProperties["Name"].Value;
                    Console.WriteLine("{0,-10} {1,-10} {2,5:1}", s1, s2, s3);
                    //session.InvokeMethod(item, "GetOwner", null).OutParameters["User"].Value);
                }
                catch (Exception ex) {
                }
            }

            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (disposed)
                {
                    return;
                }

                if (disposing)
                {
                    doneEvent.Dispose();
                }

                disposed = true;
            }

            private bool disposed = true;

        }
        static CimAsyncMultipleResults<CimInstance> AsyncListRunningProcesses(CimSession session)
        {
            Console.WriteLine("{0,-10} {1,-10} {2,4:1}", "PID", "PPID", "Name");
            return session.QueryInstancesAsync(@"root\cimv2", "WQL", "SELECT * FROM Win32_Process");
        }

        #region WMIActivities
        static void ListBasicInfo(CimSession session)
        {
            var query = session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_ComputerSystem");
            foreach (CimInstance item in query)
            {
                Console.Write("Hostname: {0} Domain: {1} ",
                item.CimInstanceProperties["Name"].Value,
                item.CimInstanceProperties["Domain"].Value);
            }

            query = session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_OperatingSystem");
            foreach (CimInstance item in query)
            {
                Console.WriteLine("Version: {0}", item.CimInstanceProperties["Version"].Value);
            }
        }
        static void ListRunningProcesses(CimSession session)
        {
            var query = session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_Process");

            Console.WriteLine("{0,-10} {1,-10} {3,-20} {2,4:1}", "PID", "PPID", "Name", "Owner");
            foreach (CimInstance item in query)
            {

                try
                {
                    Console.WriteLine("{0,-10} {1,-10} {3,-20} {2,5:1}",
                        item.CimInstanceProperties["ProcessID"].Value,
                        item.CimInstanceProperties["ParentProcessID"].Value,
                        item.CimInstanceProperties["Name"].Value,
                        session.InvokeMethod(item, "GetOwner", null).OutParameters["User"].Value);
                }
                catch (Exception ex) { }
            }
        }
        static void ListRunningServices(CimSession session)
        {
            var query = session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_Service");
            Console.WriteLine("{0,-50} {1,-10} {2,-10} {3,-10}", "Name", "State", "Mode", "Path");
            foreach (CimInstance item in query)
            {
                Console.WriteLine("{0,-50} {1,-10} {2,-10} {3,-10}",
                    item.CimInstanceProperties["Name"].Value,
                    item.CimInstanceProperties["State"].Value,
                    item.CimInstanceProperties["StartMode"].Value,
                    item.CimInstanceProperties["Pathname"].Value);
            }
        }
        static void ListSystemDrives(CimSession session)
        {
            var query = session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_LogicalDisk");
            foreach (CimInstance item in query)
            {
                Console.WriteLine("{0} {1}{2}",
                    item.CimInstanceProperties["DeviceId"].Value,
                    item.CimInstanceProperties["VolumeName"].Value,
                    item.CimInstanceProperties["ProviderName"].Value);
            }
        }
        static void ListActiveNICs(CimSession session)
        {
            var query = session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Win32_NetworkadApterConfiguration");
            foreach (CimInstance item in query)
            {
                string[] ipaddrs = (string[])(item.CimInstanceProperties["IPAddress"].Value);
                if (ipaddrs != null)
                {
                    Console.WriteLine("{0}:\n IP: {1}\n GW: {2}",
                    item.CimInstanceProperties["ServiceName"].Value,
                    ipaddrs[0],
                    ((string[])item.CimInstanceProperties["DefaultIPGateway"].Value)[0]);
                }
            }
        }
        static void ListAntiVirus(CimSession session)
        {
            // https://social.msdn.microsoft.com/Forums/en-US/6501b87e-dda4-4838-93c3-244daa355d7c/wmisecuritycenter2-productstate
            var avEnabled = new Dictionary<int, string>() {
                {11, "Enabled"},
                {10, "Enabled"},
                {01, "Disabled"},
                {00, "Disabled"}
            };
            var avUpdated = new Dictionary<int, string>() {
                {00, "up to date"},
                {10, "out of date"}
            };
            string hexState = "";

            var query = session.QueryInstances(@"root\SecurityCenter", "WQL", "SELECT * FROM AntiVirusProduct");
            foreach (CimInstance item in query)
            {
                Console.Write("{0}: ", item.CimInstanceProperties["displayName"].Value);
                hexState = (Convert.ToInt32((item.CimInstanceProperties["productState"].Value).ToString())).ToString("X");
                Console.WriteLine(avEnabled[Int16.Parse(hexState.Substring(1, 2))] + " and " + avUpdated[Int16.Parse(hexState.Substring(3, 2))]);
            }
            query = session.QueryInstances(@"root\SecurityCenter2", "WQL", "SELECT * FROM AntiVirusProduct");
            foreach (CimInstance item in query)
            {
                Console.Write("{0}: ", item.CimInstanceProperties["displayName"].Value);
                hexState = (Convert.ToInt32((item.CimInstanceProperties["productState"].Value).ToString())).ToString("X");
                Console.WriteLine(avEnabled[Int16.Parse(hexState.Substring(1, 2))] + " and " + avUpdated[Int16.Parse(hexState.Substring(3, 2))]);
            }
        }
        static void ListFiles(CimSession session, string dir)
        {
            dir = dir.Replace(@"\", @"\\");
            if (!dir.EndsWith(@"\\"))
            {
                dir += @"\\";
            }
            var query = session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM cim_logicalfile WHERE Drive='" + dir.Substring(0, 2) + "' AND Path='" + dir.Substring(2) + "'");
            foreach (CimInstance item in query)
            {
                Console.WriteLine("{0}", item.CimInstanceProperties["Name"].Value);
            }
        }
        static void ReadFile(CimSession session, string file)
        {
            // https://twitter.com/mattifestation/status/1220713684756049921
            CimInstance baseInstance = new CimInstance("PS_ModuleFile");
            baseInstance.CimInstanceProperties.Add(CimProperty.Create("InstanceID", file, CimFlags.Key));
            CimInstance modifiedInstance = session.GetInstance("ROOT/Microsoft/Windows/Powershellv3", baseInstance);

            System.Byte[] fileBytes = (byte[])modifiedInstance.CimInstanceProperties["FileData"].Value;
            Console.WriteLine(Encoding.UTF8.GetString(fileBytes, 0, fileBytes.Length));
        }
        static void FindFile(CimSession session, string file)
        {
            int i = file.LastIndexOf(".");
            string filter = "";
            if (i < 0)
            {
                filter = "Filename='" + file + "'";
            }
            else
            {
                filter = "Extension='" + file.Substring(i + 1) + "' AND Filename LIKE '" + file.Substring(0, i) + "'";
            }
            var query = session.QueryInstances(@"root\cimv2", "WQL", "SELECT * FROM Cim_DataFile WHERE " + filter);
            foreach (CimInstance item in query)
            {
                Console.WriteLine("{0}", item.CimInstanceProperties["name"].Value);
            }
        }
        #endregion

    }
}
