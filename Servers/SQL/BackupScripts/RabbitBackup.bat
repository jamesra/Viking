sqlcmd -S .\SQLEXPRESS -i "RabbitBackup.sql"

copy F:\DatabaseBackup\Rabbit\Rabbit.bak E:\Volumes\Temp\Rabbit.bak
