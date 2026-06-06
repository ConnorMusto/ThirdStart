namespace ThirdStart.Data
{
    public static class Constants
    {

        public const string AppName = "ThirdStart";

        public const string SeedDataFileName = "CSVSeedData.csv";

        public const string SyncDirectory = "Sync/";

        //for a computer user this is the path to the master task list, which is a CSV file that contains all the tasks and their details.
        //for a phone user this is the path to the list of tasks assigned to them, which is also a CSV file that contains the tasks and their details.
        //and they get that when they sync with the computer user, which is the master task list.
        public const string MasterTaskList = "MasterList.csv";

        //for the computer user , this is the directory where we store the records of each task, which are also CSV files that contain the details of each task and its history.
        public const string RecordsDirectory = "Records/";
        public const string ProjectTaskPrefix = "ProjectTask_";
        public const string RecordsFileName = "Records.csv";

        //used to store the new records from phone users before we merge them into the main records file.
        //This is to prevent conflicts and data loss.
        public const string NewRecordsFileName = "NewRecords.csv";

        public static string MasterListPath =>
            Path.Combine(FileSystem.AppDataDirectory, MasterTaskList);

        public static string RecordsPath(string TaskID)
        {
            string pathbuilder = Path.Combine(FileSystem.AppDataDirectory, RecordsDirectory, $"{ProjectTaskPrefix}{TaskID}/", RecordsFileName);
            return pathbuilder;
        }
            
    }
}