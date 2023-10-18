using System;

namespace DriveScannerNET
{
    internal class Program
    {

        Queue<String> workItems = new Queue<String>();
        int dirsScanned = 0;
        int filesScanned = 0;
        long bytesScanned = 0;
        int workItemsProcessed = 0;

        void generateWorkItems(String startingPath)
        {
            // use stack instead of recursviely calling into generateWorkItems.
            // its more efficient and we can early return once we hit 100,000 items.
            var dirs = new Queue<String>();
            dirsScanned++;
            dirs.Enqueue(startingPath);

            while(dirs.Count != 0)
            {
                var path = dirs.Dequeue();
                try
                {
                    foreach (string f in Directory.GetFiles(path))
                    {
                         long fileSize = new FileInfo(f).Length;

                        // We don't scan files longer than 1 GB for now.
                        if (fileSize > (1024 * 1024 * 1024)) continue;

                        bytesScanned += fileSize;
                        filesScanned++;
                        workItems.Enqueue(f);
                        if (workItems.Count == 100000)
                        {
                            return;
                        }
                    }
                    foreach (string d in Directory.GetDirectories(path))
                    {
                        dirsScanned++;
                        dirs.Enqueue(d);
                    }
                }
                catch (System.Exception)
                {
                    // eat the exceptions.  There are a lot of reasons we could
                    // get a failure here (permissions, etc.)
                }
            }
        }

        void processThread()
        {
            var processor = new FileProcessor();
            for(; ; )
            {
                String item = "";
                int processCount;
                lock(workItems)
                {
                    if (workItems.Count == 0) 
                    {
                        Console.WriteLine("File processor thread done.");
                        return;
                    }
                    item = workItems.Dequeue();
                    processCount = ++workItemsProcessed;
                }

                processor.processWorkItem(item);
//                Console.WriteLine("Processing " + item);

                if( processCount % 100 == 0)
                {
                    Console.WriteLine(processCount);
                }
            }
        }

        void scan()
        {
            String pathToScan = "C:\\dev";
            int threadsToUse = 10;

            var timeStart = Environment.TickCount;

            // generate the list of work items
            generateWorkItems(pathToScan);
            //            foreach (string f in workItems) Console.WriteLine(f);

            var timePhase1 = Environment.TickCount;

            var threads = new List<Thread>();
            for (int i = 0; i < threadsToUse; i++)
            {
                var thread = new Thread(processThread);
                thread.Start();
                threads.Add(thread);    
            }

            // the threads will die once there are no work items
            // left.  Just join each one
            foreach( var t in threads )
            {
                t.Join();
            }

            var timePhase2 = Environment.TickCount;

            // report findings
            var gb = bytesScanned / (double)(1024 * 1024 * 1024);

            Console.WriteLine("Threads:                " + threadsToUse );
            Console.WriteLine("Directories scanned:    " + dirsScanned );
            Console.WriteLine("Files scanned:          " + filesScanned );
            Console.WriteLine("Bytes scanned:          " + gb.ToString("0.###") + " GB" );

            // report the timings
            double durationPhase1 = (timePhase1 - timeStart) / 1000.0;
            Console.WriteLine("Time for phase 1:       " + durationPhase1 + " sec");

            double durationPhase2 = (timePhase2 - timePhase1) / 1000.0;
            Console.WriteLine("Time for phase 2:       " + durationPhase2 + " sec");

            double totalTime = (timePhase2 - timeStart) / 1000.0;
            Console.WriteLine("Total scanning time:    " + totalTime + " sec");

            double speed = gb / totalTime;
            double speedPerThread = speed / threadsToUse;
            Console.WriteLine("Bandwidth (per thread): " + speedPerThread.ToString("0.###") + " GB/sec");
            Console.WriteLine("Bandwidth (aggregate):  " + speed.ToString("0.###") + " GB/sec");
        }

        static void Main(string[] args)
        {
            Program prog = new Program();
            prog.scan();
        }
    }
}