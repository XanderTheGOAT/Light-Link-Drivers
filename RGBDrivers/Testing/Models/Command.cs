using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RGBLibrary.Models
{
    public class Command
    {
        public List<byte> CommandList { get; }
        public String Name { get; set; }

        public Command()
        {
            CommandList = new List<byte>();
        }

        public Command(List<byte> commands)
        {
            CommandList = commands;
        }

        public void AddCommand(String command)
        {
            if(System.Text.RegularExpressions.Regex.IsMatch(command, @"\A\b[0-9a-fA-F]+\b\Z"))
            {
                int hexNum = Convert.ToInt32(command,16);
                CommandList.Add((byte)hexNum);
            }
            else
            {
                //Make custom exception
                throw new Exception("Not a valid Hex string");
            }
        }

        public void AddCommand(byte command)
        {
            command = (byte)Convert.ToInt32(command+"",16);
            if (command < 0)//Make custom exception
                throw new Exception("Command must be a positive number");
            CommandList.Add(command);
        }

        private void TransferCommandsToBuffer(byte[] buffer, byte[] commands)
        {
            if (commands.Length > buffer.Length)
                throw new Exception("Commands is longer than the size of the buffer");

            Array.Copy(commands, 0, buffer, 1, commands.Length);

        }

        public override string ToString()
        {
            String value = "";
            int order = 0;
            value += "Command Name: " + Name+"\r\n";
            foreach (var command in CommandList)
            {
                value += "\t"+ ++order +"). " + command +"\r\n";
            }
            return value;
        }
    }
}
