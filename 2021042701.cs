using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Task11
{
    static class Program
    {
        const int nReader = 3;
        const int nWriter = 4;
        const int nMessanger = 100000;
        const int nBuffer = 10;
        static int iR, iW;
        static Task[] tReaders, tWriters;
        static string[] buffer;
        static bool finish;
        static bool bFull;
        static bool bEmpty;
        static int[] WriteIndexCopy;
        static int[] ReadIndexCopy;
        static int[] WriterPriority;
        static int[] ReaderPriority;
        static double[] timeWriter;
        static double[] timeReader;
        static int[] countMessageWriter;
        static int[] countMessageReader;

        static void Main(string[] args)
        {
            finish = false;
            bFull = false;
            bEmpty = true;
            iR = 0;
            iW = 0;
            timeWriter = new double[nWriter];
            timeReader = new double[nReader];
            countMessageWriter = new int[nWriter];
            countMessageReader = new int[nReader];
            WriteIndexCopy = new int[nWriter];
            ReadIndexCopy = new int[nReader];
            WriterPriority = new int[nWriter];
            ReaderPriority = new int[nReader];
            buffer = new string[nBuffer];
            for (int i = 0; i < ReadIndexCopy.Length; i++)
                ReadIndexCopy[i] = -1;
            for (int i = 0; i < WriteIndexCopy.Length; i++)
                WriteIndexCopy[i] = -1;
            Random rnd = new Random();
            for (int i = 0; i < nWriter; i++)
                WriterPriority[i] = rnd.Next(nWriter);
            for (int i = 0; i < nReader; i++)
                ReaderPriority[i] = rnd.Next(nReader);
            Manager();
            Console.WriteLine($"Thread priorities are set randomly. Size of the ring buffer is: {nBuffer}.");
            Console.WriteLine($"Number of readers: {nReader}. Number of writers: {nWriter}. Number of messages every writer: {nMessanger}.");

            for (int i = 0; i < nWriter; i++)
                    Console.WriteLine($"Writer #{i} recorded {countMessageWriter[i]} messages in {timeWriter[i]:f2} msec.");
            Console.WriteLine($"Total recorded messages is {countMessageWriter.Sum()}.");
            for (int i = 0; i < nReader; i++)
                Console.WriteLine($"Reader #{i} read {countMessageReader[i]} messages in {timeReader[i]:f2} msec.");
            Console.WriteLine($"Total readed messages is {countMessageReader.Sum()}.");
        }

        static void ReaderThread(int iReader, ManualResetEventSlim evReadyToRead, ManualResetEventSlim evStartReading)
        {
            DateTime dStart, dStop;
            dStart = DateTime.Now;
            var Messages = new List<string>();
            countMessageReader[iReader] = 0;
            while (!finish)
            {
                evReadyToRead.Set();
                evStartReading.Wait();
                if (finish && evReadyToRead.IsSet) break;
                int k = ReadIndexCopy[iReader];
                Messages.Add(buffer[k]);
                countMessageReader[iReader]++;
                bFull = false;
                evStartReading.Reset();
                ReadIndexCopy[iReader] = -1;
            }
            dStop = DateTime.Now;
            timeReader[iReader] = new double();
            timeReader[iReader] = (dStop - dStart).TotalMilliseconds;
        }
        static void WriterThread(int iWriter, ManualResetEventSlim evReadyToWrite, ManualResetEventSlim evStartWriting)
        {
            DateTime dStart, dStop;
            dStart = DateTime.Now;
            countMessageWriter[iWriter] = 0;
            string[] Messages = new string[nMessanger];
            for (int i = 0; i < Messages.Length; i++)
            {

                Messages[i] = $"{iWriter}_{i}";
            }
            int j = 0;
            while (j < Messages.Length)
            {
                evReadyToWrite.Set();
                evStartWriting.Wait();
                int k = WriteIndexCopy[iWriter];
                buffer[k] = Messages[j++];
                countMessageWriter[iWriter]++;
                bEmpty = false;
                evStartWriting.Reset();
                WriteIndexCopy[iWriter] = -1;
            }
            dStop = DateTime.Now;
            timeWriter[iWriter] = (dStop - dStart).TotalMilliseconds;
        }
        static void Manager()
        {
            ManualResetEventSlim[] evStartReading, evStartWriting;
            ManualResetEventSlim[] evReadyToRead, evReadyToWrite;
            evReadyToRead = new ManualResetEventSlim[nReader];
            evStartReading = new ManualResetEventSlim[nReader];
            evReadyToWrite = new ManualResetEventSlim[nWriter];
            evStartWriting = new ManualResetEventSlim[nWriter];
            tReaders = new Task[nReader];
            tWriters = new Task[nWriter];
            for (int i = 0; i < tReaders.Length; i++)
            {
                evReadyToRead[i] = new ManualResetEventSlim(false);
                evStartReading[i] = new ManualResetEventSlim(false);
                int i_copy = i;
                tReaders[i] = new Task(() =>
                {
                    ReaderThread(i_copy, evReadyToRead[i_copy], evStartReading[i_copy]);
                });
                tReaders[i].Start();
            }
            for (int i = 0; i < tWriters.Length; i++)
            {
                evReadyToWrite[i] = new ManualResetEventSlim(false);
                evStartWriting[i] = new ManualResetEventSlim(false);
                int i_copy = i;
                tWriters[i] = new Task(() =>
                {
                    WriterThread(i_copy, evReadyToWrite[i_copy], evStartWriting[i_copy]);
                });
                tWriters[i].Start();
            }
            while (!finish)
            {
                if (!bFull && !ReadIndexCopy.Contains(iW))
                {
                    int iWriter = GetWriter(evReadyToWrite);
                    if (iWriter != -1)
                    {
                        evReadyToWrite[iWriter].Reset();
                        WriteIndexCopy[iWriter] = iW;
                        evStartWriting[iWriter].Set();
                        iW = (iW + 1) % nBuffer;
                        if (iW == iR) bFull = true;
                    }
                }
                if (!bEmpty && !WriteIndexCopy.Contains(iR))
                {
                    int iReader = GetReader(evReadyToRead);
                    if (iReader != -1)
                    {
                        evReadyToRead[iReader].Reset();
                        ReadIndexCopy[iReader] = iR;
                        evStartReading[iReader].Set();
                        iR = (iR + 1) % nBuffer;
                        if (iR == iW) bEmpty = true;
                    }
                }
                if (tWriters.All(t => t.IsCompleted) && bEmpty)
                    finish = true;
            }
            foreach (var sr in evStartReading.Where(e => !e.IsSet))
                sr.Set();
            Task.WaitAll(tReaders);
        }
        static int GetWriter(ManualResetEventSlim[] evReadyToWrite)
        {
            var ready = new List<int>();
            for (int i = 0; i < nWriter; i++)
                if (evReadyToWrite[i].IsSet)
                    ready.Add(i);
            if (ready.Count == 0)
                return -1;
            return ready.OrderBy(i => WriterPriority[i]).First();
        }
        static int GetReader(ManualResetEventSlim[] evReadyToRead)
        {
            var ready = new List<int>();
            for (int i = 0; i < nReader; i++)
                if (evReadyToRead[i].IsSet)
                    ready.Add(i);
            if (ready.Count == 0)
                return -1;
            return ready.OrderBy(i => ReaderPriority[i]).First();
        }
    }
}