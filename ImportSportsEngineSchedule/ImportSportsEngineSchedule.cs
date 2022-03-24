using System;
using System.CommandLine;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

using Microsoft.VisualBasic.FileIO;

// This program reads a game schedule in .csv format, along with a mapping of team names to
// SportsEngine IDs, and outputs a schedule in SportsEngine format.

namespace ImportSportsEngineSchedule {

    class Program {

        static Dictionary<string, string> locationUrlMap = new Dictionary<string, string> {
            { "Chaldecott Park", @"""https://www.google.com/maps/place/Chaldecott+Park/@49.2489543,-123.1943893,17z/data=!3m1!4b1!4m5!3m4!1s0x54867319fdabfed1:0x371c9013ea6c781b!8m2!3d49.2489508!4d-123.1922006""" },
            { "Hillcrest Park", @"http://covapp.vancouver.ca/parkfinder/parkdetail.aspx?inparkid=164" },
            { "Trafalgar Park", @"""https://www.google.com/maps/place/Trafalgar+Park/@49.2510074,-123.1645553,17z/data=!3m1!4b1!4m5!3m4!1s0x548673a09b6a9b15:0x85643662d26d2056!8m2!3d49.2510039!4d-123.1623666""" },
        };

        static Dictionary<string, string> teamIdMap = new Dictionary<string, string>();

        static string GetLocationDetails(string division, string field) {
            Dictionary<string, Dictionary<string, string>> loc = new Dictionary<string, Dictionary<string, string>> {
                { "Midget", new Dictionary<string, string> {
                        { "Hillcrest Park", @"Midget Diamond" },
                        { "Trafalgar Park", @"SE Corner" }
                    }
                },
                { "Bantam", new Dictionary<string, string> {
                        { "Hillcrest Park", @"Bantam Diamond" },
                        { "Chaldecott Park", @"North Diamond" }
                    }
                }
            };

            return loc[division][field];
        }

        static int ExportSportsEngineSchedule(string division, FileInfo scheduleFile, FileInfo mappingCodeFile) {

            var mappingCodeParser = new TextFieldParser(mappingCodeFile.Name);
            mappingCodeParser.TextFieldType = FieldType.Delimited;
            mappingCodeParser.SetDelimiters(new string[] { "," });
            while (!mappingCodeParser.EndOfData) {
                string[] row = mappingCodeParser.ReadFields();
                if (row.Length == 3 && row[1] == "Team") {
                    teamIdMap.Add(row[0], row[2]);
                }
            }

            // TODO: verify the file could be opened for reading
            var parser = new TextFieldParser(scheduleFile.Name);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(new string[] { "," });
            bool headerRow = false;
            // TODO: add error handling
            using StreamWriter swOutputFile = new("SE Schedule.csv");

            string outputHeaderRow = "Start_Date,Start_Time,End_Date,End_Time,Title,Description,Location,Location_URL,Location_Details,All_Day_Event,Event_Type,Tags,Team1_ID,Team1_Division_ID,Team1_Is_Home,Team2_ID,Team2_Division_ID,Team2_Name,Custom_Opponent,Event_ID,Game_ID,Affects_Standings,Points_Win,Points_Loss,Points_Tie,Points_OT_Win,Points_OT_Loss,Division_Override";

            while (!parser.EndOfData) {
                string[] row = parser.ReadFields();

                if (headerRow) {
                    // read this record and output it to the SE schedule.
                    if (row[2] == string.Empty) {
                        continue;
                    }

                    DateTime date = DateTime.Parse(row[0]);
                    DateTime time = DateTime.Parse(row[1]);
                    if (time.Hour < 8) {
                        time = time.AddHours(12);
                    }

                    string homeTeamId = teamIdMap[row[2]];
                    string awayTeamId = teamIdMap[row[3]];
                    string field = row[4];

                    swOutputFile.WriteLine(@"{0},{1},{2},{3},,,{4},{5},{6},0,Game,,{7},,1,{8},,,,,,1,,,,,,,",
                        date.ToShortDateString(),
                        time.ToString("H:mm"),
                        date.ToShortDateString(),
                        time.AddHours(2).ToString("H:mm"),
                        field,
                        locationUrlMap[field],
                        GetLocationDetails(division, field),
                        homeTeamId,
                        awayTeamId
                    );
                }
                else {

                    string[] expectedHeaderRow = new string[] {
                        "Date",
                        "Time",
                        "Home",
                        "Away",
                        "Field"
                    };
                    // Check that the header row has the expected format & order
                    Debug.Assert(row[0] == expectedHeaderRow[0]);
                    Debug.Assert(row[1] == expectedHeaderRow[1]);
                    Debug.Assert(row[2] == expectedHeaderRow[2]);
                    Debug.Assert(row[3] == expectedHeaderRow[3]);
                    Debug.Assert(row[4] == expectedHeaderRow[4]);
                    headerRow = true;

                    swOutputFile.WriteLine(outputHeaderRow);
                }
            }

            return 0;
        }

        static int Main(string[] args) {

            var divisionOption = new Option<string>(
                "--division",
                getDefaultValue: () => "",
                description: "Division - Bantam or Midget");
            var scheduleFile = new Option<FileInfo>(
                    "--schedule",
                    "The raw schedule to transform (.csv format)");
            var mappingCodeFile = new Option<FileInfo>(
                    "--mappingcodes",
                    "The mapping codes as exported by SportsEngine");

            // Add the options to a root command:
            var rootCommand = new RootCommand {
                divisionOption,
                scheduleFile,
                mappingCodeFile
            };

            rootCommand.Description = "SportsEngine Schedule Exporter";

            rootCommand.SetHandler((string division, FileInfo s, FileInfo m) => {
                if (division != "Bantam" && division != "Midget") {
                    Console.WriteLine("Division must be either Bantam or Midget");
                    return;
                }
                if (s == null) {
                    Console.WriteLine("Missing required parameter --schedule");
                    return;
                }

                if (m == null) {
                    Console.WriteLine("Missing required parameter --mappingcodes");
                    return;
                }

                ExportSportsEngineSchedule(division, s, m);
            }, divisionOption, scheduleFile, mappingCodeFile);

            // Parse the incoming args and invoke the handler
            return rootCommand.Invoke(args);
        }
    }
}
