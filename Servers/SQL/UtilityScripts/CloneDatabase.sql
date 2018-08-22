      --First create a backup
      BACKUP DATABASE [Rabbit] TO DISK = N'C:\Backup\Rabbit.bak' WITH INIT ,
       NOUNLOAD , NAME = N'Rabbit', NOSKIP , STATS = 10, NOFORMAT

      --Next restore it to another database
      RESTORE DATABASE [Test] FROM DISK = N'C:\Backup\Rabbit.bak' WITH FILE = 1,
       MOVE N'Rabbit' TO N'C:\Database\MSSQL10.SQLEXPRESS\MSSQL\DATA\Test.MDF',
       MOVE N'Rabbit_Log' TO N'C:\Database\MSSQL10.SQLEXPRESS\MSSQL\DATA\Test.LDF', NOUNLOAD, REPLACE, STATS = 10
       