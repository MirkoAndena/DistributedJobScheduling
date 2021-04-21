using System;
using DistributedJobScheduling.Extensions;

namespace DistributedJobScheduling.Utils
{
    public static class ArgsUtils
    {
        private static string GetStringArg(string[] args, int position)
        {
            if (position >= args.Length)
                throw new Exception($"Position {position} is greater than args lenght ({args.Length}), args = {args.ToString<string>()}");

            return args[position];
        }

        public static string GetStringParam(string[] args, int position, string varName)
        {
            try { return GetStringArg(args, position); }
            catch (Exception e)
            {
                string var = Environment.GetEnvironmentVariable(varName);
                if (var != null) return var;
                else throw e; 
            }
        }

        private static int GetIntArg(string[] args, int position)
        {
            if (position >= args.Length)
                throw new Exception($"Position {position} is greater than args lenght ({args.Length}), args = {args.ToString<string>()}");

            int number;
            bool ok = Int32.TryParse(args[position], out number);

            if (!ok)
                throw new Exception($"Arg on position {position} is not an integer number, args = {args.ToString<string>()}");

            return number;
        }

        public static int GetIntParam(string[] args, int position, string varname)
        {
            try { return GetIntArg(args, position); }
            catch (Exception e)
            {
                string var = Environment.GetEnvironmentVariable(varname);
                if (var == null) throw e;
                int value;
                bool ok = Int32.TryParse(var, out value);
                if (ok) return value;
                else throw new Exception($"Envinronment variable {varname} is not an integer"); 
            }
        }

        public static bool IsPresent(string[] args, string item)
        {
            foreach (string arg in args)
                if (arg.Trim().ToLower() == item.ToLower()) return true;
            return Environment.GetEnvironmentVariable(item) != null;
        }
    }
}