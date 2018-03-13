using System;

namespace DupFind
{
    class Program
    {

        static void Main(string[] args)
        {
            try
            {
                using (var handler = new FileHandler(new ArgReader(args))) { }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                ShowDirections();
            }
        }

        private static void ShowDirections()
        {
            Console.WriteLine("dotnet DupFind -s <dir>");
        }
    }
}
