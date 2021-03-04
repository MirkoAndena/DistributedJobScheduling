using System;
using Communication;

public class Program
{
    const int ID = 2;

    static void Main(string[] args)
    {
        Workers group = Workers.Build("group.json", ID);
        
    }
}