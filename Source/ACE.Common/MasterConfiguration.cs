namespace ACE.Common
{
    public class MasterConfiguration
    {
        public GameConfiguration Server { get; set; }

        public ServerTransferConfiguration Transfer { get; set; }

        public DatabaseConfiguration MySql { get; set; }
    }
}
