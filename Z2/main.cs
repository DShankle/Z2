using System;

namespace Z2
{
    class Program
    {
        static void Main(string[] args)
        {
            
           
            var b = new Comms();
            //thread 1 runs command
            //thread 2 checks in / retrieves and delivers commands and results
            b.checkin();
          
            
        }
    }
}
