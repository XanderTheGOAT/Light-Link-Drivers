using ConsoleControl.Models;
using CSC160_ConsoleMenu;
using RGBLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleControl
{
    class Program
    {

        private static List<Commands> commands = new List<Commands>();
        static void Main(string[] args)
        {
            bool stop = false;
            bool added = false;
            List<String> options = new List<string> { "Register Device" };
            Device device = null;
            do
            {
                if (!added && !(device is null))
                {
                    added = !added;
                    options.RemoveAt(0);
                    options.Add("Change Device");
                    options.Add("Send Report");
                    options.Add("Get Report");
                }
                int choice = CIO.PromptForMenuSelection(options, true);
                switch (choice)
                {
                    case 1:
                        device = new Device("1b1c", "1b2e");
                        if (!device.FindTheHid())
                            device = null;
                        break;
                    case 2:
                        SendReportMenu(device);
                        break;
                    case 3:
                        GetReportMenu(device);
                        break;
                    case 0:
                    default:
                        stop = !stop;
                        break;
                }

                if (!stop)
                {
                    Console.WriteLine("Click Enter to Continue");
                    Console.ReadLine();
                    Console.Clear();
                }


            } while (!stop);
        }


        private static Commands CreateCommands()
        {
            Commands command = new Commands();
            bool stop = false;
            do
            {
                Console.Clear();
                Console.WriteLine("Commands:\r\n" + command);
                Console.WriteLine("Type stop when your command is done");
                Console.Write("Enter the command one byte each entry: ");
                String value = Console.ReadLine();
                if (value.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    stop = !stop;
                else
                {
                    try
                    {
                        byte num;
                        if (byte.TryParse(value, out num))
                            command.AddCommand(num);
                        else
                            command.AddCommand(value);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Enter Hex or Decimal Number");
                    }
                }
            } while (!stop);
            Console.WriteLine("Click Enter to Continue");
            Console.ReadLine();
            Console.Clear();
            if (CIO.PromptForBool("Would you like to save this command? [y/n]", "y", "n"))
            {
                Console.Write("Name the command: ");
                command.Name = Console.ReadLine();
                Console.Clear();
                commands.Add(command);
            }
            return command;
        }

        private static Commands ChooseCommand()
        {
            if (commands.Count < 1)
                return CreateCommands();

            List<String> names = new List<string>();
            foreach (var command in commands)
            {
                names.Add(command.Name + " Command");
            }
            names.Add("Create Command");

            int choice = CIO.PromptForMenuSelection(names, false) - 1;
            if (choice == commands.Count)
            {
                return CreateCommands();
            }

            return commands[choice];
        }

        private static void SendReportMenu(Device device)
        {
            if (device is null)
            {
                Console.WriteLine("Device not found");
                return;
            }
            Commands commands = CreateCommands();
            bool stop = false;
            do
            {
                List<String> options = new List<string>() { "Send Feature Report", "Send Output Report", "Change Command", "View Command" };
                int choice = CIO.PromptForMenuSelection(options, true);
                switch (choice)
                {
                    case 1:
                        device.RequestToSendFeatureReport(commands.CommandList.ToArray());
                        break;
                    case 2:
                        device.RequestToSendOutputReport(commands.CommandList.ToArray());
                        break;
                    case 3:
                        commands = ChooseCommand();
                        break;
                    case 4:
                        Console.WriteLine(commands);
                        Console.ReadLine();
                        break;
                    case 0:
                        stop = !stop;
                        break;
                }
                Console.Clear();
            } while (!stop);
            Console.WriteLine("Click Enter to Continue");
            Console.ReadLine();
            Console.Clear();
        }

        private static void GetReportMenu(Device device)
        {
            if (device is null)
            {
                Console.WriteLine("Device not found");
                return;
            }
            int choice = CIO.PromptForMenuSelection(new string[] { "Get Feature Report", "Get Input Report" }, true);
            switch (choice)
            {
                case 1:
                    device.RequestToGetFeatureReport();
                    break;
                case 2:
                    device.RequestToGetInputReport();
                    break;
            }
            Console.WriteLine("Click Enter to Continue");
            Console.ReadLine();
            Console.Clear();

        }
    }
}
