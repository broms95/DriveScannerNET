using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DriveScannerNET
{
    internal class FileProcessor
    {
        private readonly static string[] patterns = new string[] {
//            ".000000000.",            // compact SSN
              ".000-00-0000.",          // expanded SSN
//            ".0000000000000000.",     // compact CC
//            ".0000.0000.0000.0000."   // expanded CC
        };

        private readonly static char[] lookupTable = initializeLookupTable();
        private const int OVERLAP_SIZE = 32;
        private const int BUFFER_SIZE = 4096;

        private byte[] readBuffer = new byte[BUFFER_SIZE];
        private byte[] textBuffer = new byte[BUFFER_SIZE + OVERLAP_SIZE];
        private StringBuilder ifBuffer = new StringBuilder(BUFFER_SIZE + OVERLAP_SIZE);
        private String currentPath = "";

        public Int64 processWorkItem(String path)
        {
            Int64 bytesProcessed = 0;
            Array.Fill(textBuffer, (byte)' ');
 
            // setup for the next file
            currentPath = path;

            using (var sr = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, BUFFER_SIZE))
            {
                int bytesRead;
                while ((bytesRead = sr.Read(readBuffer, 0, BUFFER_SIZE)) > 0)
                {
                    bytesProcessed += bytesRead;
                    processWorkItemBuffer(bytesRead);
                }

            }

            return bytesProcessed;
        }

        protected void processWorkItemBuffer(int readBufferSize)
        {
            // prepare the text buffer.  We do this by shifting the end
            // to the beginning and copying in the new data
            Array.Copy(textBuffer, BUFFER_SIZE, textBuffer, 0, OVERLAP_SIZE);
            Array.Copy(readBuffer, 0, textBuffer, OVERLAP_SIZE, readBufferSize);
            int textBufferValid = OVERLAP_SIZE + readBufferSize;

            ifBuffer.Length = textBufferValid;
            for (int i = 0; i < textBufferValid; i++)
            {
                ifBuffer[i] = lookupTable[textBuffer[i]];
            }

            // for now we are only searching for expanded SSN but in the future
            // we can search for all sorts of patterns like CC numbers of different
            // formats.  See design doc for more.
            //
            // We might want to bite the bullet and use Regular expressions here
            // but this might be too slow.  Would need to profile once we know
            // what type of patterns we want to look for and how expensive they are.
            var stringToSearch = ifBuffer.ToString(0, textBufferValid);
            foreach (var pattern in patterns)
            {
                int position = 0;
                for (; ; )
                {
                    position = stringToSearch.IndexOf(pattern, position, StringComparison.Ordinal);
                    if (position == -1)
                    {
                        break;
                    }
                    else
                    {
                        // extract the text from the ifBuffer
                        var actualText = Encoding.UTF8.GetString(textBuffer, position + 1, pattern.Length - 2 );
                        Console.WriteLine(currentPath + ": " + actualText);
                        position++;
                    }
                }
            }
        }

        static protected char[] initializeLookupTable()
        {
            // convert the text buffer into intermediate form
            char[] table = new char[byte.MaxValue + 1];
            Array.Fill(table, '.');
            table['0'] = '0';
            table['1'] = '0';
            table['2'] = '0';
            table['3'] = '0';
            table['4'] = '0';
            table['5'] = '0';
            table['6'] = '0';
            table['7'] = '0';
            table['8'] = '0';
            table['9'] = '0';
            table['-'] = '-';
            return table;
        }
    }
}
