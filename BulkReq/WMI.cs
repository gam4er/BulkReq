using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Generic;
using Microsoft.Management.Infrastructure.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BulkReq
{
    class WMI
    {
        public WMI(WMIOptions opts)
        {
            RunWMI(opts);
        }

        public static int CurrentlyRunThreads = 0;

        public static int RunWMI(WMIOptions opts) 
        {
            CimSession session = CreateSession(opts.Host, opts.DCOM);
            ListBasicInfo(session);

            if (opts.AsyncOnly)
            {
                for (int j = 0 ; ; j++)                
                {
                    if (CurrentlyRunThreads >= opts.At)                    
                        Thread.Sleep(500);                    
                    else
                    {
                        var instanceObject = new CimInstanceWatcher();
                        AsyncListRunningProcesses(session).Subscribe(instanceObject);                        
                        CurrentlyRunThreads++;
                        instanceObject.OnCompleted();
                    }
                    if (j % 100 == 0)
                    {
                        Console.Write("\r{0}", CurrentlyRunThreads);
                    }
                }
            }
            else
            {
                for (int i = 0; i < 1000; i++)
                {
                    ListFiles(session, @"c:\windows\system32");
                    ListRunningProcesses(session);
                    ListRunningServices(session);
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
            public void OnCompleted()
            {
                //Console.WriteLine("Done");
                CurrentlyRunThreads--;
            }

            public void OnError(Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
                //return -1;
            }

            public void OnNext(CimInstance value)
            {
                //Console.WriteLine("Value: " + value);
                try
                {
                    string s1 = (string)(value.CimInstanceProperties["ProcessID"].Value);
                    string s2 = (string)(value.CimInstanceProperties["ParentProcessID"].Value);
                    string s3 = (string)(value.CimInstanceProperties["Name"].Value);
                    //Console.WriteLine("{0,-10} {1,-10} {3,-20} {2,5:1}",
                    /*
                    Console.WriteLine("{0,-10} {1,-10} {2,5:1}",
                        value.CimInstanceProperties["ProcessID"].Value,
                        value.CimInstanceProperties["ParentProcessID"].Value,
                        value.CimInstanceProperties["Name"].Value);*/
                    //session.InvokeMethod(item, "GetOwner", null).OutParameters["User"].Value);
                }
                catch (Exception ex) { }
            }
           
        }
        static CimAsyncMultipleResults<CimInstance> AsyncListRunningProcesses(CimSession session)
        {
            //Console.WriteLine("{0,-10} {1,-10} {2,4:1}", "PID", "PPID", "Name");
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
