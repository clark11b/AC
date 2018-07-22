using System;
using System.Globalization;
using System.IO;

using ACE.Database.Models.World;

namespace ACE.Database.SQLFormatters.World
{
    public class EventSQLWriter : SQLWriter
    {
        public string GetDefaultFileName(Event input)
        {
            string fileName = input.Name;
            fileName = IllegalInFileName.Replace(fileName, "_");
            fileName += ".sql";

            return fileName;
        }

        public void CreateSQLDELETEStatement(Event input, StreamWriter writer)
        {
            writer.WriteLine($"DELETE FROM `event` WHERE `name` = '{input.Name.Replace("'", "''")}';");
        }

        public void CreateSQLINSERTStatement(Event input, StreamWriter writer)
        {
            writer.WriteLine("INSERT INTO `event` (`name`, `start_Time`, `end_Time`, `state`)");

            writer.WriteLine("VALUES (" +
                             $"'{input.Name.Replace("'", "''")}', " +
                             $"{(input.StartTime == -1 ? $"{input.StartTime}" : $"{input.StartTime} /* {DateTimeOffset.FromUnixTimeSeconds(input.StartTime).DateTime.ToUniversalTime().ToString(CultureInfo.InvariantCulture)} */")}, " +
                             $"{(input.EndTime == -1 ? $"{input.EndTime}" : $"{input.EndTime} /* {DateTimeOffset.FromUnixTimeSeconds(input.EndTime).DateTime.ToUniversalTime().ToString(CultureInfo.InvariantCulture)} */")}, " +
                             $"{input.State}" +
                             ");");
        }
    }
}
